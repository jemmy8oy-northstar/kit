using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c> wiring slice 1: with <c>KIT_PIPELINE=on</c> a note left in Kit queues a triage run
/// (BEH-ORCH-1) on the runner pool, and the run's end lets the next start. Driven as he drives it: notes go through
/// the router, runs are claimed and reported through <see cref="RunnerPoolRoutes"/> as a runner pod does.
/// </summary>
public class PipelineTests
{
    private const string PoolToken = "pool-secret";

    private const string Corpus = """
        # kit:repo someone/demo@dev

        behaviour BEH-A "A user sees the list"
          actor engineer

        behaviour BEH-B "A user adds an item"
          actor engineer
        """;

    private sealed class Rig : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("kit-pipeline-").FullName;

        public Rig(string corpus = Corpus, bool on = true, IReadOnlySet<string>? projects = null, bool canOpenPulls = true, StageTurnSettings? turns = null)
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), corpus);
            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Pipeline = on ? new Pipeline(corpora, Pool, Store, Models, projects: projects, pulls: canOpenPulls ? Pulls : null, turns: turns) : null;
            // One fixed minute: every note shares its date, as notes left in the same minute do, so a ref alone cannot tell them apart.
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, clock: new Clock(), runStore: Store, runLauncher: Pool, runnerPool: Pool, pipeline: Pipeline);
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, pipeline: Pipeline);
        }

        public MemoryAgentRunStore Store { get; }

        public Opener Pulls { get; } = new();

        public RunnerPool Pool { get; }

        public Pipeline? Pipeline { get; }

        public KitRouter Router { get; }

        public RunnerPoolRoutes Routes { get; }

        public void Note(string text, string kind = "note", string? behaviour = null)
        {
            var r = Router.Route("POST", "/api/projects/demo/notes", body: behaviour is null ? Json(new { text, kind }) : Json(new { text, kind, behaviour }));
            Assert.Equal(200, r.Status);
        }

        public PoolClaim? Claim()
        {
            var r = Routes.ClaimAsync(PoolToken).GetAwaiter().GetResult();
            return r.Status == 204 ? null : Assert.IsType<PoolClaim>(r.Body);
        }

        private int finished;

        // A finished triage reports one bundle, as every triage of at least one note does.
        public void Finish(PoolClaim claim, string status = "finished")
        {
            var slug = $"work-{++finished}";
            var body = status == "finished" ? Json(new { status, bundles = new[] { new { slug, text = BundleText(slug) } } }) : Json(new { status });
            Assert.Equal(200, Routes.Finished(claim.Token, body).Status);
        }

        public KitResponse FinishWith(PoolClaim claim, params object[] bundles) =>
            Routes.Finished(claim.Token, Json(new { status = "finished", bundles }));

        public void Dispose() => Directory.Delete(root, recursive: true);
    }

    // Records each pull request asked for and answers with Answer: a page, or GitHub's reason for none.
    private sealed class Opener : IPullRequestOpener
    {
        public List<(string Head, string Base, string Title, string Body, string? Repository)> Asked { get; } = [];

        public PullRequestResult Answer { get; set; } = new() { Opened = true, Number = 7, Url = "https://github.com/someone/demo/pull/7" };

        public Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, string? repository = null, CancellationToken cancellationToken = default)
        {
            Asked.Add((head, baseBranch, title, body, repository));
            return Task.FromResult<IPullRequestResult>(Answer);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 5, 12, 30, 0, TimeSpan.Zero);
    }

    private static readonly StageModelSettings Models = new("claude-haiku-5-5", "claude-opus-5-5", "claude-haiku-5-5", "claude-haiku-5-5");

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void A_note_queues_a_triage_run_of_the_projects_notes_on_the_repo_its_spec_names()
    {
        using var rig = new Rig();

        rig.Note("the list should sort by date");

        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("someone/demo", claim.Repo);
        Assert.Equal("dev", claim.BaseBranch);
        Assert.Equal("claude-haiku-5-5", claim.Model);
        Assert.Contains("You are the triage stage for project \"demo\"", claim.Task);
        Assert.Contains("the list should sort by date", claim.Task);
        Assert.Contains("- BEH-A: A user sees the list", claim.Task);
        Assert.Contains("- BEH-B: A user adds an item", claim.Task);
        var item = Assert.Single(rig.Pipeline!.Items());
        Assert.Equal((PipelineStage.Triage, WorkItemStatus.Running), (item.Stage, item.Status));
    }

    [Fact]
    public void With_the_pipeline_off_a_note_starts_nothing()
    {
        using var rig = new Rig(on: false);

        rig.Note("the list should sort by date");

        Assert.Null(rig.Claim());
        Assert.Empty(rig.Store.List());
    }

    [Fact]
    public void A_behaviour_request_queues_triage_but_a_bell_does_not()
    {
        using var bell = new Rig();
        bell.Note("", kind: "bell", behaviour: "BEH-A");
        Assert.Null(bell.Claim());
        Assert.Empty(bell.Store.List());
        Assert.Empty(bell.Pipeline!.Items());

        using var request = new Rig();
        request.Note("a page that exports the list", kind: "request");
        Assert.Contains("a page that exports the list", Assert.IsType<PoolClaim>(request.Claim()).Task);
    }

    [Fact]
    public void Notes_left_while_triage_runs_queue_one_more_run_which_reads_only_the_new_notes()
    {
        using var rig = new Rig();
        rig.Note("first note");
        var first = Assert.IsType<PoolClaim>(rig.Claim());

        rig.Note("second note");
        rig.Note("third note");

        Assert.Null(rig.Claim());
        Assert.Equal(2, rig.Pipeline!.Items().Count);

        rig.Finish(first);

        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.DoesNotContain("first note", next.Task);
        Assert.Contains("second note", next.Task);
        Assert.Contains("third note", next.Task);
        Assert.Equal(WorkItemStatus.Done, rig.Pipeline.Items()[0].Status);
        Assert.Equal(WorkItemStatus.Running, rig.Pipeline.Items()[1].Status);
    }

    [Fact]
    public void A_triage_run_that_failed_stops_its_item_and_its_notes_are_read_again_by_the_next()
    {
        using var rig = new Rig();
        rig.Note("first note");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), "failed");

        var stopped = Assert.Single(rig.Pipeline!.Items());
        Assert.Equal(WorkItemStatus.Stopped, stopped.Status);
        Assert.Equal("the triage run failed", stopped.StoppedBecause);
        Assert.Null(rig.Claim());

        rig.Note("second note");

        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("first note", next.Task);
        Assert.Contains("second note", next.Task);
    }

    [Fact]
    public void Notes_a_finished_triage_did_not_take_are_queued_for_another_run_at_once()
    {
        using var rig = new Rig();
        // A task is at most 8000 characters, and every note costs its heading line, so 150 notes cannot all fit one.
        rig.Note("note-first");
        var first = Assert.IsType<PoolClaim>(rig.Claim());
        for (var i = 1; i <= 150; i++)
        {
            rig.Note($"note-{i:D3}");
        }

        rig.Finish(first);
        var second = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("note-001", second.Task);
        Assert.DoesNotContain("note-150", second.Task);

        rig.Finish(second);

        var third = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("note-150", third.Task);
        Assert.DoesNotContain("note-001", third.Task);
        Assert.DoesNotContain("note-first", third.Task);
    }

    [Fact]
    public void A_project_whose_spec_names_no_repo_stops_the_item_and_starts_no_run()
    {
        using var rig = new Rig(corpus: "behaviour BEH-A \"A user sees the list\"\n  actor engineer\n");

        rig.Note("the list should sort by date");

        Assert.Null(rig.Claim());
        Assert.Empty(rig.Store.List());
        var item = Assert.Single(rig.Pipeline!.Items());
        Assert.Equal(WorkItemStatus.Stopped, item.Status);
        Assert.Contains("# kit:repo", item.StoppedBecause);
    }

    [Fact]
    public void A_run_the_pipeline_did_not_start_is_not_its_business()
    {
        using var rig = new Rig();
        rig.Pipeline!.RunEnded("run-nobody-started", succeeded: true);

        Assert.Empty(rig.Pipeline.Items());
    }

    private static string BundleText(string slug, string? flag = null) =>
        $"# Sort the list\nWork-item: {slug}\n" + (flag is null ? "" : $"Flag: {flag}\n")
        + "Cards:\n- BEH-A\nNotes:\n- demo.notes.md#x\nFiles:\n- src/List.cs\nContext:\nsort by date\n";

    [Fact]
    public void A_triage_reporting_a_bundle_starts_its_work_items_spec_and_keeps_the_bundle()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");

        Assert.Equal(200, rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") }).Status);

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (item.Stage, item.Status));
        Assert.Equal(["BEH-A"], rig.Pipeline.Bundle("demo", "list-sorting")!.Cards);
    }

    [Fact]
    public void A_flagged_bundle_starts_a_review_of_the_triage()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Review, WorkItemStatus.Running, PipelineStage.Triage), (item.Stage, item.Status, item.ReviewedStage));
    }

    [Fact]
    public void A_set_aside_bundle_stops_for_a_person_with_its_flag_and_its_notes_are_not_triaged_again()
    {
        using var rig = new Rig();
        // Not a note the plain rule catches, so a run is started and the triage itself sets it aside.
        rig.Note("asdf, sorry");

        rig.FinishWith(rig.Claim()!, new { slug = TriageStage.SetAside, text = BundleText(TriageStage.SetAside, flag: "a test note, asks for nothing") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == TriageStage.SetAside);
        Assert.Equal((WorkItemStatus.Stopped, "set aside: a test note, asks for nothing"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_1_A_lone_test_note_starts_no_run_and_is_set_aside_and_not_triaged_again()
    {
        using var rig = new Rig();

        rig.Note("Test");

        Assert.Null(rig.Claim());
        Assert.Empty(rig.Store.List());
        var aside = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == TriageStage.SetAside);
        Assert.Equal((WorkItemStatus.Stopped, "set aside: a test note, no run started"), (aside.Status, aside.StoppedBecause));
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Status is WorkItemStatus.Queued or WorkItemStatus.Running);

        rig.Note("the list should sort by date");

        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("the list should sort by date", claim.Task);
        Assert.DoesNotContain("### demo.notes.md#2026-11-05 12:30\nTest", claim.Task);
        Assert.Single(rig.Pipeline.Items(), i => i.Slug == TriageStage.SetAside);
    }

    [Fact]
    public void BEH_ORCH_1_A_test_note_among_real_ones_is_set_aside_and_only_the_real_ones_reach_the_run()
    {
        using var rig = new Rig();
        rig.Note("This is a test.");
        rig.Note("the list should sort by date");
        rig.Note("test");

        var claim = Assert.IsType<PoolClaim>(rig.Claim());

        Assert.Contains("the list should sort by date", claim.Task);
        Assert.Contains("## New notes (1)", claim.Task);
        Assert.DoesNotContain("This is a test.", claim.Task);
        Assert.Equal(WorkItemStatus.Stopped, Assert.Single(rig.Pipeline!.Items(), i => i.Slug == TriageStage.SetAside).Status);

        // A later set-aside, from a run or the rule, replaces the item rather than throwing on its slug.
        rig.FinishWith(claim, new { slug = TriageStage.SetAside, text = BundleText(TriageStage.SetAside, flag: "accidental") });
        rig.Note("ignore");
        Assert.Single(rig.Pipeline.Items(), i => i.Slug == TriageStage.SetAside);
    }

    [Fact]
    public void BEH_ORCH_1_A_backlog_bundle_stops_as_on_the_backlog_and_starts_nothing()
    {
        using var rig = new Rig();
        rig.Note("one day the list could export to csv");

        rig.FinishWith(rig.Claim()!, new { slug = TriageStage.Backlog, text = BundleText(TriageStage.Backlog, flag: "an idea for later") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == TriageStage.Backlog);
        Assert.Equal((WorkItemStatus.Stopped, "on the backlog: an idea for later"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
        Assert.Equal(["BEH-A"], rig.Pipeline.Bundle("demo", TriageStage.Backlog)!.Cards);
    }

    [Fact]
    public void A_bundle_that_is_malformed_too_large_or_for_another_slug_stops_its_item_with_the_reason()
    {
        using var rig = new Rig();
        rig.Note("n");

        rig.FinishWith(
            rig.Claim()!,
            new { slug = "broken", text = "not a bundle" },
            new { slug = "huge", text = (string?)null },
            new { slug = "named-wrong", text = BundleText("something-else") });

        string? Why(string slug) => Assert.Single(rig.Pipeline!.Items(), i => i.Slug == slug).StoppedBecause;
        Assert.StartsWith("its bundle is malformed: line 1", Why("broken"));
        Assert.Equal("its bundle was too large for the runner to report", Why("huge"));
        Assert.Equal("its bundle names work item \"something-else\"", Why("named-wrong"));
        Assert.Null(rig.Pipeline!.Bundle("demo", "broken"));
    }

    [Fact]
    public void BEH_ORCH_5_Each_stages_run_is_claimed_with_that_stages_turn_limit()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(15, triage.MaxTurns);

        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });
        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(30, spec.MaxTurns);

        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });
        Assert.Equal(100, Assert.IsType<PoolClaim>(rig.Claim()).MaxTurns);
    }

    [Fact]
    public void BEH_ORCH_5_A_flagged_stage_is_reviewed_on_the_review_turn_limit()
    {
        using var rig = new Rig();

        var review = Triaged(rig, flag: "touches the schema");

        Assert.Equal(20, review.MaxTurns);
        Assert.Contains("review", review.Task, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BEH_ORCH_5_A_turn_limit_that_is_set_reaches_the_claim()
    {
        using var rig = new Rig(turns: new StageTurnSettings(3, 4, 5, 6));
        rig.Note("the list should sort by date");

        Assert.Equal(3, Assert.IsType<PoolClaim>(rig.Claim()).MaxTurns);
    }

    [Fact]
    public void A_running_build_holds_the_projects_next_triage_until_it_ends()
    {
        using var rig = new Rig();
        rig.Note("first note");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Specced("list-sorting") });
        var build = Assert.IsType<PoolClaim>(rig.Claim());

        rig.Note("second note");
        Assert.Null(rig.Claim());

        rig.FinishWith(build, new { slug = "list-sorting", text = Built("list-sorting") });
        Assert.Contains("second note", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    // The bundle as a spec run leaves it: a test plan added to the end of its Context.
    private static string Specced(string slug, string? flag = null) =>
        BundleText(slug, flag) + "Test plan:\n- BEH-A: the list is sorted by date\n";

    // The bundle as a build run leaves it: what it built added after the test plan.
    private static string Built(string slug, string? flag = null) =>
        Specced(slug, flag) + "Built:\n- BEH-A: ListTests.Sorts_by_date\n- the suite ran 12 tests and passed\n";

    // Triage, then spec, of list-sorting; the build run of it is what is claimed next.
    private static PoolClaim Specced(Rig rig)
    {
        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting") });
        return Assert.IsType<PoolClaim>(rig.Claim());
    }

    [Fact]
    public void A_specced_item_starts_a_build_run_on_its_branch_with_the_bundle_and_its_test_plan()
    {
        using var rig = new Rig();

        var build = Specced(rig);

        Assert.Equal(("someone/demo", "dev", "claude-haiku-5-5"), (build.Repo, build.BaseBranch, build.Model));
        Assert.Contains("You are the build stage for project \"demo\"", build.Task);
        Assert.Contains("Check out branch kit/work/list-sorting", build.Task);
        Assert.Contains("- BEH-A: the list is sorted by date", build.Task);
        Assert.Contains("Do not open a pull request", build.Task);
        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Running), (item.Stage, item.Status));
    }

    [Fact]
    public void A_build_that_says_what_it_built_is_done_and_kit_opens_its_pull_request_into_the_branch_it_ran_on()
    {
        using var rig = new Rig();

        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting") });

        var asked = Assert.Single(rig.Pulls.Asked);
        Assert.Equal(("kit/work/list-sorting", "dev", "Sort the list", "someone/demo"), (asked.Head, asked.Base, asked.Title, asked.Repository));
        Assert.Contains("Built:\n- BEH-A: ListTests.Sorts_by_date", asked.Body);
        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, "https://github.com/someone/demo/pull/7"), (item.Stage, item.Status, item.PullRequest));
        Assert.Null(rig.Claim());
    }

    [Theory]
    [InlineData("not said", "the build run left no list of what was built")]
    [InlineData("unchanged", "the build run left the bundle unchanged")]
    [InlineData("failed", "the build run failed")]
    public void A_build_run_that_did_not_say_what_it_built_stops_the_item_and_opens_nothing(string report, string why)
    {
        using var rig = new Rig();
        var build = Specced(rig);

        var body = report switch
        {
            "not said" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = Specced("list-sorting").Replace("sort by date", "sort by date, done") } } }),
            "unchanged" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = Specced("list-sorting") } } }),
            _ => Json(new { status = "failed" }),
        };
        Assert.Equal(200, rig.Routes.Finished(build.Token, body).Status);

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Stopped, why), (item.Stage, item.Status, item.StoppedBecause));
        Assert.Empty(rig.Pulls.Asked);
    }

    [Fact]
    public void A_build_that_flags_itself_is_reviewed_and_an_agreed_review_opens_the_pull_request()
    {
        using var rig = new Rig();

        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting", flag: "the sort may be unstable") });
        Assert.Empty(rig.Pulls.Asked);
        var review = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the review stage", review.Task);

        rig.FinishWith(review, new { slug = "list-sorting", text = Built("list-sorting", "the sort may be unstable").Replace("Cards:", "Review: agreed — stable enough\nCards:") });

        Assert.Contains("Review: agreed — stable enough", Assert.Single(rig.Pulls.Asked).Body);
        Assert.Equal(WorkItemStatus.Done, Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting").Status);
    }

    [Fact]
    public void A_pull_request_github_would_not_open_or_kit_cannot_open_stops_the_built_item_with_the_reason()
    {
        using var refused = new Rig();
        refused.Pulls.Answer = new PullRequestResult { Reason = "GitHub said: No commits between dev and kit/work/list-sorting" };
        refused.FinishWith(Specced(refused), new { slug = "list-sorting", text = Built("list-sorting") });
        var item = Assert.Single(refused.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request could not be opened: GitHub said: No commits between dev and kit/work/list-sorting"), (item.Status, item.StoppedBecause));

        using var none = new Rig(canOpenPulls: false);
        none.FinishWith(Specced(none), new { slug = "list-sorting", text = Built("list-sorting") });
        Assert.Equal("its pull request could not be opened: Kit has no way to open a pull request", Assert.Single(none.Pipeline!.Items(), i => i.Slug == "list-sorting").StoppedBecause);
    }

    [Fact]
    public void A_review_of_a_build_that_drops_its_built_list_stops_the_item_and_opens_nothing()
    {
        using var rig = new Rig();
        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting", flag: "unsure") });

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Specced("list-sorting", "unsure").Replace("Cards:", "Review: revised — trimmed\nCards:") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((WorkItemStatus.Stopped, "the review run left no list of what was built"), (item.Status, item.StoppedBecause));
        Assert.Empty(rig.Pulls.Asked);
    }

    [Fact]
    public void A_build_waiting_starts_before_an_item_earlier_in_the_list_that_a_triage_sent_back_to_spec()
    {
        using var rig = new Rig();
        rig.Note("first note");
        rig.FinishWith(rig.Claim()!, new { slug = "one", text = BundleText("one") }, new { slug = "two", text = BundleText("two") });
        rig.FinishWith(rig.Claim()!, new { slug = "one", text = Specced("one") });
        rig.FinishWith(rig.Claim()!, new { slug = "one", text = Built("one") });
        var specTwo = Assert.IsType<PoolClaim>(rig.Claim());
        rig.Note("second note");

        // two's spec ends with a triage queued, so the triage runs first and sends one, earlier in the list, back to spec.
        rig.FinishWith(specTwo, new { slug = "two", text = Specced("two") });
        rig.FinishWith(rig.Claim()!, new { slug = "one", text = BundleText("one").Replace("sort by date", "sort by date, newest first") });

        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the build stage", next.Task);
        Assert.Contains("Work-item: two", next.Task);
    }

    [Fact]
    public void A_build_task_needs_a_test_plan()
    {
        var bundle = ContextBundle.Parse(BundleText("list-sorting")).Bundle!;

        Assert.Equal("the bundle has no test plan to build from", Assert.Throws<InvalidOperationException>(() => BuildStage.BuildTask("demo", bundle)).Message);
    }

    // Triage reports one bundle for list-sorting; the spec run of it is what is claimed next.
    private static PoolClaim Triaged(Rig rig, string? flag = null)
    {
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag) });
        return Assert.IsType<PoolClaim>(rig.Claim());
    }

    [Fact]
    public void A_bundle_queued_for_spec_starts_a_spec_run_on_its_branch_with_the_bundle_and_the_spec_path()
    {
        using var rig = new Rig();

        var spec = Triaged(rig);

        Assert.Equal(("someone/demo", "dev", "claude-haiku-5-5"), (spec.Repo, spec.BaseBranch, spec.Model));
        Assert.Contains("You are the spec stage for project \"demo\"", spec.Task);
        Assert.Contains("Check out branch kit/work/list-sorting", spec.Task);
        Assert.Contains("Edit the spec at demo.beh", spec.Task);
        Assert.Contains("Work-item: list-sorting", spec.Task);
        Assert.Contains("Write no tests and no code", spec.Task);
        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (item.Stage, item.Status));
    }

    [Fact]
    public void A_spec_run_reporting_its_bundle_changed_starts_the_items_build_and_keeps_the_new_bundle()
    {
        using var rig = new Rig();

        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Running), (item.Stage, item.Status));
        Assert.Contains("Test plan:", rig.Pipeline.Bundle("demo", "list-sorting")!.Context);
    }

    [Theory]
    [InlineData("unchanged", "the spec run left the bundle unchanged")]
    [InlineData("none", "the spec run reported no bundle for its work item")]
    [InlineData("other", "the spec run reported no bundle for its work item")]
    [InlineData("malformed", "its bundle is malformed: line 1 must be the title, \"# <title>\"")]
    [InlineData("huge", "its bundle was too large for the runner to report")]
    [InlineData("failed", "the spec run failed")]
    [InlineData("flag only", "the spec run wrote no test plan")]
    public void A_spec_run_that_did_not_change_its_own_bundle_stops_the_item_with_the_reason(string report, string why)
    {
        using var rig = new Rig();
        var spec = Triaged(rig);

        var body = report switch
        {
            "unchanged" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = (string?)BundleText("list-sorting") } } }),
            "none" => Json(new { status = "finished" }),
            "other" => Json(new { status = "finished", bundles = new[] { new { slug = "another", text = (string?)Specced("another") } } }),
            "malformed" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = (string?)"not a bundle" } } }),
            "huge" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = (string?)null } } }),
            "flag only" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = (string?)BundleText("list-sorting", flag: "unsure") } } }),
            _ => Json(new { status = "failed" }),
        };
        Assert.Equal(200, rig.Routes.Finished(spec.Token, body).Status);

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Stopped, why), (item.Stage, item.Status, item.StoppedBecause));
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Slug == "another");
    }

    [Fact]
    public void A_flagged_triage_is_reviewed_first_and_an_agreed_review_starts_the_spec()
    {
        using var rig = new Rig();

        var review = Triaged(rig, flag: "unsure which card");
        Assert.Equal("claude-opus-5-5", review.Model);
        Assert.Contains("You are the review stage for project \"demo\"", review.Task);
        Assert.Contains("unsure which card", review.Task);

        rig.FinishWith(review, new { slug = "list-sorting", text = BundleText("list-sorting", "unsure which card").Replace("Cards:", "Review: agreed — BEH-A is right\nCards:") });

        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", spec.Task);
        Assert.Contains("Review: agreed — BEH-A is right", spec.Task);
        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running, (PipelineStage?)null), (item.Stage, item.Status, item.ReviewedStage));
    }

    [Fact]
    public void A_spec_that_flags_itself_is_reviewed_and_an_escalated_review_stops_the_item_for_a_person()
    {
        using var rig = new Rig();

        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting", flag: "two cards may conflict") });
        var flagged = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Review, PipelineStage.Spec), (flagged.Stage, flagged.ReviewedStage));

        var review = Assert.IsType<PoolClaim>(rig.Claim());
        rig.FinishWith(review, new { slug = "list-sorting", text = Specced("list-sorting", "two cards may conflict").Replace("Cards:", "Review: escalated — which sort order does he want?\nCards:") });

        var item = Assert.Single(rig.Pipeline.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((WorkItemStatus.Stopped, "which sort order does he want?"), (item.Status, item.StoppedBecause));
    }

    [Fact]
    public void A_review_run_that_failed_or_changed_nothing_stops_the_item()
    {
        using var failed = new Rig();
        failed.Routes.Finished(Triaged(failed, flag: "unsure").Token, Json(new { status = "failed" }));
        Assert.Equal("the review run failed", Assert.Single(failed.Pipeline!.Items(), i => i.Slug == "list-sorting").StoppedBecause);

        using var unchanged = new Rig();
        unchanged.FinishWith(Triaged(unchanged, flag: "unsure"), new { slug = "list-sorting", text = BundleText("list-sorting", "unsure") });
        Assert.Equal("the review run left the bundle unchanged", Assert.Single(unchanged.Pipeline!.Items(), i => i.Slug == "list-sorting").StoppedBecause);
    }

    [Fact]
    public void A_triage_that_updates_a_bundle_waiting_for_spec_hands_the_spec_the_new_bundle()
    {
        using var rig = new Rig();
        rig.Note("first note");
        var first = rig.Claim()!;
        rig.Note("second note");
        rig.FinishWith(first, new { slug = "list-sorting", text = BundleText("list-sorting") });

        // The second triage goes first, and reports the same work item with more context.
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting").Replace("sort by date", "sort by date, newest first") });

        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("newest first", spec.Task);
        Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
    }

    [Fact]
    public void A_review_that_left_no_review_line_stops_the_item()
    {
        using var rig = new Rig();

        rig.FinishWith(Triaged(rig, flag: "unsure"), new { slug = "list-sorting", text = BundleText("list-sorting", "unsure").Replace("sort by date", "sort by date, newest first") });

        Assert.Equal("the review left no Review line", Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting").StoppedBecause);
    }

    [Fact]
    public void Notes_waiting_are_triaged_before_any_bundle_is_specced_and_each_item_is_built_before_the_next_is_specced()
    {
        using var rig = new Rig();
        rig.Note("first note");
        var triage = rig.Claim()!;
        rig.Note("second note");
        rig.FinishWith(triage, new { slug = "one", text = BundleText("one") }, new { slug = "two", text = BundleText("two") });

        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("second note", next.Task);
        Assert.Null(rig.Claim());
        rig.FinishWith(next, new { slug = "three", text = BundleText("three") });

        var first = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("Work-item: one", first.Task);
        Assert.Null(rig.Claim());
        rig.FinishWith(first, new { slug = "one", text = Specced("one") });

        // An item goes on to its build before the next item is specced: one piece of work is finished at a time.
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the build stage", build.Task);
        Assert.Contains("Work-item: one", build.Task);
        Assert.Null(rig.Claim());
        rig.FinishWith(build, new { slug = "one", text = Built("one") });

        var second = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("Work-item: two", second.Task);
        Assert.Contains("You are the spec stage", second.Task);
    }

    [Fact]
    public void A_later_triage_updating_a_bundle_replaces_its_item_and_a_bad_update_drops_the_old_bundle()
    {
        using var rig = new Rig();
        rig.Note("first note");
        var first = rig.Claim()!;
        rig.Note("second note");
        rig.FinishWith(first, new { slug = "list-sorting", text = BundleText("list-sorting") });

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = "# broken" });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal(WorkItemStatus.Stopped, item.Status);
        Assert.Null(rig.Pipeline.Bundle("demo", "list-sorting"));
    }

    [Fact]
    public void A_failed_triage_takes_no_bundles()
    {
        using var rig = new Rig();
        rig.Note("n");

        Assert.Equal(200, rig.Routes.Finished(rig.Claim()!.Token, Json(new { status = "failed", bundles = new[] { new { slug = "x", text = BundleText("x") } } })).Status);

        Assert.DoesNotContain(rig.Pipeline!.Items(), i => i.Slug == "x");
    }

    [Fact]
    public void A_finished_triage_with_no_usable_bundle_stops_and_its_notes_are_read_again()
    {
        using var rig = new Rig();
        rig.Note("first note");

        Assert.Equal(200, rig.Routes.Finished(rig.Claim()!.Token, Json(new { status = "finished" })).Status);

        var stopped = Assert.Single(rig.Pipeline!.Items());
        Assert.Equal("the triage reported no usable bundle", stopped.StoppedBecause);
        Assert.Null(rig.Claim());

        rig.Note("second note");
        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("first note", next.Task);

        rig.FinishWith(next, new { slug = "broken", text = "not a bundle" });
        rig.Note("third note");
        Assert.Contains("first note", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void A_failed_report_with_malformed_bundles_still_ends_the_run()
    {
        using var rig = new Rig();
        rig.Note("n");

        var r = rig.Routes.Finished(rig.Claim()!.Token, JsonDocument.Parse("""{"status":"failed","bundles":"x"}""").RootElement);

        Assert.Equal(200, r.Status);
        Assert.Equal(AgentRunStatus.Failed, rig.Store.List()[0].Status);
    }

    [Theory]
    [InlineData("""{"status":"finished","bundles":"x"}""")]
    [InlineData("""{"status":"finished","bundles":[{"text":"t"}]}""")]
    [InlineData("""{"status":"finished","bundles":[{"slug":"Bad Slug","text":"t"}]}""")]
    [InlineData("""{"status":"finished","bundles":[{"slug":"a","text":7}]}""")]
    [InlineData("""{"status":"finished","bundles":[{"slug":"a","text":"t"},{"slug":"a","text":"t"}]}""")]
    [InlineData("""{"status":"finished","bundles":[{"slug":"triage #1","text":"t"}]}""")]
    public void Malformed_bundles_are_400_and_the_run_is_not_ended(string body)
    {
        using var rig = new Rig();
        rig.Note("n");
        var claim = rig.Claim()!;

        var r = rig.Routes.Finished(claim.Token, JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal("bad-bundles", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Equal(AgentRunStatus.Running, rig.Store.List()[0].Status);
    }

    [Fact]
    public void More_than_the_most_bundles_is_400()
    {
        using var rig = new Rig();
        rig.Note("n");

        var r = rig.FinishWith(rig.Claim()!, Enumerable.Range(0, RunnerPoolRoutes.MaxBundles + 1).Select(i => (object)new { slug = $"b{i}", text = "t" }).ToArray());

        Assert.Equal(400, r.Status);
    }

    [Fact]
    public void Cards_are_each_behaviours_id_and_title_in_file_order()
    {
        Assert.Equal([("BEH-A", "A user sees the list"), ("BEH-B", "A user adds an item")], Pipeline.Cards(Corpus));
    }

    [Fact]
    public void A_note_on_a_project_outside_the_list_queues_nothing_and_one_inside_it_does()
    {
        using var outside = new Rig(projects: new HashSet<string> { "kit" });
        outside.Note("the list should sort by date");
        Assert.Null(outside.Claim());
        Assert.Empty(outside.Pipeline!.Items());

        using var inside = new Rig(projects: new HashSet<string> { "kit", "demo" });
        inside.Note("the list should sort by date");
        Assert.NotNull(inside.Claim());
    }

    [Fact]
    public void KIT_PIPELINE_PROJECTS_names_the_projects_triaged_and_unset_means_all()
    {
        Assert.Null(KitSettings.ParsePipelineProjects(null));
        Assert.Null(KitSettings.ParsePipelineProjects(" "));
        Assert.Equal(new HashSet<string> { "kit", "kit-ui" }, KitSettings.ParsePipelineProjects(" kit , kit-ui "));
        foreach (var bad in new[] { "kit,", "Kit", "kit;kit-ui", "kit,,kit-ui" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineProjects(bad));
        }
    }

    [Fact]
    public void KIT_PIPELINE_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipeline(null));
        Assert.False(KitSettings.ParsePipeline(" "));
        Assert.False(KitSettings.ParsePipeline("off"));
        Assert.True(KitSettings.ParsePipeline(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipeline(bad));
        }
    }
}
