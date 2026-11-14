using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-9</c> slice D1: runner pods claim queued runs from Kit and report them over. The
/// routes are driven through <see cref="RunnerPoolRoutes"/> (the host mounts them ahead of the router, as it does
/// the model proxy) and runs are started through the router, as the operator starts them.
/// </summary>
public class RunnerPoolTests
{
    private const string PoolToken = "pool-secret-do-not-print";
    private const string Operator = "operator-pw";

    private sealed class Rig
    {
        public Rig(string? poolToken = PoolToken)
        {
            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(
                corpora,
                viewer,
                new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
                Operator,
                new OriginPolicy(new UrlParser(), null),
                new SessionStore(),
                new SignInThrottle(),
                runStore: Store,
                runLauncher: Pool,
                runnerPool: Pool);
            Routes = new RunnerPoolRoutes(Pool, Store, poolToken, TimeSpan.Zero);
            var signIn = Router.Route("POST", "/api/session", body: Json(new { password = Operator }));
            Assert.Equal(200, signIn.Status);
            Cookie = signIn.SetCookie!.Split(';')[0];
        }

        public MemoryAgentRunStore Store { get; }

        public RunnerPool Pool { get; }

        public KitRouter Router { get; }

        public RunnerPoolRoutes Routes { get; }

        public string Cookie { get; }

        public AgentRunStarted Start(string project, string repo = "someone/repo", string task = "do the thing")
        {
            var r = Router.Route("POST", $"/api/projects/{project}/runs", Cookie, body: Json(new { repo, task }));
            Assert.Equal(201, r.Status);
            return Assert.IsType<AgentRunStarted>(r.Body);
        }

