using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

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

        public Rig(string corpus = Corpus, bool on = true, IReadOnlySet<string>? projects = null, bool canOpenPulls = true, StageTurnSettings? turns = null, bool scheduled = false, string? schedule = null, ITriagedNoteStore? sorted = null, string? root = null, bool merge = false, TimeProvider? clock = null, ITicketStore? tickets = null, bool contextJob = false, bool planJob = false, bool buildJob = false, bool mergeJob = false, IPullRequestMerger? merger = null, int runRetries = 0)
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
            Pool = new RunnerPool(Store, clock);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Pipeline = on ? new Pipeline(corpora, Pool, Store, Models, clock: clock, projects: projects, pulls: canOpenPulls ? Pulls : null, turns: turns, scheduled: scheduled, sorted: Sorted, merger: merger ?? Merges, merge: merge, tickets: Tickets, jobs: Jobs(contextJob, planJob, buildJob, mergeJob), runRetries: runRetries) : null;
            // One fixed minute: every note shares its date, as notes left in the same minute do, so a ref alone cannot tell them apart.
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, clock: new Clock(), runStore: Store, runLauncher: Pool, runnerPool: Pool, pipeline: Pipeline);
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, clock: clock, pipeline: Pipeline);
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
        public Rig Restart(ITriagedNoteStore? sorted = null, ITicketStore? tickets = null, bool contextJob = false, bool planJob = false, bool buildJob = false) => new(sorted: sorted ?? Sorted, root: root, tickets: tickets, contextJob: contextJob, planJob: planJob, buildJob: buildJob);

        // The stages a job owns, from the three switches (BEH-ORCH-11).
        private static IReadOnlySet<PipelineStage> Jobs(bool context, bool plan, bool build, bool mergeJob) =>
            new[] { (context, PipelineStage.Context), (plan, PipelineStage.Spec), (build, PipelineStage.Build), (mergeJob, PipelineStage.Merge) }.Where(j => j.Item1).Select(j => j.Item2).ToHashSet();

        // His edit of the spec between two looks of the pipeline (BEH-ORCH-11, slice 6).
        public void SetCorpus(string corpus) => File.WriteAllText(Path.Combine(root, "demo.beh"), corpus);

        // A second project, with the same repo as the first (BEH-ORCH-11: the context job takes one ticket per project).
        public void AddProject(string name) => File.WriteAllText(Path.Combine(root, name + ".beh"), Corpus);

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

        // Whether each ask brought the branch up to date first (BEH-ORCH-11, slice 7).
        public List<bool> UpdateFirst { get; } = [];

        // The answer for a pull request, when it is not the default one.
        public Dictionary<string, IPullRequestMerge> AnswerFor { get; } = [];

        // The commit each ask said a Kit test run had passed at (BEH-ORCH-12), null when none had.
        public List<string?> TestedSha { get; } = [];

        public async Task<IPullRequestMerge> MergeIfGreenAsync(string pullRequestUrl, string? repository, CancellationToken cancellationToken, bool updateFirst = false, string? testedSha = null)
        {
            Asked.Add(pullRequestUrl);
            UpdateFirst.Add(updateFirst);
            TestedSha.Add(testedSha);
            if (AnswerFor.TryGetValue(pullRequestUrl, out var own))
            {
                return own;
            }

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
    public void A_run_whose_runner_never_reports_fails_as_lost_after_the_lease_and_frees_its_project()
    {
        // BEH-ORCH-14: his 2026-11-16 22:56Z and 22:58Z notes waited until a restart at 07:30Z the next day.
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        rig.Note("the list should sort by date");
        var lost = Assert.IsType<PoolClaim>(rig.Claim());

        clock.Advance(RunnerPool.Lease - TimeSpan.FromMinutes(1));
        Assert.Null(rig.Claim());
        Assert.Equal(AgentRunStatus.Running, rig.Store.List().Single(r => r.Id == lost.Id).Status);

        clock.Advance(TimeSpan.FromMinutes(2));
        rig.Claim();
        var run = rig.Store.List().Single(r => r.Id == lost.Id);
        Assert.Equal((AgentRunStatus.Failed, RunnerPool.LostFailure), (run.Status, run.Failure));
        Assert.Equal(409, rig.Routes.Finished(lost.Token, Json(new { status = "finished" })).Status);
        Assert.Empty(rig.Pool.Lost());

        // The project is free: the failed triage's retry is handed out, and it reads the note again.
        clock.Advance(Pipeline.RetryAfter);
        var retry = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.NotEqual(lost.Id, retry.Id);
        Assert.Contains("the list should sort by date", retry.Task);
    }

    [Fact]
    public void BEH_ORCH_17_The_runs_list_says_when_a_runner_took_each_run_still_going_and_when_it_counts_as_lost()
    {
        // BEH-ORCH-17: his 2026-11-19 19:29Z note: "how long does a test run actually take atm".
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        rig.Note("the list should sort by date");
        Assert.Empty(Assert.IsType<AgentRunList>(rig.Router.Route("GET", "/api/runs").Body).Claims);

        clock.Advance(TimeSpan.FromMinutes(3));
        var taken = Assert.IsType<PoolClaim>(rig.Claim());
        var claim = Assert.Single(Assert.IsType<AgentRunList>(rig.Router.Route("GET", "/api/runs").Body).Claims);
        Assert.Equal(taken.Id, claim.RunId);
        Assert.Equal(clock.GetUtcNow(), claim.ClaimedAt);
        Assert.Equal(claim.ClaimedAt + RunnerPool.Lease, claim.LostAt);

        // Once it reports, it is over and no longer claimed.
        rig.FinishWith(taken, new { slug = "list-sorting", text = BundleText("list-sorting") });
        Assert.DoesNotContain(Assert.IsType<AgentRunList>(rig.Router.Route("GET", "/api/runs").Body).Claims, c => c.RunId == taken.Id);
    }

    [Fact]
    public void BEH_ORCH_17_A_test_run_limit_is_three_times_the_slower_recorded_time_once_a_cold_run_is_recorded()
    {
        // BEH-ORCH-17: the warm suite took 378 seconds on 2026-11-19; the limit waits on a cold run timed on the runner.
        // A changed recorded figure fails here, so the limit cannot drift from the card without someone noticing.
        int? cold = Orchestrator.TestColdSeconds;
        var expected = cold is { } c ? (3 * Math.Max(378, c) + 59) / 60 * 60 : 1800;
        Assert.Equal(expected, Orchestrator.TestSeconds);
    }

    [Fact]
    public void BEH_ORCH_17_Only_a_test_run_has_a_time_limit_of_its_own()
    {
        Assert.Equal(Orchestrator.TestSeconds, Orchestrator.SecondsFor(PipelineStage.Test));
        foreach (var stage in Enum.GetValues<PipelineStage>().Where(s => s != PipelineStage.Test))
        {
            Assert.Null(Orchestrator.SecondsFor(stage));
        }
    }

    [Fact]
    public void BEH_ORCH_17_A_test_runs_lease_is_its_limit_plus_ten_minutes_and_a_run_with_no_limit_keeps_150()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), RunnerPool.LeaseMargin);
        Assert.Equal(TimeSpan.FromSeconds(Orchestrator.TestSeconds) + RunnerPool.LeaseMargin, RunnerPool.LeaseFor(Orchestrator.TestSeconds));
        Assert.Equal(TimeSpan.FromMinutes(150), RunnerPool.LeaseFor(null));
    }

    [Fact]
    public void BEH_ORCH_17_A_test_run_gives_up_at_its_limit_and_is_counted_lost_ten_minutes_after_that_not_at_150()
    {
        // BEH-ORCH-17: kit#768's test run went 90 minutes without a word on 2026-11-19, on a suite that takes 6.
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        var test = rig.Store.Create("operator", "kit", "m", "h1", clock.GetUtcNow(), "list-sorting", "test");
        rig.Pool.Enqueue(test, "tok", "o/r", "t", "dev", 15, Orchestrator.TestSeconds);
        var taken = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(Orchestrator.TestSeconds, taken.MaxSeconds);

        var limit = TimeSpan.FromSeconds(Orchestrator.TestSeconds);
        var claim = Assert.Single(Assert.IsType<AgentRunList>(rig.Router.Route("GET", "/api/runs").Body).Claims);
        Assert.Equal(clock.GetUtcNow() + limit, claim.GivesUpAt);
        Assert.Equal(clock.GetUtcNow() + limit + RunnerPool.LeaseMargin, claim.LostAt);

        clock.Advance(limit + RunnerPool.LeaseMargin - TimeSpan.FromSeconds(1));
        Assert.Empty(rig.Pool.Lost());
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(test.Id, Assert.Single(rig.Pool.Lost()));
    }

    [Fact]
    public void BEH_ORCH_20_A_test_or_merge_run_is_claimed_before_an_older_build_on_another_project()
    {
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        var build = rig.Store.Create("operator", "kit-ui", "m", "h1", clock.GetUtcNow(), "a-page", "build");
        rig.Pool.Enqueue(build, "tok", "o/ui", "t", "dev", 15);
        var resolve = rig.Store.Create("operator", "other", "m", "h2", clock.GetUtcNow(), "a-conflict", "resolve");
        rig.Pool.Enqueue(resolve, "tok", "o/other", "t", "dev", 15);
        var test = rig.Store.Create("operator", "kit", "m", "h3", clock.GetUtcNow(), "list-sorting", "test");
        rig.Pool.Enqueue(test, "tok", "o/r", "t", "dev", 15, Orchestrator.TestSeconds);

        // Oldest first among test and merge runs, and the build only once neither is left.
        Assert.Equal(resolve.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);
        Assert.Equal(test.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);
        Assert.Equal(build.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);
    }

    [Fact]
    public void BEH_ORCH_20_A_test_run_whose_project_is_busy_does_not_hold_up_a_build_on_a_free_one()
    {
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        var running = rig.Store.Create("operator", "kit", "m", "h1", clock.GetUtcNow(), "first", "build");
        rig.Pool.Enqueue(running, "tok", "o/r", "t", "dev", 15);
        Assert.Equal(running.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);

        var test = rig.Store.Create("operator", "kit", "m", "h2", clock.GetUtcNow(), "second", "test");
        rig.Pool.Enqueue(test, "tok", "o/r", "t", "dev", 15, Orchestrator.TestSeconds);
        var build = rig.Store.Create("operator", "kit-ui", "m", "h3", clock.GetUtcNow(), "a-page", "build");
        rig.Pool.Enqueue(build, "tok", "o/ui", "t", "dev", 15);

        Assert.Equal(build.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);
        Assert.True(RunnerPool.GoesFirst("test") && RunnerPool.GoesFirst("resolve"));
        Assert.False(RunnerPool.GoesFirst("build") || RunnerPool.GoesFirst("triage") || RunnerPool.GoesFirst(null));
    }

    [Fact]
    public void BEH_ORCH_17_A_run_with_no_limit_of_its_own_keeps_the_150_minute_lease_and_no_give_up_time()
    {
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        var build = rig.Store.Create("operator", "kit", "m", "h1", clock.GetUtcNow(), "list-sorting", "build");
        rig.Pool.Enqueue(build, "tok", "o/r", "t", "dev", 15);
        Assert.Null(Assert.IsType<PoolClaim>(rig.Claim()).MaxSeconds);

        var claim = Assert.Single(Assert.IsType<AgentRunList>(rig.Router.Route("GET", "/api/runs").Body).Claims);
        Assert.Null(claim.GivesUpAt);
        Assert.Equal(clock.GetUtcNow() + RunnerPool.Lease, claim.LostAt);
        clock.Advance(TimeSpan.FromMinutes(41));
        Assert.Empty(rig.Pool.Lost());
    }

    [Fact]
    public void BEH_ORCH_19_After_a_restart_a_forgotten_queued_run_fails_at_once_and_a_forgotten_running_one_at_its_own_lease()
    {
        // BEH-ORCH-17: his 2026-11-19 20:28Z screenshot of Runs → Now: runs "waiting for a runner · 306h" and "running since
        // before Kit last started", left going for ever by earlier restarts. BEH-ORCH-19: his 23:43Z screenshot still had
        // them, because the lease was counted from the restart and Kit restarts at every release.
        var clock = new Ticking();
        var store = new MemoryAgentRunStore();
        var queued = store.Create("operator", "kit", "m", "h1", clock.GetUtcNow(), null, "triage");
        var old = store.Create("operator", "kit", "m", "h2", clock.GetUtcNow(), "list-sorting", "build");
        Assert.True(store.StartIfQueued(old.Id));
        clock.Advance(TimeSpan.FromHours(3));
        var recent = store.Create("operator", "kit", "m", "h4", clock.GetUtcNow(), "other", "build");
        Assert.True(store.StartIfQueued(recent.Id));
        clock.Advance(TimeSpan.FromMinutes(10));

        // Kit starts again: a new pool over the same store.
        var pool = new RunnerPool(store, clock);
        var routes = new RunnerPoolRoutes(pool, store, PoolToken, TimeSpan.Zero, clock: clock);
        var fresh = store.Create("operator", "kit", "m", "h3", clock.GetUtcNow(), null, "triage");
        Assert.Equal(204, routes.ClaimAsync(PoolToken).GetAwaiter().GetResult().Status);

        Assert.Equal((AgentRunStatus.Failed, RunnerPool.LostFailure), (store.List().Single(r => r.Id == queued.Id).Status, store.List().Single(r => r.Id == queued.Id).Failure));
        // Three hours in, it is past its lease: it goes at once rather than getting a fresh one.
        Assert.Equal((AgentRunStatus.Failed, RunnerPool.LostFailure), (store.List().Single(r => r.Id == old.Id).Status, store.List().Single(r => r.Id == old.Id).Failure));
        Assert.Equal(AgentRunStatus.Running, store.List().Single(r => r.Id == recent.Id).Status);
        Assert.Equal(AgentRunStatus.Queued, store.List().Single(r => r.Id == fresh.Id).Status);
        Assert.Empty(pool.Claimed());

        // One made ten minutes before the restart may still report until the lease, counted from the restart, runs out.
        clock.Advance(RunnerPool.Lease - TimeSpan.FromMinutes(1));
        routes.ClaimAsync(PoolToken).GetAwaiter().GetResult();
        Assert.Equal(AgentRunStatus.Running, store.List().Single(r => r.Id == recent.Id).Status);
        clock.Advance(TimeSpan.FromMinutes(2));
        routes.ClaimAsync(PoolToken).GetAwaiter().GetResult();
        Assert.Equal((AgentRunStatus.Failed, RunnerPool.LostFailure), (store.List().Single(r => r.Id == recent.Id).Status, store.List().Single(r => r.Id == recent.Id).Failure));
        Assert.Equal(AgentRunStatus.Queued, store.List().Single(r => r.Id == fresh.Id).Status);
    }

    [Fact]
    public void A_lost_run_is_given_out_once_and_again_only_when_it_could_not_be_failed()
    {
        // BEH-ORCH-14: two claims at once cannot both fail it, and a store error on failing it does not forget it.
        var clock = new Ticking();
        using var rig = new Rig(clock: clock);
        rig.Note("the list should sort by date");
        var lost = Assert.IsType<PoolClaim>(rig.Claim());
        clock.Advance(RunnerPool.Lease + TimeSpan.FromMinutes(1));

        Assert.Equal(lost.Id, Assert.Single(rig.Pool.Lost()));
        Assert.Empty(rig.Pool.Lost());

        rig.Pool.StillLost(lost.Id);
        Assert.Equal(lost.Id, Assert.Single(rig.Pool.Lost()));
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
    public void A_note_left_on_a_card_reaches_triage_with_the_card_it_was_left_on()
    {
        // His 2026-11-16 22:56Z note on BEH-USERS-12, "I think this is ready now", was set aside as naming no card.
        using var onCard = new Rig();
        onCard.Note("I think this is ready now", behaviour: "BEH-A");
        Assert.Contains("(Left on card BEH-A.)\nI think this is ready now", Assert.IsType<PoolClaim>(onCard.Claim()).Task);

        using var onProject = new Rig();
        onProject.Note("I think this is ready now");
        var task = Assert.IsType<PoolClaim>(onProject.Claim()).Task;
        Assert.Contains("I think this is ready now", task);
        Assert.DoesNotContain("(Left on card", task);
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
        Assert.Equal(30, triage.MaxTurns);

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
    public void BEH_ORCH_19_a_build_run_that_fails_is_run_again_twice_and_then_blocked_saying_where_it_stopped()
    {
        // His 2026-11-19 23:57Z screenshot of Blocked: "almost every run is the bundle was unchanged or build failed", each
        // stopped for good by its first failure.
        using var rig = new Rig(runRetries: 2);
        var build = Specced(rig);

        for (var run = 1; run <= 2; run++)
        {
            Assert.Equal(200, rig.Routes.Finished(build.Token, Json(new { status = "failed", failure = "claude exit 1" })).Status);
            var queued = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
            Assert.Equal((PipelineStage.Build, WorkItemStatus.Running), (queued.Stage, queued.Status));
            var again = Assert.IsType<PoolClaim>(rig.Claim());
            Assert.NotEqual(build.Id, again.Id);
            Assert.Contains("You are the build stage", again.Task);
            build = again;
        }

        Assert.Equal(200, rig.Routes.Finished(build.Token, Json(new { status = "failed", failure = "claude exit 1" })).Status);
        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((WorkItemStatus.Stopped, "the build run failed (claude exit 1), 3 runs in a row"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_19_a_build_run_that_changed_nothing_is_run_again_and_a_good_one_then_opens_the_pull_request()
    {
        using var rig = new Rig(runRetries: 2);
        var build = Specced(rig);

        rig.FinishWith(build, new { slug = "list-sorting", text = Specced("list-sorting") });
        rig.FinishWith(Assert.IsType<PoolClaim>(rig.Claim()), new { slug = "list-sorting", text = Built("list-sorting") });

        Assert.Single(rig.Pulls.Asked);
        Assert.Equal(WorkItemStatus.Done, Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting").Status);
    }

    [Fact]
    public void BEH_ORCH_19_with_no_retries_a_lost_build_run_blocks_at_once_and_says_it_was_lost()
    {
        using var rig = new Rig();
        var build = Specced(rig);

        rig.Pipeline!.RunEnded(build.Id, false, failure: RunnerPool.LostFailure);

        Assert.Equal("the build run failed (lost)", Assert.Single(rig.Pipeline.Items(), i => i.Slug == "list-sorting").StoppedBecause);
    }

    [Fact]
    public void BEH_ORCH_19_after_a_restart_a_ticket_a_run_blocked_is_tried_again_and_one_waiting_on_a_person_is_not()
    {
        var store = new MemoryTicketStore();
        store.Save(Seeded("list-sorting") with { BlockedBecause = "the spec run failed" });
        store.Save(Seeded("other", priority: 4) with { BlockedBecause = "Decision for a person: which sort order" });
        using var rig = new Rig(tickets: store);

        rig.Pipeline!.Resume();

        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", spec.Task);
        Assert.Contains("list-sorting", spec.Task);
        Assert.Null(store.Read("demo").Single(t => t.Slug == "list-sorting").BlockedBecause);
        Assert.Equal("Trying again after a restart: the block was a run's, not a decision (the spec run failed)", store.Comments("demo", "list-sorting").Last().Body);
        Assert.Equal((WorkItemStatus.Stopped, "Decision for a person: which sort order"), (rig.Pipeline.Items().Single(i => i.Slug == "other").Status, rig.Pipeline.Items().Single(i => i.Slug == "other").StoppedBecause));
    }

    [Theory]
    [InlineData("the build run failed", true)]
    [InlineData("the spec run left the bundle unchanged", true)]
    [InlineData("the context run reported no bundle for its work item", true)]
    [InlineData("the test run was lost, 3 runs in a row", true)]
    [InlineData("the review run failed", false)]
    [InlineData("the project's tests failed at abc1234", true)]
    [InlineData("the build run failed (claude exit 1), 3 runs in a row", true)]
    [InlineData("the spec run wrote no test plan", false)]
    [InlineData("its pull request was not merged: conflict", false)]
    [InlineData("the conflict-resolution run failed", true)]
    [InlineData("its pull request was not merged: it has a conflict with its base branch", true)]
    [InlineData("its pull request was not merged: it still conflicts with its base after an agent tried to resolve it", true)]
    [InlineData("its pull request was not merged: its base kept moving and it conflicted again after 3 resolve runs", true)]
    [InlineData("its pull request was not merged: ci did not succeed (failure)", false)]
    [InlineData("Decision for a person: which sort order", false)]
    public void BEH_ORCH_19_only_a_block_a_run_made_is_retryable(string because, bool retryable) =>
        Assert.Equal(retryable, Pipeline.Retryable(because));

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
        Assert.Contains("Read the cards under Cards in the spec at demo.beh", spec.Task);
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

    // ---- BEH-PR-1: the pipeline raises a pull request for finished work, into the integration branch ----

    [Fact]
    public void BEH_PR_1_finished_work_raises_exactly_one_pull_request_and_the_pull_request_names_the_card_it_builds()
    {
        using var rig = new Rig();

        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting") });

        var asked = Assert.Single(rig.Pulls.Asked);
        Assert.Contains("Built:\n- BEH-A: ListTests.Sorts_by_date", asked.Body);
    }

    [Fact]
    public void BEH_PR_1_the_pull_request_targets_the_integration_branch_the_run_started_from_and_never_main()
    {
        using var rig = new Rig();

        rig.FinishWith(Specced(rig), new { slug = "list-sorting", text = Built("list-sorting") });

        var asked = Assert.Single(rig.Pulls.Asked);
        Assert.Equal("dev", asked.Base);
        Assert.NotEqual("main", asked.Base);
    }

    [Fact]
    public void BEH_PR_1_work_that_is_not_finished_raises_no_pull_request()
    {
        using var rig = new Rig();
        var build = Specced(rig);
        Assert.Empty(rig.Pulls.Asked);

        Assert.Equal(200, rig.Routes.Finished(build.Token, Json(new { status = "failed" })).Status);

        Assert.Empty(rig.Pulls.Asked);
    }

    // ---- BEH-PR-2: the pipeline completes a pull request only when the corpus says it may merge -------

    [Fact]
    public async Task BEH_PR_2_a_raised_pull_request_with_merging_off_is_left_open_and_nothing_is_merged()
    {
        using var rig = await BuiltRig(merge: false);

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Empty(rig.Merges.Asked);
        var item = BuiltItem(rig);
        Assert.Equal((WorkItemStatus.Done, "https://github.com/someone/demo/pull/7", false), (item.Status, item.PullRequest, item.Merged));
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
    public async Task BEH_ORCH_10_with_merging_off_the_built_item_says_why_it_is_not_merged()
    {
        using var rig = await BuiltRig(merge: false);
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        var item = Assert.Single(Listed(rig), i => i.Slug == "list-sorting");
        Assert.False(item.Merged);
        Assert.Equal(("build", "done"), (item.Stage, item.Status));
        Assert.Contains("KIT_PIPELINE_MERGE is off", item.MergeNote);
        Assert.Null(item.StoppedBecause);
    }

    [Fact]
    public async Task BEH_ORCH_10_a_built_item_still_waiting_says_what_it_waits_for_and_is_not_stopped()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Waiting = true };
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        var item = Assert.Single(Listed(rig), i => i.Slug == "list-sorting");
        Assert.False(item.Merged);
        Assert.Null(item.StoppedBecause);
        Assert.Equal("waiting for its checks to pass, or for GitHub to say whether it can merge", item.MergeNote);
    }

    [Fact]
    public async Task BEH_ORCH_10_the_reason_a_look_is_waiting_is_the_one_shown()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Waiting = true, Reason = "brought up to date with dev" };
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Equal("brought up to date with dev", Assert.Single(Listed(rig), i => i.Slug == "list-sorting").MergeNote);
    }

    [Fact]
    public async Task BEH_ORCH_10_once_merged_the_waiting_note_is_gone()
    {
        using var rig = await BuiltRig();
        rig.Merges.Answer = new PullRequestMerge { Waiting = true };
        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);
        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeReadyAsync(CancellationToken.None);

        var item = Assert.Single(Listed(rig), i => i.Slug == "list-sorting");
        Assert.True(item.Merged);
        Assert.Null(item.MergeNote);
    }

    private static IReadOnlyList<IPipelineItem> Listed(Rig rig) =>
        Assert.IsType<PipelineItemList>(rig.Router.Route("GET", "/api/pipeline").Body).Items;

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
        Assert.Equal((TicketState.Ready, "the spec run failed", "the spec run failed"), (ticket.State, ticket.StoppedBecause, ticket.BlockedBecause));
    }

    [Fact]
    public void BEH_ORCH_16_a_blocked_ticket_gets_its_reason_as_one_comment()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);

        rig.Finish(spec, status: "failed");

        var comment = Assert.Single(rig.Tickets.Comments("demo", "list-sorting"));
        Assert.Equal(("kit", "Blocked: the spec run failed"), (comment.Author, comment.Body));
    }

    [Fact]
    public void BEH_ORCH_16_a_projects_tickets_are_served_with_their_state_blocked_tag_and_thread()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);
        rig.Finish(spec, status: "failed");

        var r = rig.Router.Route("GET", "/api/projects/demo/tickets");

        Assert.Equal(200, r.Status);
        var list = Assert.IsType<TicketList>(r.Body);
        Assert.True(list.On);
        var ticket = Assert.Single(list.Tickets);
        Assert.Equal(("list-sorting", "ready", "the spec run failed"), (ticket.Slug, ticket.State, ticket.BlockedBecause));
        var comment = Assert.Single(ticket.Comments);
        Assert.Equal(("kit", "Blocked: the spec run failed"), (comment.Author, comment.Body));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ\z", comment.At);
    }

    [Fact]
    public void BEH_ORCH_16_his_reply_on_a_blocked_ticket_wakes_triage_on_it_and_its_update_clears_the_tag()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");

        var r = rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by the date column, newest first" }));

        Assert.Equal(200, r.Status);
        Assert.True(Assert.IsType<TicketReplyResult>(r.Body).Woke);
        Assert.Equal(["Blocked: the spec run failed", "sort by the date column, newest first"], rig.Tickets.Comments("demo", "list-sorting").Select(c => c.Body));
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("The reply: sort by the date column, newest first\nIt is @operator's reply on ticket list-sorting (\"Sort the list\"), blocked because: the spec run failed", triage.Task);
        Assert.Contains("Report this note under THAT ticket's slug, list-sorting, on its branch kit/work/list-sorting", triage.Task);
        Assert.Contains("- list-sorting [ready] Sort the list", triage.Task); // listed, though blocked, so the update has a ticket to name

        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Null(Ticket(rig).BlockedBecause);
        var next = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage for project \"demo\"", next.Task);
    }

    [Fact]
    public void BEH_ORCH_16_a_reply_a_finished_triage_has_read_is_not_read_again()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });
        rig.Finish(rig.Claim()!, status: "failed"); // blocked again, by its next spec run

        rig.Note("an unrelated note");

        Assert.DoesNotContain("The reply: sort by date", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_16_a_triage_that_read_his_reply_but_left_the_ticket_says_so_on_its_thread()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));

        rig.FinishWith(rig.Claim()!, new { slug = "something-else", text = BundleText("something-else") });

        Assert.Equal("the spec run failed", Ticket(rig).BlockedBecause);
        var last = rig.Tickets.Comments("demo", "list-sorting")[^1];
        Assert.Equal(Pipeline.KitAuthor, last.Author);
        Assert.StartsWith("Read your reply. The triage did not update this ticket, so it is still blocked", last.Body);
    }

    [Fact]
    public void BEH_ORCH_16_a_triage_that_updated_the_ticket_adds_no_unchanged_comment()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.DoesNotContain(rig.Tickets.Comments("demo", "list-sorting"), c => c.Body.StartsWith("Read your reply", StringComparison.Ordinal));
    }

    [Fact]
    public void BEH_ORCH_16_the_longest_reply_reaches_the_triage_whole()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        var reply = string.Concat(Enumerable.Repeat("date! ", KitRouter.MaxReply / 6)) + "END";
        Assert.True(reply.Length <= KitRouter.MaxReply);

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = reply }));
        var task = Assert.IsType<PoolClaim>(rig.Claim()).Task;

        Assert.Contains(reply, task);
        Assert.DoesNotContain("[truncated", task);
    }

    [Fact]
    public void BEH_ORCH_16_a_failed_triage_reads_the_reply_again()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));
        rig.Finish(rig.Claim()!, status: "failed");

        rig.Note("an unrelated note");

        Assert.Contains("The reply: sort by date", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_16_a_reply_on_a_ticket_that_is_not_blocked_goes_on_its_thread_and_wakes_nothing()
    {
        using var rig = new Rig();
        Triaged(rig); // its spec run is claimed and running, so the ticket is ready and not blocked

        var r = rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "looks right" }));

        Assert.False(Assert.IsType<TicketReplyResult>(r.Body).Woke);
        Assert.Equal("looks right", Assert.Single(rig.Tickets.Comments("demo", "list-sorting")).Body);
        Assert.Null(rig.Claim());
    }

    [Theory]
    [InlineData("/api/projects/demo/tickets/no-such/comments", "{\"body\":\"hi\"}", 404)]
    [InlineData("/api/projects/nope/tickets/list-sorting/comments", "{\"body\":\"hi\"}", 404)]
    [InlineData("/api/projects/demo/tickets/list-sorting/comments", "{\"body\":\"   \"}", 400)]
    [InlineData("/api/projects/demo/tickets/list-sorting/comments", "{}", 400)]
    public void BEH_ORCH_16_a_reply_with_no_ticket_or_no_text_is_refused(string path, string body, int status)
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");

        Assert.Equal(status, rig.Router.Route("POST", path, body: JsonDocument.Parse(body).RootElement).Status);
        Assert.Single(rig.Tickets.Comments("demo", "list-sorting")); // only Kit's own Blocked comment
    }

    [Fact]
    public void BEH_ORCH_16_a_reply_longer_than_the_limit_is_refused()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");

        var r = rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = new string('x', KitRouter.MaxReply + 1) }));

        Assert.Equal(400, r.Status);
    }

    [Fact]
    public void BEH_ORCH_16_after_a_restart_a_blocked_ticket_stays_stopped_and_no_job_takes_it()
    {
        using var rig = new Rig();
        // Not a run's own failure, which BEH-ORCH-19 tries again after a restart.
        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = BundleText("list-sorting").Replace("sort by date", "sort by date, newest first") });

        using var restarted = rig.Restart(tickets: rig.Tickets);
        restarted.Pipeline!.Resume();

        Assert.Null(restarted.Claim());
        var item = Assert.Single(restarted.Pipeline.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((WorkItemStatus.Stopped, "the spec run wrote no test plan"), (item.Status, item.StoppedBecause));
        Assert.Single(restarted.Tickets.Comments("demo", "list-sorting"));
    }

    [Fact]
    public void BEH_ORCH_16_the_triage_run_is_told_where_the_project_spec_is()
    {
        using var rig = new Rig();

        var task = TriageClaim(rig).Task;

        Assert.Contains("Edit the spec at demo.beh", task);
    }

    [Fact]
    public void BEH_ORCH_16_the_triage_task_does_not_list_a_blocked_ticket()
    {
        using var rig = new Rig();

        var task = TriageClaim(rig, Seeded("list-sorting"), Seeded("stuck", TicketState.Ready) with { BlockedBecause = "the spec run failed" }).Task;

        Assert.Contains("## Open tickets (1)\n- list-sorting [ready]", task);
        Assert.DoesNotContain("stuck", task);
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
        stopped.FinishWith(Triaged(stopped), new { slug = "list-sorting", text = BundleText("list-sorting").Replace("sort by date", "sort by date, newest first") });
        using var afterStop = stopped.Restart(tickets: stopped.Tickets);
        afterStop.Pipeline!.Resume();
        Assert.Null(afterStop.Claim());
        Assert.Equal(WorkItemStatus.Stopped, Assert.Single(afterStop.Pipeline.Items()).Status); // BEH-ORCH-16: kept, blocked

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
    // A seeded priority other than 3 stands for one read earlier: its card (BEH-STORED) is not in the spec, so slice 6 keeps the stored value.
    private static PipelineTicket Seeded(string slug, TicketState state = TicketState.Ready, int priority = 3, string? title = null)
    {
        var text = priority == DefaultSeedPriority ? BundleText(slug) : StoredPriority(BundleText(slug));
        var bundle = ContextBundle.Parse(text).Bundle!;
        return new PipelineTicket("demo", slug, title ?? bundle.Title, state, priority, bundle.Cards, bundle.Render(), null, null, false);
    }

    private const int DefaultSeedPriority = 3;

    private static string StoredPriority(string bundleText) => bundleText.Replace("Cards:\n- BEH-A\n", "Cards:\n- BEH-STORED\n", StringComparison.Ordinal);

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
    public void BEH_ORCH_11_the_triage_task_does_not_list_a_built_ticket_so_an_update_cannot_strand_its_pull_request()
    {
        using var rig = new Rig();

        var task = TriageClaim(rig, Seeded("list-sorting"), Seeded("next-step", TicketState.Planned), Seeded("has-a-pr", TicketState.Built)).Task;

        Assert.Contains("## Open tickets (2)\n", task);
        Assert.Contains("- next-step [planned]", task);
        Assert.DoesNotContain("has-a-pr", task);
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

    // ---- BEH-ORCH-11, slice 3: the context job ----------------------------------------------------------

    // The bundle as a context run leaves it: what a planner needs added to its Context.
    private static string Contexted(string slug, string? flag = null) =>
        BundleText(slug, flag).Replace("sort by date", "sort by date\n- covered by ListTests.Sorts_by_date");

    // Triage of list-sorting with the context job on: a note, and the triage's report. No run follows until the job ticks.
    private static void TriagedForContext(Rig rig)
    {
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });
    }

    // Tickets in the store at new, as a restart finds them, for a pipeline with the context job on and two projects.
    private static Rig ContextRig(params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(contextJob: true, tickets: store);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return rig;
    }

    private static List<PoolClaim> Claims(Rig rig)
    {
        var claims = new List<PoolClaim>();
        while (rig.Claim() is { } claim)
        {
            claims.Add(claim);
        }

        return claims;
    }

    private static WorkItem ItemOf(Rig rig, string slug, string project = "demo") => Assert.Single(rig.Pipeline!.Items(), i => i.Project == project && i.Slug == slug);

    [Fact]
    public void BEH_ORCH_11_with_the_context_switch_off_a_triaged_item_goes_straight_to_spec_and_the_ticket_is_ready()
    {
        using var rig = new Rig();

        var spec = Triaged(rig);

        Assert.Contains("You are the spec stage for project \"demo\"", spec.Task);
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
        rig.Pipeline!.JobTick(PipelineStage.Context);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_11_with_the_context_switch_on_a_triaged_item_waits_as_a_new_ticket_and_no_run_starts_until_the_job_ticks()
    {
        using var rig = new Rig(contextJob: true);

        TriagedForContext(rig);

        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.New, Ticket(rig).State);
        Assert.Single(rig.Store.List());

        rig.Pipeline!.JobTick(PipelineStage.Context);

        var context = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the context stage for project \"demo\"", context.Task);
        Assert.Contains("Work-item: list-sorting", context.Task);
        Assert.Contains("Write no spec, no tests and no code", context.Task);
        Assert.Equal(("someone/demo", "dev", "claude-haiku-5-5", StageTurnSettings.DefaultTriage), (context.Repo, context.BaseBranch, context.Model, context.MaxTurns));
        Assert.Equal((PipelineStage.Context, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal("context", rig.Store.List().Single(r => r.WorkItem == "list-sorting").Stage);
    }

    [Fact]
    public void BEH_ORCH_11_one_tick_takes_only_the_highest_priority_new_ticket()
    {
        using var rig = ContextRig(Seeded("a", TicketState.New, 3), Seeded("b", TicketState.New, 1), Seeded("c", TicketState.New, 1), Seeded("d", TicketState.New, 2));

        rig.Pipeline!.JobTick(PipelineStage.Context);
        rig.Pipeline.JobTick(PipelineStage.Context);

        var claim = Assert.Single(Claims(rig));
        Assert.Contains("Work-item: b", claim.Task);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "b").Status);
        Assert.All(new[] { "a", "c", "d" }, slug => Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(rig, slug).Stage, ItemOf(rig, slug).Status)));
    }

    [Fact]
    public void BEH_ORCH_11_among_equal_priorities_the_oldest_ticket_goes_first()
    {
        using var rig = ContextRig(Seeded("old", TicketState.New, 2), Seeded("newer", TicketState.New, 2), Seeded("newest", TicketState.New, 2));

        rig.Pipeline!.JobTick(PipelineStage.Context);

        Assert.Contains("Work-item: old", Assert.Single(Claims(rig)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_a_project_with_a_run_in_flight_is_skipped_and_another_project_goes_ahead()
    {
        // "busy" is ready, so on start it is specced and its project has a run in flight; "waiting" is new beside it.
        using var rig = ContextRig(Seeded("busy", TicketState.Ready), Seeded("waiting", TicketState.New, 1), Seeded("o1", TicketState.New, 3) with { Project = "other" });

        rig.Pipeline!.JobTick(PipelineStage.Context);

        var claims = Claims(rig);
        Assert.Equal(2, claims.Count);
        Assert.Contains(claims, c => c.Task.Contains("You are the spec stage") && c.Task.Contains("Work-item: busy"));
        Assert.Contains(claims, c => c.Task.Contains("You are the context stage") && c.Task.Contains("Work-item: o1"));
        Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(rig, "waiting").Stage, ItemOf(rig, "waiting").Status));
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "o1", "other").Status);
    }

    [Fact]
    public void BEH_ORCH_11_with_nothing_waiting_a_tick_enqueues_no_run()
    {
        using var empty = ContextRig();
        empty.Pipeline!.JobTick(PipelineStage.Context);
        Assert.Null(empty.Claim());
        Assert.Empty(empty.Store.List());

        // A ticket past new is not the context job's: its one run is the spec the start gave it, and a tick adds none.
        using var later = ContextRig(Seeded("r", TicketState.Ready));
        later.Pipeline!.JobTick(PipelineStage.Context);
        Assert.Contains("You are the spec stage", Assert.Single(Claims(later)).Task);
        Assert.Single(later.Store.List());
    }

    [Fact]
    public void BEH_ORCH_11_a_context_run_that_worked_makes_the_ticket_ready_and_queues_it_for_spec()
    {
        using var rig = new Rig(contextJob: true);
        TriagedForContext(rig);
        rig.Pipeline!.JobTick(PipelineStage.Context);

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Contexted("list-sorting") });

        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
        Assert.Contains("covered by ListTests", Ticket(rig).Bundle);
        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage for project \"demo\"", spec.Task);
        Assert.Contains("covered by ListTests", spec.Task);
    }

    [Theory]
    [InlineData("failed", "the context run failed")]
    [InlineData("unchanged", "the context run left the bundle unchanged")]
    [InlineData("no bundle", "the context run reported no bundle for its work item")]
    public void BEH_ORCH_11_a_context_run_that_did_not_work_stops_the_ticket_with_why(string report, string why)
    {
        using var rig = new Rig(contextJob: true);
        TriagedForContext(rig);
        rig.Pipeline!.JobTick(PipelineStage.Context);
        var context = Assert.IsType<PoolClaim>(rig.Claim());

        var body = report switch
        {
            "unchanged" => Json(new { status = "finished", bundles = new[] { new { slug = "list-sorting", text = BundleText("list-sorting") } } }),
            "no bundle" => Json(new { status = "finished", bundles = Array.Empty<object>() }),
            _ => Json(new { status = "failed" }),
        };
        Assert.Equal(200, rig.Routes.Finished(context.Token, body).Status);

        Assert.Equal((WorkItemStatus.Stopped, why), (ItemOf(rig, "list-sorting").Status, ItemOf(rig, "list-sorting").StoppedBecause));
        Assert.Equal((TicketState.New, why), (Ticket(rig).State, Ticket(rig).BlockedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_11_a_flagged_context_run_is_reviewed_and_an_agreed_review_queues_spec()
    {
        using var rig = new Rig(contextJob: true);
        TriagedForContext(rig);
        rig.Pipeline!.JobTick(PipelineStage.Context);

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Contexted("list-sorting", flag: "unsure which tests cover it") });

        var item = ItemOf(rig, "list-sorting");
        Assert.Equal((PipelineStage.Review, WorkItemStatus.Running, PipelineStage.Context), (item.Stage, item.Status, item.ReviewedStage));
        Assert.Equal(TicketState.New, Ticket(rig).State);
        var review = Assert.IsType<PoolClaim>(rig.Claim());

        rig.FinishWith(review, new { slug = "list-sorting", text = Contexted("list-sorting", "unsure which tests cover it").Replace("Cards:", "Review: agreed — fine\nCards:") });

        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
    }

    [Fact]
    public void BEH_ORCH_11_a_flagged_triage_that_a_review_agreed_to_waits_for_the_context_job()
    {
        using var rig = new Rig(contextJob: true);
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting", flag: "unsure which card") });
        var review = Assert.IsType<PoolClaim>(rig.Claim());

        rig.FinishWith(review, new { slug = "list-sorting", text = BundleText("list-sorting", "unsure which card").Replace("Cards:", "Review: agreed — fine\nCards:") });

        Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_11_after_a_restart_a_new_ticket_waits_for_the_job_when_the_switch_is_on_and_is_specced_when_it_is_off()
    {
        using var first = new Rig(contextJob: true);
        TriagedForContext(first);
        Assert.Equal(TicketState.New, Ticket(first).State);

        using var on = first.Restart(tickets: first.Tickets, contextJob: true);
        on.Pipeline!.Resume();
        Assert.Null(on.Claim());
        Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(on, "list-sorting").Stage, ItemOf(on, "list-sorting").Status));
        on.Pipeline.JobTick(PipelineStage.Context);
        Assert.Contains("You are the context stage", Assert.IsType<PoolClaim>(on.Claim()).Task);

        using var off = first.Restart(tickets: first.Tickets);
        off.Pipeline!.Resume();
        Assert.Contains("You are the spec stage", Assert.IsType<PoolClaim>(off.Claim()).Task);
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (ItemOf(off, "list-sorting").Stage, ItemOf(off, "list-sorting").Status));
    }

    [Fact]
    public void BEH_ORCH_11_a_resumed_tickets_priority_is_kept_when_it_is_saved_again()
    {
        using var rig = ContextRig(Seeded("a", TicketState.New, 1));
        rig.Pipeline!.JobTick(PipelineStage.Context);

        rig.FinishWith(rig.Claim()!, new { slug = "a", text = StoredPriority(Contexted("a")) });

        Assert.Equal((TicketState.Ready, 1), (rig.Tickets.Read("demo").Single().State, rig.Tickets.Read("demo").Single().Priority));
    }

    [Fact]
    public void BEH_ORCH_11_KIT_PIPELINE_JOB_CONTEXT_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineJobContext(null));
        Assert.False(KitSettings.ParsePipelineJobContext(""));
        Assert.False(KitSettings.ParsePipelineJobContext("off"));
        Assert.True(KitSettings.ParsePipelineJobContext(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineJobContext(bad));
        }

        Assert.False(new KitSettings("d", "r", null, "dist").PipelineJobContext);
    }

    [Fact]
    public void BEH_ORCH_11_the_context_stage_runs_on_triages_model_and_turns_unless_set_and_the_job_looks_every_two_minutes()
    {
        Assert.Equal(("claude-haiku-5-5", 30), (KitSettings.ParseStageModelContext(null), KitSettings.ParseStageTurnsContext(null)));
        Assert.Equal(("claude-haiku-5-5", 30), (new KitSettings("d", "r", null, "dist").StageModelContext, new KitSettings("d", "r", null, "dist").StageTurnsContext));
        Assert.Equal(("claude-sonnet-5-5", 7), (KitSettings.ParseStageModelContext(" claude-sonnet-5-5 "), KitSettings.ParseStageTurnsContext(" 7 ")));
        Assert.Equal("claude-haiku-5-5", Orchestrator.ModelFor(PipelineStage.Context, Models));
        Assert.Equal("m-ctx", Orchestrator.ModelFor(PipelineStage.Context, Models with { Context = "m-ctx" }));
        Assert.Equal(TimeSpan.FromMinutes(2), KitSettings.ParsePipelineJobInterval(null));
        Assert.Equal(TimeSpan.FromMinutes(5), KitSettings.ParsePipelineJobInterval(" 5 "));
        foreach (var bad in new[] { "0", "-1", "soon", "1.5" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineJobInterval(bad));
        }
    }

    // ---- BEH-ORCH-11, slice 4: the plan job --------------------------------------------------------------

    // Tickets in the store, as a restart finds them, for a pipeline with the plan job on and two projects.
    private static Rig PlanRig(params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(planJob: true, tickets: store);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return rig;
    }

    // A planned ticket whose bundle carries a test plan, so that on start it is built (a run in flight for its project).
    private static PipelineTicket PlannedSeed(string slug, int priority = 3)
    {
        var ticket = Seeded(slug, TicketState.Planned, priority);
        return ticket with { Bundle = ticket.Bundle!.Replace("sort by date", "sort by date\n" + SpecStage.TestPlanHeading + "\n- BEH-A: sorts by date") };
    }

    [Fact]
    public void BEH_ORCH_11_with_the_plan_switch_off_a_queued_spec_item_starts_as_before_and_a_plan_tick_does_nothing()
    {
        using var rig = new Rig(contextJob: true);
        TriagedForContext(rig);
        rig.Pipeline!.JobTick(PipelineStage.Context);
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Contexted("list-sorting") });

        Assert.Contains("You are the spec stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);

        using var plain = new Rig();
        Triaged(plain);
        plain.Pipeline!.JobTick(PipelineStage.Spec);
        Assert.Null(plain.Claim());
    }

    [Fact]
    public void BEH_ORCH_11_with_the_plan_switch_on_and_the_context_switch_off_a_triaged_item_waits_at_ready_until_the_plan_tick()
    {
        using var rig = new Rig(planJob: true);

        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);

        rig.Pipeline!.JobTick(PipelineStage.Context);
        Assert.Null(rig.Claim());

        rig.Pipeline.JobTick(PipelineStage.Spec);

        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage for project \"demo\"", spec.Task);
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal("spec", rig.Store.List().Single(r => r.WorkItem == "list-sorting").Stage);
    }

    [Fact]
    public void BEH_ORCH_11_one_plan_tick_takes_only_the_highest_priority_ready_ticket()
    {
        using var rig = PlanRig(Seeded("a", TicketState.Ready, 3), Seeded("b", TicketState.Ready, 1), Seeded("c", TicketState.Ready, 1), Seeded("d", TicketState.Ready, 2));
        Assert.Null(rig.Claim());

        rig.Pipeline!.JobTick(PipelineStage.Spec);
        rig.Pipeline.JobTick(PipelineStage.Spec);

        var claim = Assert.Single(Claims(rig));
        Assert.Contains("Work-item: b", claim.Task);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "b").Status);
        Assert.All(new[] { "a", "c", "d" }, slug => Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (ItemOf(rig, slug).Stage, ItemOf(rig, slug).Status)));
    }

    [Fact]
    public void BEH_ORCH_11_among_equal_priorities_the_plan_job_takes_the_oldest_ticket_first()
    {
        using var rig = PlanRig(Seeded("old", TicketState.Ready, 2), Seeded("newer", TicketState.Ready, 2), Seeded("newest", TicketState.Ready, 2));

        rig.Pipeline!.JobTick(PipelineStage.Spec);

        Assert.Contains("Work-item: old", Assert.Single(Claims(rig)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_the_plan_job_skips_a_project_with_a_run_in_flight_and_another_project_goes_ahead()
    {
        // "busy" is planned, so on start it is built and its project has a run in flight; "waiting" is ready beside it.
        using var rig = PlanRig(PlannedSeed("busy"), Seeded("waiting", TicketState.Ready, 1), Seeded("o1", TicketState.Ready, 3) with { Project = "other" });

        rig.Pipeline!.JobTick(PipelineStage.Spec);

        var claims = Claims(rig);
        Assert.Equal(2, claims.Count);
        Assert.Contains(claims, c => c.Task.Contains("You are the build stage") && c.Task.Contains("Work-item: busy"));
        Assert.Contains(claims, c => c.Task.Contains("You are the spec stage") && c.Task.Contains("Work-item: o1"));
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (ItemOf(rig, "waiting").Stage, ItemOf(rig, "waiting").Status));
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "o1", "other").Status);
    }

    [Fact]
    public void BEH_ORCH_11_with_nothing_waiting_a_plan_tick_enqueues_no_run()
    {
        using var empty = PlanRig();
        empty.Pipeline!.JobTick(PipelineStage.Spec);
        Assert.Null(empty.Claim());
        Assert.Empty(empty.Store.List());

        // A ticket past ready is not the plan job's: its one run is the build the start gave it, and a tick adds none.
        using var later = PlanRig(PlannedSeed("p"));
        later.Pipeline!.JobTick(PipelineStage.Spec);
        Assert.Contains("You are the build stage", Assert.Single(Claims(later)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_with_both_switches_on_a_context_run_that_worked_leaves_the_ticket_waiting_for_the_plan_tick()
    {
        using var rig = new Rig(contextJob: true, planJob: true);
        TriagedForContext(rig);
        rig.Pipeline!.JobTick(PipelineStage.Context);

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Contexted("list-sorting") });

        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
        Assert.Contains("covered by ListTests", Ticket(rig).Bundle);

        rig.Pipeline.JobTick(PipelineStage.Context);
        Assert.Null(rig.Claim());

        rig.Pipeline.JobTick(PipelineStage.Spec);
        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", spec.Task);
        Assert.Contains("covered by ListTests", spec.Task);
    }

    [Fact]
    public void BEH_ORCH_11_with_the_plan_switch_on_a_scheduled_card_waits_for_the_plan_tick()
    {
        using var rig = new Rig(ScheduledCorpus, scheduled: true, schedule: "BEH-A\n", planJob: true);

        rig.Pipeline!.ScheduleChanged("demo");

        Assert.Null(rig.Claim());
        var item = Assert.Single(rig.Pipeline.Items());
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (item.Stage, item.Status));

        rig.Pipeline.JobTick(PipelineStage.Spec);
        Assert.Contains("You are the spec stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_after_a_restart_a_ready_ticket_waits_for_the_plan_job_when_the_switch_is_on_and_is_specced_when_it_is_off()
    {
        using var first = new Rig();
        Triaged(first);
        Assert.Equal(TicketState.Ready, Ticket(first).State);

        using var on = first.Restart(tickets: first.Tickets, planJob: true);
        on.Pipeline!.Resume();
        Assert.Null(on.Claim());
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Queued), (ItemOf(on, "list-sorting").Stage, ItemOf(on, "list-sorting").Status));
        on.Pipeline.JobTick(PipelineStage.Spec);
        Assert.Contains("You are the spec stage", Assert.IsType<PoolClaim>(on.Claim()).Task);

        using var off = first.Restart(tickets: first.Tickets);
        off.Pipeline!.Resume();
        Assert.Contains("You are the spec stage", Assert.IsType<PoolClaim>(off.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_KIT_PIPELINE_JOB_PLAN_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineJobPlan(null));
        Assert.False(KitSettings.ParsePipelineJobPlan(""));
        Assert.False(KitSettings.ParsePipelineJobPlan("off"));
        Assert.True(KitSettings.ParsePipelineJobPlan(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineJobPlan(bad));
        }

        var settings = new KitSettings("d", "r", null, "dist");
        Assert.False(settings.PipelineJobPlan);
        Assert.Empty(settings.PipelineJobs);
        Assert.Equal([PipelineStage.Spec], (settings with { PipelineJobPlan = true }).PipelineJobs);
        Assert.Equal([PipelineStage.Context, PipelineStage.Spec], (settings with { PipelineJobContext = true, PipelineJobPlan = true }).PipelineJobs);
    }

    [Fact]
    public void BEH_ORCH_11_a_job_is_registered_only_for_a_switch_that_is_on()
    {
        static List<PipelineJob> Jobs(KitSettings settings)
        {
            using var provider = new ServiceCollection().AddKitServices(settings).BuildServiceProvider();
            return provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<PipelineJob>().ToList();
        }

        var baseline = new KitSettings("d", "r", null, "dist") with { Pipeline = true, RunnerPoolToken = "pool-token" };

        Assert.Empty(Jobs(baseline));
        Assert.Single(Jobs(baseline with { PipelineJobPlan = true }));
        Assert.Equal(2, Jobs(baseline with { PipelineJobContext = true, PipelineJobPlan = true }).Count);
        Assert.Empty(Jobs(new KitSettings("d", "r", null, "dist") with { PipelineJobPlan = true }));
    }

    // ---- BEH-SCHED-1 (kit.beh): each pipeline stage runs on its own schedule ----------------------------
    // A stage's wake is what starts it: a note for triage, its own job tick for build (BEH-ORCH-11). The
    // conflict in kit.beh BEH-SCHED-1 (a slow build on the same project against one live run per project,
    // BEH-ORCH-3) is not tested here: it is raised for review, and the reading built keeps one run per project.

    [Fact]
    public void BEH_SCHED_1_a_build_that_has_not_woken_does_not_run_while_triage_runs_on_its_own_wake()
    {
        using var rig = new Rig(buildJob: true);

        // Triage's wake is a note left in Kit: triage runs, and its bundle makes the spec stage the next run.
        var spec = Triaged(rig);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "list-sorting").Status);
        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });

        // The build's wake has not fired, so the specced item waits as a planned ticket and no build runs.
        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));

        // Only its own wake starts the build.
        rig.Pipeline!.JobTick(PipelineStage.Build);
        Assert.Contains("You are the build stage for project \"demo\"", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_SCHED_1_an_idle_build_does_not_hold_back_triage_when_triage_wakes()
    {
        using var rig = new Rig(buildJob: true);

        // Build's wake fires with nothing waiting for it: it makes no model call.
        rig.Pipeline!.JobTick(PipelineStage.Build);
        Assert.Null(rig.Claim());

        // Triage's wake, a note, runs triage at once, without another tick.
        rig.Note("the list should sort by date");
        Assert.Contains("You are the triage stage for project \"demo\"", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    // ---- BEH-ORCH-11, slice 5: the build job -------------------------------------------------------------

    // Tickets in the store, as a restart finds them, for a pipeline with the build job on and two projects.
    private static Rig BuildRig(params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(buildJob: true, tickets: store);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return rig;
    }

    [Fact]
    public void BEH_ORCH_11_with_the_build_switch_off_a_specced_items_build_starts_as_before_and_a_build_tick_does_nothing()
    {
        using var rig = new Rig();
        var build = Specced(rig);
        Assert.Contains("You are the build stage", build.Task);

        rig.Pipeline!.JobTick(PipelineStage.Build);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_11_with_the_build_switch_on_a_specced_item_waits_as_a_planned_ticket_until_the_build_tick()
    {
        using var rig = new Rig(buildJob: true);

        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting") });

        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Queued), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
        Assert.Equal(TicketState.Planned, Ticket(rig).State);

        rig.Pipeline!.JobTick(PipelineStage.Spec);
        rig.Pipeline.JobTick(PipelineStage.Context);
        Assert.Null(rig.Claim());

        rig.Pipeline.JobTick(PipelineStage.Build);

        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the build stage for project \"demo\"", build.Task);
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Running), (ItemOf(rig, "list-sorting").Stage, ItemOf(rig, "list-sorting").Status));
    }

    [Fact]
    public void BEH_ORCH_11_one_build_tick_takes_only_the_highest_priority_planned_ticket()
    {
        using var rig = BuildRig(PlannedSeed("a", 3), PlannedSeed("b", 1), PlannedSeed("c", 1), PlannedSeed("d", 2));
        Assert.Null(rig.Claim());

        rig.Pipeline!.JobTick(PipelineStage.Build);
        rig.Pipeline.JobTick(PipelineStage.Build);

        var claim = Assert.Single(Claims(rig));
        Assert.Contains("Work-item: b", claim.Task);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "b").Status);
        Assert.All(new[] { "a", "c", "d" }, slug => Assert.Equal((PipelineStage.Build, WorkItemStatus.Queued), (ItemOf(rig, slug).Stage, ItemOf(rig, slug).Status)));
    }

    [Fact]
    public void BEH_ORCH_11_among_equal_priorities_the_build_job_takes_the_oldest_ticket_first()
    {
        using var rig = BuildRig(PlannedSeed("old", 2), PlannedSeed("newer", 2), PlannedSeed("newest", 2));

        rig.Pipeline!.JobTick(PipelineStage.Build);

        Assert.Contains("Work-item: old", Assert.Single(Claims(rig)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_the_build_job_skips_a_project_with_a_run_in_flight_and_another_project_goes_ahead()
    {
        // "busy" is ready, so on resume its spec starts (the plan job is off) and its project has a run in flight.
        using var rig = BuildRig(Seeded("busy", TicketState.Ready), PlannedSeed("waiting", 1), PlannedSeed("o1", 3) with { Project = "other" });
        Assert.Contains("You are the spec stage", Assert.Single(Claims(rig)).Task);

        rig.Pipeline!.JobTick(PipelineStage.Build);

        var claim = Assert.Single(Claims(rig));
        Assert.Contains("You are the build stage", claim.Task);
        Assert.Contains("Work-item: o1", claim.Task);
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Queued), (ItemOf(rig, "waiting").Stage, ItemOf(rig, "waiting").Status));
    }

    [Fact]
    public void BEH_ORCH_11_a_built_ticket_is_never_taken_by_a_build_tick()
    {
        using var rig = BuildRig(Seeded("built", TicketState.Built), Seeded("merged", TicketState.Merged));

        rig.Pipeline!.JobTick(PipelineStage.Build);

        Assert.Null(rig.Claim());
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done), (ItemOf(rig, "built").Stage, ItemOf(rig, "built").Status));
        Assert.Empty(rig.Store.List());
    }

    [Fact]
    public void BEH_ORCH_11_after_a_restart_a_planned_ticket_waits_for_the_build_job_when_the_switch_is_on_and_is_built_when_it_is_off()
    {
        using var first = new Rig();
        Specced(first);
        Assert.Equal(TicketState.Planned, Ticket(first).State);

        using var on = first.Restart(tickets: first.Tickets, buildJob: true);
        on.Pipeline!.Resume();
        Assert.Null(on.Claim());
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Queued), (ItemOf(on, "list-sorting").Stage, ItemOf(on, "list-sorting").Status));
        on.Pipeline.JobTick(PipelineStage.Build);
        Assert.Contains("You are the build stage", Assert.IsType<PoolClaim>(on.Claim()).Task);

        using var off = first.Restart(tickets: first.Tickets);
        off.Pipeline!.Resume();
        Assert.Contains("You are the build stage", Assert.IsType<PoolClaim>(off.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_with_all_three_switches_on_a_ticket_walks_new_then_ready_then_planned_one_tick_at_a_time()
    {
        using var rig = new Rig(contextJob: true, planJob: true, buildJob: true);
        TriagedForContext(rig);
        Assert.Equal(TicketState.New, Ticket(rig).State);
        Assert.Null(rig.Claim());

        rig.Pipeline!.JobTick(PipelineStage.Context);
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Contexted("list-sorting") });
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
        Assert.Null(rig.Claim());

        rig.Pipeline.JobTick(PipelineStage.Spec);
        var spec = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the spec stage", spec.Task);
        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });
        Assert.Equal(TicketState.Planned, Ticket(rig).State);
        Assert.Null(rig.Claim());

        rig.Pipeline.JobTick(PipelineStage.Build);
        Assert.Contains("You are the build stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_KIT_PIPELINE_JOB_BUILD_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineJobBuild(null));
        Assert.False(KitSettings.ParsePipelineJobBuild(""));
        Assert.False(KitSettings.ParsePipelineJobBuild("off"));
        Assert.True(KitSettings.ParsePipelineJobBuild(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineJobBuild(bad));
        }

        var settings = new KitSettings("d", "r", null, "dist");
        Assert.False(settings.PipelineJobBuild);
        Assert.Empty(settings.PipelineJobs);
        Assert.Equal([PipelineStage.Build], (settings with { PipelineJobBuild = true }).PipelineJobs);
        Assert.Equal([PipelineStage.Context, PipelineStage.Spec, PipelineStage.Build], (settings with { PipelineJobContext = true, PipelineJobPlan = true, PipelineJobBuild = true }).PipelineJobs);
    }

    [Fact]
    public void BEH_ORCH_11_a_job_is_registered_for_the_build_switch()
    {
        var baseline = new KitSettings("d", "r", null, "dist") with { Pipeline = true, RunnerPoolToken = "pool-token", PipelineJobBuild = true };

        using var provider = new ServiceCollection().AddKitServices(baseline).BuildServiceProvider();
        Assert.Single(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<PipelineJob>());
    }

    [Fact]
    public void BEH_ORCH_11_the_context_task_carries_the_bundle_or_says_where_to_read_it()
    {
        var bundle = ContextBundle.Parse(BundleText("list-sorting")).Bundle!;
        var task = ContextStage.BuildTask("demo", "demo.beh", bundle);
        Assert.Contains("Check out branch kit/work/list-sorting from origin", task);
        Assert.Contains("the spec at demo.beh", task);
        Assert.Contains("Work-item: list-sorting", task);
        Assert.Contains("Report the bundle back under the same Work-item slug", task);

        var big = bundle with { Context = new string('x', KitRouter.MaxTask) };
        Assert.Contains("read it at .kit/bundles/list-sorting.md on that branch first", ContextStage.BuildTask("demo", "demo.beh", big));
    }

    // ---- BEH-ORCH-11, slice 6: a ticket's priority comes from its cards ---------------------------------

    private static string PriorityCorpus(int urgent = 1) => $"""
        # kit:repo someone/demo@dev

        behaviour BEH-A "A user sees the list"
          actor engineer
          priority {urgent}

        behaviour BEH-B "A user adds an item"
          actor engineer
          priority 3

        behaviour BEH-C "A user removes an item"
          actor engineer
        """;

    // A bundle text listing exactly these cards.
    private static string BundleWith(string slug, params string[] cards) =>
        BundleText(slug).Replace("Cards:\n- BEH-A\n", "Cards:\n" + string.Concat(cards.Select(c => $"- {c}\n")), StringComparison.Ordinal);

    private static PipelineTicket NewSeed(string slug, params string[] cards)
    {
        var bundle = ContextBundle.Parse(BundleWith(slug, cards)).Bundle!;
        return new PipelineTicket("demo", slug, bundle.Title, TicketState.New, 3, bundle.Cards, bundle.Render(), null, null, false);
    }

    private static Rig PriorityRig(params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(PriorityCorpus(), contextJob: true, tickets: store);
        rig.Pipeline!.Resume();
        return rig;
    }

    // The priority a triage's bundle listing these cards gives the ticket it opens, as its saved row shows it.
    private static int TriagedPriority(params string[] cards)
    {
        using var rig = new Rig(PriorityCorpus());
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleWith("list-sorting", cards) });
        return Ticket(rig).Priority;
    }

    [Fact]
    public void BEH_ORCH_11_a_tickets_priority_is_the_lowest_among_its_cards()
    {
        Assert.Equal(1, TriagedPriority("BEH-A", "BEH-B"));
        Assert.Equal(1, TriagedPriority("BEH-B", "BEH-A"));
        Assert.Equal(3, TriagedPriority("BEH-B"));
    }

    [Fact]
    public void BEH_ORCH_11_a_ticket_with_no_card_or_no_priority_on_its_cards_has_priority_3()
    {
        Assert.Equal(3, TriagedPriority("new: a thing nobody has written yet"));
        Assert.Equal(3, TriagedPriority("BEH-C"));
        Assert.Equal(3, TriagedPriority("BEH-C", "new: another thing"));
    }

    [Fact]
    public void BEH_ORCH_11_a_card_missing_from_the_corpus_is_ignored()
    {
        Assert.Equal(1, TriagedPriority("BEH-GONE-1", "BEH-A"));
        Assert.Equal(3, TriagedPriority("BEH-GONE-1"));
    }

    [Fact]
    public void BEH_ORCH_11_a_priority_5_card_counts_as_4()
    {
        using var rig = new Rig(PriorityCorpus().Replace("priority 3", "priority 5", StringComparison.Ordinal));
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleWith("list-sorting", "BEH-B") });

        Assert.Equal(4, Ticket(rig).Priority);
    }

    [Fact]
    public void BEH_ORCH_11_a_scheduled_card_makes_a_ticket_with_its_own_priority()
    {
        var corpus = ScheduledCorpus.Replace("pending\n", "pending\n  priority 2\n", StringComparison.Ordinal);
        Assert.Contains("priority 2", corpus);
        using var rig = new Rig(corpus, scheduled: true, schedule: "BEH-A\n");

        rig.Pipeline!.ScheduleChanged("demo");

        Assert.Equal(2, rig.Tickets.Read("demo").Single().Priority);
    }

    [Fact]
    public void BEH_ORCH_11_a_job_tick_takes_a_priority_1_ticket_ahead_of_an_older_priority_3_one()
    {
        using var rig = PriorityRig(NewSeed("old", "BEH-B"), NewSeed("urgent", "BEH-A"));

        rig.Pipeline!.JobTick(PipelineStage.Context);

        Assert.Contains("Work-item: urgent", Assert.Single(Claims(rig)).Task);
        Assert.Equal((PipelineStage.Context, WorkItemStatus.Queued), (ItemOf(rig, "old").Stage, ItemOf(rig, "old").Status));
    }

    [Fact]
    public void BEH_ORCH_11_changing_a_cards_priority_between_ticks_reorders_the_waiting_tickets()
    {
        using var rig = PriorityRig(NewSeed("first", "BEH-A"), NewSeed("second", "BEH-B"));
        rig.SetCorpus(PriorityCorpus(urgent: 4));

        rig.Pipeline!.JobTick(PipelineStage.Context);

        Assert.Contains("Work-item: second", Assert.Single(Claims(rig)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_removing_a_cards_priority_puts_its_ticket_back_to_3()
    {
        // "urgent" takes BEH-A's 1 at the first tick's read; with the line gone it is 3, so the older ticket goes first.
        using var rig = PriorityRig(NewSeed("old", "BEH-B"), NewSeed("urgent", "BEH-A") with { Priority = 1 });
        var corpus = PriorityCorpus().Replace("  priority 1\n", "", StringComparison.Ordinal);
        Assert.DoesNotContain("priority 1", corpus);
        rig.SetCorpus(corpus);

        rig.Pipeline!.JobTick(PipelineStage.Context);

        Assert.Contains("Work-item: old", Assert.Single(Claims(rig)).Task);
    }

    [Fact]
    public void BEH_ORCH_11_the_triage_task_lists_tickets_by_their_cards_current_priority()
    {
        using var rig = new Rig(PriorityCorpus());
        var many = Enumerable.Range(1, TriageStage.MaxTickets + 3).Select(i => i == 9 ? NewSeed("t-9", "BEH-A") with { State = TicketState.Ready } : NewSeed($"t-{i}", "BEH-B") with { State = TicketState.Ready }).ToArray();

        var task = TriageClaim(rig, many).Task;

        Assert.Contains($"## Open tickets ({TriageStage.MaxTickets + 3})\n- t-9 [ready]", task);
    }

    [Fact]
    public void BEH_ORCH_11_a_corpus_that_cannot_be_read_in_a_tick_keeps_the_priorities_they_had_and_stops_nothing()
    {
        // "urgent" was saved at priority 1 and "old" at 3; the corpus then stops parsing (9 is not on the card scale).
        using var rig = PriorityRig(NewSeed("old", "BEH-C"), NewSeed("urgent", "BEH-C") with { Priority = 1 });
        rig.SetCorpus("# kit:repo someone/demo@dev\n\nbehaviour BEH-A \"A\"\n  priority 9\n");

        rig.Pipeline!.JobTick(PipelineStage.Context);

        Assert.Contains("Work-item: urgent", Assert.Single(Claims(rig)).Task);
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Status == WorkItemStatus.Stopped);
    }

    // ---- BEH-ORCH-11, slice 7: the merge job --------------------------------------------------------------

    private static string PullOf(int number) => $"https://github.com/someone/demo/pull/{number}";

    // Built tickets as a restart finds them: a done item with its pull request. Slugs ending in a number take that pull request.
    private static PipelineTicket BuiltSeed(string slug, int pull, string project = "demo", params string[] cards) =>
        NewSeed(slug, cards.Length == 0 ? ["BEH-B"] : cards) with { Project = project, State = TicketState.Built, PullRequest = PullOf(pull) };

    private static Rig MergeRig(bool mergeJob = true, bool merge = false, params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(PriorityCorpus(), merge: merge, mergeJob: mergeJob, tickets: store);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return rig;
    }

    [Fact]
    public async Task BEH_ORCH_11_with_the_merge_switch_off_the_merge_of_BEH_ORCH_10_is_unchanged_and_a_tick_does_nothing()
    {
        using var rig = MergeRig(mergeJob: false, merge: true, BuiltSeed("a", 1), BuiltSeed("b", 2));

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Empty(rig.Merges.Asked);

        await rig.Pipeline.MergeReadyAsync(CancellationToken.None);

        Assert.Equal([PullOf(1), PullOf(2)], rig.Merges.Asked);
        Assert.Equal([false, false], rig.Merges.UpdateFirst);
        Assert.All(rig.Pipeline.Items(), i => Assert.True(i.Merged));
    }

    [Fact]
    public async Task BEH_ORCH_11_with_the_merge_switch_on_MergeReadyAsync_merges_nothing_even_with_KIT_PIPELINE_MERGE_on()
    {
        using var rig = MergeRig(mergeJob: true, merge: true, BuiltSeed("a", 1));

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Empty(rig.Merges.Asked);
        Assert.False(ItemOf(rig, "a").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_merge_tick_merges_only_the_highest_priority_built_item_and_leaves_the_rest()
    {
        // b's card is priority 1, a's is 3 and older: b goes first.
        using var rig = MergeRig(true, false, BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), BuiltSeed("c", 3, "demo", "BEH-B"));

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(2)], rig.Merges.Asked);
        Assert.True(ItemOf(rig, "b").Merged);
        Assert.False(ItemOf(rig, "a").Merged);
        Assert.False(ItemOf(rig, "c").Merged);
        Assert.Equal(TicketState.Merged, rig.Tickets.Read("demo").Single(t => t.Slug == "b").State);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        // Equal priority: the older one first.
        Assert.Equal([PullOf(2), PullOf(1)], rig.Merges.Asked);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_merge_that_is_waiting_is_left_and_the_next_tick_asks_for_the_same_item()
    {
        using var rig = MergeRig(true, false, BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        rig.Merges.Answer = new PullRequestMerge { Waiting = true, Reason = "brought up to date with dev" };

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(2), PullOf(2)], rig.Merges.Asked);
        Assert.All(rig.Pipeline.Items(), i => Assert.Equal((WorkItemStatus.Done, false), (i.Status, i.Merged)));
    }

    [Fact]
    public async Task BEH_ORCH_11_a_refused_merge_stops_the_item_and_the_next_tick_takes_the_next_one()
    {
        using var rig = MergeRig(true, false, BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        rig.Merges.Answer = new PullRequestMerge { Reason = "it has a conflict with its base branch" };

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        var stopped = ItemOf(rig, "b");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: it has a conflict with its base branch"), (stopped.Status, stopped.StoppedBecause));
        Assert.Equal([PullOf(2)], rig.Merges.Asked);
        var ticket = Assert.Single(rig.Tickets.Read("demo"), t => t.Slug == "b");
        Assert.Equal((TicketState.Built, stopped.StoppedBecause), (ticket.State, ticket.BlockedBecause)); // BEH-ORCH-16: built but blocked

        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(2), PullOf(1)], rig.Merges.Asked);
        Assert.True(ItemOf(rig, "a").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_11_each_project_gets_one_merge_per_tick()
    {
        using var rig = MergeRig(true, false, BuiltSeed("a", 1), BuiltSeed("b", 2), BuiltSeed("x", 3, "other"), BuiltSeed("y", 4, "other"));

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1), PullOf(3)], rig.Merges.Asked.Order());
        Assert.True(ItemOf(rig, "a").Merged);
        Assert.False(ItemOf(rig, "b").Merged);
        Assert.True(ItemOf(rig, "x", "other").Merged);
        Assert.False(ItemOf(rig, "y", "other").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_11_the_merge_job_asks_for_the_branch_to_be_brought_up_to_date_first()
    {
        using var rig = MergeRig(true, false, BuiltSeed("a", 1));

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        Assert.Equal([true], rig.Merges.UpdateFirst);
    }

    [Fact]
    public void BEH_ORCH_11_KIT_PIPELINE_JOB_MERGE_is_off_unless_it_says_on_and_a_typo_throws()
    {
        Assert.False(KitSettings.ParsePipelineJobMerge(null));
        Assert.False(KitSettings.ParsePipelineJobMerge(""));
        Assert.False(KitSettings.ParsePipelineJobMerge("off"));
        Assert.True(KitSettings.ParsePipelineJobMerge(" on "));
        foreach (var bad in new[] { "true", "1", "ON", "yes" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePipelineJobMerge(bad));
        }

        var settings = new KitSettings("d", "r", null, "dist");
        Assert.False(settings.PipelineJobMerge);
        Assert.Empty(settings.PipelineJobs);
        Assert.Equal([PipelineStage.Merge], (settings with { PipelineJobMerge = true }).PipelineJobs);

        var baseline = settings with { Pipeline = true, RunnerPoolToken = "pool-token", PipelineJobMerge = true };
        using var provider = new ServiceCollection().AddKitServices(baseline).BuildServiceProvider();
        Assert.Single(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<PipelineJob>());
    }

    // ---- BEH-ORCH-11, slice 7b: an agent resolves a conflict ------------------------------------------------

    private static readonly PullRequestMerge Conflicting = new() { Conflict = true, Reason = "it has a conflict with its base branch" };

    private static Rig ResolveRig(StageTurnSettings? turns = null, params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var rig = new Rig(PriorityCorpus(), mergeJob: true, tickets: store, turns: turns);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return rig;
    }

    private static void EndRun(Rig rig, PoolClaim claim, bool succeeded) =>
        Assert.Equal(200, rig.Routes.Finished(claim.Token, Json(new { status = succeeded ? "finished" : "failed" })).Status);

    // A failed run as the runner reports it, naming its step: "verdict exit 1" is a test run that did not pass.
    private static void EndRunFailed(Rig rig, PoolClaim claim, string failure) =>
        Assert.Equal(200, rig.Routes.Finished(claim.Token, Json(new { status = "failed", failure })).Status);

    private const string Red = "verdict exit 1";

    [Fact]
    public async Task BEH_ORCH_11_with_the_merge_switch_off_a_conflict_still_stops_the_item_and_starts_no_run()
    {
        using var rig = MergeRig(mergeJob: false, merge: true, BuiltSeed("a", 1));
        rig.Merges.Answer = Conflicting;

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: it has a conflict with its base branch"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_11_with_the_merge_switch_on_a_conflict_starts_exactly_one_resolve_run_on_the_pull_requests_branch()
    {
        using var rig = ResolveRig(new StageTurnSettings(10, 11, 12, 77), BuiltSeed("a", 1));
        rig.Merges.Answer = Conflicting;

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(("someone/demo", "dev", "claude-haiku-5-5", 77), (claim.Repo, claim.BaseBranch, claim.Model, claim.MaxTurns));
        Assert.Contains("You are the conflict-resolution stage for project \"demo\"", claim.Task);
        Assert.Contains("kit/work/a", claim.Task);
        Assert.Contains("origin/dev", claim.Task);
        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Merge, WorkItemStatus.Running, PullOf(1)), (item.Stage, item.Status, item.PullRequest));
        Assert.Equal(TicketState.Built, rig.Tickets.Read("demo").Single().State);
        Assert.Contains(rig.Store.List(), r => r.Stage == "resolve");

        // The next look neither asks for a merge nor starts a second run.
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1)], rig.Merges.Asked);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_11_a_resolve_run_that_worked_builds_the_item_again_and_the_next_tick_asks_to_merge_it()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = Conflicting;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, PullOf(1), false), (item.Stage, item.Status, item.PullRequest, item.Merged));
        Assert.Null(rig.Claim());

        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1), PullOf(1)], rig.Merges.Asked);
        Assert.True(ItemOf(rig, "a").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_resolve_run_that_failed_stops_the_item_with_why()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = Conflicting;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), false);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "the conflict-resolution run failed"), (item.Status, item.StoppedBecause));
        Assert.Equal((TicketState.Built, item.StoppedBecause), (rig.Tickets.Read("demo").Single().State, rig.Tickets.Read("demo").Single().BlockedBecause));
    }

    [Fact]
    public async Task BEH_ORCH_11_a_second_conflict_after_a_resolve_run_stops_the_item_without_another_run()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = Conflicting;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: it still conflicts with its base after an agent tried to resolve it"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    private static PullRequestMerge ConflictingAt(string head) => new() { Conflict = true, HeadSha = head, Reason = "it has a conflict with its base branch" };

    [Fact]
    public async Task BEH_ORCH_11_a_conflict_at_a_new_head_after_a_resolve_run_that_worked_gets_another_run()
    {
        // claude-code-bot#151: the run resolved it (new head h1), then the base moved on again.
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = ConflictingAt("h0");
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        rig.Merges.Answer = ConflictingAt("h1");
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Merge, WorkItemStatus.Running), (item.Stage, item.Status));
        Assert.Contains("You are the conflict-resolution stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_conflict_at_the_same_head_after_a_resolve_run_stops_the_item_without_another_run()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = ConflictingAt("h0");
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: it still conflicts with its base after an agent tried to resolve it"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_11_a_ticket_gets_at_most_three_resolve_runs_however_often_its_base_moves()
    {
        Assert.Equal(3, Pipeline.MaxResolveRuns);
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        for (var run = 0; run < Pipeline.MaxResolveRuns; run++)
        {
            rig.Merges.Answer = ConflictingAt($"h{run}");
            await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
            EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);
        }

        rig.Merges.Answer = ConflictingAt("h9");
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its pull request was not merged: its base kept moving and it conflicted again after 3 resolve runs"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_19_a_built_ticket_blocked_by_a_conflict_comes_back_after_a_restart_and_gets_a_resolve_run()
    {
        // claude-code-bot#151: kit#868 and others were stopped for good, because a built ticket has no reply path.
        using var rig = ResolveRig(null, BuiltSeed("a", 1) with { BlockedBecause = "its pull request was not merged: it still conflicts with its base after an agent tried to resolve it" });

        Assert.Null(rig.Tickets.Read("demo").Single().BlockedBecause);
        rig.Merges.Answer = ConflictingAt("h0");
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        Assert.Equal((PipelineStage.Merge, WorkItemStatus.Running), (ItemOf(rig, "a").Stage, ItemOf(rig, "a").Status));
        Assert.Contains("You are the conflict-resolution stage", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_ORCH_11_the_resolve_task_says_how_to_resolve_each_shared_kind_of_file()
    {
        var task = ResolveStage.BuildTask("demo", ContextBundle.Parse(Specced("a")).Bundle!, "dev");

        Assert.Contains("git diff --name-only --diff-filter=U", task);
        Assert.Contains(".kit/memory.md is a log of notes: keep every line from both sides", task);
        Assert.Contains("A .beh spec: usually each side added its own behaviour blocks", task);
        Assert.Contains("keep dev's id and give the branch's behaviour the next free one", task);
        Assert.Contains("do not pick one: push nothing and say the run failed, naming that behaviour", task);
        Assert.Contains("A .tests.json map: keep every key from both sides", task);
        Assert.Contains("it must find nothing", task);
        Assert.True(task.Length <= KitRouter.MaxTask);
    }

    [Fact]
    public async Task BEH_ORCH_11_no_other_ticket_of_the_project_merges_while_its_resolve_run_is_in_flight()
    {
        // b (priority 1) is the top item and conflicts; a waits behind it. Another project is not held.
        using var rig = ResolveRig(null, BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), BuiltSeed("x", 3, "other"));
        rig.Merges.AnswerFor[PullOf(2)] = Conflicting;

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Equal(PipelineStage.Merge, ItemOf(rig, "b").Stage);
        Assert.True(ItemOf(rig, "x", "other").Merged);

        rig.Merges.Asked.Clear();
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Empty(rig.Merges.Asked);
        Assert.False(ItemOf(rig, "a").Merged);

        // Once the run has ended, a is looked at again only after b has merged (b is built again and is the top item).
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);
        rig.Merges.AnswerFor.Clear();
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(2)], rig.Merges.Asked);
        Assert.True(ItemOf(rig, "b").Merged);
    }

    [Fact]
    public void BEH_ORCH_11_the_resolve_task_says_which_branch_to_merge_into_and_what_not_to_do()
    {
        var bundle = ContextBundle.Parse(Specced("a")).Bundle!;

        var task = ResolveStage.BuildTask("demo", bundle, "dev");

        Assert.Contains("Check out branch kit/work/a from origin", task);
        Assert.Contains("Merge origin/dev into it", task);
        Assert.Contains("keeping the intent of both sides", task);
        Assert.Contains("whole test suite", task);
        Assert.Contains("push it to kit/work/a on origin", task);
        Assert.Contains("Do not open a pull request, do not touch any other branch, and never force-push", task);
        Assert.Contains("## Bundle (.kit/bundles/a.md)", task);
        Assert.True(task.Length <= KitRouter.MaxTask);
    }

    [Fact]
    public void BEH_ORCH_11_the_resolve_stage_runs_on_the_build_stages_model_and_turns_unless_set()
    {
        Assert.Null(KitSettings.ParseStageModelResolve(null));
        Assert.Null(KitSettings.ParseStageModelResolve(" "));
        Assert.Equal("claude-opus-5-5", KitSettings.ParseStageModelResolve(" claude-opus-5-5 "));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelResolve("not a model!"));
        Assert.Null(KitSettings.ParseStageTurnsResolve(null));
        Assert.Equal(9, KitSettings.ParseStageTurnsResolve(" 9 "));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsResolve("0"));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsResolve("many"));
        var settings = new KitSettings("d", "r", null, "dist");
        Assert.Equal((null, null), (settings.StageModelResolve, settings.StageTurnsResolve));

        Assert.Equal("claude-haiku-5-5", Orchestrator.ModelFor(PipelineStage.Merge, Models));
        Assert.Equal("m-res", Orchestrator.ModelFor(PipelineStage.Merge, Models with { Resolve = "m-res" }));
        Assert.Equal(StageTurnSettings.DefaultBuild, Orchestrator.TurnsFor(PipelineStage.Merge, StageTurnSettings.Defaults));
        Assert.Equal(5, Orchestrator.TurnsFor(PipelineStage.Merge, StageTurnSettings.Defaults with { Resolve = 5 }));
    }

    // ---- BEH-ORCH-12: Kit runs a private project's test suite itself -----------------------------------------

    private const string HeadA = "abc1234def5678";

    private const string HeadB = "fed9876cba4321";

    private static PullRequestMerge NeedsTest(string sha) => new() { NeedsTest = true, HeadSha = sha, Reason = $"no Kit test run has passed at {sha[..7]}" };

    [Fact]
    public async Task BEH_ORCH_12_a_merge_look_that_needs_a_test_starts_one_test_run_at_that_commit_on_the_pull_requests_branch()
    {
        using var rig = ResolveRig(new StageTurnSettings(10, 11, 12, 77), BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(("someone/demo", "dev", "claude-haiku-5-5", 77), (claim.Repo, claim.BaseBranch, claim.Model, claim.MaxTurns));
        Assert.Equal("test", claim.Stage);
        Assert.Contains("You are the test stage for project \"demo\"", claim.Task);
        Assert.Contains(HeadA, claim.Task);
        Assert.Contains("kit/work/a", claim.Task);
        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Test, WorkItemStatus.Running, PullOf(1)), (item.Stage, item.Status, item.PullRequest));
        Assert.Equal(TicketState.Built, rig.Tickets.Read("demo").Single().State);
        Assert.Contains(rig.Store.List(), r => r.Stage == "test");
        Assert.Equal([null], rig.Merges.TestedSha);

        // The next look neither asks for a merge nor starts a second run: the project has a test running.
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1)], rig.Merges.Asked);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_12_a_test_run_that_passed_is_remembered_and_the_next_merge_look_merges_that_commit()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, PullOf(1), false), (item.Stage, item.Status, item.PullRequest, item.Merged));
        Assert.Null(rig.Claim());

        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([null, HeadA], rig.Merges.TestedSha);
        Assert.True(ItemOf(rig, "a").Merged);
        Assert.Equal(TicketState.Merged, rig.Tickets.Read("demo").Single().State);
    }

    [Fact]
    public async Task BEH_ORCH_12_a_test_run_that_failed_leaves_the_item_built_and_the_next_merge_look_starts_a_fix_run_at_that_head()
    {
        // claude-code-bot#151: five pull requests failed the spec gate at their merge-time test and each stopped for good.
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRunFailed(rig, Assert.IsType<PoolClaim>(rig.Claim()), Red);

        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, null), (item.Stage, item.Status, item.StoppedBecause));
        Assert.Null(rig.Tickets.Read("demo").Single().BlockedBecause);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var fix = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("fix", fix.Stage);
        Assert.True(RunnerPool.GoesFirst(fix.Stage));
        Assert.Contains("You are the fix stage for project \"demo\"", fix.Task);
        Assert.Contains(HeadA, fix.Task);
        Assert.Contains("kit/work/a", fix.Task);
        Assert.Contains("Do not delete or weaken a test", fix.Task);
        Assert.Equal((PipelineStage.Merge, WorkItemStatus.Running), (ItemOf(rig, "a").Stage, ItemOf(rig, "a").Status));

        // The fix pushed a new head; the next look tests that one, not the red one.
        EndRun(rig, fix, true);
        rig.Merges.Answer = NeedsTest(HeadB);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var retest = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("test", retest.Stage);
        Assert.Contains(HeadB, retest.Task);
    }

    [Fact]
    public async Task BEH_ORCH_12_a_fix_run_that_left_the_head_where_it_was_stops_the_item_with_why()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRunFailed(rig, Assert.IsType<PoolClaim>(rig.Claim()), Red);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        // Still at HeadA, the commit that already failed: no test is paid for again, and no second fix.
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its tests still fail at abc1234 after a fix run changed nothing"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());
        Assert.True(Pipeline.Retryable(item.StoppedBecause));
    }

    [Fact]
    public async Task BEH_ORCH_12_a_ticket_gets_at_most_two_fix_runs_and_a_failed_fix_run_says_so()
    {
        Assert.Equal(2, Pipeline.MaxFixRuns);
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        string[] heads = ["h0aaaaaaaa", "h1bbbbbbbb", "h2cccccccc"];
        for (var run = 0; run < Pipeline.MaxFixRuns; run++)
        {
            rig.Merges.Answer = NeedsTest(heads[run]);
            await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
            EndRunFailed(rig, Assert.IsType<PoolClaim>(rig.Claim()), Red); // the test at this head fails
            await rig.Pipeline.MergeTickAsync(CancellationToken.None);
            var fix = Assert.IsType<PoolClaim>(rig.Claim());
            Assert.Contains("You are the fix stage", fix.Task);
            EndRun(rig, fix, true);
        }

        rig.Merges.Answer = NeedsTest(heads[2]);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRunFailed(rig, Assert.IsType<PoolClaim>(rig.Claim()), Red);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "its tests still fail at h2ccccc after 2 fix runs"), (item.Status, item.StoppedBecause));
        Assert.Null(rig.Claim());

        // A fix run that fails names itself, not a conflict resolution.
        using var other = ResolveRig(null, BuiltSeed("b", 2));
        other.Merges.Answer = NeedsTest(HeadA);
        await other.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRunFailed(other, Assert.IsType<PoolClaim>(other.Claim()), Red);
        await other.Pipeline.MergeTickAsync(CancellationToken.None);
        EndRun(other, Assert.IsType<PoolClaim>(other.Claim()), false);

        var failed = ItemOf(other, "b");
        Assert.Equal((WorkItemStatus.Stopped, "the fix run failed"), (failed.Status, failed.StoppedBecause));
        Assert.True(Pipeline.Retryable(failed.StoppedBecause));
    }

    [Theory]
    [InlineData("time exit 124")]
    [InlineData("claude exit 1")]
    [InlineData(null)]
    public async Task BEH_ORCH_12_a_test_run_that_ran_out_of_time_or_crashed_stops_the_item_and_starts_no_fix(string? failure)
    {
        // A run that never reached its verdict says nothing about the code, so there is nothing for a fix run to fix.
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        if (failure is null)
        {
            EndRun(rig, claim, false);
        }
        else
        {
            EndRunFailed(rig, claim, failure);
        }

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "the project's tests failed at abc1234"), (item.Status, item.StoppedBecause));

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1)], rig.Merges.Asked);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_12_build_resolve_fix_and_test_all_name_the_same_whole_suite_with_its_ci_steps()
    {
        // claude-code-bot#151: the build ran the test projects, the test run ran every CI step, and only the second ran the spec gate.
        Assert.Contains(".github/workflows", TestStage.WholeSuite);
        Assert.Contains("spec gate", TestStage.WholeSuite);
        var bundle = ContextBundle.Parse(Specced("a")).Bundle!;
        Assert.Contains(TestStage.WholeSuite, BuildStage.BuildTask("demo", bundle));
        Assert.Contains(TestStage.WholeSuite, ResolveStage.BuildTask("demo", bundle, "dev"));
        Assert.Contains(TestStage.WholeSuite, FixStage.BuildTask("demo", bundle, "dev", HeadA));
        Assert.Contains(TestStage.WholeSuite, TestStage.BuildTask("demo", bundle, "dev", HeadA));
    }

    [Fact]
    public async Task BEH_ORCH_19_a_lost_test_run_leaves_the_ticket_built_and_the_next_merge_look_tests_it_again()
    {
        // kit#768's test run was lost at the 2026-11-19 20:17Z restart, which blocked it as "tests failed".
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        var lost = Assert.IsType<PoolClaim>(rig.Claim());

        // As RunnerPoolRoutes.FailLost does it: the run is failed as lost, then its item is told.
        rig.Store.SetStatus(lost.Id, AgentRunStatus.Failed, DateTimeOffset.UtcNow, RunnerPool.LostFailure);
        rig.Pipeline.RunEnded(lost.Id, false, failure: RunnerPool.LostFailure);

        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done, null), (item.Stage, item.Status, item.StoppedBecause));
        Assert.Null(rig.Tickets.Read("demo").Single().BlockedBecause);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.NotEqual(lost.Id, Assert.IsType<PoolClaim>(rig.Claim()).Id);
    }

    [Fact]
    public async Task BEH_ORCH_19_a_test_run_lost_three_times_in_a_row_blocks_the_ticket_rather_than_testing_for_ever()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);

        for (var run = 1; run <= Pipeline.RunRetries + 1; run++)
        {
            await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
            var lost = Assert.IsType<PoolClaim>(rig.Claim());
            rig.Store.SetStatus(lost.Id, AgentRunStatus.Failed, DateTimeOffset.UtcNow, RunnerPool.LostFailure);
            rig.Pipeline.RunEnded(lost.Id, false, failure: RunnerPool.LostFailure);
        }

        var item = ItemOf(rig, "a");
        Assert.Equal((WorkItemStatus.Stopped, "the test run was lost, 3 runs in a row"), (item.Status, item.StoppedBecause));
        Assert.Equal((TicketState.Built, item.StoppedBecause), (rig.Tickets.Read("demo").Single().State, rig.Tickets.Read("demo").Single().BlockedBecause));
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_12_a_head_that_moved_after_a_pass_is_tested_again_at_the_new_commit()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        // The merger sees a new head: the pass at HeadA does not cover it.
        rig.Merges.Answer = NeedsTest(HeadB);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal([null, HeadA], rig.Merges.TestedSha);
        var second = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains(HeadB, second.Task);
        Assert.DoesNotContain(HeadA, second.Task);
        Assert.Equal(PipelineStage.Test, ItemOf(rig, "a").Stage);
        Assert.Equal(2, rig.Store.List().Count(r => r.Stage == "test"));

        EndRun(rig, second, true);
        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal(HeadB, rig.Merges.TestedSha[^1]);
        Assert.True(ItemOf(rig, "a").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_12_a_project_with_another_run_in_flight_starts_no_test_and_the_item_stays_built()
    {
        using var rig = ResolveRig(null, BuiltSeed("a", 1));
        rig.Note("something to triage");
        Assert.IsType<PoolClaim>(rig.Claim());
        rig.Merges.Answer = NeedsTest(HeadA);

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        Assert.Equal([PullOf(1)], rig.Merges.Asked);
        var item = ItemOf(rig, "a");
        Assert.Equal((PipelineStage.Build, WorkItemStatus.Done), (item.Stage, item.Status));
        Assert.Null(rig.Claim());
        Assert.DoesNotContain(rig.Store.List(), r => r.Stage == "test");
    }

    [Fact]
    public async Task BEH_ORCH_12_a_test_the_merge_job_could_not_start_gets_the_projects_next_free_slot_before_another_build()
    {
        // His 2026-11-19 21:07Z comment on claude-code-bot#145: "I don't see the merge runners at all". The build job took
        // a freed project at once, while the merge look was still asking GitHub, so with tickets planned no test ever ran.
        var store = new MemoryTicketStore();
        foreach (var ticket in new[] { BuiltSeed("a", 1), PlannedSeed("p1", 1), PlannedSeed("p2", 2) })
        {
            store.Save(ticket);
        }

        using var rig = new Rig(mergeJob: true, buildJob: true, tickets: store);
        rig.Pipeline!.Resume();
        rig.Pipeline.JobTick(PipelineStage.Build);
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("build", build.Stage);

        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.DoesNotContain(rig.Store.List(), r => r.Stage == "test");

        // The build ends: the build job, looking first, leaves the project for the merge job's test.
        EndRun(rig, build, true);
        rig.Pipeline.JobTick(PipelineStage.Build);
        Assert.Equal(WorkItemStatus.Queued, ItemOf(rig, "p2").Status);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        var test = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("test", test.Stage);

        // Once the test is going, the merge job wants nothing more: when it ends, the build job takes the project again.
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        EndRun(rig, test, true);
        rig.Pipeline.JobTick(PipelineStage.Build);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "p2").Status);
    }

    [Fact]
    public async Task BEH_ORCH_12_with_only_the_merge_job_on_a_run_that_ends_leaves_the_project_for_the_test_the_merge_job_wants()
    {
        // His 2026-11-20 01:20Z comment on claude-code-bot#145: "I still don't see things getting merged". Production runs
        // the merge job alone, so a planned ticket's build starts the moment the run before it ends, never from a job tick,
        // and the reservation above was read by the build job only: seven pull requests were built overnight, none tested.
        var store = new MemoryTicketStore();
        foreach (var ticket in new[] { BuiltSeed("a", 1), PlannedSeed("p1", 1), PlannedSeed("p2", 2) })
        {
            store.Save(ticket);
        }

        using var rig = new Rig(mergeJob: true, tickets: store);
        rig.Pipeline!.Resume();
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("build", build.Stage);

        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.DoesNotContain(rig.Store.List(), r => r.Stage == "test");

        // The build ends: the next planned ticket waits, and the merge job's next look starts the test.
        EndRun(rig, build, true);
        Assert.Equal(WorkItemStatus.Queued, ItemOf(rig, "p2").Status);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        var test = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("test", test.Stage);

        // When the test ends, the waiting build goes.
        EndRun(rig, test, true);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "p2").Status);
    }

    [Fact]
    public async Task BEH_ORCH_12_a_reservation_the_merge_job_no_longer_needs_starts_what_waited_behind_it()
    {
        // The merge job wanted the project, then found nothing to test (the pull request was merged elsewhere): the build
        // that waited must not wait for some other run's end that may never come.
        var store = new MemoryTicketStore();
        foreach (var ticket in new[] { BuiltSeed("a", 1), PlannedSeed("p1", 1), PlannedSeed("p2", 2) })
        {
            store.Save(ticket);
        }

        using var rig = new Rig(mergeJob: true, tickets: store);
        rig.Pipeline!.Resume();
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        rig.Merges.Answer = NeedsTest(HeadA);
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        EndRun(rig, build, true);
        Assert.Equal(WorkItemStatus.Queued, ItemOf(rig, "p2").Status);

        rig.Merges.Answer = new PullRequestMerge { Merged = true };
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.True(ItemOf(rig, "a").Merged);
        Assert.Equal(WorkItemStatus.Running, ItemOf(rig, "p2").Status);
    }

    [Fact]
    public async Task BEH_ORCH_12_no_other_ticket_of_the_project_merges_while_its_test_run_is_in_flight()
    {
        // b (priority 1) is the top item and needs a test; a waits behind it. Another project is not held.
        using var rig = ResolveRig(null, BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), BuiltSeed("x", 3, "other"));
        rig.Merges.AnswerFor[PullOf(2)] = NeedsTest(HeadA);

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Equal(PipelineStage.Test, ItemOf(rig, "b").Stage);
        Assert.True(ItemOf(rig, "x", "other").Merged);

        rig.Merges.Asked.Clear();
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Empty(rig.Merges.Asked);
        Assert.False(ItemOf(rig, "a").Merged);
    }

    // ---- BEH-ORCH-18: the merge train --------------------------------------------------------------------------

    private const string TrainHead = "7a1n0000000000000000000000000000000000aa";

    private const string BaseHead = "ba5e0000000000000000000000000000000000bb";

    // A merger that can also build trains: GitHub's side of BEH-ORCH-18, with the head each pull request rode at named after it.
    private sealed class TrainMerger : IPullRequestMerger, IMergeTrain
    {
        // The rig's own merger, for the single way.
        public Merger Single { get; set; } = new();

        public bool Private { get; set; } = true;

        public HashSet<string> Conflicts { get; } = [];

        public string Base { get; set; } = BaseHead;

        public List<(string Repository, string Base, string Branch, IReadOnlyList<string> Pulls)> Built { get; } = [];

        public List<(string Url, string Sha)> MergedAt { get; } = [];

        public Task<IPullRequestMerge> MergeIfGreenAsync(string pullRequestUrl, string? repository, CancellationToken cancellationToken, bool updateFirst = false, string? testedSha = null) =>
            Single.MergeIfGreenAsync(pullRequestUrl, repository, cancellationToken, updateFirst, testedSha);

        public Task<TrainBuilt> BuildAsync(string repository, string baseBranch, string trainBranch, IReadOnlyList<string> pullRequestUrls, CancellationToken cancellationToken)
        {
            Built.Add((repository, baseBranch, trainBranch, pullRequestUrls));
            return Task.FromResult(Private
                ? new TrainBuilt(TrainHead, Base, pullRequestUrls.Where(u => !Conflicts.Contains(u)).Select(u => (u, HeadAt(u))).ToList(), pullRequestUrls.Where(Conflicts.Contains).ToList())
                : new TrainBuilt(null, null, [], [], NotPrivate: true));
        }

        public Task<string?> MergeAtAsync(string pullRequestUrl, string headSha, CancellationToken cancellationToken)
        {
            MergedAt.Add((pullRequestUrl, headSha));
            return Task.FromResult<string?>(null);
        }

        public Task<string?> HeadOfAsync(string repository, string branch, CancellationToken cancellationToken) => Task.FromResult<string?>(Base);

        public static string HeadAt(string url) => "head-of-" + url[(url.LastIndexOf('/') + 1)..];
    }

    private static (Rig Rig, TrainMerger Train) TrainRig(params PipelineTicket[] seeded)
    {
        var store = new MemoryTicketStore();
        foreach (var ticket in seeded)
        {
            store.Save(ticket);
        }

        var train = new TrainMerger();
        var rig = new Rig(PriorityCorpus(), mergeJob: true, tickets: store, merger: train);
        train.Single = rig.Merges;
        rig.Merges.Answer = NeedsTest(HeadA);
        rig.AddProject("other");
        rig.Pipeline!.Resume();
        return (rig, train);
    }

    [Fact]
    public async Task BEH_ORCH_18_built_tickets_on_one_private_repo_ride_one_train_and_one_test_run_tests_it()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), BuiltSeed("c", 3, "demo", "BEH-B"));
        using var _ = rig;

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);

        var built = Assert.Single(train.Built);
        Assert.Equal(("someone/demo", "dev", "kit/train/dev"), (built.Repository, built.Base, built.Branch));
        Assert.Equal(3, built.Pulls.Count);
        var claim = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("test", claim.Stage);
        Assert.Contains("kit/train/dev", claim.Task);
        Assert.Contains(TrainHead, claim.Task);
        Assert.Single(rig.Store.List(), r => r.Stage == "test");

        // While it runs, nothing of the project is merged or tested alone, and the train is not built again.
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.Single(train.Built);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public async Task BEH_ORCH_18_a_repo_refused_a_train_once_is_asked_again_at_the_next_look()
    {
        // The refusal is about the pull requests of that look (one had its own Actions run), not the repo for ever.
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        rig.Merges.Answer = new PullRequestMerge { Waiting = true };
        train.Private = false;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Null(rig.Claim());

        train.Private = true;
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal(2, train.Built.Count);
        Assert.Contains("kit/train/dev", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public async Task BEH_ORCH_18_with_only_the_merge_job_on_a_train_gets_its_lead_projects_next_free_slot()
    {
        // Production's shape (claude-code-bot#145, 2026-11-20): builds start as runs end, not from a job.
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), PlannedSeed("p1", 1), PlannedSeed("p2", 2));
        using var _ = rig;
        var build = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("build", build.Stage);

        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.Null(rig.Claim());

        EndRun(rig, build, true);
        Assert.Equal(WorkItemStatus.Queued, ItemOf(rig, "p2").Status);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        var test = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal("test", test.Stage);
        Assert.Contains("kit/train/dev", test.Task);
    }

    [Fact]
    public async Task BEH_ORCH_18_a_train_that_passed_merges_each_pull_request_in_order_at_the_commit_it_rode_at()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        var order = train.Built[0].Pulls;
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal(order.Select(u => (u, TrainMerger.HeadAt(u))).ToList(), train.MergedAt);
        Assert.True(ItemOf(rig, "a").Merged);
        Assert.True(ItemOf(rig, "b").Merged);
        Assert.All(rig.Tickets.Read("demo"), t => Assert.Equal(TicketState.Merged, t.State));
    }

    [Fact]
    public async Task BEH_ORCH_18_a_base_that_moved_after_the_train_was_built_merges_nothing_and_the_train_is_built_again()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);
        train.Base = "m0ved000000000000000000000000000000000cc";

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Empty(train.MergedAt);
        Assert.False(ItemOf(rig, "a").Merged);
        Assert.Equal(2, train.Built.Count);
        Assert.Equal("test", Assert.IsType<PoolClaim>(rig.Claim()).Stage);
    }

    [Fact]
    public async Task BEH_ORCH_18_a_passed_train_whose_base_cannot_be_read_waits_and_one_whose_rider_changed_merges_nothing()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), true);

        // GitHub could not say where the base is: nothing is merged, nothing is rebuilt, and no test is paid for again.
        train.Base = null!;
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.Empty(train.MergedAt);
        Assert.Single(train.Built);
        Assert.Null(rig.Claim());

        // A triage rewrote a rider's bundle meanwhile, so it is no longer built: the train without it was never tested, and
        // none of it is merged.
        train.Base = BaseHead;
        rig.Note("b should also sort by name");
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Equal(200, rig.FinishWith(triage, new { slug = "b", text = BundleText("b") }).Status);
        Assert.False(rig.Pipeline.Items().Any(i => i.Project == "demo" && i.Slug == "b" && i is { Stage: PipelineStage.Build, Status: WorkItemStatus.Done }));
        await rig.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.Empty(train.MergedAt);
        Assert.False(ItemOf(rig, "a").Merged);
    }

    [Fact]
    public async Task BEH_ORCH_18_a_train_that_failed_stops_no_ticket_and_its_riders_go_the_single_way()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        EndRun(rig, Assert.IsType<PoolClaim>(rig.Claim()), false);

        Assert.All(new[] { ItemOf(rig, "a"), ItemOf(rig, "b") }, i => Assert.Equal((PipelineStage.Build, WorkItemStatus.Done), (i.Stage, i.Status)));
        rig.Merges.Asked.Clear();

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Single(train.Built);
        Assert.Single(rig.Merges.Asked);
        var alone = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains(HeadA, alone.Task);
        Assert.DoesNotContain("kit/train/", alone.Task);
    }

    [Fact]
    public async Task BEH_ORCH_19_a_train_whose_test_run_was_lost_is_built_again_rather_than_sending_its_riders_the_single_way()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var _ = rig;
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        var lost = Assert.IsType<PoolClaim>(rig.Claim());
        rig.Store.SetStatus(lost.Id, AgentRunStatus.Failed, DateTimeOffset.UtcNow, RunnerPool.LostFailure);
        rig.Pipeline.RunEnded(lost.Id, false, failure: RunnerPool.LostFailure);

        Assert.All(new[] { ItemOf(rig, "a"), ItemOf(rig, "b") }, i => Assert.Equal((PipelineStage.Build, WorkItemStatus.Done), (i.Stage, i.Status)));

        await rig.Pipeline.MergeTickAsync(CancellationToken.None);

        Assert.Equal(2, train.Built.Count);
        Assert.Contains("kit/train/", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public async Task BEH_ORCH_18_a_rider_that_conflicts_leaves_the_train_and_a_public_repo_gets_none()
    {
        var (rig, train) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"), BuiltSeed("c", 3, "demo", "BEH-B"));
        using var _ = rig;
        train.Conflicts.Add(PullOf(3));
        await rig.Pipeline!.MergeTickAsync(CancellationToken.None);
        Assert.DoesNotContain(PullOf(3), Assert.IsType<PoolClaim>(rig.Claim()).Task.Split('\n').First(l => l.StartsWith("This branch is a merge train", StringComparison.Ordinal)));

        var (open, publicTrain) = TrainRig(BuiltSeed("a", 1, "demo", "BEH-B"), BuiltSeed("b", 2, "demo", "BEH-A"));
        using var __ = open;
        publicTrain.Private = false;
        await open.Pipeline!.MergeTickAsync(CancellationToken.None);
        await open.Pipeline.MergeTickAsync(CancellationToken.None);
        Assert.Single(publicTrain.Built);
        Assert.DoesNotContain("kit/train/", Assert.IsType<PoolClaim>(open.Claim()).Task);
    }

    [Fact]
    public async Task BEH_ORCH_12_with_the_merge_switch_off_no_test_is_asked_for_or_run()
    {
        using var rig = MergeRig(mergeJob: false, merge: true, BuiltSeed("a", 1));
        rig.Merges.Answer = NeedsTest(HeadA);

        await rig.Pipeline!.MergeReadyAsync(CancellationToken.None);

        Assert.Equal([false], rig.Merges.UpdateFirst);
        Assert.Equal([null], rig.Merges.TestedSha);
        Assert.Null(rig.Claim());
        Assert.DoesNotContain(rig.Pipeline.Items(), i => i.Stage == PipelineStage.Test);
    }

    [Fact]
    public void BEH_ORCH_12_the_test_task_names_the_commit_and_branch_and_forbids_changing_anything()
    {
        var bundle = ContextBundle.Parse(Specced("a")).Bundle!;

        var task = TestStage.BuildTask("demo", bundle, "dev", HeadA);

        Assert.Contains($"check out exactly commit {HeadA} (a detached checkout)", task);
        Assert.Contains("Fetch branch kit/work/a from origin", task);
        Assert.Contains("whole test suite", task);
        Assert.Contains("the run succeeds only if the whole suite passed at that commit", task);
        Assert.Contains($"must END with exactly one of these two lines", task);
        Assert.Contains($"`KIT-TEST: PASS {HeadA}`", task);
        Assert.Contains("`KIT-TEST: FAIL`", task);
        Assert.Contains($"checked out at commit {HeadA} when you finish: the runner checks HEAD", task);
        Assert.Contains("do not edit, commit or push anything, do not open a pull request", task);
        Assert.Contains("## Bundle (.kit/bundles/a.md)", task);
        Assert.True(task.Length <= KitRouter.MaxTask);
    }

    [Fact]
    public void BEH_ORCH_12_the_test_stage_runs_on_triages_model_and_the_build_stages_turns_unless_set()
    {
        Assert.Null(KitSettings.ParseStageModelTest(null));
        Assert.Null(KitSettings.ParseStageModelTest(" "));
        Assert.Equal("claude-opus-5-5", KitSettings.ParseStageModelTest(" claude-opus-5-5 "));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelTest("not a model!"));
        Assert.Null(KitSettings.ParseStageTurnsTest(null));
        Assert.Equal(9, KitSettings.ParseStageTurnsTest(" 9 "));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsTest("0"));
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsTest("many"));
        var settings = new KitSettings("d", "r", null, "dist");
        Assert.Equal((null, null), (settings.StageModelTest, settings.StageTurnsTest));

        var models = Models with { Triage = "m-tri", Build = "m-build" };
        Assert.Equal("m-tri", Orchestrator.ModelFor(PipelineStage.Test, models));
        Assert.Equal("m-test", Orchestrator.ModelFor(PipelineStage.Test, models with { Test = "m-test" }));
        Assert.Equal(StageTurnSettings.DefaultBuild, Orchestrator.TurnsFor(PipelineStage.Test, StageTurnSettings.Defaults));
        Assert.Equal(5, Orchestrator.TurnsFor(PipelineStage.Test, StageTurnSettings.Defaults with { Test = 5 }));
    }

    // ---- BEH-TICKET-1 to 4: a ticket is not a spec; triage turns loose input into a change and a ticket; blocked is a tag;
    // technical design and implementation follow a ready ticket. The same rig as the BEH-ORCH cards above.

    // A question for him an escalated review raises, with the default he gets if he stays silent (BEH-TICKET-3).
    private const string Question = "which sort order does he want? Silence: sort by date, newest first";

    private static string Escalated(string text, string question) => text.Replace("Cards:", $"Review: escalated — {question}\nCards:");

    [Fact]
    public void BEH_TICKET_1_a_ticket_on_the_board_shows_its_status_and_open_questions_and_no_spec_text()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");

        var list = Assert.IsType<TicketList>(rig.Router.Route("GET", "/api/projects/demo/tickets").Body);

        var ticket = Assert.Single(list.Tickets);
        Assert.Equal(("ready", "the spec run failed"), (ticket.State, ticket.BlockedBecause));
        Assert.Equal(["Blocked: the spec run failed"], ticket.Comments.Select(c => c.Body));
        Assert.Equal(new[] { "Slug", "Title", "State", "Priority", "Cards", "PullRequest", "BlockedBecause", "Comments" }, typeof(ITicketView).GetProperties().Select(p => p.Name));
        var json = JsonSerializer.Serialize(list);
        Assert.DoesNotContain("src/List.cs", json);
        Assert.DoesNotContain("sort by date", json);
    }

    [Fact]
    public void BEH_TICKET_1_the_spec_of_a_change_is_read_from_that_changes_feature_branch()
    {
        using var rig = new Rig();

        var spec = Triaged(rig);

        Assert.Contains("Check out branch kit/work/list-sorting from origin", spec.Task);
        Assert.Contains("Read the cards under Cards in the spec at demo.beh", spec.Task);
        Assert.Equal("kit/work/list-sorting", TriageStage.Branch(Ticket(rig).Slug));
    }

    [Fact]
    public void BEH_TICKET_1_two_changes_give_two_tickets_each_on_its_own_feature_branch()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date and export");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") }, new { slug = "list-export", text = BundleText("list-export") });

        var tickets = rig.Tickets.Read("demo");
        Assert.Equal(["list-export", "list-sorting"], tickets.Select(t => t.Slug).OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(2, tickets.Select(t => TriageStage.Branch(t.Slug)).Distinct().Count());
        Assert.Equal(["kit/work/list-export", "kit/work/list-sorting"], tickets.Select(t => TriageStage.Branch(t.Slug)).OrderBy(s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void BEH_TICKET_2_a_note_reaches_triage_and_makes_a_ticket_in_the_new_state()
    {
        using var rig = new Rig(contextJob: true);
        rig.Note("the list should sort by date");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal(TicketState.New, Ticket(rig).State);
    }

    [Fact]
    public void BEH_TICKET_2_a_comment_on_a_card_reaches_triage_and_makes_a_ticket_in_the_new_state()
    {
        using var rig = new Rig(contextJob: true);
        rig.Note("sorting should be stable", behaviour: "BEH-A");

        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("sorting should be stable", triage.Task);
        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal(TicketState.New, Ticket(rig).State);
    }

    [Fact]
    public void BEH_TICKET_2_a_chat_message_he_has_Kit_note_reaches_triage_as_a_note_and_makes_a_ticket_in_the_new_state()
    {
        // The Chat page saves what he asks it to note through the notes route, as a note: the server writes nothing for a chat turn.
        using var rig = new Rig(contextJob: true);
        rig.Note("the list should sort by date, as we discussed in chat");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal(TicketState.New, Ticket(rig).State);
    }

    [Fact]
    public void BEH_TICKET_2_triage_makes_its_spec_change_on_a_feature_branch_and_never_on_the_default_branch()
    {
        var task = TriageStage.BuildTask("demo", [("demo.notes.md#x", "the list should sort by date")], []).Text;

        Assert.Contains("on branch kit/work/<work-item-slug>", task);
        Assert.Contains("not the default branch", task);
        Assert.DoesNotContain("on the default branch", task);
    }

    [Fact]
    public void BEH_TICKET_2_a_card_triage_removes_is_marked_to_be_deleted_with_its_reason_and_not_deleted_outright()
    {
        var task = TriageStage.BuildTask("demo", [("demo.notes.md#x", "drop the export")], []).Text;

        Assert.Contains("Delete no card", task);
        Assert.Contains("contract my assumption: delete this card, because <reason>", task);
        Assert.Contains("Delete no card: when a note says one should go, add to it", task);
    }

    [Fact]
    public void BEH_TICKET_3_a_ticket_with_an_open_question_for_him_is_blocked_and_stays_the_same_ticket()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);
        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting", flag: "two cards may conflict") });
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Escalated(Specced("list-sorting", "two cards may conflict"), Question) });

        var ticket = Assert.Single(rig.Tickets.Read("demo"));
        Assert.Equal(("list-sorting", TicketState.Ready, Question), (ticket.Slug, ticket.State, ticket.BlockedBecause));
    }

    [Fact]
    public void BEH_TICKET_3_blocked_combines_with_new_and_setting_and_clearing_the_tag_leaves_the_state_new()
    {
        using var rig = new Rig(contextJob: true);
        TriagedForContext(rig);
        Assert.Equal(TicketState.New, Ticket(rig).State);

        rig.Pipeline!.JobTick(PipelineStage.Context);
        rig.Finish(rig.Claim()!, status: "failed");
        Assert.Equal((TicketState.New, "the context run failed"), (Ticket(rig).State, Ticket(rig).BlockedBecause));

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal((TicketState.New, (string?)null), (Ticket(rig).State, Ticket(rig).BlockedBecause));
    }

    [Fact]
    public void BEH_TICKET_3_blocked_combines_with_ready_and_setting_and_clearing_the_tag_leaves_the_state_ready()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");
        Assert.Equal((TicketState.Ready, "the spec run failed"), (Ticket(rig).State, Ticket(rig).BlockedBecause));

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date" }));
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal((TicketState.Ready, (string?)null), (Ticket(rig).State, Ticket(rig).BlockedBecause));
    }

    [Fact]
    public void BEH_TICKET_3_his_reply_is_recorded_on_the_ticket_and_the_ticket_moves_to_ready_once_no_question_is_open()
    {
        using var rig = new Rig();
        rig.Finish(Triaged(rig), status: "failed");

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "sort by date, newest first" }));
        Assert.Equal(["Blocked: the spec run failed", "sort by date, newest first"], rig.Tickets.Comments("demo", "list-sorting").Select(c => c.Body));
        Assert.Equal(TicketState.Ready, Ticket(rig).State);
        Assert.NotNull(Ticket(rig).BlockedBecause);

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal((TicketState.Ready, (string?)null), (Ticket(rig).State, Ticket(rig).BlockedBecause));
    }

    [Fact]
    public void BEH_TICKET_3_if_he_stays_silent_on_a_blocked_ticket_triage_acts_on_the_default_written_on_it()
    {
        using var rig = new Rig();
        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting", flag: "two cards may conflict") });
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Escalated(Specced("list-sorting", "two cards may conflict"), Question) });
        Assert.Contains("Silence: sort by date, newest first", Ticket(rig).BlockedBecause);

        // He says nothing. The next note makes triage run, and it is told the default on the blocked ticket.
        rig.Note("the export should keep the sort order");
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("## Blocked, no reply from him (act on the default written on each)\n- list-sorting [ready] Sort the list: sort by date, newest first\n", triage.Task);

        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal((TicketState.Ready, (string?)null), (Ticket(rig).State, Ticket(rig).BlockedBecause));
        Assert.Contains("end the note with \"Silence: <what you would do if he says nothing>\"", ReviewStage.BuildTask("demo", ContextBundle.Parse(BundleText("list-sorting", "unsure")).Bundle!));
    }

    [Fact]
    public void BEH_TICKET_4_a_ready_ticket_goes_to_technical_design_then_to_implementation_in_that_order()
    {
        using var rig = new Rig();
        var design = Triaged(rig);
        Assert.Contains("You are the spec stage for project \"demo\"", design.Task);
        Assert.Equal(TicketState.Ready, Ticket(rig).State);

        rig.FinishWith(design, new { slug = "list-sorting", text = Specced("list-sorting") });

        var implement = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("You are the build stage for project \"demo\"", implement.Task);
        Assert.Equal(TicketState.Planned, Ticket(rig).State);

        rig.FinishWith(implement, new { slug = "list-sorting", text = Built("list-sorting") });

        Assert.Equal(TicketState.Built, Ticket(rig).State);
    }

    [Fact]
    public void BEH_TICKET_4_technical_design_can_block_its_ticket_with_a_question_and_clear_it_when_no_question_is_open()
    {
        using var rig = new Rig();
        rig.FinishWith(Triaged(rig), new { slug = "list-sorting", text = Specced("list-sorting", flag: "two cards may conflict") });
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Escalated(Specced("list-sorting", "two cards may conflict"), Question) });
        Assert.Equal((TicketState.Ready, Question), (Ticket(rig).State, Ticket(rig).BlockedBecause));

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "newest first" }));
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Equal((TicketState.Ready, (string?)null), (Ticket(rig).State, Ticket(rig).BlockedBecause));
    }

    [Fact]
    public void BEH_TICKET_4_implementation_can_block_its_ticket_with_a_question_and_clear_it_when_no_question_is_open()
    {
        using var rig = new Rig();
        var implement = Specced(rig);
        rig.FinishWith(implement, new { slug = "list-sorting", text = Built("list-sorting", flag: "unsure which sort") });
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = Escalated(Built("list-sorting", "unsure which sort"), Question) });
        Assert.Equal((TicketState.Planned, Question), (Ticket(rig).State, Ticket(rig).BlockedBecause));

        rig.Router.Route("POST", "/api/projects/demo/tickets/list-sorting/comments", body: Json(new { body = "newest first" }));
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = BundleText("list-sorting") });

        Assert.Null(Ticket(rig).BlockedBecause);
    }

    // A bundle whose Cards item has no "- " (line 4), so ContextBundle.Parse refuses it.
    private static string MalformedCards(string slug) =>
        $"# Sort the list\nWork-item: {slug}\nCards:\nBEH-A\nNotes:\n- demo.notes.md#x\nFiles:\n- src/List.cs\nContext:\nsort by date\n";

    // A triage stopped on a malformed bundle, then a later triage that rewrote it to the shape with the same Cards and Notes.
    private static void RewriteMalformedBundle(Rig rig)
    {
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = MalformedCards("list-sorting") });
        rig.Note("and sort by name too");
        rig.FinishWith(Assert.IsType<PoolClaim>(rig.Claim()), new { slug = "list-sorting", text = BundleText("list-sorting") });
    }

    [Fact]
    public void BEH_TICKET_5_a_bundle_that_fails_its_shape_check_is_listed_on_the_board_with_the_line_that_is_wrong_and_no_build_starts()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");

        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = MalformedCards("list-sorting") });

        var ticket = Assert.Single(rig.Tickets.Read("demo"), t => t.Slug == "list-sorting");
        Assert.Equal("its bundle is malformed: line 4 is not a \"- item\" in Cards", ticket.BlockedBecause);
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_TICKET_5_a_well_formed_bundle_is_not_listed_as_malformed_and_its_build_run_starts_as_before()
    {
        using var rig = new Rig();
        var spec = Triaged(rig);
        Assert.Null(Ticket(rig).BlockedBecause);

        rig.FinishWith(spec, new { slug = "list-sorting", text = Specced("list-sorting") });

        Assert.Null(Ticket(rig).BlockedBecause);
        Assert.Contains("You are the build stage for project \"demo\"", Assert.IsType<PoolClaim>(rig.Claim()).Task);
    }

    [Fact]
    public void BEH_TICKET_5_triage_rewrites_a_malformed_bundle_so_that_it_parses_keeps_its_cards_and_notes_and_puts_each_item_on_its_own_line()
    {
        using var rig = new Rig();
        rig.Note("the list should sort by date");
        rig.FinishWith(rig.Claim()!, new { slug = "list-sorting", text = MalformedCards("list-sorting") });

        rig.Note("and sort by name too");
        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("## Malformed bundles (rewrite each)", triage.Task);
        Assert.Contains("- list-sorting [new] ", triage.Task);
        Assert.Contains("its bundle is malformed: line 4 is not a \"- item\" in Cards", triage.Task);
        Assert.Contains("Keep its Cards and Notes items unchanged", triage.Task);
        Assert.Contains("every list item on its own \"- \" line", triage.Task);

        rig.FinishWith(triage, new { slug = "list-sorting", text = BundleText("list-sorting") });

        var item = Assert.Single(rig.Pipeline!.Items(), i => i.Slug == "list-sorting");
        Assert.Equal((PipelineStage.Spec, WorkItemStatus.Running), (item.Stage, item.Status));
        var bundle = rig.Pipeline.Bundle("demo", "list-sorting")!;
        Assert.Equal(["BEH-A"], bundle.Cards);
        Assert.Equal(["demo.notes.md#x"], bundle.Notes);
        Assert.Contains("Cards:\n- BEH-A\nNotes:\n- demo.notes.md#x\n", bundle.Render());
    }

    [Fact]
    public void BEH_TICKET_5_after_triage_rewrites_a_malformed_bundle_the_fix_is_recorded_beside_the_ticket()
    {
        using var rig = new Rig();

        RewriteMalformedBundle(rig);

        var fix = Assert.Single(rig.Tickets.Comments("demo", "list-sorting"), c => c.Body.StartsWith("Fixed: triage rewrote the malformed bundle", StringComparison.Ordinal));
        Assert.Equal("kit", fix.Author);
        Assert.Contains("It was: its bundle is malformed: line 4 is not a \"- item\" in Cards", fix.Body);
        Assert.Null(Ticket(rig).BlockedBecause);
    }
}
