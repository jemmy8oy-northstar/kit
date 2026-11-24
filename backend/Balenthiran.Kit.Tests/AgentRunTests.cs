using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-9</c> slice A: a user's agent runs are recorded, started by the operator, and reach the
/// model only through Kit's metered proxy. Anthropic is a scripted handler, so nothing touches the network and
/// each test can assert on exactly what Kit sent upstream.
/// </summary>
public class AgentRunTests
{
    private const string Key = "sk-ant-kit-own-key-do-not-print";
    private const string Sonnet = "claude-sonnet-5-5";
    private const string Operator = "operator-pw";
    private const string Password = "a-long-enough-password";
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    // ---- tokens ----------------------------------------------------------------------------------

    [Fact]
    public void A_run_token_has_the_kit_prefix_is_random_and_hashes_to_lower_case_hex()
    {
        var a = AgentTokens.Mint();
        var b = AgentTokens.Mint();

        Assert.StartsWith(AgentTokens.Prefix, a, StringComparison.Ordinal);
        Assert.NotEqual(a, b);
        Assert.Matches("^kitrun_[A-Za-z0-9_-]{43}$", a);

        var hash = AgentTokens.Hash(a);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, AgentTokens.Hash(a));
        Assert.NotEqual(hash, AgentTokens.Hash(b));
        Assert.DoesNotContain(a, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_token_is_never_stored_only_its_hash_and_is_never_listed()
    {
        var (router, store, _, cookie) = OperatorRouter();

        var started = Assert.IsType<AgentRunStarted>(router.Route("POST", "/api/projects/kit/runs", cookie).Body);

        Assert.Null(store.ByTokenHash(started.Token));
        Assert.Equal(started.Run.Id, store.ByTokenHash(AgentTokens.Hash(started.Token))!.Id);
        var listed = JsonSerializer.Serialize(router.Route("GET", "/api/runs", cookie).Body, typeof(AgentRunList), new JsonSerializerOptions());
        Assert.DoesNotContain(started.Token, listed, StringComparison.Ordinal);
        Assert.DoesNotContain(AgentTokens.Hash(started.Token), listed, StringComparison.Ordinal);
        Assert.DoesNotContain(started.Token, JsonSerializer.Serialize(store.List(), new JsonSerializerOptions()), StringComparison.Ordinal);
    }

    // ---- prices ----------------------------------------------------------------------------------

    [Fact]
    public void Prices_follow_the_card_and_the_cache_multipliers()
    {
        Assert.Equal((4m, 20m), AgentPrices.Of("claude-opus-5-5"));
        Assert.Equal((2m, 10m), AgentPrices.Of(Sonnet));
        Assert.Equal((1m, 5m), AgentPrices.Of("claude-haiku-4-5"));
        Assert.Equal((2m, 10m), AgentPrices.Of("CLAUDE-SONNET-5-5"));

        const long Million = 1_000_000;
        Assert.Equal(4m, AgentPrices.Cost("claude-opus-5-5", Million, 0, 0, 0, 0));
        Assert.Equal(20m, AgentPrices.Cost("claude-opus-5-5", 0, Million, 0, 0, 0));
        Assert.Equal(5m, AgentPrices.Cost("claude-opus-5-5", 0, 0, Million, 0, 0));
        Assert.Equal(8m, AgentPrices.Cost("claude-opus-5-5", 0, 0, 0, Million, 0));
        Assert.Equal(0.4m, AgentPrices.Cost("claude-opus-5-5", 0, 0, 0, 0, Million));
        Assert.Equal(0.2m, AgentPrices.Cost(Sonnet, 0, 0, 0, 0, Million));
        Assert.Equal(0m, AgentPrices.Cost(Sonnet, 0, 0, 0, 0, 0));
    }

    [Theory]
    [InlineData("mystery-9")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_model_is_priced_at_the_dearest_input_and_output_in_the_list_so_a_name_cannot_dodge_the_cap(string? model)
    {
        Assert.Equal((10m, 50m), AgentPrices.Of("claude-fable-5-1"));
        Assert.Equal(AgentPrices.Of("claude-fable-5-1"), AgentPrices.Of(model));
        Assert.Equal(60m, AgentPrices.Cost(model, 1_000_000, 1_000_000, 0, 0, 0));
    }

    // ---- reading usage ---------------------------------------------------------------------------

    [Fact]
    public void Usage_is_read_from_a_non_stream_answer_and_a_missing_field_stays_zero()
    {
        var tally = new UsageTally();
        tally.Apply(JsonDocument.Parse("""{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":30,"cache_creation_input_tokens":40}""").RootElement);

        Assert.True(tally.Any);
        Assert.Equal((10, 20, 30), (tally.Input, tally.Output, tally.Read));
        Assert.Equal((0, 40), (tally.Write5m, tally.Write1h));

        var bare = new UsageTally();
        bare.Apply(JsonDocument.Parse("""{"output_tokens":7,"input_tokens":-3,"cache_read_input_tokens":"many"}""").RootElement);
        Assert.Equal((0, 7, 0), (bare.Input, bare.Output, bare.Read));

        var none = new UsageTally();
        none.Apply(JsonDocument.Parse("[1]").RootElement);
        Assert.False(none.Any);
    }

    [Fact]
    public void Cache_writes_are_taken_by_lifetime_when_the_answer_splits_them()
    {
        var tally = new UsageTally();
        tally.Apply(JsonDocument.Parse("""{"cache_creation_input_tokens":50,"cache_creation":{"ephemeral_5m_input_tokens":40,"ephemeral_1h_input_tokens":10}}""").RootElement);

        Assert.Equal((40, 10), (tally.Write5m, tally.Write1h));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(64)]
    [InlineData(100000)]
    public void Usage_is_read_from_a_stream_split_at_any_chunk_boundary(int chunk)
    {
        foreach (var text in new[] { Sse, Sse.Replace("\n", "\r\n", StringComparison.Ordinal) })
        {
            var tally = new UsageTally();
            var reader = new SseUsageReader(tally);
            foreach (var piece in Split(Encoding.UTF8.GetBytes(text), chunk))
            {
                reader.Feed(piece);
            }

            Assert.True(tally.Any);
            Assert.Equal((100, 200, 40, 10, 1000), (tally.Input, tally.Output, tally.Write5m, tally.Write1h, tally.Read));
        }
    }

    [Fact]
    public void A_stream_line_over_a_megabyte_is_skipped_and_the_next_event_is_still_read()
    {
        var tally = new UsageTally();
        var reader = new SseUsageReader(tally);

        reader.Feed(Encoding.UTF8.GetBytes("data: " + new string('x', 2 * 1024 * 1024) + "\n"));
        reader.Feed(Encoding.UTF8.GetBytes("data: {\"type\":\"message_delta\",\"usage\":{\"output_tokens\":9}}\n"));
        reader.Feed(Encoding.UTF8.GetBytes("data: {\"type\":\"message_delta\",\"usage\":{\"output_tok"));

        Assert.Equal(9, tally.Output);
    }

    [Fact]
    public void A_stream_line_that_is_not_json_is_skipped_without_breaking_the_relay()
    {
        var tally = new UsageTally();
        var reader = new SseUsageReader(tally);

        reader.Feed(Encoding.UTF8.GetBytes("data: {\"type\":\"message_delta\" broken\n: comment\nevent: message_delta\n"));
        Assert.False(tally.Any);

        reader.Feed(Encoding.UTF8.GetBytes("data:{\"type\":\"message_delta\",\"usage\":{\"output_tokens\":4}}\n"));
        Assert.Equal(4, tally.Output);
    }

    // ---- the proxy refuses what must not spend ---------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kitrun_not-a-token-kit-minted")]
    [InlineData(Key)]
    public async Task An_unknown_or_missing_token_is_401_and_reaches_no_model(string? token)
    {
        var rig = new Rig();

        var r = await Call(rig.Proxy, token, Msg());

        Assert.Equal(401, r.Status);
        Assert.Equal("authentication_error", ErrorType(r));
        Assert.Empty(rig.Model.Sent);
    }

    [Theory]
    [InlineData(AgentRunStatus.Finished)]
    [InlineData(AgentRunStatus.Failed)]
    [InlineData(AgentRunStatus.Refused)]
    public async Task A_run_that_is_over_is_403_and_reaches_no_model(string status)
    {
        var rig = new Rig();
        rig.Store.SetStatus(rig.Run.Id, status, Noon);

        var r = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(403, r.Status);
        Assert.Equal("permission_error", ErrorType(r));
        Assert.Contains(status, ErrorMessage(r), StringComparison.Ordinal);
        Assert.Empty(rig.Model.Sent);
    }

    [Fact]
    public async Task A_run_older_than_the_limit_is_403_and_one_inside_it_is_served()
    {
        var rig = new Rig();

        rig.Clock.Now = Noon + AgentProxy.DefaultMaxAge;
        Assert.Equal(200, (await Call(rig.Proxy, rig.Token, Msg())).Status);

        rig.Clock.Now = Noon + AgentProxy.DefaultMaxAge + TimeSpan.FromSeconds(1);
        var old = await Call(rig.Proxy, rig.Token, Msg());
        Assert.Equal(403, old.Status);
        Assert.Equal("permission_error", ErrorType(old));
        Assert.Contains("hours old", ErrorMessage(old), StringComparison.Ordinal);
        Assert.Equal(1, rig.Model.Sent.Count);

        // Expiry is for every call, count_tokens included, and whatever the run has spent.
        Assert.Equal(403, (await Call(rig.Proxy, rig.Token, Msg(), countTokens: true)).Status);
    }

    [Fact]
    public async Task The_run_limit_is_a_setting_and_a_shorter_one_expires_a_run_sooner()
    {
        var rig = new Rig(maxAge: TimeSpan.FromMinutes(30));

        rig.Clock.Now = Noon.AddMinutes(30);
        Assert.Equal(200, (await Call(rig.Proxy, rig.Token, Msg())).Status);
        rig.Clock.Now = Noon.AddMinutes(31);
        Assert.Equal(403, (await Call(rig.Proxy, rig.Token, Msg())).Status);

        Assert.Equal(AgentProxy.DefaultMaxAge, KitSettings.ParseAgentRunMaxAge(null));
        Assert.Equal(AgentProxy.DefaultMaxAge, KitSettings.ParseAgentRunMaxAge(" "));
        Assert.Equal(TimeSpan.FromMinutes(30), KitSettings.ParseAgentRunMaxAge("0.5"));
        Assert.Equal(TimeSpan.FromHours(6), KitSettings.ParseAgentRunMaxAge("6"));
        foreach (var bad in new[] { "0", "-1", "soon", "1e3", "100000" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseAgentRunMaxAge(bad));
        }
    }

    [Fact]
    public async Task Without_Kits_key_the_proxy_is_503_and_reaches_no_model()
    {
        var rig = new Rig(key: null);

        var r = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(503, r.Status);
        Assert.Contains("KIT_ANTHROPIC_API_KEY", ErrorMessage(r), StringComparison.Ordinal);
        Assert.Empty(rig.Model.Sent);
    }

    [Fact]
    public async Task A_run_at_its_cap_is_402_and_one_just_under_it_is_served()
    {
        var rig = new Rig(cap: 1m);

        rig.Store.AddUsage(rig.Run.Id, new AgentUsage(0, 0, 0, 0), 0.999999m);
        Assert.Equal(200, (await Call(rig.Proxy, rig.Token, Msg())).Status);

        rig.Store.AddUsage(rig.Run.Id, new AgentUsage(0, 0, 0, 0), 0.000001m);
        var r = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(402, r.Status);
        Assert.Equal("billing_error", ErrorType(r));
        Assert.Equal(1, rig.Model.Sent.Count);
    }

    [Fact]
    public async Task A_cap_of_zero_stops_every_runs_first_call_and_leaves_it_queued()
    {
        var rig = new Rig(cap: 0m);

        Assert.Equal(402, (await Call(rig.Proxy, rig.Token, Msg())).Status);

        Assert.Empty(rig.Model.Sent);
        Assert.Equal(AgentRunStatus.Queued, rig.Store.ByTokenHash(AgentTokens.Hash(rig.Token))!.Status);
    }

    [Fact]
    public async Task A_body_that_ran_past_the_limit_is_413_and_reaches_no_model()
    {
        var rig = new Rig();

        var r = await Call(rig.Proxy, rig.Token, null);

        Assert.Equal(413, r.Status);
        Assert.Equal("request_too_large", ErrorType(r));
        Assert.Empty(rig.Model.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("{\"model\":")]
    public async Task A_body_that_is_not_a_json_object_is_400_and_reaches_no_model(string body)
    {
        var rig = new Rig();

        var r = await Call(rig.Proxy, rig.Token, Encoding.UTF8.GetBytes(body));

        Assert.Equal(400, r.Status);
        Assert.Equal("invalid_request_error", ErrorType(r));
        Assert.Empty(rig.Model.Sent);
        Assert.Equal(AgentRunStatus.Queued, rig.Store.ByTokenHash(AgentTokens.Hash(rig.Token))!.Status);
    }

    [Fact]
    public async Task The_refusal_check_that_runs_before_the_body_agrees_with_the_proxy()
    {
        var rig = new Rig();
        var refused = new List<int>();
        Stream Begin(int status, string _)
        {
            refused.Add(status);
            return new MemoryStream();
        }

        Assert.True(await rig.Proxy.RefuseEarlyAsync("kitrun_nope", false, Begin));
        Assert.True(await rig.Proxy.RefuseEarlyAsync(null, false, Begin));
        Assert.False(await rig.Proxy.RefuseEarlyAsync(rig.Token, false, Begin));
        rig.Clock.Now = Noon.AddDays(1);
        Assert.True(await rig.Proxy.RefuseEarlyAsync(rig.Token, true, Begin));

        Assert.Equal([401, 401, 403], refused);
    }

    // ---- what is sent upstream -------------------------------------------------------------------

    [Fact]
    public async Task The_proxy_sends_Kits_key_and_never_the_run_token_upstream()
    {
        var rig = new Rig();

        var r = await Call(rig.Proxy, rig.Token, Msg(), version: "2024-01-01", beta: "tools-2024,x-flag");

        Assert.Equal(200, r.Status);
        var sent = Assert.Single(rig.Model.Sent);
        Assert.Equal("https://api.anthropic.com/v1/messages", sent.Uri.AbsoluteUri);
        Assert.Equal(Key, sent.Headers["x-api-key"]);
        Assert.Equal("2024-01-01", sent.Headers["anthropic-version"]);
        Assert.Equal("tools-2024,x-flag", sent.Headers["anthropic-beta"]);
        Assert.DoesNotContain(rig.Token, string.Join("\n", sent.Headers.Select(h => h.Key + ": " + h.Value)), StringComparison.Ordinal);
        Assert.False(sent.Headers.ContainsKey("authorization"));
        Assert.DoesNotContain(Key, Encoding.UTF8.GetString(r.Body), StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetString(Msg()), sent.Body);
    }

    [Fact]
    public async Task The_version_defaults_and_a_beta_flag_is_sent_only_when_the_caller_gave_one()
    {
        var rig = new Rig();

        await Call(rig.Proxy, rig.Token, Msg());

        var sent = Assert.Single(rig.Model.Sent);
        Assert.Equal("2023-06-01", sent.Headers["anthropic-version"]);
        Assert.False(sent.Headers.ContainsKey("anthropic-beta"));
    }

    [Fact]
    public async Task The_first_call_moves_a_queued_run_to_running_and_a_refused_one_does_not()
    {
        var rig = new Rig();
        Assert.Equal(AgentRunStatus.Queued, rig.Run.Status);

        await Call(rig.Proxy, rig.Token, Encoding.UTF8.GetBytes("not json"));
        Assert.Equal(AgentRunStatus.Queued, rig.Current.Status);

        await Call(rig.Proxy, rig.Token, Msg());
        Assert.Equal(AgentRunStatus.Running, rig.Current.Status);
    }

    // ---- recording what a call used --------------------------------------------------------------

    [Fact]
    public async Task A_non_stream_answer_is_relayed_and_its_usage_and_cost_are_added_to_the_run()
    {
        var answer = """{"id":"m","content":[],"usage":{"input_tokens":1000,"output_tokens":500,"cache_read_input_tokens":2000,"cache_creation_input_tokens":100}}""";
        var rig = new Rig(_ => Ok(answer));

        var r = await Call(rig.Proxy, rig.Token, Msg());
        await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(200, r.Status);
        Assert.Equal("application/json", r.ContentType.Split(';')[0]);
        Assert.Equal(answer, Encoding.UTF8.GetString(r.Body));
        // Sonnet: 1000 in x2 + 500 out x10 + 100 one-hour writes x4 + 2000 reads x0.2, per million, twice.
        Assert.Equal(2000, rig.Current.InputTokens);
        Assert.Equal(1000, rig.Current.OutputTokens);
        Assert.Equal(200, rig.Current.CacheWriteTokens);
        Assert.Equal(4000, rig.Current.CacheReadTokens);
        Assert.Equal(0.0156m, rig.Current.CostUsd);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(100000)]
    public async Task A_streamed_answer_is_relayed_unchanged_and_its_usage_and_cost_are_recorded(int chunk)
    {
        var bytes = Encoding.UTF8.GetBytes(Sse);
        var rig = new Rig(_ => Stream(bytes, chunk));

        var r = await Call(rig.Proxy, rig.Token, Msg(stream: true));

        Assert.Equal(200, r.Status);
        Assert.Equal("text/event-stream", r.ContentType.Split(';')[0]);
        Assert.Equal(bytes, r.Body);
        Assert.Equal(100, rig.Current.InputTokens);
        Assert.Equal(200, rig.Current.OutputTokens);
        Assert.Equal(50, rig.Current.CacheWriteTokens);
        Assert.Equal(1000, rig.Current.CacheReadTokens);
        Assert.Equal(0.00254m, rig.Current.CostUsd);
        Assert.Equal(AgentRunStatus.Running, rig.Current.Status);
    }

    [Fact]
    public async Task A_stream_that_breaks_part_way_is_still_charged_for_what_was_seen()
    {
        var bytes = Encoding.UTF8.GetBytes(Sse);
        var cut = Sse.IndexOf("event: content_block_delta", StringComparison.Ordinal);
        var rig = new Rig(_ => Stream(bytes[..cut], 20, breakAfter: true));

        await Call(rig.Proxy, rig.Token, Msg(stream: true));

        Assert.Equal(100, rig.Current.InputTokens);
        Assert.Equal(1, rig.Current.OutputTokens);
        Assert.Equal(1000, rig.Current.CacheReadTokens);
    }

    [Fact]
    public async Task The_dearest_price_is_charged_for_a_model_the_list_does_not_name()
    {
        var answer = """{"usage":{"input_tokens":1000000,"output_tokens":0}}""";
        var rig = new Rig(_ => Ok(answer));

        await Call(rig.Proxy, rig.Token, Msg("mystery-9"));

        Assert.Equal(10m, rig.Current.CostUsd);
    }

    [Fact]
    public async Task A_caller_who_disconnects_before_a_non_stream_answer_is_read_is_still_charged()
    {
        var rig = new Rig(_ => Ok("""{"usage":{"input_tokens":1000000,"output_tokens":0}}"""));
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();

        await Call(rig.Proxy, rig.Token, Msg(), cancellationToken: gone.Token);

        Assert.Equal(1000000, rig.Current.InputTokens);
        Assert.Equal(2m, rig.Current.CostUsd);
    }

    [Fact]
    public async Task A_caller_who_disconnects_before_any_stream_usage_is_seen_is_still_charged_for_the_whole_stream()
    {
        var rig = new Rig(_ => Stream(Encoding.UTF8.GetBytes(Sse), 64));
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();

        var r = await Call(rig.Proxy, rig.Token, Msg(stream: true), cancellationToken: gone.Token);

        Assert.Empty(r.Body);
        Assert.Equal(100, rig.Current.InputTokens);
        Assert.Equal(200, rig.Current.OutputTokens);
        Assert.Equal(1000, rig.Current.CacheReadTokens);
        Assert.Equal(0.00254m, rig.Current.CostUsd);
    }

    [Fact]
    public async Task A_fifth_call_in_flight_is_429_rate_limit_error_reaches_no_model_and_a_slot_frees_when_one_ends()
    {
        var started = 0;
        using var release = new ManualResetEventSlim();
        var rig = new Rig(_ =>
        {
            Interlocked.Increment(ref started);
            release.Wait(TimeSpan.FromSeconds(30));
            return Ok("""{"usage":{"input_tokens":1,"output_tokens":1}}""");
        });

        var held = Enumerable.Range(0, AgentProxy.MaxInFlight).Select(_ => Task.Run(() => Call(rig.Proxy, rig.Token, Msg()))).ToList();
        for (var waited = 0; Volatile.Read(ref started) < AgentProxy.MaxInFlight && waited < 3000; waited++)
        {
            await Task.Delay(10);
        }

        var fifth = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(AgentProxy.MaxInFlight, started);
        Assert.Equal(429, fifth.Status);
        Assert.Equal("rate_limit_error", ErrorType(fifth));
        release.Set();
        Assert.All(await Task.WhenAll(held), r => Assert.Equal(200, r.Status));
        Assert.Equal(200, (await Call(rig.Proxy, rig.Token, Msg())).Status);
    }

    [Fact]
    public async Task A_call_that_fails_upstream_frees_its_slot_so_failures_never_lock_a_run_out()
    {
        var rig = new Rig(_ => throw new HttpRequestException("down"));

        for (var i = 0; i < AgentProxy.MaxInFlight + 2; i++)
        {
            Assert.Equal(502, (await Call(rig.Proxy, rig.Token, Msg())).Status);
        }
    }

    [Fact]
    public async Task A_store_that_fails_to_record_spend_does_not_break_the_answer()
    {
        var inner = new MemoryAgentRunStore();
        var token = AgentTokens.Mint();
        inner.Create("james", "kit", Sonnet, AgentTokens.Hash(token), Noon);
        var proxy = new AgentProxy(new FailingStore(inner), new HttpClient(new ScriptedModel(_ => Ok("""{"usage":{"input_tokens":1,"output_tokens":1}}"""))), Key, clock: new Clock(Noon));

        var r = await Call(proxy, token, Msg());

        Assert.Equal(200, r.Status);
    }

    [Fact]
    public void A_message_delta_with_only_cache_totals_never_lowers_what_message_start_recorded()
    {
        var tally = new UsageTally();
        tally.Apply(JsonDocument.Parse("""{"input_tokens":100,"cache_read_input_tokens":1000,"cache_creation_input_tokens":50,"cache_creation":{"ephemeral_5m_input_tokens":40,"ephemeral_1h_input_tokens":10}}""").RootElement);

        tally.Apply(JsonDocument.Parse("""{"input_tokens":0,"output_tokens":200,"cache_read_input_tokens":0,"cache_creation_input_tokens":50}""").RootElement);

        Assert.Equal((100, 200, 1000), (tally.Input, tally.Output, tally.Read));
        Assert.Equal((40, 10), (tally.Write5m, tally.Write1h));

        // A total that grew past the split adds only the difference, as the dearer kind.
        tally.Apply(JsonDocument.Parse("""{"cache_creation_input_tokens":70}""").RootElement);
        Assert.Equal((40, 30), (tally.Write5m, tally.Write1h));
    }

    [Fact]
    public void The_conditional_move_to_running_happens_only_from_queued_and_never_revives_a_finished_run()
    {
        var store = new MemoryAgentRunStore();
        var run = store.Create("james", "kit", Sonnet, AgentTokens.Hash("a"), Noon);

        Assert.True(store.StartIfQueued(run.Id));
        Assert.Equal(AgentRunStatus.Running, store.ByTokenHash(AgentTokens.Hash("a"))!.Status);
        Assert.False(store.StartIfQueued(run.Id));

        var over = store.Create("james", "kit", Sonnet, AgentTokens.Hash("b"), Noon);
        store.SetStatus(over.Id, AgentRunStatus.Failed, Noon);
        Assert.False(store.StartIfQueued(over.Id));
        Assert.Equal(AgentRunStatus.Failed, store.ByTokenHash(AgentTokens.Hash("b"))!.Status);
        Assert.False(store.StartIfQueued("no-such-run"));
    }

    [Fact]
    public void The_model_client_never_follows_a_redirect_so_Kits_key_cannot_be_carried_to_another_host()
    {
        Assert.False(ServiceRegistration.AgentHandler().AllowAutoRedirect);
    }

    [Fact]
    public async Task An_upstream_error_is_relayed_as_it_came_and_records_nothing()
    {
        var rig = new Rig(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"type":"error","error":{"type":"rate_limit_error","message":"slow"},"usage":{"input_tokens":999}}""", Encoding.UTF8, "application/json"),
        });

        var r = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(429, r.Status);
        Assert.Contains("rate_limit_error", Encoding.UTF8.GetString(r.Body), StringComparison.Ordinal);
        Assert.Equal(0, rig.Current.InputTokens);
        Assert.Equal(0m, rig.Current.CostUsd);
    }

    [Fact]
    public async Task A_model_that_cannot_be_reached_is_502_and_the_message_does_not_carry_the_key_or_token()
    {
        var rig = new Rig(_ => throw new HttpRequestException($"connect failed for {Key}"));

        var r = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(502, r.Status);
        var text = Encoding.UTF8.GetString(r.Body);
        Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Token, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counting_tokens_does_not_spend_is_not_capped_and_does_not_start_the_run()
    {
        var answer = """{"input_tokens":4242,"usage":{"input_tokens":4242,"output_tokens":9}}""";
        var rig = new Rig(_ => Ok(answer), cap: 1m);
        rig.Store.AddUsage(rig.Run.Id, new AgentUsage(0, 0, 0, 0), 2m);

        var counted = await Call(rig.Proxy, rig.Token, Msg(), countTokens: true);
        var spent = await Call(rig.Proxy, rig.Token, Msg());

        Assert.Equal(200, counted.Status);
        Assert.Equal(answer, Encoding.UTF8.GetString(counted.Body));
        Assert.Equal("https://api.anthropic.com/v1/messages/count_tokens", Assert.Single(rig.Model.Sent).Uri.AbsoluteUri);
        Assert.Equal(402, spent.Status);
        Assert.Equal(2m, rig.Current.CostUsd);
        Assert.Equal(0, rig.Current.InputTokens);
        Assert.Equal(AgentRunStatus.Queued, rig.Current.Status);
    }

    [Fact]
    public async Task Counting_tokens_still_needs_a_known_token_and_Kits_key()
    {
        Assert.Equal(401, (await Call(new Rig().Proxy, "kitrun_nope", Msg(), countTokens: true)).Status);

        var keyless = new Rig(key: null);
        Assert.Equal(503, (await Call(keyless.Proxy, keyless.Token, Msg(), countTokens: true)).Status);
    }

    // ---- the operator's routes -------------------------------------------------------------------

    [Fact]
    public void The_operator_starts_a_run_and_is_given_its_token_once_and_the_launcher_is_handed_the_same_one()
    {
        var launcher = new RecordingLauncher();
        var (router, store, _, cookie) = OperatorRouter(launcher);

        var r = router.Route("POST", "/api/projects/kit/runs", cookie, body: Json(new { model = "claude-opus-5-5" }));

        Assert.Equal(201, r.Status);
        var started = Assert.IsType<AgentRunStarted>(r.Body);
        Assert.StartsWith(AgentTokens.Prefix, started.Token, StringComparison.Ordinal);
        Assert.Equal("kit", started.Run.Project);
        Assert.Equal("claude-opus-5-5", started.Run.Model);
        Assert.Equal(AgentRunStatus.Queued, started.Run.Status);
        Assert.Equal(Noon, started.Run.Started);
        Assert.Equal(NoRunnerLauncher.Note, started.Launch);
        Assert.Equal(started.Token, launcher.Token);
        Assert.Equal(started.Run.Id, launcher.Run!.Id);
        Assert.Single(store.List());

        // A body, or none, and the default is Sonnet.
        var plain = Assert.IsType<AgentRunStarted>(router.Route("POST", "/api/projects/kit/runs", cookie).Body);
        Assert.Equal(KitRouter.DefaultRunModel, plain.Run.Model);
        Assert.Equal(Sonnet, plain.Run.Model);
        Assert.NotEqual(started.Token, plain.Token);

        // The token is in the 201 once: the list never has it.
        Assert.DoesNotContain(started.Token, JsonSerializer.Serialize(router.Route("GET", "/api/runs", cookie).Body, typeof(AgentRunList)), StringComparison.Ordinal);
    }

    [Fact]
    public void Starting_a_run_on_an_unknown_project_is_404_and_records_nothing()
    {
        var (router, store, _, cookie) = OperatorRouter();

        var r = router.Route("POST", "/api/projects/no-such-app/runs", cookie);

        Assert.Equal(404, r.Status);
        Assert.Equal("no-such-project", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(store.List());
    }

    [Theory]
    [InlineData("""{"model":7}""")]
    [InlineData("""{"model":""}""")]
    [InlineData("""{"model":"has space"}""")]
    [InlineData("""{"model":"-leading-dash"}""")]
    [InlineData("""{"model":"../../etc"}""")]
    [InlineData("[1]")]
    [InlineData("\"opus\"")]
    public void A_model_that_is_not_a_model_id_or_a_body_that_is_not_an_object_is_400_and_records_nothing(string body)
    {
        var (router, store, _, cookie) = OperatorRouter();

        var r = router.Route("POST", "/api/projects/kit/runs", cookie, body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Empty(store.List());
    }

    [Fact]
    public async Task A_launcher_that_throws_fails_the_run_and_its_token_then_reaches_nothing()
    {
        var (router, store, _, cookie) = OperatorRouter(new ThrowingLauncher());

        var r = router.Route("POST", "/api/projects/kit/runs", cookie);

        Assert.Equal(201, r.Status);
        var started = Assert.IsType<AgentRunStarted>(r.Body);
        Assert.Equal(AgentRunStatus.Failed, started.Run.Status);
        Assert.Equal(Noon, started.Run.Ended);
        Assert.Equal("the runner failed to start the run", started.Launch);
        Assert.DoesNotContain("launcher-secret", JsonSerializer.Serialize(started), StringComparison.Ordinal);

        var rig = new Rig(store: store, run: store.List()[0], token: started.Token);
        Assert.Equal(403, (await Call(rig.Proxy, started.Token, Msg())).Status);
    }

    [Fact]
    public void Only_the_operator_starts_a_run()
    {
        var (router, store, _, _) = OperatorRouter();

        Assert.Equal(401, router.Route("POST", "/api/projects/kit/runs").Status);
        Assert.Equal(403, router.Route("POST", "/api/projects/kit/runs", SignIn(router, "ann")).Status);
        Assert.Empty(store.List());
    }

    [Fact]
    public void Runs_are_listed_to_the_operator_alone_newest_first_with_the_cap()
    {
        var (router, store, clock, cookie) = OperatorRouter(cap: 2.5m);
        router.Route("POST", "/api/projects/kit/runs", cookie);
        clock.Now = Noon.AddMinutes(5);
        var second = Assert.IsType<AgentRunStarted>(router.Route("POST", "/api/projects/kit/runs", cookie).Body);
        store.AddUsage(second.Run.Id, new AgentUsage(10, 20, 30, 40), 0.5m);

        Assert.Equal(401, router.Route("GET", "/api/runs").Status);
        Assert.Equal(403, router.Route("GET", "/api/runs", SignIn(router, "ann")).Status);

        var list = router.Route("GET", "/api/runs", cookie);
        Assert.Equal(200, list.Status);
        var body = Assert.IsType<AgentRunList>(list.Body);
        Assert.Equal(2.5m, body.CapUsd);
        Assert.Equal(2, body.Runs.Count);
        Assert.Equal(second.Run.Id, body.Runs[0].Id);
        Assert.Equal((10, 20, 30, 40, 0.5m), (body.Runs[0].InputTokens, body.Runs[0].OutputTokens, body.Runs[0].CacheWriteTokens, body.Runs[0].CacheReadTokens, body.Runs[0].CostUsd));
    }

    [Fact]
    public void BEH_UI_7_the_run_list_is_newest_start_first_whatever_order_the_runs_were_recorded_in()
    {
        var (router, store, _, cookie) = OperatorRouter();
        var older = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(-30));
        var newest = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(10));
        var middle = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(5));

        var body = Assert.IsType<AgentRunList>(router.Route("GET", "/api/runs", cookie).Body);

        Assert.Equal([newest.Id, middle.Id, older.Id], body.Runs.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void BEH_UI_7_a_run_still_running_is_placed_by_its_start_not_at_the_bottom()
    {
        var (router, store, _, cookie) = OperatorRouter();
        var running = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(1));
        Assert.True(store.StartIfQueued(running.Id));
        var fiveMinutes = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(5));
        store.SetStatus(fiveMinutes.Id, AgentRunStatus.Finished, Noon.AddMinutes(6));
        var nineMinutes = store.Create(Operator, "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(9));
        store.SetStatus(nineMinutes.Id, AgentRunStatus.Finished, Noon.AddMinutes(10));

        var body = Assert.IsType<AgentRunList>(router.Route("GET", "/api/runs", cookie).Body);

        Assert.Equal([nineMinutes.Id, fiveMinutes.Id, running.Id], body.Runs.Select(r => r.Id).ToArray());
        Assert.Null(body.Runs[2].Ended);
    }

    [Fact]
    public void BEH_ORCH_9_the_pipeline_is_listed_to_the_operator_alone()
    {
        var (router, _, _, cookie) = OperatorRouter();

        Assert.Equal(401, router.Route("GET", "/api/pipeline").Status);
        Assert.Equal(403, router.Route("GET", "/api/pipeline", SignIn(router, "ann")).Status);
        Assert.Equal(200, router.Route("GET", "/api/pipeline", cookie).Status);
    }

    [Fact]
    public void BEH_VIS_1_the_run_page_is_served_to_the_owner_alone()
    {
        var (router, _, _, cookie) = OperatorRouter();

        Assert.Equal(401, router.Route("GET", "/api/runs/visibility").Status);
        Assert.Equal(403, router.Route("GET", "/api/runs/visibility", SignIn(router, "ann")).Status);
        Assert.Equal(200, router.Route("GET", "/api/runs/visibility", cookie).Status);
    }

    [Fact]
    public void BEH_VIS_2_with_no_session_cost_record_every_stage_cost_is_unknown_and_the_page_still_renders()
    {
        var (router, store, _, cookie) = OperatorRouter();
        var triage = store.Create("james", "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon, "slug-a", "triage");
        store.SetStatus(triage.Id, AgentRunStatus.Finished, Noon.AddMinutes(1));
        var spec = store.Create("james", "kit", Sonnet, AgentTokens.Hash(AgentTokens.Mint()), Noon.AddMinutes(2), "slug-a", "spec");
        store.SetStatus(spec.Id, AgentRunStatus.Finished, Noon.AddMinutes(3));

        var r = router.Route("GET", "/api/runs/visibility", cookie);

        Assert.Equal(200, r.Status);
        var page = Assert.IsAssignableFrom<IRunVisibility>(r.Body);
        Assert.Equal(2, page.Finished.Single().Stages.Count);
        Assert.All(page.Finished.Single().Stages, s => Assert.Null(s.CostUsd));
    }

    [Fact]
    public void BEH_ORCH_16_a_projects_tickets_are_listed_to_the_operator_alone()
    {
        var (router, _, _, cookie) = OperatorRouter();

        Assert.Equal(401, router.Route("GET", "/api/projects/kit/tickets").Status);
        Assert.Equal(403, router.Route("GET", "/api/projects/kit/tickets", SignIn(router, "ann")).Status);
        Assert.Equal(200, router.Route("GET", "/api/projects/kit/tickets", cookie).Status);
        Assert.Equal(404, router.Route("GET", "/api/projects/no-such/tickets", cookie).Status);
    }

    [Fact]
    public void BEH_ORCH_16_a_reply_on_a_ticket_is_the_operators_alone_and_needs_the_pipeline()
    {
        var (router, _, _, cookie) = OperatorRouter();
        var body = JsonDocument.Parse("{\"body\":\"hi\"}").RootElement;

        Assert.Equal(401, router.Route("POST", "/api/projects/kit/tickets/x/comments", body: body).Status);
        Assert.Equal(403, router.Route("POST", "/api/projects/kit/tickets/x/comments", SignIn(router, "ann"), body: body).Status);
        Assert.Equal(409, router.Route("POST", "/api/projects/kit/tickets/x/comments", cookie, body: body).Status); // this router runs no pipeline
    }

    [Fact]
    public void With_no_password_runs_are_served_to_loopback_only()
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        KitRouter Open(string host) => new(
            corpora, viewer, new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")), null, new OriginPolicy(new UrlParser(), null),
            new SessionStore(), new SignInThrottle(), host: host, runStore: new MemoryAgentRunStore());

        Assert.Equal(200, Open("127.0.0.1").Route("GET", "/api/runs").Status);
        Assert.Equal(201, Open("127.0.0.1").Route("POST", "/api/projects/kit/runs").Status);
        Assert.Equal(401, Open("0.0.0.0").Route("GET", "/api/runs").Status);
        Assert.Equal(403, Open("0.0.0.0").Route("POST", "/api/projects/kit/runs").Status);
    }

    [Fact]
    public void The_run_cap_is_read_from_the_environment_or_refused()
    {
        Assert.Equal(AgentProxy.DefaultCapUsd, KitSettings.ParseAgentRunCap(null));
        Assert.Equal(2.5m, KitSettings.ParseAgentRunCap("2.50"));
        Assert.Equal(0m, KitSettings.ParseAgentRunCap("0"));
        foreach (var bad in new[] { "-1", "lots", "$5", "1,5" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseAgentRunCap(bad));
        }
    }

    // ---- the host --------------------------------------------------------------------------------

    [Fact]
    public async Task An_unknown_token_with_an_oversized_body_is_401_not_413_and_nothing_is_relayed()
    {
        var store = new MemoryAgentRunStore();
        var model = new ScriptedModel(_ => Ok("""{"usage":{"input_tokens":1}}"""));
        await using var app = Host(store, model);
        await app.StartAsync();

        // A raw socket: it promises 33 MB, sends 64 KB and stops. A host that reads the body before it checks
        // the token waits here for the rest and never answers; the token is checked first, so it is 401 at once.
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, Port(app));
        var stream = client.GetStream();
        var head = $"POST /api/agent/v1/messages HTTP/1.1\r\nHost: kit\r\nx-api-key: kitrun_not-a-token-kit-minted\r\nContent-Type: application/json\r\nContent-Length: {33 * 1024 * 1024}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(new byte[64 * 1024]);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var buffer = new byte[4096];
        var got = new StringBuilder();
        while (!got.ToString().Contains("authentication_error", StringComparison.Ordinal) && !got.ToString().StartsWith("HTTP/1.1 413", StringComparison.Ordinal))
        {
            var n = await stream.ReadAsync(buffer, timeout.Token);
            Assert.NotEqual(0, n);
            got.Append(Encoding.UTF8.GetString(buffer, 0, n));
        }

        Assert.StartsWith("HTTP/1.1 401", got.ToString(), StringComparison.Ordinal);
        Assert.Contains("authentication_error", got.ToString(), StringComparison.Ordinal);
        Assert.Empty(model.Sent);
    }

    [Fact]
    public async Task The_host_serves_the_proxy_for_a_known_token_and_refuses_anything_but_POST()
    {
        var store = new MemoryAgentRunStore();
        var token = AgentTokens.Mint();
        var run = store.Create("james", "kit", Sonnet, AgentTokens.Hash(token), DateTimeOffset.UtcNow);
        var model = new ScriptedModel(_ => Ok("""{"usage":{"input_tokens":1000000,"output_tokens":0}}"""));
        await using var app = Host(store, model);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Port(app)}") };

        HttpRequestMessage Post(string path)
        {
            var q = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(Encoding.UTF8.GetString(Msg()), Encoding.UTF8, "application/json") };
            q.Headers.TryAddWithoutValidation("x-api-key", token);
            return q;
        }

        var ok = await http.SendAsync(Post("/api/agent/v1/messages"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2m, store.ByTokenHash(AgentTokens.Hash(token))!.CostUsd);
        Assert.Equal(Key, Assert.Single(model.Sent).Headers["x-api-key"]);

        var counted = await http.SendAsync(Post("/api/agent/v1/messages/count_tokens"));
        Assert.Equal(HttpStatusCode.OK, counted.StatusCode);
        Assert.Equal(2m, store.ByTokenHash(AgentTokens.Hash(token))!.CostUsd);
        Assert.EndsWith("/count_tokens", model.Sent[1].Uri.AbsolutePath, StringComparison.Ordinal);

        var get = await http.GetAsync("/api/agent/v1/messages");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Contains("POST", get.Content.Headers.Allow.Concat(get.Headers.TryGetValues("Allow", out var allow) ? allow : []));
        Assert.Equal(2, model.Sent.Count);
        Assert.Equal(run.Id, store.List()[0].Id);
    }

    [Fact]
    public async Task The_host_relays_a_31_MB_body_past_Kestrels_own_limit_and_answers_a_33_MB_one_with_the_proxys_413()
    {
        var store = new MemoryAgentRunStore();
        var token = AgentTokens.Mint();
        store.Create("james", "kit", Sonnet, AgentTokens.Hash(token), DateTimeOffset.UtcNow);
        var model = new ScriptedModel(_ => Ok("""{"usage":{"input_tokens":1,"output_tokens":1}}"""));
        await using var app = Host(store, model);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Port(app)}"), Timeout = TimeSpan.FromMinutes(2) };

        async Task<HttpResponseMessage> Post(int bytes)
        {
            var head = $$"""{"model":"{{Sonnet}}","pad":""";
            var json = head + "\"" + new string('a', bytes - head.Length - 3) + "\"}";
            var q = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/messages") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            q.Headers.TryAddWithoutValidation("x-api-key", token);
            return await http.SendAsync(q);
        }

        var big = await Post(31 * 1024 * 1024);
        Assert.Equal(HttpStatusCode.OK, big.StatusCode);
        Assert.Single(model.Sent);

        // One byte over the proxy's limit is the most Kestrel lets in, so the proxy's own sentence answers it.
        var tooBig = await Post(AgentProxy.MaxBody + 1);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Contains("request_too_large", await tooBig.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Single(model.Sent);
    }

    [Fact]
    public async Task The_host_marks_the_proxys_json_answers_and_refusals_no_store()
    {
        var store = new MemoryAgentRunStore();
        var token = AgentTokens.Mint();
        store.Create("james", "kit", Sonnet, AgentTokens.Hash(token), DateTimeOffset.UtcNow);
        await using var app = Host(store, new ScriptedModel(_ => Ok("""{"usage":{"input_tokens":1,"output_tokens":1}}""")));
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Port(app)}") };

        HttpRequestMessage Post(string? key)
        {
            var q = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/messages") { Content = new StringContent(Encoding.UTF8.GetString(Msg()), Encoding.UTF8, "application/json") };
            if (key is not null)
            {
                q.Headers.TryAddWithoutValidation("x-api-key", key);
            }

            return q;
        }

        var answered = await http.SendAsync(Post(token));
        var refused = await http.SendAsync(Post("kitrun_unknown"));
        var wrongMethod = await http.GetAsync("/api/agent/v1/messages");

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.All([answered, refused, wrongMethod], r => Assert.True(r.Headers.CacheControl?.NoStore));
    }

    [Fact]
    public void The_answer_that_carries_a_run_token_is_no_store()
    {
        var (router, _, _, cookie) = OperatorRouter();

        var r = router.Route("POST", "/api/projects/kit/runs", cookie);

        Assert.Equal(201, r.Status);
        Assert.Equal("no-store", r.CacheControl);
    }

    // ---- the database store ----------------------------------------------------------------------

    [Fact]
    public void The_postgres_store_keeps_a_run_by_the_hash_of_its_token_and_adds_usage_atomically()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresAgentRunStore(connection);
        var hash = AgentTokens.Hash(AgentTokens.Mint());
        var run = store.Create("james", $"p{Guid.NewGuid():N}"[..12], Sonnet, hash, Noon);

        Assert.Equal(AgentRunStatus.Queued, store.ByTokenHash(hash)!.Status);
        Assert.Null(store.ByTokenHash(AgentTokens.Hash("kitrun_other")));
        Assert.Null(store.ByTokenHash("x' OR '1'='1"));

        store.AddUsage(run.Id, new AgentUsage(1, 2, 3, 4), 0.25m);
        var after = store.AddUsage(run.Id, new AgentUsage(10, 20, 30, 40), 0.5m)!;
        Assert.Equal((11, 22, 33, 44, 0.75m), (after.InputTokens, after.OutputTokens, after.CacheWriteTokens, after.CacheReadTokens, after.CostUsd));
        Assert.Null(store.AddUsage("no-such-run", new AgentUsage(1, 1, 1, 1), 1m));

        store.SetStatus(run.Id, AgentRunStatus.Finished, Noon.AddMinutes(1));
        var done = store.ByTokenHash(hash)!;
        Assert.Equal(AgentRunStatus.Finished, done.Status);
        Assert.Equal(Noon.AddMinutes(1), done.Ended);
        Assert.Contains(store.List(), r => r.Id == run.Id);
    }

    // ---- rig -------------------------------------------------------------------------------------

    private const string Sse =
        "event: message_start\n"
        + "data: {\"type\":\"message_start\",\"message\":{\"id\":\"m\",\"usage\":{\"input_tokens\":100,\"cache_creation_input_tokens\":50,\"cache_read_input_tokens\":1000,\"cache_creation\":{\"ephemeral_5m_input_tokens\":40,\"ephemeral_1h_input_tokens\":10},\"output_tokens\":1}}}\n\n"
        + "event: ping\ndata: {\"type\":\"ping\"}\n\n"
        + "event: content_block_delta\n"
        + "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"the words message_delta and message_start in text\"}}\n\n"
        + "event: message_delta\n"
        + "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":200}}\n\n"
        + "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";

    private static byte[] Msg(string model = Sonnet, bool stream = false) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model, stream, max_tokens = 16, messages = new[] { new { role = "user", content = "hi" } } }));

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Stream(byte[] bytes, int chunk, bool breakAfter = false)
    {
        var content = new StreamContent(new ChunkedStream(Split(bytes, chunk).ToList(), breakAfter));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static IEnumerable<byte[]> Split(byte[] bytes, int size)
    {
        for (var at = 0; at < bytes.Length; at += size)
        {
            yield return bytes[at..Math.Min(bytes.Length, at + size)];
        }
    }

    private static async Task<Reply> Call(AgentProxy proxy, string? token, byte[]? body, bool countTokens = false, string? version = null, string? beta = null, CancellationToken cancellationToken = default)
    {
        var status = 0;
        var contentType = "";
        var output = new MemoryStream();
        await proxy.ServeAsync(
            new AgentCall(token, version, beta, countTokens, body),
            (s, c) =>
            {
                status = s;
                contentType = c;
                return output;
            },
            cancellationToken);
        return new Reply(status, contentType, output.ToArray());
    }

    private static string ErrorType(Reply r) => JsonDocument.Parse(r.Body).RootElement.GetProperty("error").GetProperty("type").GetString()!;

    private static string ErrorMessage(Reply r) => JsonDocument.Parse(r.Body).RootElement.GetProperty("error").GetProperty("message").GetString()!;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string SignIn(KitRouter router, string? name)
    {
        var r = router.Route("POST", "/api/session", body: name is null ? Json(new { password = Operator }) : Json(new { username = name, password = Password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static (KitRouter Router, MemoryAgentRunStore Store, Clock Clock, string Operator) OperatorRouter(IAgentRunLauncher? launcher = null, decimal cap = AgentProxy.DefaultCapUsd)
    {
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(Password, iterations: 1000));
        var store = new MemoryAgentRunStore();
        var clock = new Clock(Noon);
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var router = new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            Operator,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            users: users,
            clock: clock,
            runStore: store,
            runLauncher: launcher ?? new NoRunnerLauncher(),
            agentRunCap: cap);
        return (router, store, clock, SignIn(router, null));
    }

    private static WebApplication Host(IAgentRunStore store, ScriptedModel model) =>
        KitServer.Build(
            ["--urls", "http://127.0.0.1:0"],
            new KitSettings(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot, null, Path.Combine(RepoLayout.Root, "no-bundle-here")) { AnthropicApiKey = Key },
            s =>
            {
                s.AddSingleton(store);
                s.AddSingleton<HttpMessageHandler>(model);
            });

    private static int Port(WebApplication app) =>
        new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    private sealed record Reply(int Status, string ContentType, byte[] Body);

    private sealed record Sent(Uri Uri, Dictionary<string, string> Headers, string Body);

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>The proxy's side of the proxy: one proxy, one store, one run with its token, and Anthropic scripted.</summary>
    private sealed class Rig
    {
        public Rig(Func<HttpRequestMessage, HttpResponseMessage>? answer = null, decimal cap = 5m, string? key = Key, TimeSpan? maxAge = null,
            MemoryAgentRunStore? store = null, IAgentRun? run = null, string? token = null)
        {
            Store = store ?? new MemoryAgentRunStore();
            Token = token ?? AgentTokens.Mint();
            Run = run ?? Store.Create("james", "kit", Sonnet, AgentTokens.Hash(Token), Noon);
            Model = new ScriptedModel(answer ?? (_ => Ok("""{"usage":{"input_tokens":1,"output_tokens":1}}""")));
            Clock = new Clock(Noon);
            Proxy = new AgentProxy(Store, new HttpClient(Model), key, cap, Clock, maxAge);
        }

        public MemoryAgentRunStore Store { get; }

        public string Token { get; }

        public IAgentRun Run { get; }

        public ScriptedModel Model { get; }

        public Clock Clock { get; }

        public AgentProxy Proxy { get; }

        /// <summary>The run as the store has it now.</summary>
        public IAgentRun Current => Store.ByTokenHash(AgentTokens.Hash(Token))!;
    }

    /// <summary>Anthropic: answers from a script and records what it was sent, headers included.</summary>
    private sealed class ScriptedModel(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Sent> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()))
            {
                headers[h.Key] = string.Join(",", h.Value);
            }

            Sent.Add(new Sent(request.RequestUri!, headers, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return answer(request);
        }
    }

    /// <summary>Hands the bytes back one scripted piece per read, as a socket would, and can end by failing.</summary>
    private sealed class ChunkedStream(List<byte[]> chunks, bool breakAfter) : Stream
    {
        private int next;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Take(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Take(Span<byte> into)
        {
            if (next >= chunks.Count)
            {
                return breakAfter ? throw new IOException("the connection broke") : 0;
            }

            var chunk = chunks[next++];
            chunk.CopyTo(into);
            return chunk.Length;
        }
    }

    /// <summary>A store whose spend cannot be written, as a database that has gone away.</summary>
    private sealed class FailingStore(MemoryAgentRunStore inner) : IAgentRunStore
    {
        public IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started, string? workItem = null, string? stage = null) => inner.Create(owner, project, model, tokenHash, started, workItem, stage);

        public IReadOnlyList<IAgentRun> List() => inner.List();

        public IAgentRun? ByTokenHash(string tokenHash) => inner.ByTokenHash(tokenHash);

        public IAgentRun? AddUsage(string id, AgentUsage usage, decimal costUsd) => throw new InvalidOperationException($"database down for {Key}");

        public void SetStatus(string id, string status, DateTimeOffset? ended = null, string? failure = null) => inner.SetStatus(id, status, ended, failure);

        public bool StartIfQueued(string id) => inner.StartIfQueued(id);
    }

    private sealed class RecordingLauncher : IAgentRunLauncher
    {
        public IAgentRun? Run { get; private set; }

        public string? Token { get; private set; }

        public Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default)
        {
            Run = run;
            Token = runToken;
            return new NoRunnerLauncher().LaunchAsync(run, runToken, cancellationToken);
        }
    }

    private sealed class ThrowingLauncher : IAgentRunLauncher
    {
        public Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"launcher-secret {runToken}");
    }
}
