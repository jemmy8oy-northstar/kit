using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:idle &lt;mode&gt;</c> (BEH-SETTINGS-5): how busy the agent is when nothing is asked of it —
/// read from Kit's own primary file, written by the operator alone, one commit-shaped edit at a time.
/// </summary>
public class IdleTests
{
    private const string Plain = "# kit:repo o/r@dev\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Chosen = "# kit:repo o/r@dev\n# kit:idle ideas\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router) Rig(bool accounts, string text = Plain)
    {
        var dir = Directory.CreateTempSubdirectory("kit-idle-").FullName;
        File.WriteAllText(Path.Combine(dir, "kit.beh"), text);

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), accounts ? Operator : null,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: accounts ? users : null);
        return (dir, router);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string SignIn(KitRouter router, string? name, string password)
    {
        var r = router.Route("POST", "/api/session", body: Json(name is null ? new { password } : (object)new { username = name, password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static IIdleSetting SettingOf(KitRouter router)
    {
        var r = router.Route("GET", "/api/settings/idle");
        Assert.Equal(200, r.Status);
        return Assert.IsType<IdleSetting>(r.Body);
    }

    private static string IdleOf(KitRouter router) => SettingOf(router).Idle;

    private static string OrderOf(KitRouter router) => string.Join(",", SettingOf(router).Order);

    [Fact]
    public void No_line_means_the_default_which_is_test_health()
    {
        Assert.Equal("health", IdleDirective.Default);
        Assert.Equal(["health"], IdleDirective.ReadOrder(Plain));
        Assert.Equal("health", IdleDirective.Read(Plain));
        Assert.Equal("health", OrderOf(Rig(false).Router));
    }

    [Theory]
    [InlineData("health", "health")]
    [InlineData("ideas", "ideas")]
    [InlineData("health, ideas", "health,ideas")]
    [InlineData("ideas, health", "ideas,health")]
    [InlineData("ideas,health", "ideas,health")]
    public void An_ordered_list_is_read_in_his_order(string value, string expected)
    {
        Assert.Equal(expected, string.Join(",", IdleDirective.ReadOrder($"# kit:idle {value}\nbehaviour BEH-1 \"a\"\n")));
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("on-change")]
    [InlineData("none")]
    public void Legacy_idle_and_on_change_and_none_read_as_nothing_switched_on(string value)
    {
        var text = $"# kit:idle {value}\nbehaviour BEH-1 \"a\"\n";

        Assert.Empty(IdleDirective.ReadOrder(text));
        Assert.Equal("idle", IdleDirective.Read(text));
    }

    [Theory]
    [InlineData("# kit:idle\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle sleepy\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle ideas now\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle health, health\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle health, sleepy\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle health,\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idled ideas\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:idle ideas\nbehaviour BEH-1 \"a\"\n")]
    public void A_malformed_or_duplicate_or_unknown_item_line_is_the_default(string text)
    {
        Assert.Equal(["health"], IdleDirective.ReadOrder(text));
    }

    [Fact]
    public void A_malformed_line_is_skipped_for_the_next_good_one()
    {
        Assert.Equal(["ideas"], IdleDirective.ReadOrder("# kit:idle nonsense\n# kit:idle ideas\nbehaviour BEH-1 \"a\"\n"));
    }

    [Fact]
    public void The_route_serves_the_order_and_the_first_item_as_json()
    {
        var (_, router) = Rig(false, "# kit:idle ideas, health\nbehaviour BEH-1 \"a\"\n");

        Assert.Equal("ideas,health", OrderOf(router));
        Assert.Equal("ideas", IdleOf(router));
        var json = JsonNode.Parse(new EngineJsonSerialiser().Serialise(router.Route("GET", "/api/settings/idle").Body!))!;
        Assert.Equal("ideas", json["idle"]!.GetValue<string>());
        Assert.Equal(["ideas", "health"], json["order"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void The_writer_adds_the_line_after_the_leading_directives_and_replaces_it_after()
    {
        var added = _writer.SetIdleOrder(Plain, ["ideas"]);

        Assert.True(added.Ok);
        Assert.Equal(Chosen, added.Text);
        Assert.Equal(Chosen.Replace("ideas", "ideas, health", StringComparison.Ordinal), _writer.SetIdleOrder(added.Text!, ["ideas", "health"]).Text);
        Assert.StartsWith("# kit:idle health, ideas\nbehaviour", _writer.SetIdleOrder("behaviour BEH-1 \"a\"\n", ["health", "ideas"]).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_writer_writes_none_when_everything_is_off()
    {
        var off = _writer.SetIdleOrder(Plain, []);

        Assert.True(off.Ok);
        Assert.Equal(Plain.Replace("\n\n", "\n# kit:idle none\n\n", StringComparison.Ordinal), off.Text);
        Assert.Empty(IdleDirective.ReadOrder(off.Text!));
    }

    [Fact]
    public void The_legacy_writer_maps_a_mode_to_an_order()
    {
        Assert.Equal(Chosen, _writer.SetIdle(Plain, "ideas").Text);
        Assert.Contains("# kit:idle none", _writer.SetIdle(Plain, "idle").Text, StringComparison.Ordinal);
        Assert.Contains("# kit:idle none", _writer.SetIdle(Plain, "on-change").Text, StringComparison.Ordinal);
        Assert.Equal("bad-mode", _writer.SetIdle(Plain, "sleepy").Error);
    }

    [Fact]
    public void Setting_what_it_already_says_changes_nothing()
    {
        Assert.Equal(Chosen, _writer.SetIdleOrder(Chosen, ["ideas"]).Text);
        Assert.Equal(Plain, _writer.SetIdleOrder(Plain, ["health"]).Text);
    }

    [Fact]
    public void A_duplicated_or_malformed_line_is_replaced_by_one_good_one()
    {
        var messy = "# kit:idle ideas\n# kit:idle nonsense\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n";

        var fixedUp = _writer.SetIdleOrder(messy, ["health", "ideas"]);

        Assert.Equal("# kit:idle health, ideas\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n", fixedUp.Text);
    }

    [Theory]
    [InlineData("sleepy")]
    [InlineData("health", "health")]
    [InlineData("health", "sleepy")]
    public void The_writer_refuses_an_unknown_or_duplicate_item(params string[] order)
    {
        var r = _writer.SetIdleOrder(Plain, order);

        Assert.False(r.Ok);
        Assert.Equal("bad-order", r.Error);
    }

    [Fact]
    public void The_operator_sets_the_order_over_the_route_and_the_file_changes()
    {
        var (dir, router) = Rig(false);
        var file = Path.Combine(dir, "kit.beh");

        var set = router.Route("POST", "/api/settings/idle", body: Json(new { order = new[] { "ideas", "health" } }));

        Assert.Equal(200, set.Status);
        Assert.Equal("ideas,health", OrderOf(router));
        Assert.Contains("# kit:idle ideas, health", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/settings/idle", body: Json(new { order = new[] { "ideas", "health" } })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:idle").Length - 1);

        Assert.Equal(200, router.Route("POST", "/api/settings/idle", body: Json(new { order = Array.Empty<string>() })).Status);
        Assert.Equal("idle", IdleOf(router));
        Assert.Contains("# kit:idle none", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void The_route_still_accepts_the_legacy_idle_mode()
    {
        var (dir, router) = Rig(false);

        Assert.Equal(200, router.Route("POST", "/api/settings/idle", body: Json(new { idle = "ideas" })).Status);
        Assert.Equal("ideas", OrderOf(router));

        Assert.Equal(200, router.Route("POST", "/api/settings/idle", body: Json(new { idle = "on-change" })).Status);
        Assert.Equal("", OrderOf(router));
        Assert.Contains("# kit:idle none", File.ReadAllText(Path.Combine(dir, "kit.beh")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_signed_in_user_who_is_not_the_operator_is_refused_and_nothing_is_written()
    {
        var (dir, router) = Rig(true);
        var ann = SignIn(router, "ann", AnnPassword);

        var refused = router.Route("POST", "/api/settings/idle", ann, body: Json(new { idle = "idle" }));

        Assert.Equal(403, refused.Status);
        Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));

        var him = SignIn(router, null, Operator);
        Assert.Equal(200, router.Route("POST", "/api/settings/idle", him, body: Json(new { idle = "idle" })).Status);
        Assert.Equal("idle", IdleOf(router));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"idle\":\"sleepy\"}")]
    [InlineData("{\"idle\":true}")]
    [InlineData("{\"idle\":null}")]
    [InlineData("{\"order\":\"health\"}")]
    [InlineData("{\"order\":[1]}")]
    [InlineData("{\"order\":[\"sleepy\"]}")]
    [InlineData("{\"order\":[\"health\",\"health\"]}")]
    public void A_bad_order_or_mode_is_a_400_and_nothing_is_written(string body)
    {
        var (dir, router) = Rig(false);

        var r = router.Route("POST", "/api/settings/idle", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));
    }
}
