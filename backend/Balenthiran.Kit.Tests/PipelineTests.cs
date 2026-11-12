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
        private readonly string root;
        private readonly bool owned;

        public Rig(string corpus = Corpus, bool on = true, IReadOnlySet<string>? projects = null, bool canOpenPulls = true, StageTurnSettings? turns = null, bool scheduled = false, string? schedule = null, ITriagedNoteStore? sorted = null, string? root = null, bool merge = false, TimeProvider? clock = null, ITicketStore? tickets = null)
        {
            owned = root is null;
            Tickets = tickets ?? new MemoryTicketStore();
            this.root = root ?? Directory.CreateTempSubdirectory("kit-pipeline-").FullName;
            root = this.root;
            Sorted = sorted ?? new MemoryTriagedNoteStore();
            File.WriteAllText(Path.Combine(root, "demo.beh"), corpus);
            if (schedule is not null)
            {
                File.WriteAllText(Path.Combine(root, "demo.schedule"), schedule);
            }

            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Pipeline = on ? new Pipeline(corpora, Pool, Store, Models, clock: clock, projects: projects, pulls: canOpenPulls ? Pulls : null, turns: turns, scheduled: scheduled, sorted: Sorted, merger: Merges, merge: merge, tickets: Tickets) : null;
            // One fixed minute: every note shares its date, as notes left in the same minute do, so a ref alone cannot tell them apart.
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, clock: new Clock(), runStore: Store, runLauncher: Pool, runnerPool: Pool, pipeline: Pipeline);
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, pipeline: Pipeline);
        }

        public MemoryAgentRunStore Store { get; }

        public ITriagedNoteStore Sorted { get; }

        public ITicketStore Tickets { get; }

        public Opener Pulls { get; } = new();

        public Merger Merges { get; } = new();

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

        // Kit restarting: the same files and the same triaged-notes store, but a new pool, run store and pipeline,
        // so everything kept in memory is gone. Disposing the restarted rig leaves the files to this one. Tickets
        // are kept only when asked for (BEH-ORCH-11), so the BEH-ORCH-8 restarts above them are as they were.
        public Rig Restart(ITriagedNoteStore? sorted = null, ITicketStore? tickets = null) => new(sorted: sorted ?? Sorted, root: root, tickets: tickets);

        public void Dispose()
        {
            if (owned)
            {
                Directory.Delete(root, recursive: true);
            }
        }
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

    // Records each pull request asked to be merged and answers with Answer (BEH-ORCH-10); OnAsk runs once, during the first ask.
    private sealed class Merger : IPullRequestMerger
    {
        public List<string> Asked { get; } = [];

        public IPullRequestMerge Answer { get; set; } = new PullRequestMerge { Merged = true };

        public Func<Task>? OnAsk { get; set; }

        public async Task<IPullRequestMerge> MergeIfGreenAsync(string pullRequestUrl, string? repository, CancellationToken cancellationToken)
        {
            Asked.Add(pullRequestUrl);
            var answer = Answer;
            if (OnAsk is { } during)
            {
                OnAsk = null;
                Answer = new PullRequestMerge { Merged = true };
                await during();
            }

            return answer;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 5, 12, 30, 0, TimeSpan.Zero);
    }

    // A clock whose timers fire only when the test moves it on, so a retry's hour costs no wait.
    private sealed class Ticking : TimeProvider
    {
        private readonly List<Tick> ticks = [];
        private DateTimeOffset now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

        public int Pending
        {
            get
            {
                lock (ticks)
                {
                    return ticks.Count(t => !t.Disposed);
                }
            }
        }

        public override DateTimeOffset GetUtcNow() => now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var tick = new Tick(callback, state, now + dueTime);
            lock (ticks)
            {
                ticks.Add(tick);
            }

            return tick;
        }

        public void Advance(TimeSpan by)
        {
            now += by;
            List<Tick> due;
            lock (ticks)
            {
                due = ticks.Where(t => !t.Disposed && t.Due <= now).ToList();
            }

            foreach (var t in due)
            {
                t.Callback(t.State);
            }
        }

        private sealed class Tick(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public TimerCallback Callback { get; } = callback;

            public object? State { get; } = state;

            public DateTimeOffset Due { get; } = due;

            public bool Disposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose() => Disposed = true;

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }
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
    public void BEH_ORCH_2_A_low_level_flag_is_reviewed_on_the_cheaper_model()
    {
        using var rig = new Rig();

        Assert.Equal("claude-sonnet-5-5", Triaged(rig, flag: "unsure of the wording (level: low)").Model);
    }

    [Fact]
    public void BEH_ORCH_2_The_spec_and_build_tasks_say_what_the_levels_mean()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);
        Assert.Contains("level: low", spec.Task);
        Assert.Contains("level: high", spec.Task);

        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("level: low", build.Task);
        Assert.Contains("level: high", build.Task);
    }

    [Theory]
    [InlineData("touches the schema (level: high)")]
    [InlineData("touches the schema")]
    [InlineData("touches the schema (level: mid)")]
    public void BEH_ORCH_2_A_high_unlevelled_or_unreadable_flag_is_reviewed_on_the_review_model(string flag)
    {
        using var rig = new Rig();

        Assert.Equal("claude-opus-5-5", Triaged(rig, flag: flag).Model);
    }

    [Fact]
    public void BEH_ORCH_5_A_turn_limit_that_is_set_reaches_the_claim()
    {
        using var rig = new Rig(turns: new StageTurnSettings(3, 4, 5, 6));
        rig.Note("the list should sort by date");

        Assert.Equal(3, Assert.IsType<PoolClaim>(rig.Claim()).MaxTurns);
    }

    [Fact]
    public void BEH_ORCH_6_The_memory_store_keeps_the_work_item_a_run_was_created_for()
    {
        var store = new MemoryAgentRunStore();
        var now = DateTimeOffset.UtcNow;
        var forItem = store.Create("ann", "demo", "m", "h1", now, "list-sorting");
        var none = store.Create("ann", "demo", "m", "h2", now);

        Assert.Equal("list-sorting", forItem.WorkItem);
        Assert.Null(none.WorkItem);
        store.AddUsage(forItem.Id, new AgentUsage(1, 2, 0, 0), 0.1m);
        Assert.Equal("list-sorting", store.ByTokenHash("h1")!.WorkItem);
    }

    [Fact]
    public void BEH_ORCH_6_A_triage_run_has_no_work_item_and_spec_and_build_runs_carry_the_slug()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Null(Assert.Single(rig.Store.List()).WorkItem);

        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });
        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("list-sorting", rig.Store.List()[0].WorkItem);

        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });
        Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("list-sorting", rig.Store.List()[0].WorkItem);
        Assert.Equal(3, rig.Store.List().Count);
        Assert.Single(rig.Store.List(), r => r.WorkItem is null);
    }

    [Fact]
    public void BEH_ORCH_6_The_runs_json_carries_each_runs_work_item()
    {
        var store = new MemoryAgentRunStore();
        store.Create("ann", "demo", "m", "h1", DateTimeOffset.UtcNow, "list-sorting");
        store.Create("ann", "demo", "m", "h2", DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(new AgentRunList { CapUsd = 1m, Runs = store.List() }, typeof(AgentRunList), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var doc = JsonDocument.Parse(json);
        var runs = doc.RootElement.GetProperty("runs").EnumerateArray().ToList();
        Assert.Equal(JsonValueKind.Null, runs[0].GetProperty("workItem").ValueKind);
        Assert.Equal("list-sorting", runs[1].GetProperty("workItem").GetString());
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

    private const string ScheduledCorpus = """
        # kit:repo someone/demo@dev

        behaviour BEH-A "A user sees the list"
          source defined demo.notes.md#2026-11-07-0151
          pending
          contract the list sorts by date
          contract the newest is first

        behaviour BEH-B "A user adds an item"
          pending
        """;

    [Fact]
    public void BEH_ORCH_4_with_the_switch_off_a_scheduled_card_is_not_queued()
    {
        using var rig = new Rig(ScheduledCorpus, schedule: "BEH-A\n");

        rig.Pipeline!.ScheduleChanged("demo");
        Assert.Null(rig.Claim());
        rig.Note("the list should sort by date");

        Assert.Contains("You are the triage stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Stage == PipelineStage.Spec);
    }

    [Fact]
    public void BEH_ORCH_4_the_first_scheduled_card_starts_at_spec_with_a_bundle_kit_wrote_and_no_triage()
    {
        using var rig = new Rig(ScheduledCorpus, scheduled: true, schedule: "BEH-A\nBEH-B\n");

        rig.Pipeline!.ScheduleChanged("demo");

        var item = Assert.Single(rig.Pipeline.Items());
        Assert.Equal(("beh-a", PipelineStage.Spec, WorkItemStatus.Running), (item.Slug, item.Stage, item.Status));
        var bundle = Assert.IsType<ContextBundle>(rig.Pipeline.Bundle("demo", "beh-a"));
        Assert.Equal("A user sees the list", bundle.Title);
        Assert.Equal(["BEH-A"], bundle.Cards);
        Assert.Equal(["demo.notes.md#2026-11-07-0151"], bundle.Notes);
        Assert.Empty(bundle.Files);
        Assert.Contains("the list sorts by date", bundle.Context);
        Assert.Contains("the newest is first", bundle.Context);
        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", claim.Task);
        Assert.Contains("create it from the branch you started on", claim.Task);
        Assert.Contains("Work-item: beh-a", claim.Task);
        Assert.Contains("the newest is first", claim.Task);
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Stage == PipelineStage.Triage);
    }

    [Fact]
    public void BEH_ORCH_4_a_queued_triage_goes_before_the_scheduled_card()
    {
        using var rig = new Rig(ScheduledCorpus, scheduled: true, schedule: "BEH-A\n");

        rig.Note("the list should sort by date");

        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the triage stage", claim.Task);
        Assert.Contains(rig.Pipeline!.Items(), i => i.Slug == "beh-a" && i.Stage == PipelineStage.Spec && i.Status == WorkItemStatus.Queued);

        rig.Finish(claim);

        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", spec.Task);
    }

    [Fact]
    public void BEH_ORCH_4_a_paused_scheduled_card_is_skipped_for_the_next()
    {
        var corpus = ScheduledCorpus.Replace("  pending\n  contract the list sorts", "  pending\n  paused\n  contract the list sorts");
        Assert.Contains("paused", corpus);
        using var rig = new Rig(corpus, scheduled: true, schedule: "BEH-A\nBEH-B\n");

        rig.Pipeline!.ScheduleChanged("demo");

        Assert.Equal(["beh-b"], rig.Pipeline.Items().Select(i => i.Slug));
    }

    [Fact]
    public void BEH_ORCH_4_a_second_scheduled_card_waits_while_the_first_is_unfinished()
    {
        using var rig = new Rig(ScheduledCorpus, scheduled: true, schedule: "BEH-A\nBEH-B\n");
        rig.Pipeline!.ScheduleChanged("demo");
        var claim = Assert.IsType<PoolClaim>(rig.Claim());

        rig.Pipeline.ScheduleChanged("demo");

        Assert.Equal(["beh-a"], rig.Pipeline.Items().Where(i => i.Stage == PipelineStage.Spec).Select(i => i.Slug));

        rig.Finish(claim, status: "failed");

        Assert.Contains(rig.Pipeline.Items(), i => i.Slug == "beh-b" && i.Stage == PipelineStage.Spec && i.Status == WorkItemStatus.Running);
    }

    [Fact]
    public void BEH_ORCH_4_KIT_PIPELINE_SCHEDULED_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineScheduled(null));
        Assert.False(KitSettings.ParsePipelineScheduled(" "));
        Assert.False(KitSettings.ParsePipelineScheduled("off"));
        Assert.True(KitSettings.ParsePipelineScheduled(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineScheduled(bad));
        }
    }

    // ---- BEH-ORCH-10: the pipeline merges the pull request of what it built ---------------------------

    private static async Task<Rig> BuiltRig(bool merge = true)
    {
        var rig = new Rig(merge: merge);
        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting") });
        await Task.Yield();
        return rig;
    }

    private static WorkItem BuiltItem(Rig rig) => Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");

    [Fact]
    public async Task BEH_ORCH_10_a_built_item_whose_pull_request_is_green_is_merged_once()
    {
        using var rig = await BuiltRig();

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);
        await rig.Pipeline.MergeReadyAsync(CancellationToken.None);

        Assert.Equal(["https://github.com/someone/demo/pull/7"], rig.Merges.Asked);
        var item = BuiltItem(rig);
        Assert.True(item.Merged);
        Assert.Equal(WorkItemStatus.Done, item.Status);
    }

    [Fact]
    public async Task BEH_ORCH_10_a_pull_request_still_waiting_is_left_alone_and_asked_again_next_time()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Waiting = true };

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);
        await rig.Pipeline.MergeReadyAsync(CancellationToken.None);

        Assert.Equal(2, rig.Merges.Asked.Count);
        var item = BuiltItem(rig);
        Assert.False(item.Merged);
        Assert.Equal(WorkItemStatus.Done, item.Status);
    }

    [Fact]
    public async Task BEH_ORCH_10_a_refused_merge_stops_the_item_with_the_reason_and_is_not_asked_again()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Reason = "ci did not succeed (failure)" };

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);
        await rig.Pipeline.MergeReadyAsync(CancellationToken.None);

        var item = BuiltItem(rig);
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: ci did not succeed (failure)"), (item.Status, item.StoppedBecause));
        Assert.Single(rig.Merges.Asked);
    }

    [Fact]
    public async Task BEH_ORCH_10_with_merging_off_nothing_is_asked()
    {
        using var rig = await BuiltRig(merge: false);

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Empty(rig.Merges.Asked);
        Assert.False(BuiltItem(rig).Merged);
    }

    [Fact]
    public async Task BEH_ORCH_10_an_item_that_changed_while_the_merge_was_asked_is_left_alone()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Reason = "late refusal" };

        // While the first look waits on GitHub, a second look merges the item: the first must not stop it.
        rig.Merges.OnAsk = () => rig.Pipeline!.MergeReadyAsync(CancellationToken.None);
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        var item = BuiltItem(rig);
        Assert.True(item.Merged);
        Assert.Equal(WorkItemStatus.Done, item.Status);
        Assert.Null(item.StoppedBecause);
    }

    [Fact]
    public async Task BEH_ORCH_10_the_pipeline_lists_whether_each_item_was_merged()
    {
        using var rig = await BuiltRig();
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        var list = Assert.IsType<PipelineItemList>(rig.Router.Route("GET", "/api/pipeline").Body);

        Assert.True(Assert.Single(list.Items, i => i.Slug == "list-sorting").Merged);
        Assert.Contains("\"merged\":true", JsonSerializer.Serialize(list, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BEH_ORCH_10_KIT_PIPELINE_MERGE_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineMerge(null));
        Assert.False(KitSettings.ParsePipelineMerge(" "));
        Assert.False(KitSettings.ParsePipelineMerge("off"));
        Assert.True(KitSettings.ParsePipelineMerge(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineMerge(bad));
        }
    }

    // ---- BEH-ORCH-9: the pipeline page's data ---------------------------------------------------------

    [Fact]
    public void BEH_ORCH_9_the_pipeline_lists_each_work_item_with_its_stage_status_and_flag()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card") });

        var r = rig.Router.Route("GET", "/api/pipeline");

        Assert.Equal(200, r.Status);
        var list = Assert.IsType<PipelineItemList>(r.Body);
        Assert.True(list.On);
        Assert.Equal(["triage #1", "list-sorting", "triage #2"], list.Items.Select(i => i.Slug));
        var item = list.Items[1];
        Assert.Equal(("demo", "review", "running", "unsure which card"), (item.Project, item.Stage, item.Status, item.Flag));
        Assert.Null(item.Review);
        Assert.Equal("done", list.Items[0].Status);
    }

    [Fact]
    public void BEH_ORCH_9_a_stopped_item_says_why_and_a_reviewed_one_carries_its_outcome()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card") });
        var review = rig.Claim()!;
        rig.FinishWith(review, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card").Replace("Cards:", "Review: escalated — he must pick the card\nCards:") });

        var item = Assert.Single(Assert.IsType<PipelineItemList>(rig.Router.Route("GET", "/api/pipeline").Body).Items, i => i.Slug == "list-sorting");

        Assert.Equal(("stopped", "escalated", "he must pick the card"), (item.Status, item.Review, item.ReviewNote));
        Assert.NotNull(item.StoppedBecause);
    }

    [Fact]
    public void BEH_ORCH_9_each_pipeline_run_records_its_stage_and_a_run_by_hand_none()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });
        var byHand = rig.Store.Create("james", "demo", "m", "hand", DateTimeOffset.UtcNow);

        var runs = rig.Store.List();
        Assert.Equal(["spec", "triage"], runs.Where(r => r.Stage is not null).Select(r => r.Stage!).Order());
        Assert.Equal("list-sorting", Assert.Single(runs, r => r.Stage == "spec").WorkItem);
        Assert.Null(byHand.Stage);
    }

    [Fact]
    public void BEH_ORCH_9_with_the_pipeline_off_the_list_is_empty_and_says_so()
    {
        using var rig = new Rig(on: false);

        var list = Assert.IsType<PipelineItemList>(rig.Router.Route("GET", "/api/pipeline").Body);

        Assert.False(list.On);
        Assert.Empty(list.Items);
    }

    // ---- BEH-ORCH-8: the notes a triage sorted survive a restart ---------------------------------------

    [Fact]
    public void BEH_ORCH_8_after_a_restart_a_note_triages_only_the_notes_no_triage_has_sorted()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));

        using var restarted = rig.Restart();
        restarted.Note("the list should export");

        var task = Assert.IsType<PoolClaim>(restarted.Claim()).Task;
        Assert.Contains("the list should export", task);
        Assert.DoesNotContain("the list should sort by date", task);
    }

    [Fact]
    public void BEH_ORCH_8_without_the_store_a_restart_triages_every_note_again()
    {
        // The other side of the seam: the store is what remembers, not something else that survives a restart.
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));

        using var restarted = rig.Restart(new MemoryTriagedNoteStore());
        restarted.Note("the list should export");

        Assert.Contains("the list should sort by date", Assert.IsType<PoolClaim>(restarted.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_8_on_start_a_note_whose_triage_failed_is_triaged_without_a_new_note()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));
        rig.Note("the list should export");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");

        using var restarted = rig.Restart();
        restarted.Pipeline!.Resume();

        var task = Assert.IsType<PoolClaim>(restarted.Claim()).Task;
        Assert.Contains("the list should export", task);
        Assert.DoesNotContain("the list should sort by date", task);
    }

    [Fact]
    public void BEH_ORCH_8_a_failed_triage_is_tried_again_an_hour_later_on_the_notes_still_unsorted()
    {
        var time = new Ticking();
        using var rig = new Rig(clock: time);
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed"); // the spec run of the bundle it reported
        rig.Note("the list should export");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        Assert.Null(rig.Claim());

        time.Advance(TimeSpan.FromMinutes(59));
        Assert.Null(rig.Claim());

        time.Advance(TimeSpan.FromMinutes(1));
        var task = Assert.IsType<PoolClaim>(rig.Claim()).Task;
        Assert.Contains("the list should export", task);
        Assert.DoesNotContain("the list should sort by date", task);
    }

    [Fact]
    public void BEH_ORCH_8_a_triage_that_works_ends_the_retries()
    {
        var time = new Ticking();
        using var rig = new Rig(clock: time);
        rig.Note("the list should export");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        Assert.Equal(1, time.Pending);

        // A new note's triage works before the hour is up: nothing is left to retry.
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));
        Assert.Equal(0, time.Pending);
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed"); // the spec run of the bundle it reported

        time.Advance(TimeSpan.FromHours(2));
        Assert.Null(rig.Claim());

        // And the count starts again: one failure afterwards schedules a retry, as a first failure does.
        rig.Note("the list should print");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        Assert.Equal(1, time.Pending);
    }

    [Fact]
    public void BEH_ORCH_8_a_triage_failing_while_a_retry_is_pending_does_not_add_a_second()
    {
        var time = new Ticking();
        using var rig = new Rig(clock: time);
        rig.Note("the list should export");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        rig.Note("the list should print");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");

        Assert.Equal(1, time.Pending);
        time.Advance(TimeSpan.FromHours(1));
        Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_8_a_failing_triage_is_tried_again_twelve_times_and_then_stops()
    {
        var time = new Ticking();
        using var rig = new Rig(clock: time);
        rig.Note("the list should export");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        for (var retry = 1; retry <= Pipeline.MaxRetries; retry++)
        {
            Assert.Equal(1, time.Pending);
            time.Advance(Pipeline.RetryAfter);
            rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()), status: "failed");
        }

        // The twelfth retry failed too: nothing more is scheduled.
        Assert.Equal(0, time.Pending);
        time.Advance(TimeSpan.FromDays(1));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_8_on_start_nothing_runs_when_every_note_is_sorted_or_none_was_ever_recorded()
    {
        using var sorted = new Rig();
        sorted.Note("the list should sort by date");
        sorted.Finish(Assert.IsType<PoolClaim>(sorted.Claim()));
        using var allSorted = sorted.Restart();
        allSorted.Pipeline!.Resume();
        Assert.Null(allSorted.Claim());

        // Never recorded: the first start with a store must not triage the whole notes file without a note from him.
        using var failed = new Rig();
        failed.Note("the list should sort by date");
        failed.Finish(Assert.IsType<PoolClaim>(failed.Claim()), status: "failed");
        using var fresh = failed.Restart();
        fresh.Pipeline!.Resume();
        Assert.Null(fresh.Claim());
        Assert.Empty(fresh.Store.List());
    }

    [Fact]
    public void BEH_ORCH_8_a_set_aside_test_note_is_recorded_as_sorted_too()
    {
        using var rig = new Rig();
        rig.Note("test");
        Assert.Null(rig.Claim());
        Assert.Single(rig.Sorted.Read("demo"));
        Assert.Contains(rig.Pipeline!.Items(), i => i.Slug == TriageStage.SetAside);

        using var restarted = rig.Restart();
        restarted.Note("the list should export");

        Assert.IsType<PoolClaim>(restarted.Claim());
        Assert.DoesNotContain(restarted.Pipeline!.Items(), i => i.Slug == TriageStage.SetAside);
    }

    [Fact]
    public void BEH_ORCH_8_the_store_keeps_a_hash_of_a_note_not_its_text()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.Finish(Assert.IsType<PoolClaim>(rig.Claim()));

        var key = Assert.Single(rig.Sorted.Read("demo"));
        Assert.Matches("^[0-9a-f]{64}$", key);
        Assert.Equal(["demo"], rig.Sorted.Projects());
    }

    [Fact]
    public void BEH_ORCH_8_the_postgres_store_keeps_keys_per_project_and_ignores_a_repeat()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresTriagedNoteStore(connection);
        var project = $"p{Guid.NewGuid():N}"[..12];
        var other = $"p{Guid.NewGuid():N}"[..12];

        store.Add(project, ["a", "b"]);
        store.Add(project, ["b", "c"]);
        store.Add(other, ["a"]);
        store.Add(project, []);

        Assert.Equal(["a", "b", "c"], store.Read(project).Order(StringComparer.Ordinal));
        Assert.Equal(["a"], store.Read(other));
        Assert.Empty(store.Read($"p{Guid.NewGuid():N}"[..12]));
        Assert.Contains(project, store.Projects());
    }

    // ---- BEH-ORCH-11, slice 1: tickets kept in the database --------------------------------------------

    private static PipelineTicket Ticket(Rig rig) => Assert.Single(rig.Tickets.Read("demo"), t => t.Slug == "list-sorting");

    [Fact]
    public void BEH_ORCH_11_a_bundle_a_triage_reports_is_written_as_a_ticket_and_the_triage_is_not()
    {
        using var rig = new Rig();

        Triaged(rig);

        var ticket = Ticket(rig);
        Assert.Equal((TicketState.Ready, 3, "Sort the list"), (ticket.State, ticket.Priority, ticket.Title));
        Assert.Equal(["BEH-A"], ticket.Cards);
        Assert.Contains("Work-item: list-sorting", ticket.Bundle);
        Assert.Null(ticket.PullRequest);
        Assert.Single(rig.Tickets.Read("demo"));
    }

    [Fact]
    public async Task BEH_ORCH_11_a_ticket_follows_its_item_from_spec_to_build_to_a_pull_request_to_merged()
    {
        using var rig = await BuiltRig(merge: true);
        Assert.Equal((TicketState.Built, "https://github.com/someone/demo/pull/7"), (Ticket(rig).State, Ticket(rig).PullRequest));
        Assert.Contains("Built:", Ticket(rig).Bundle);

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Equal(TicketState.Merged, Ticket(rig).State);
    }

    [Fact]
    public void BEH_ORCH_11_a_ticket_being_built_is_planned_and_keeps_the_spec_runs_bundle()
    {
        using var rig = new Rig();

        Specced(rig);

        var ticket = Ticket(rig);
        Assert.Equal(TicketState.Planned, ticket.State);
        Assert.Contains("Test plan:", ticket.Bundle);
    }

    [Fact]
    public void BEH_ORCH_11_a_ticket_whose_run_failed_is_stopped_with_why()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);

        rig.Finish(spec, status: "failed");

        var ticket = Ticket(rig);
        Assert.Equal((TicketState.Stopped, "the spec run failed"), (ticket.State, ticket.StoppedBecause));
    }

    [Fact]
    public void BEH_ORCH_11_after_a_restart_a_ready_ticket_is_specced_again_from_its_bundle()
    {
        using var rig = new Rig();
        Triaged(rig);

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();

        var spec = Assert.IsType<PoolClaim>(restarted.Claim());
        Assert.Contains("You are the spec stage for project \"demo\"", spec.Task);
        Assert.Contains("Work-item: list-sorting", spec.Task);
        var item = Assert.Single(restarted.Pipeline.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (item.Stage, item.Status));
    }

    [Fact]
    public void BEH_ORCH_11_after_a_restart_a_planned_ticket_is_built_from_its_test_plan()
    {
        using var rig = new Rig();
        Specced(rig);

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();

        var build = Assert.IsType<PoolClaim>(restarted.Claim());
        Assert.Contains("You are the build stage for project \"demo\"", build.Task);
        Assert.Contains("- BEH-A: the list is sorted by date", build.Task);
    }

    [Fact]
    public async Task BEH_ORCH_11_after_a_restart_a_built_ticket_keeps_its_pull_request_and_is_not_built_again()
    {
        using var rig = await BuiltRig(merge: false);

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();

        Assert.Null(restarted.Claim());
        var item = Assert.Single(restarted.Pipeline.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, "https://github.com/someone/demo/pull/7"), (item.Stage, item.Status, item.PullRequest));
        Assert.Contains("Built:", restarted.Pipeline.Bundle("demo", "list-sorting")!.Context);
    }

    [Fact]
    public async Task BEH_ORCH_11_after_a_restart_stopped_and_merged_tickets_are_not_resumed()
    {
        using var stopped = new Rig();
        stopped.Finish(Triaged(stopped), status: "failed");
        using var afterStop = stopped.Restart(tickets: stopped.Tickets);
        afterStop.Pipeline!.Resume();
        Assert.Null(afterStop.Claim());
        Assert.Empty(afterStop.Pipeline.Items());

        using var merged = await BuiltRig(merge: true);
        await merged.Pipeline!.MergeReadyAsync(CancellationToken.None);
        using var afterMerge = merged.Restart(tickets: merged.Tickets);
        afterMerge.Pipeline!.Resume();
        Assert.Null(afterMerge.Claim());
        Assert.Empty(afterMerge.Pipeline.Items());
    }

    [Fact]
    public void BEH_ORCH_11_without_the_ticket_store_a_restart_forgets_the_ticket()
    {
        // The other side of the seam: the store is what remembers.
        using var rig = new Rig();
        Triaged(rig);

        using var restarted = rig.Restart();
        restarted.Pipeline!.Resume();

        Assert.Null(restarted.Claim());
        Assert.Empty(restarted.Pipeline.Items());
    }

    [Fact]
    public void BEH_ORCH_11_a_resumed_ticket_is_not_added_twice_by_a_second_resume()
    {
        using var rig = new Rig();
        Triaged(rig);

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();
        restarted.Pipeline.Resume();

        Assert.Single(restarted.Pipeline.Items(), i => i.Slug == "list-sorting");
    }

    [Fact]
    public void BEH_ORCH_11_a_scheduled_ticket_is_still_specced_from_its_card_after_a_restart()
    {
        using var rig = new Rig(ScheduledCorpus, scheduled: true, schedule: "BEH-A\n");
        rig.Pipeline!.ScheduleChanged("demo");
        var ticket = Assert.Single(rig.Tickets.Read("demo"));
        Assert.Equal(("beh-a", TicketState.Ready, true), (ticket.Slug, ticket.State, ticket.Scheduled));

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();

        Assert.Contains("create it from the branch you started on", Assert.IsType<PoolClaim>(restarted.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_the_postgres_store_keeps_a_ticket_across_stores_and_replaces_it_on_save()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        var project = $"p{Guid.NewGuid():N}"[..12];
        var ticket = new PipelineTicket(project, "list-sorting", "Sort the list", TicketState.Ready, 3, ["BEH-A", "BEH-B"], "# Sort the list\n", null, null, false);
        using (var store = new PostgresTicketStore(connection))
        {
            store.Save(ticket);
            store.Save(ticket with { State = TicketState.Built, PullRequest = "https://github.com/someone/demo/pull/7" });
            store.Save(ticket with { Slug = "other", State = TicketState.Stopped, StoppedBecause = "the spec run failed", Bundle = null });
        }

        using var again = new PostgresTicketStore(connection);
        var all = again.Read(project);
        Assert.Equal(["list-sorting", "other"], all.Select(t => t.Slug));
        Assert.Equal((TicketState.Built, "https://github.com/someone/demo/pull/7", (IReadOnlyList<string>)["BEH-A", "BEH-B"]), (all[0].State, all[0].PullRequest, all[0].Cards));
        Assert.Equal(("the spec run failed", null), (all[1].StoppedBecause, all[1].Bundle));
        Assert.Equal(["list-sorting"], again.Open().Where(t => t.Project == project).Select(t => t.Slug));
    }

    // ---- BEH-ORCH-11, slice 2: triage reads and updates open tickets ------------------------------------

    // A ticket row as the pipeline writes it: its bundle is the real rendering of a parsed bundle.
    private static PipelineTicket Seeded(string slug, TicketState state = TicketState.Ready, int priority = 3, string? title = null)
    {
        var bundle = ContextBundle.Parse(BundleText(slug)).Bundle!;
        return new PipelineTicket("demo", slug, title ?? bundle.Title, state, priority, bundle.Cards, bundle.Render(), null, null, false);
    }

    private static PoolClaim TriageClaim(Rig rig, params PipelineTicket[] tickets)
    {
        foreach (var t in tickets)
        {
            rig.Tickets.Save(t);
        }

        rig.Note("the list should sort by date");
        var claim = rig.Claim()!;
        Assert.Contains("You are the triage stage", claim.Task);
        return claim;
    }

    [Fact]
    public void BEH_ORCH_11_the_triage_task_lists_the_open_tickets_and_not_the_merged_or_stopped_ones()
    {
        using var rig = new Rig();

        var task = TriageClaim(rig, Seeded("list-sorting"), Seeded("already-merged", TicketState.Merged), Seeded("gave-up", TicketState.Stopped)).Task;

        Assert.Contains("## Open tickets (1)\n- list-sorting [ready] Sort the list (BEH-A)\n", task);
        Assert.DoesNotContain("already-merged", task);
        Assert.DoesNotContain("gave-up", task);
        Assert.Contains("report a bundle with THAT ticket's slug", task);
    }

    [Fact]
    public void BEH_ORCH_11_a_triage_with_no_open_ticket_has_no_ticket_section_or_instruction()
    {
        using var rig = new Rig();

        var task = TriageClaim(rig, Seeded("already-merged", TicketState.Merged)).Task;

        Assert.DoesNotContain("Open tickets", task);
        Assert.DoesNotContain("THAT ticket's slug", task);
    }

    [Fact]
    public void BEH_ORCH_11_a_bundle_reported_under_an_open_tickets_slug_updates_it_rather_than_opening_a_second()
    {
        using var rig = new Rig();
        var claim = TriageClaim(rig, Seeded("list-sorting"));
        rig.Pipeline!.Resume();
        var updated = BundleText("list-sorting").Replace("sort by date", "sort by date, newest first", StringComparison.Ordinal).Replace("- BEH-A", "- BEH-A\n- BEH-B", StringComparison.Ordinal);

        rig.FinishWith(claim, new { slug = "list-sorting", text = updated });

        Assert.Single(rig.Pipeline.Items(), i => i.Slug == "list-sorting");
        var ticket = Ticket(rig);
        Assert.Single(rig.Tickets.Read("demo"));
        Assert.Equal(["BEH-A", "BEH-B"], ticket.Cards);
        Assert.Contains("newest first", ticket.Bundle);
        Assert.Contains("newest first", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_a_bundle_under_a_new_slug_still_opens_a_ticket_and_leaves_the_open_one_alone()
    {
        using var rig = new Rig();
        var claim = TriageClaim(rig, Seeded("list-sorting"));

        rig.FinishWith(claim, new { slug = "another-thing", text = BundleText("another-thing") });

        Assert.Equal(["list-sorting", "another-thing"], rig.Tickets.Read("demo").Select(t => t.Slug));
        Assert.Equal(Seeded("list-sorting").Bundle, rig.Tickets.Read("demo")[0].Bundle);
    }

    [Fact]
    public void BEH_ORCH_11_the_ticket_list_holds_the_highest_priority_first_and_at_most_the_cap()
    {
        using var rig = new Rig();
        var many = Enumerable.Range(1, TriageStage.MaxTickets + 10).Select(i => Seeded($"t-{i}", priority: i == 7 ? 1 : 3)).ToArray();

        var task = TriageClaim(rig, many).Task;

        Assert.Contains($"## Open tickets ({TriageStage.MaxTickets + 10})\n- t-7 [ready]", task);
        Assert.Equal(TriageStage.MaxTickets, task.Split('\n').Count(l => l.StartsWith("- t-", StringComparison.Ordinal)));
        Assert.Contains("- t-60 [ready]", task);
        Assert.DoesNotContain("- t-10 [ready]", task);
        Assert.Contains("10 more open tickets not listed", task);
        Assert.InRange(task.Length, 1, KitRouter.MaxTask);
    }

    [Fact]
    public void BEH_ORCH_11_long_ticket_lists_are_cut_by_size_and_never_push_the_task_past_its_limit()
    {
        var long60 = new string('w', TriageStage.MaxTicketTitle * 2);
        var tickets = Enumerable.Range(1, TriageStage.MaxTickets)
            .Select(i => Seeded($"ticket-{i}", title: long60) with { Cards = ["BEH-A", "BEH-B", "BEH-C", "BEH-D", "BEH-E", "new: " + long60] })
            .ToList();

        var section = TriageStage.TicketSection(tickets);
        var task = TriageStage.BuildTask("demo", [("demo.notes.md#x", new string('n', 6000))], [("BEH-A", "A card")], tickets);

        Assert.InRange(section.Length, 1, TriageStage.MaxTicketChars + 120);
        Assert.Contains("more open tickets not listed", section);
        Assert.Contains($"{new string('w', TriageStage.MaxTicketTitle - 1)}…", section);
        Assert.DoesNotContain(long60, section);
        Assert.Contains("+2 more", section);
        Assert.InRange(task.Text.Length, 1, KitRouter.MaxTask);
        Assert.Equal(1, task.NotesTaken);
    }
}
