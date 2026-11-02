using System.Net;
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
/// kit-ui.beh <c>BEH-SPECAGENT-4</c>: an Inbox card is talked through with the spec agent. The model
/// is a fake <see cref="ITriageChat"/>; the tests of the real client script Anthropic's HTTP answers
/// and check what was sent: Haiku, the key, the card in a cached system block, the turns in order.
/// </summary>
public class TriageTests
{
    private const string ApiKey = "sk-ant-not-a-real-key-do-not-print";

    // ---- the routes ------------------------------------------------------------------------------

    [Fact]
    public void A_turn_sends_the_card_and_the_chat_to_the_model_and_answers_its_reply()
    {
        var model = new FakeModel("It asks whether to gate the gate.");
        var r = Router(model).Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "what is this asking?")));

        Assert.Equal(200, r.Status);
        var answer = Assert.IsType<TriageAnswer>(r.Body);
        Assert.Equal("It asks whether to gate the gate.", answer.Reply);
        Assert.Equal(KitRouter.DefaultTriageLimit - 1, answer.Remaining);

        var (context, turns) = Assert.Single(model.Calls);
        Assert.Contains("behaviour BEH-GATE-2 \"Could-not-look is never reported as green\"", context, StringComparison.Ordinal);
        Assert.Contains("behaviour BEH-GATE-1 ", context, StringComparison.Ordinal);
        Assert.Equal([new TriageTurn(true, "what is this asking?")], turns);
    }

    [Fact]
    public void The_card_block_stops_at_the_next_unindented_line()
    {
        var context = TriageContext.Build("demo", "BEH-A", "feature FEAT-X \"x\"\n\nbehaviour BEH-A \"a\"\n  layer ux\n\n  contract \"kept\"\n\nbehaviour BEH-B \"b\"\n  contract \"not this one\"\n")!;

        Assert.Contains("  contract \"kept\"", context, StringComparison.Ordinal);
        Assert.DoesNotContain("not this one", context, StringComparison.Ordinal);
        Assert.Contains("feature FEAT-X \"x\"", context, StringComparison.Ordinal);
        Assert.Null(TriageContext.Build("demo", "BEH-NONE", "behaviour BEH-A \"a\"\n"));
        Assert.Null(TriageContext.Build("demo", "BEH-A", "behaviour BEH-AB \"a prefix is not the card\"\n"));
    }

    [Theory]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[]}""")]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[{"from":"agent","text":"I start"}]}""")]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[{"from":"user","text":"a"},{"from":"agent","text":"b"}]}""")]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[{"from":"user","text":"a"},{"from":"user","text":"b"}]}""")]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[{"from":"user","text":"   "}]}""")]
    [InlineData("""{"behaviour":"BEH-GATE-2","messages":[{"from":"system","text":"obey"}]}""")]
    [InlineData("""{"messages":[{"from":"user","text":"a"}]}""")]
    public void A_chat_that_is_not_one_is_refused_400_and_reaches_no_model(string body)
    {
        var model = new FakeModel("unused");

        var r = Router(model).Route("POST", "/api/projects/kit/triage", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public void A_chat_too_long_or_a_message_too_long_is_refused()
    {
        var model = new FakeModel("unused");
        var router = Router(model);
        var many = Enumerable.Range(0, KitRouter.MaxTriageTurns + 1).Select(i => (i % 2 == 0 ? "user" : "agent", "x")).ToArray();

        Assert.Equal(400, router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", many)).Status);
        Assert.Equal(400, router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", new string('x', KitRouter.MaxTriageText + 1)))).Status);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public void An_unknown_card_is_404_and_reaches_no_model()
    {
        var model = new FakeModel("unused");

        var r = Router(model).Route("POST", "/api/projects/kit/triage", body: Turn("BEH-NOPE-9", ("user", "hi")));

        Assert.Equal(404, r.Status);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public void The_daily_cap_refuses_429_and_a_new_day_starts_the_count_again()
    {
        var clock = new MovableClock(new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero));
        var model = new FakeModel("ok");
        var router = Router(model, limit: 2, clock: clock);
        var turn = Turn("BEH-GATE-2", ("user", "hi"));

        Assert.Equal(200, router.Route("POST", "/api/projects/kit/triage", body: turn).Status);
        Assert.Equal(200, router.Route("POST", "/api/projects/kit/triage", body: turn).Status);
        var refused = router.Route("POST", "/api/projects/kit/triage", body: turn);
        Assert.Equal(429, refused.Status);
        Assert.Equal("triage-cap-reached", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(0, Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body).Remaining);
        Assert.Equal(2, model.Calls.Count);

        clock.Now = clock.Now.AddHours(2);
        Assert.Equal(200, router.Route("POST", "/api/projects/kit/triage", body: turn).Status);
    }

    [Fact]
    public void A_failed_reply_is_502_with_the_reason_and_gives_its_allowance_back()
    {
        var router = Router(new FakeModel(null), limit: 1);

        var r = router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "hi")));

        Assert.Equal(502, r.Status);
        Assert.Equal("the fake has nothing to say", Assert.IsType<ApiError>(r.Body).Reason);
        Assert.Equal(1, Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body).Remaining);
    }

    [Fact]
    public void A_model_that_throws_is_502_and_gives_its_allowance_back()
    {
        var router = Router(new ThrowingModel(), limit: 1);

        var r = router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "hi")));

        Assert.Equal(502, r.Status);
        Assert.Equal(1, Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body).Remaining);
    }

    [Fact]
    public void Without_the_key_GET_says_off_and_a_turn_is_503_naming_the_secret()
    {
        var router = Router(null);

        var status = Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body);
        var r = router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "hi")));

        Assert.False(status.Configured);
        Assert.Null(status.Model);
        Assert.Equal(503, r.Status);
        Assert.Contains("kit-anthropic", Assert.IsType<ApiError>(r.Body).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void GET_triage_is_served_only_to_a_session_on_a_locked_Kit()
    {
        Assert.Equal(401, Router(new FakeModel("ok"), password: "pw").Route("GET", "/api/triage").Status);
    }

    // ---- start-up --------------------------------------------------------------------------------

    [Fact]
    public void The_key_switches_the_chat_on_never_prints_and_the_cap_is_read_or_refused()
    {
        var off = KitSettings.FromEnvironment(Env(), RepoLayout.Root);
        var on = KitSettings.FromEnvironment(Env(("KIT_ANTHROPIC_API_KEY", ApiKey), ("KIT_TRIAGE_DAILY_LIMIT", "7")), RepoLayout.Root);

        using var offServices = new ServiceCollection().AddKitServices(off).BuildServiceProvider();
        using var onServices = new ServiceCollection().AddKitServices(on).BuildServiceProvider();

        Assert.False(off.Triage);
        Assert.Null(offServices.GetService<ITriageChat>());
        Assert.True(on.Triage);
        Assert.Equal(AnthropicTriageChat.DefaultModel, onServices.GetRequiredService<ITriageChat>().Model);
        Assert.Equal(7, Assert.IsType<TriageStatus>(onServices.GetRequiredService<IKitRouter>().Route("GET", "/api/triage").Body).Remaining);
        Assert.DoesNotContain(ApiKey, on.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Env(("KIT_TRIAGE_DAILY_LIMIT", "lots")), RepoLayout.Root));
    }

    // ---- the Chat page (BEH-CHAT-1 to BEH-CHAT-4) -------------------------------------------------

    [Fact]
    public void A_chat_turn_sends_the_projects_outline_the_other_projects_and_the_chat_instructions()
    {
        var model = new FakeModel("Shall we start with the gate?");
        var r = Router(model).Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "[Now talking about kit] what is left?")));

        Assert.Equal(200, r.Status);
        Assert.Equal("Shall we start with the gate?", Assert.IsType<TriageAnswer>(r.Body).Reply);
        var (context, turns) = Assert.Single(model.Calls);
        Assert.StartsWith("Project: kit\n", context, StringComparison.Ordinal);
        Assert.Contains("behaviour BEH-GATE-2 ", context, StringComparison.Ordinal);
        Assert.DoesNotContain("item in hand", context, StringComparison.Ordinal);
        Assert.Equal([new TriageTurn(true, "[Now talking about kit] what is left?")], turns);
        Assert.Equal(TriageContext.ChatInstructions, Assert.Single(model.Instructions));
    }

    [Fact]
    public void A_chat_turn_with_an_item_in_hand_sends_that_card_too()
    {
        var model = new FakeModel("Approve it.");
        Assert.Equal(200, Router(model).Route("POST", "/api/projects/kit/chat", body: Chat("BEH-GATE-2", ("user", "and this one?"))).Status);

        var (context, _) = Assert.Single(model.Calls);
        Assert.Contains("The item in hand, waiting on the owner:\nbehaviour BEH-GATE-2 ", context, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chat_context_names_the_other_projects_and_not_its_own()
    {
        var context = TriageContext.BuildChat("tracks", null, "behaviour BEH-A \"a\"\n", ["kit", "snip-it", "tracks"])!;

        Assert.Contains("The other projects: kit, snip-it\n", context, StringComparison.Ordinal);
        Assert.Contains("behaviour BEH-A \"a\"", context, StringComparison.Ordinal);
        Assert.Contains("The other projects: none", TriageContext.BuildChat("solo", null, "", ["solo"]), StringComparison.Ordinal);
        Assert.Null(TriageContext.BuildChat("tracks", "BEH-NONE", "behaviour BEH-A \"a\"\n", ["tracks"]));

        // BEH-CHAT-4: what the names are for. The agent asks, names candidates, and leaves the switch to him.
        Assert.Contains("seems to be about a different project, ask whether it is", TriageContext.ChatInstructions, StringComparison.Ordinal);
        Assert.Contains("name it in their next message or pick it on the page", TriageContext.ChatInstructions, StringComparison.Ordinal);

        // BEH-CHAT-2: the item is on the page from the start, so the agent infers his route rather than asking for a click.
        Assert.Contains("Infer which from what they say", TriageContext.ChatInstructions, StringComparison.Ordinal);

        // His 17:52Z and 17:54Z notes: the page tells the agent, in order, when an item came into view and what he did with it.
        Assert.Contains("[In view now: id \"title\"] means that item came into the chat at that point", TriageContext.ChatInstructions, StringComparison.Ordinal);
        Assert.Contains("[He approved id] says what the owner did with an item on the page", TriageContext.ChatInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chat_naming_an_unknown_or_malformed_item_is_refused_and_reaches_no_model()
    {
        var model = new FakeModel("unreachable");
        var router = Router(model);

        Assert.Equal(404, router.Route("POST", "/api/projects/kit/chat", body: Chat("BEH-NOPE-9", ("user", "hi"))).Status);
        Assert.Equal(400, router.Route("POST", "/api/projects/kit/chat", body: Chat("", ("user", "hi"))).Status);
        Assert.Equal(400, router.Route("POST", "/api/projects/kit/chat", body: JsonDocument.Parse("""{"behaviour":7,"messages":[{"from":"user","text":"hi"}]}""").RootElement).Status);
        Assert.Equal(400, router.Route("POST", "/api/projects/kit/chat", body: JsonDocument.Parse("""{"messages":[]}""").RootElement).Status);
        Assert.Equal(404, router.Route("POST", "/api/projects/no-such-app/chat", body: Chat(null, ("user", "hi"))).Status);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public void The_chat_and_the_inbox_share_one_daily_allowance_and_keep_their_own_instructions()
    {
        var model = new FakeModel("ok");
        var router = Router(model, limit: 2);

        Assert.Equal(200, router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "a"))).Status);
        Assert.Equal(200, router.Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "b"))).Status);
        Assert.Equal(429, router.Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "c"))).Status);
        Assert.Equal(429, router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "d"))).Status);
        Assert.Equal([null, TriageContext.ChatInstructions], model.Instructions);
    }

    [Fact]
    public void Without_the_key_a_chat_turn_is_503_and_a_failed_one_gives_its_allowance_back()
    {
        Assert.Equal(503, Router(null).Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "hi"))).Status);

        var router = Router(new FakeModel(null), limit: 1);
        Assert.Equal(502, router.Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "hi"))).Status);
        Assert.Equal(1, Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body).Remaining);
    }

    // ---- the chat agent acting on the item (BEH-CHAT-6) -------------------------------------------

    [Fact]
    public void Only_the_chat_offers_actions_and_its_answer_carries_the_one_asked_for()
    {
        var model = new FakeModel("Approved it.", new TriageAction("approve"));
        var router = Router(model);

        var chat = router.Route("POST", "/api/projects/kit/chat", body: Chat("BEH-GATE-2", ("user", "yes, approve it")));
        Assert.Equal(new TriageAction("approve"), Assert.IsType<TriageAnswer>(chat.Body).Action);

        // The Inbox's Talk it through stays as it was: no tools offered, and nothing acted on.
        var inbox = router.Route("POST", "/api/projects/kit/triage", body: Turn("BEH-GATE-2", ("user", "yes")));
        Assert.Null(Assert.IsType<TriageAnswer>(inbox.Body).Action);
        Assert.Equal([true, false], model.Offered);
    }

    [Fact]
    public void An_action_on_an_item_is_dropped_when_none_is_in_hand_but_an_undo_is_kept()
    {
        var approve = Router(new FakeModel("Done.", new TriageAction("approve"))).Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "approve it")));
        Assert.Null(Assert.IsType<TriageAnswer>(approve.Body).Action);

        var undo = Router(new FakeModel("Undone.", new TriageAction("undo"))).Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "undo that")));
        Assert.Equal(new TriageAction("undo"), Assert.IsType<TriageAnswer>(undo.Body).Action);

        // An action with no words and nothing it may do is no reply at all, and costs no allowance.
        var router = Router(new FakeModel("", new TriageAction("approve")), limit: 1);
        Assert.Equal(502, router.Route("POST", "/api/projects/kit/chat", body: Chat(null, ("user", "approve it"))).Status);
        Assert.Equal(1, Assert.IsType<TriageStatus>(router.Route("GET", "/api/triage").Body).Remaining);
    }

    [Fact]
    public void A_tool_call_reads_as_an_action_and_one_without_its_words_is_dropped()
    {
        var both = AnthropicTriageChat.Parse("""{"content":[{"type":"text","text":"Posted."},{"type":"tool_use","id":"t","name":"answer_item","input":{"text":" blue "}}]}""");
        Assert.Equal("Posted.", both.Text);
        Assert.Equal(new TriageAction("answer", "blue"), both.Action);

        var alone = AnthropicTriageChat.Parse("""{"content":[{"type":"tool_use","id":"t","name":"approve_item","input":{}}]}""");
        Assert.Equal("", alone.Text);
        Assert.Equal(new TriageAction("approve"), alone.Action);

        Assert.Equal(new TriageAction("undo"), AnthropicTriageChat.Parse("""{"content":[{"type":"tool_use","id":"t","name":"undo_last","input":{}}]}""").Action);
        Assert.Null(AnthropicTriageChat.Parse("""{"content":[{"type":"tool_use","id":"t","name":"deny_item","input":{}}]}""").Text);
        Assert.Null(AnthropicTriageChat.Parse("""{"content":[{"type":"tool_use","id":"t","name":"delete_repo","input":{}}]}""").Text);
    }

    [Fact]
    public async Task The_client_offers_the_tools_only_when_asked()
    {
        var api = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi."}]}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi."}]}"""));
        var chat = new AnthropicTriageChat(new HttpClient(api), ApiKey);

        await chat.ReplyAsync("THE PROJECT", [new TriageTurn(true, "hi")], "BE THE CHAT", actions: true);
        await chat.ReplyAsync("THE CARD", [new TriageTurn(true, "hi")]);

        var sent = api.Sent.Select(s => JsonDocument.Parse(s.Body!).RootElement).ToList();
        Assert.Equal(
            ["approve_item", "deny_item", "answer_item", "undo_last"],
            sent[0].GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
        Assert.False(sent[1].TryGetProperty("tools", out _));
    }

    // ---- the real client, against a scripted API -------------------------------------------------

    [Fact]
    public async Task The_client_sends_the_chat_instructions_in_place_of_the_cards()
    {
        var api = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi."}]}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi."}]}"""));
        var chat = new AnthropicTriageChat(new HttpClient(api), ApiKey);

        await chat.ReplyAsync("THE PROJECT", [new TriageTurn(true, "hi")], "BE THE CHAT");
        await chat.ReplyAsync("THE CARD", [new TriageTurn(true, "hi")]);

        var systems = api.Sent.Select(s => JsonDocument.Parse(s.Body!).RootElement.GetProperty("system")[0].GetProperty("text").GetString()).ToList();
        Assert.Equal("BE THE CHAT", systems[0]);
        Assert.StartsWith("You are Kit's spec agent. You talk one card", systems[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_client_sends_haiku_the_key_a_cached_card_and_the_turns_and_reads_the_text()
    {
        var api = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Approve it."}]}"""));
        HttpRequestMessage? seen = null;
        var chat = new AnthropicTriageChat(new HttpClient(new Peek(api, r => seen = r)), ApiKey);

        var reply = await chat.ReplyAsync("THE CARD", [new TriageTurn(true, "should I?"), new TriageTurn(false, "maybe"), new TriageTurn(true, "and now?")]);

        Assert.Equal("Approve it.", reply.Text);
        var (method, uri, _, body) = Assert.Single(api.Sent);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://api.anthropic.com/v1/messages", uri.ToString());
        Assert.Equal(ApiKey, seen!.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", seen.Headers.GetValues("anthropic-version").Single());

        var sent = JsonDocument.Parse(body!).RootElement;
        Assert.Equal("claude-haiku-5-5", sent.GetProperty("model").GetString());
        Assert.Equal(AnthropicTriageChat.MaxTokens, sent.GetProperty("max_tokens").GetInt32());
        var card = sent.GetProperty("system")[1];
        Assert.Equal("THE CARD", card.GetProperty("text").GetString());
        Assert.Equal("ephemeral", card.GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Equal(["user", "assistant", "user"], sent.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("and now?", sent.GetProperty("messages")[2].GetProperty("content").GetString());
    }

    [Fact]
    public async Task An_API_refusal_is_a_failure_with_its_status_and_never_echoes_the_body()
    {
        var api = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.TooManyRequests, """{"error":{"message":"THE CARD was too long"}}"""));
        var chat = new AnthropicTriageChat(new HttpClient(api), ApiKey);

        var reply = await chat.ReplyAsync("THE CARD", [new TriageTurn(true, "hi")]);

        Assert.Null(reply.Text);
        Assert.Contains("429", reply.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain("THE CARD", reply.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reply_with_no_text_block_is_a_failure_not_an_empty_answer()
    {
        Assert.Null(AnthropicTriageChat.Parse("""{"content":[{"type":"tool_use","id":"x"}]}""").Text);
        Assert.Equal("a b", AnthropicTriageChat.Parse("""{"content":[{"type":"text","text":"a "},{"type":"text","text":"b"}]}""").Text);
    }

    // ---- rig -------------------------------------------------------------------------------------

    private static KitRouter Router(ITriageChat? model, int limit = KitRouter.DefaultTriageLimit, TimeProvider? clock = null, string? password = null)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            clock: clock,
            triage: model,
            triageLimit: limit);
    }

    private static JsonElement Turn(string behaviour, params (string From, string Text)[] messages) =>
        JsonSerializer.SerializeToElement(new { behaviour, messages = messages.Select(m => new { from = m.From, text = m.Text }) });

    private static JsonElement Chat(string? behaviour, params (string From, string Text)[] messages) =>
        behaviour is null
            ? JsonSerializer.SerializeToElement(new { messages = messages.Select(m => new { from = m.From, text = m.Text }) })
            : JsonSerializer.SerializeToElement(new { behaviour, messages = messages.Select(m => new { from = m.From, text = m.Text }) });

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs)
    {
        var env = pairs.ToDictionary(p => p.Key, p => p.Value);
        return k => env.GetValueOrDefault(k);
    }

    private sealed class FakeModel(string? reply, TriageAction? action = null) : ITriageChat
    {
        public List<(string Context, IReadOnlyList<TriageTurn> Turns)> Calls { get; } = [];

        /// <summary>What each call was told to be: null is the Inbox card's own instructions.</summary>
        public List<string?> Instructions { get; } = [];

        /// <summary>Whether each call offered the model its actions (BEH-CHAT-6).</summary>
        public List<bool> Offered { get; } = [];

        public string Model => "fake-model";

        public Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, bool actions = false, CancellationToken cancellationToken = default)
        {
            Calls.Add((context, turns.ToList()));
            Instructions.Add(instructions);
            Offered.Add(actions);
            return Task.FromResult(reply is null ? new TriageReply(null, "the fake has nothing to say") : new TriageReply(reply, null, action));
        }
    }

    private sealed class ThrowingModel : ITriageChat
    {
        public string Model => "throwing-model";

        public Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, bool actions = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unforeseen");
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Hands each request to <paramref name="look"/> before <paramref name="inner"/> answers it: the headers FakeGitHub does not record.</summary>
    private sealed class Peek(HttpMessageHandler inner, Action<HttpRequestMessage> look) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            look(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