        public KitResponse Claim(string? header = PoolToken) => Routes.ClaimAsync(header).GetAwaiter().GetResult();
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void A_claim_with_no_pool_configured_is_401_no_pool()
    {
        var rig = new Rig(poolToken: null);

        var r = rig.Claim();

        Assert.Equal(401, r.Status);
        Assert.Equal("no-pool", Assert.IsType<ApiError>(r.Body).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData(PoolToken + "x")]
    public void A_claim_with_a_missing_or_wrong_pool_header_is_401_not_a_runner(string? header)
    {
        var rig = new Rig();
        rig.Start("kit");

        var r = rig.Claim(header);

        Assert.Equal(401, r.Status);
        Assert.Equal("not-a-runner", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Equal(AgentRunStatus.Queued, rig.Store.List()[0].Status);
    }

    [Fact]
    public void An_empty_queue_is_204_with_no_body()
    {
        var r = new Rig().Claim();

        Assert.Equal(204, r.Status);
        Assert.Null(r.Body);
    }

    [Fact]
    public void BEH_ORCH_5_A_run_started_by_hand_is_claimed_with_no_turn_limit()
    {
        var rig = new Rig();
        rig.Start("kit");

        var claim = Assert.IsType<PoolClaim>(rig.Claim().Body);

        Assert.Null(claim.MaxTurns);
    }

    [Fact]
    public void BEH_ORCH_12_A_run_started_by_hand_is_claimed_with_no_stage()
    {
        var rig = new Rig();
        rig.Start("kit");

        var claim = Assert.IsType<PoolClaim>(rig.Claim().Body);

        Assert.Null(claim.Stage);
    }

    [Fact]
    public void A_started_run_with_a_repo_and_task_is_claimed_with_the_token_the_start_returned_and_moves_to_running()
    {
        var rig = new Rig();
        var started = rig.Start("kit", "octo/cat", "fix the bug");
        Assert.Equal(RunnerPool.QueuedNote, started.Launch);
        Assert.Equal(AgentRunStatus.Queued, started.Run.Status);

        var r = rig.Claim();

        Assert.Equal(200, r.Status);
        Assert.Equal("no-store", r.CacheControl);
        var claim = Assert.IsType<PoolClaim>(r.Body);
        Assert.Equal(started.Run.Id, claim.Id);
        Assert.Equal(started.Token, claim.Token);
        Assert.Equal(("octo/cat", "fix the bug", KitRouter.DefaultRunModel), (claim.Repo, claim.Task, claim.Model));
        Assert.Equal(AgentRunStatus.Running, rig.Store.List()[0].Status);
        Assert.Equal(204, rig.Claim().Status);
    }

    [Fact]
    public void A_project_has_one_live_run_but_another_project_is_not_held_up_and_finishing_frees_it()
    {
        var rig = new Rig();
        var first = rig.Start("kit");
        var second = rig.Start("kit");
        var other = rig.Start("kit-ui");

        Assert.Equal(first.Run.Id, Assert.IsType<PoolClaim>(rig.Claim().Body).Id);

        // The second kit run waits behind the first; the other project's run goes past it.
        Assert.Equal(other.Run.Id, Assert.IsType<PoolClaim>(rig.Claim().Body).Id);
        Assert.Equal(204, rig.Claim().Status);
        Assert.Equal(AgentRunStatus.Queued, rig.Store.ByTokenHash(AgentTokens.Hash(second.Token))!.Status);

        var done = rig.Routes.Finished(first.Token, Json(new { status = "finished" }));
        Assert.Equal(200, done.Status);

        Assert.Equal(second.Run.Id, Assert.IsType<PoolClaim>(rig.Claim().Body).Id);
    }

    [Fact]
    public void A_run_that_ended_while_queued_is_dropped_not_handed_out()
    {
        var rig = new Rig();
        var gone = rig.Start("kit");
        var next = rig.Start("kit");
        rig.Store.SetStatus(gone.Run.Id, AgentRunStatus.Failed, DateTimeOffset.UtcNow);

        Assert.Equal(next.Run.Id, Assert.IsType<PoolClaim>(rig.Claim().Body).Id);
    }

    [Fact]
    public void A_waiting_claim_is_woken_by_a_run_being_queued()
    {
        var rig = new Rig();
        var routes = new RunnerPoolRoutes(rig.Pool, rig.Store, PoolToken, TimeSpan.FromSeconds(30));

        var waiting = routes.ClaimAsync(PoolToken);
        Assert.False(waiting.IsCompleted);
        var started = rig.Start("kit");

        Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(started.Run.Id, Assert.IsType<PoolClaim>(waiting.Result.Body).Id);
    }

    [Fact]
    public void Finished_needs_a_known_run_token_and_a_run_that_is_not_already_over()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        Assert.Equal(401, rig.Routes.Finished("kitrun_nope", Json(new { status = "finished" })).Status);
        Assert.Equal(401, rig.Routes.Finished(null, Json(new { status = "finished" })).Status);
        Assert.Equal(400, rig.Routes.Finished(started.Token, Json(new { status = "running" })).Status);
        Assert.Equal(400, rig.Routes.Finished(started.Token, null).Status);

        var ok = rig.Routes.Finished(started.Token, Json(new { status = "failed" }));
        Assert.Equal(200, ok.Status);
        var run = Assert.IsAssignableFrom<Balenthiran.Kit.Abstractions.DataModels.IAgentRun>(ok.Body);
        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.NotNull(run.Ended);

        Assert.Equal(409, rig.Routes.Finished(started.Token, Json(new { status = "finished" })).Status);
    }

    private static object Usage(long input = 1_000_000, long output = 100_000, long write = 200_000, long read = 3_000_000) => new
    {
        input_tokens = input,
        output_tokens = output,
        cache_creation_input_tokens = write,
        cache_read_input_tokens = read,
    };

    [Fact]
    public void A_finished_report_with_usage_records_the_counts_and_prices_them_for_the_runs_model()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        var r = rig.Routes.Finished(started.Token, Json(new { status = "finished", usage = Usage() }));

        Assert.Equal(200, r.Status);
        var run = rig.Store.ByTokenHash(AgentTokens.Hash(started.Token))!;
        Assert.Equal((1_000_000L, 100_000L, 200_000L, 3_000_000L), (run.InputTokens, run.OutputTokens, run.CacheWriteTokens, run.CacheReadTokens));
        Assert.Equal(AgentPrices.Cost(run.Model, 1_000_000, 100_000, 200_000, 0, 3_000_000), run.CostUsd);
        Assert.True(run.CostUsd > 0);
        Assert.Equal(AgentRunStatus.Finished, run.Status);
    }

