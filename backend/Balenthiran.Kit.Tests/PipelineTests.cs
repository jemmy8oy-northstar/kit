using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
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

        public Rig(string corpus = Corpus, bool on = true)
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), corpus);
            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Pipeline = on ? new Pipeline(corpora, Pool, Store, Models) : null;
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

        public void Finish(PoolClaim claim, string status = "finished") =>
            Assert.Equal(200, Routes.Finished(claim.Token, Json(new { status })).Status);

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

    [Fact]
    public void Cards_are_each_behaviours_id_and_title_in_file_order()
    {
        Assert.Equal([("BEH-A", "A user sees the list"), ("BEH-B", "A user adds an item")], Pipeline.Cards(Corpus));
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
