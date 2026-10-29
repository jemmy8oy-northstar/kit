using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:promote self</c> (BEH-SETTINGS-4): the per-project grant for the agent to merge dev into
/// main itself — read into the project list, written by the operator alone, one commit-shaped
/// file edit at a time.
/// </summary>
public class PromoteTests
{
    private const string Plain = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Granted = "# kit:repo o/r@dev\n# kit:promote self\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router, ProjectViewer Viewer) Rig(bool accounts, params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-promote-").FullName;
        foreach (var (app, text) in files)
        {
            File.WriteAllText(Path.Combine(dir, app + ".beh"), text);
        }

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), accounts ? Operator : null,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: accounts ? users : null);
        return (dir, router, viewer);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string SignIn(KitRouter router, string? name, string password)
    {
        var r = router.Route("POST", "/api/session", body: Json(name is null ? new { password } : (object)new { username = name, password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static string? PromoteOf(ProjectViewer viewer, string app) => ((IProjectSummary)viewer.Summary(app)).Promote;

    [Fact]
    public void The_directive_is_read_into_the_summary_as_self()
    {
        var (_, _, viewer) = Rig(false, ("app", Granted));

        Assert.Equal("self", PromoteOf(viewer, "app"));
        Assert.Equal("self", JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("app")))!["promote"]!.GetValue<string>());
    }

    [Fact]
    public void Absent_means_no_key_at_all_never_false_or_null()
    {
        var (_, _, viewer) = Rig(false, ("app", Plain));

        Assert.Null(PromoteOf(viewer, "app"));
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("app")))!.AsObject().ContainsKey("promote"));
    }

    [Theory]
    [InlineData("# kit:promote\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:promote all\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:promoted self\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:promote self\nbehaviour BEH-1 \"a\"\n")]
    public void Only_the_exact_grant_counts(string text)
    {
        Assert.Null(PromoteOf(Rig(false, ("app", text)).Viewer, "app"));
    }

    [Fact]
    public void Only_the_primary_file_of_a_project_can_grant()
    {
        var (_, _, viewer) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\n# kit:promote self\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        Assert.Null(PromoteOf(viewer, "kit"));
        Assert.Null(PromoteOf(viewer, "kit-ui"));
    }

    [Fact]
    public void The_writer_adds_the_line_after_the_leading_directives_and_removing_restores_the_text()
    {
        var on = _writer.SetPromote("# kit:repo o/r@dev\n\n" + Plain, true);

        Assert.True(on.Ok);
        Assert.Equal(Granted, on.Text);
        Assert.Equal("# kit:repo o/r@dev\n\n" + Plain, _writer.SetPromote(on.Text!, false).Text);
        Assert.StartsWith("# kit:promote self\nbehaviour", _writer.SetPromote(Plain, true).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Setting_the_state_it_is_already_in_changes_nothing()
    {
        Assert.Equal(Granted, _writer.SetPromote(Granted, true).Text);
        Assert.Equal(Plain, _writer.SetPromote(Plain, false).Text);
    }

    [Fact]
    public void The_operator_turns_it_on_and_off_over_the_route()
    {
        var (dir, router, viewer) = Rig(false, ("app", Plain));
        var file = Path.Combine(dir, "app.beh");

        var on = router.Route("POST", "/api/projects/app/promote", body: Json(new { promote = true }));
        Assert.Equal(200, on.Status);
        Assert.Equal("self", PromoteOf(viewer, "app"));
        Assert.Contains("# kit:promote self", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/promote", body: Json(new { promote = true })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:promote").Length - 1);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/promote", body: Json(new { promote = false })).Status);
        Assert.Null(PromoteOf(viewer, "app"));
        Assert.Equal(Plain, File.ReadAllText(file));
    }

    [Fact]
    public void A_signed_in_user_who_is_not_the_operator_is_refused_and_nothing_is_written()
    {
        var (dir, router, viewer) = Rig(true, ("app", Plain));
        var ann = SignIn(router, "ann", AnnPassword);

        var refused = router.Route("POST", "/api/projects/app/promote", ann, body: Json(new { promote = true }));

        Assert.Equal(403, refused.Status);
        Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "app.beh")));
        Assert.Null(PromoteOf(viewer, "app"));

        var him = SignIn(router, null, Operator);
        Assert.Equal(200, router.Route("POST", "/api/projects/app/promote", him, body: Json(new { promote = true })).Status);
        Assert.Equal("self", PromoteOf(viewer, "app"));
    }

    [Fact]
    public void A_secondary_file_of_a_project_is_refused_saying_where_to_set_it()
    {
        var (dir, router, _) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        var r = router.Route("POST", "/api/projects/kit-ui/promote", body: Json(new { promote = true }));

        Assert.Equal(409, r.Status);
        Assert.Equal("not-primary", Assert.IsType<ApiError>(r.Body).Error);
        Assert.DoesNotContain("kit:promote", File.ReadAllText(Path.Combine(dir, "kit-ui.beh")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"promote\":\"yes\"}")]
    [InlineData("{\"promote\":null}")]
    public void The_body_must_say_true_or_false(string body)
    {
        var (_, router, _) = Rig(false, ("app", Plain));

        var r = router.Route("POST", "/api/projects/app/promote", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
    }
}
