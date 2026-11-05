using System.Text.Json;
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

        public Rig(string corpus = Corpus, bool on = true, IReadOnlySet<string>? projects = null)
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), corpus);
            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Pipeline = on ? new Pipeline(corpora, Pool, Store, Models, projects: projects) : null;
            // One fixed minute: every note shares its date, as notes left in the same minute do, so a ref alone cannot tell them apart.
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, clock: new Clock(), runStore: Store, runLauncher: Pool, runnerPool: Pool, pipeline: Pipeline);
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, pipeline: Pipeline);
        }

        public MemoryAgentRunStore Store { get; }

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
    public void A_triage_reporting_a_bundle_queues_its_work_item_for_spec_and_keeps_the_bundle()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");

        Assert.Equal(200, rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") }).Status);

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (item.Stage, item.Status));
        Assert.Equal(["BEH-A"], rig.Pipeline.Bundle("demo", "list-sorting")!.Cards);
    }

    [Fact]
    public void A_flagged_bundle_queues_its_item_for_review_of_the_triage()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Review, WorkItemStatus.Queued, PipelineStage.Triage), (item.Stage, item.Status, item.ReviewedStage));
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
    public void Items_waiting_for_a_stage_not_yet_wired_do_not_hold_the_projects_next_triage()
    {
        using var rig = new Rig();
        rig.Note("first note");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        // The spec item is now ahead of any triage in the queue.
        rig.Note("second note");

        Assert.Contains("second note", Assert.IsType<PoolClaim>(rig.Claim()).Task);
        Assert.Equal(WorkItemStatus.Queued, Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting").Status);
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
