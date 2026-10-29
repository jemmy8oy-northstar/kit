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

    private static string IdleOf(KitRouter router)
    {
        var r = router.Route("GET", "/api/settings/idle");
        Assert.Equal(200, r.Status);
        return ((IIdleSetting)Assert.IsType<IdleSetting>(r.Body)).Idle;
    }

    [Fact]
    public void No_line_means_the_default_which_is_test_health()
    {
        Assert.Equal("health", IdleDirective.Default);
        Assert.Equal("health", IdleDirective.Read(Plain));
        Assert.Equal("health", IdleOf(Rig(false).Router));
    }

    [Theory]
    [InlineData("on-change")]
    [InlineData("health")]
    [InlineData("ideas")]
    [InlineData("idle")]
    public void Each_of_the_four_modes_is_read_when_spelled_exactly(string mode)
    {
        Assert.Equal(mode, IdleDirective.Read($"# kit:idle {mode}\nbehaviour BEH-1 \"a\"\n"));
    }

    [Theory]
    [InlineData("# kit:idle\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle sleepy\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idle ideas now\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:idled ideas\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:idle ideas\nbehaviour BEH-1 \"a\"\n")]
    public void A_malformed_line_is_the_default(string text)
    {
        Assert.Equal("health", IdleDirective.Read(text));
    }

    [Fact]
    public void The_route_serves_the_chosen_mode_as_json()
    {
        var (_, router) = Rig(false, Chosen);

        Assert.Equal("ideas", IdleOf(router));
        Assert.Equal("ideas", JsonNode.Parse(new EngineJsonSerialiser().Serialise(router.Route("GET", "/api/settings/idle").Body!))!["idle"]!.GetValue<string>());
    }

    [Fact]
    public void The_writer_adds_the_line_after_the_leading_directives_and_replaces_it_after()
    {
        var added = _writer.SetIdle(Plain, "ideas");

        Assert.True(added.Ok);
        Assert.Equal(Chosen, added.Text);
        Assert.Equal(Chosen.Replace("ideas", "idle", StringComparison.Ordinal), _writer.SetIdle(added.Text!, "idle").Text);
        Assert.StartsWith("# kit:idle idle\nbehaviour", _writer.SetIdle("behaviour BEH-1 \"a\"\n", "idle").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Setting_what_it_already_says_changes_nothing()
    {
        Assert.Equal(Chosen, _writer.SetIdle(Chosen, "ideas").Text);
        Assert.Equal(Plain, _writer.SetIdle(Plain, "health").Text);
    }

    [Fact]
    public void A_duplicated_or_malformed_line_is_replaced_by_one_good_one()
    {
        var messy = "# kit:idle ideas\n# kit:idle nonsense\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n";

        var fixedUp = _writer.SetIdle(messy, "idle");

        Assert.Equal("# kit:idle idle\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n", fixedUp.Text);
    }

    [Fact]
    public void The_writer_refuses_a_mode_that_is_not_one_of_the_four()
    {
        var r = _writer.SetIdle(Plain, "sleepy");

        Assert.False(r.Ok);
        Assert.Equal("bad-mode", r.Error);
    }

    [Fact]
    public void The_operator_chooses_over_the_route_and_the_file_changes()
    {
        var (dir, router) = Rig(false);
        var file = Path.Combine(dir, "kit.beh");

        var set = router.Route("POST", "/api/settings/idle", body: Json(new { idle = "idle" }));

        Assert.Equal(200, set.Status);
        Assert.Equal("idle", IdleOf(router));
        Assert.Contains("# kit:idle idle", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/settings/idle", body: Json(new { idle = "idle" })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:idle").Length - 1);
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
    public void The_body_must_name_one_of_the_four_modes(string body)
    {
        var (dir, router) = Rig(false);

        var r = router.Route("POST", "/api/settings/idle", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));
    }
}