    [Fact]
    public void A_failed_run_still_has_its_usage_recorded()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status = "failed", usage = Usage(output: 7) })).Status);

        Assert.Equal(7, rig.Store.ByTokenHash(AgentTokens.Hash(started.Token))!.OutputTokens);
    }

    [Fact]
    public void Usage_the_proxy_already_counted_is_not_counted_again()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();
        rig.Store.AddUsage(started.Run.Id, new AgentUsage(10, 20, 0, 0), 0.5m);

        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status = "finished", usage = Usage() })).Status);

        var run = rig.Store.ByTokenHash(AgentTokens.Hash(started.Token))!;
        Assert.Equal((10L, 20L, 0.5m), (run.InputTokens, run.OutputTokens, run.CostUsd));
    }

    [Fact]
    public void A_report_with_no_usage_or_null_usage_counts_nothing()
    {
        var rig = new Rig();
        var a = rig.Start("kit");
        var b = rig.Start("kit-ui");
        rig.Claim();
        rig.Claim();

        Assert.Equal(200, rig.Routes.Finished(a.Token, Json(new { status = "finished" })).Status);
        Assert.Equal(200, rig.Routes.Finished(b.Token, Json(new { status = "finished", usage = (object?)null })).Status);

        Assert.All(rig.Store.List(), run => Assert.Equal(0m, run.CostUsd));
    }

    [Theory]
    [InlineData("""{"status":"finished","usage":"lots"}""")]
    [InlineData("""{"status":"finished","usage":{"input_tokens":1,"output_tokens":2,"cache_creation_input_tokens":3}}""")]
    [InlineData("""{"status":"finished","usage":{"input_tokens":-1,"output_tokens":2,"cache_creation_input_tokens":3,"cache_read_input_tokens":4}}""")]
    [InlineData("""{"status":"finished","usage":{"input_tokens":1.5,"output_tokens":2,"cache_creation_input_tokens":3,"cache_read_input_tokens":4}}""")]
    [InlineData("""{"status":"finished","usage":{"input_tokens":"1","output_tokens":2,"cache_creation_input_tokens":3,"cache_read_input_tokens":4}}""")]
    public void Bad_usage_is_400_and_the_run_is_left_running(string body)
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        var r = rig.Routes.Finished(started.Token, JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal("bad-usage", Assert.IsType<ApiError>(r.Body).Error);
        var run = rig.Store.ByTokenHash(AgentTokens.Hash(started.Token))!;
        Assert.Equal((AgentRunStatus.Running, 0m), (run.Status, run.CostUsd));
    }

    [Fact]
    public void A_failed_report_with_a_failure_stores_it_and_it_is_listed()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status = "failed", failure = "clone exit 128" })).Status);

        Assert.Equal("clone exit 128", rig.Store.List().Single().Failure);
    }

    [Fact]
    public void A_report_with_no_failure_leaves_it_null()
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status = "failed" })).Status);

        Assert.Null(rig.Store.List().Single().Failure);
    }

    [Theory]
    [InlineData("""{"status":"finished","failure":"clone exit 128"}""")]
    [InlineData("""{"status":"failed","failure":"clone exit"}""")]
    [InlineData("""{"status":"failed","failure":"Clone exit 1"}""")]
    [InlineData("""{"status":"failed","failure":"clone exit 1234"}""")]
    [InlineData("""{"status":"failed","failure":"fatal: https://x:tok@github.com exit 1"}""")]
    [InlineData("""{"status":"failed","failure":"clone exit 1\nmore"}""")]
    [InlineData("""{"status":"failed","failure":7}""")]
    public void A_bad_failure_is_400_and_the_run_is_left_running(string body)
    {
        var rig = new Rig();
        var started = rig.Start("kit");
        rig.Claim();

        var r = rig.Routes.Finished(started.Token, JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal("bad-failure", Assert.IsType<ApiError>(r.Body).Error);
        var run = rig.Store.ByTokenHash(AgentTokens.Hash(started.Token))!;
        Assert.Equal((AgentRunStatus.Running, (string?)null), (run.Status, run.Failure));
    }

    [Theory]
    [InlineData("""{"repo":"no-slash","task":"t"}""")]
    [InlineData("""{"repo":"a/b/c","task":"t"}""")]
    [InlineData("""{"repo":"a b/c","task":"t"}""")]
    [InlineData("""{"repo":7,"task":"t"}""")]
    [InlineData("""{"repo":"a/b","task":""}""")]
    [InlineData("""{"repo":"a/b","task":7}""")]
    public void A_malformed_repo_or_task_is_400_and_records_nothing(string body)
    {
        var rig = new Rig();

        var r = rig.Router.Route("POST", "/api/projects/kit/runs", rig.Cookie, body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Empty(rig.Store.List());
    }

    [Fact]
    public void A_task_over_8000_characters_is_400()
    {
        var rig = new Rig();

        var r = rig.Router.Route("POST", "/api/projects/kit/runs", rig.Cookie, body: Json(new { repo = "a/b", task = new string('x', 8001) }));

        Assert.Equal(400, r.Status);
        Assert.Empty(rig.Store.List());
    }

    [Fact]
    public void A_run_started_without_a_repo_and_task_is_not_queued_for_the_pool()
    {
        var rig = new Rig();

        var r = Assert.IsType<AgentRunStarted>(rig.Router.Route("POST", "/api/projects/kit/runs", rig.Cookie).Body);

        Assert.NotEqual(RunnerPool.QueuedNote, r.Launch);
        Assert.Equal(204, rig.Claim().Status);
    }

    [Fact]
    public void Neither_the_listing_nor_the_store_ever_holds_the_run_token_or_task()
    {
        var rig = new Rig();
        var started = rig.Start("kit", task: "a task nobody may list");
        rig.Claim();

        var listed = JsonSerializer.Serialize(rig.Router.Route("GET", "/api/runs", rig.Cookie).Body, typeof(AgentRunList), new JsonSerializerOptions());
        var stored = JsonSerializer.Serialize(rig.Store.List(), new JsonSerializerOptions());
        foreach (var text in new[] { listed, stored })
        {
            Assert.DoesNotContain(started.Token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("a task nobody may list", text, StringComparison.Ordinal);
            Assert.DoesNotContain(PoolToken, text, StringComparison.Ordinal);
        }
    }
}
