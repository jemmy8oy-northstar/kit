using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:conflicts build|ask|wait</c> (BEH-CONFLICT-2): what the agent does when specs conflict —
/// read into the project list, written by the operator alone, one commit-shaped file edit at a time.
/// Build, the default, is no line at all.
/// </summary>
public class ConflictModeTests
{
    private const string Plain = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Asking = "# kit:repo o/r@dev\n# kit:conflicts ask\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router, ProjectViewer Viewer) Rig(bool accounts, params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-conflicts-").FullName;
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

    private static string? ModeOf(ProjectViewer viewer, string app) => ((IProjectSummary)viewer.Summary(app)).OnConflict;

    [Fact]
    public void BEH_CONFLICT_2_ask_and_wait_are_read_into_the_summary_and_build_is_no_key()
    {
        var (_, _, viewer) = Rig(false, ("a", Asking), ("b", Asking.Replace("ask", "wait", StringComparison.Ordinal)), ("c", Plain));

        Assert.Equal("ask", ModeOf(viewer, "a"));
        Assert.Equal("wait", ModeOf(viewer, "b"));
        Assert.Equal("ask", JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("a")))!["onConflict"]!.GetValue<string>());
        Assert.Null(ModeOf(viewer, "c"));
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("c")))!.AsObject().ContainsKey("onConflict"));
    }

    [Theory]
    [InlineData("# kit:conflicts\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:conflicts never\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:conflictsx ask\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:conflicts ask\nbehaviour BEH-1 \"a\"\n")]
    public void BEH_CONFLICT_2_only_an_exact_choice_counts_and_anything_else_is_build(string text)
    {
        Assert.Null(ModeOf(Rig(false, ("app", text)).Viewer, "app"));
    }

    [Fact]
    public void BEH_CONFLICT_2_only_the_primary_file_of_a_project_can_choose()
    {
        var (_, _, viewer) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\n# kit:conflicts wait\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        Assert.Null(ModeOf(viewer, "kit"));
        Assert.Null(ModeOf(viewer, "kit-ui"));
    }

    [Fact]
    public void BEH_CONFLICT_2_the_writer_adds_replaces_and_removes_the_line_and_build_restores_the_text()
    {
        var ask = _writer.SetConflictMode("# kit:repo o/r@dev\n\n" + Plain, "ask");
        Assert.True(ask.Ok);
        Assert.Equal(Asking, ask.Text);

        var wait = _writer.SetConflictMode(ask.Text!, "wait");
        Assert.Contains("# kit:conflicts wait", wait.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("conflicts ask", wait.Text, StringComparison.Ordinal);

        Assert.Equal("# kit:repo o/r@dev\n\n" + Plain, _writer.SetConflictMode(wait.Text!, "build").Text);
        Assert.StartsWith("# kit:conflicts ask\nbehaviour", _writer.SetConflictMode(Plain, "ask").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_CONFLICT_2_choosing_what_the_file_already_says_changes_nothing()
    {
        Assert.Equal(Asking, _writer.SetConflictMode(Asking, "ask").Text);
        Assert.Equal(Plain, _writer.SetConflictMode(Plain, "build").Text);
    }

    [Fact]
    public void BEH_CONFLICT_2_a_mode_that_is_not_build_ask_or_wait_is_refused()
    {
        var r = _writer.SetConflictMode(Plain, "maybe");

        Assert.False(r.Ok);
        Assert.Equal("bad-mode", r.Error);
    }

    [Fact]
    public void BEH_CONFLICT_2_the_operator_chooses_over_the_route_and_it_is_a_commit_shaped_edit()
    {
        var (dir, router, viewer) = Rig(false, ("app", Plain));
        var file = Path.Combine(dir, "app.beh");

        Assert.Equal(200, router.Route("POST", "/api/projects/app/conflicts", body: Json(new { mode = "wait" })).Status);
        Assert.Equal("wait", ModeOf(viewer, "app"));
        Assert.Contains("# kit:conflicts wait", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/conflicts", body: Json(new { mode = "wait" })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:conflicts").Length - 1);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/conflicts", body: Json(new { mode = "build" })).Status);
        Assert.Null(ModeOf(viewer, "app"));
        Assert.Equal(Plain, File.ReadAllText(file));
    }

    [Fact]
    public void BEH_CONFLICT_2_a_signed_in_user_who_is_not_the_operator_is_refused_and_nothing_is_written()
    {
        var (dir, router, viewer) = Rig(true, ("app", Plain));
        var signIn = router.Route("POST", "/api/session", body: Json(new { username = "ann", password = AnnPassword }));
        var ann = signIn.SetCookie!.Split(';')[0];

        var refused = router.Route("POST", "/api/projects/app/conflicts", ann, body: Json(new { mode = "ask" }));

        Assert.Equal(403, refused.Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "app.beh")));
        Assert.Null(ModeOf(viewer, "app"));
    }

    [Fact]
    public void BEH_CONFLICT_2_a_secondary_file_of_a_project_is_refused_saying_where_to_set_it()
    {
        var (dir, router, _) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        var r = router.Route("POST", "/api/projects/kit-ui/conflicts", body: Json(new { mode = "ask" }));

        Assert.Equal(409, r.Status);
        Assert.Equal("not-primary", Assert.IsType<ApiError>(r.Body).Error);
        Assert.DoesNotContain("kit:conflicts", File.ReadAllText(Path.Combine(dir, "kit-ui.beh")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"mode\":\"maybe\"}")]
    [InlineData("{\"mode\":true}")]
    [InlineData("{\"mode\":null}")]
    public void BEH_CONFLICT_2_the_body_must_name_build_ask_or_wait(string body)
    {
        var (_, router, _) = Rig(false, ("app", Plain));

        var r = router.Route("POST", "/api/projects/app/conflicts", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
    }
}
