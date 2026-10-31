using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:goal maintained</c> (BEH-NARR-2): a project with no goal is marked as kept going — read into
/// the project list, written by the operator alone, one commit-shaped file edit at a time.
/// </summary>
public class GoalTests
{
    private const string Plain = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Maintained = "# kit:repo o/r@dev\n# kit:goal maintained\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router, ProjectViewer Viewer) Rig(bool accounts, params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-goal-").FullName;
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

    private static string? GoalOf(ProjectViewer viewer, string app) => ((IProjectSummary)viewer.Summary(app)).Goal;

    [Fact]
    public void BEH_NARR_2_the_directive_is_read_into_the_summary_as_maintained()
    {
        var (_, _, viewer) = Rig(false, ("app", Maintained));

        Assert.Equal("maintained", GoalOf(viewer, "app"));
        Assert.Equal("maintained", JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("app")))!["goal"]!.GetValue<string>());
    }

    [Fact]
    public void BEH_NARR_2_absent_means_no_key_at_all()
    {
        var (_, _, viewer) = Rig(false, ("app", Plain));

        Assert.Null(GoalOf(viewer, "app"));
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("app")))!.AsObject().ContainsKey("goal"));
    }

    [Theory]
    [InlineData("# kit:goal\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:goal building\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:goals maintained\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:goal maintained\nbehaviour BEH-1 \"a\"\n")]
    public void BEH_NARR_2_only_the_exact_word_counts(string text)
    {
        Assert.Null(GoalOf(Rig(false, ("app", text)).Viewer, "app"));
    }

    [Fact]
    public void BEH_NARR_2_only_the_primary_file_of_a_project_can_mark_it()
    {
        var (_, _, viewer) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\n# kit:goal maintained\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        Assert.Null(GoalOf(viewer, "kit"));
        Assert.Null(GoalOf(viewer, "kit-ui"));
    }

    [Fact]
    public void BEH_NARR_2_the_writer_adds_the_line_after_the_leading_directives_and_removing_restores_the_text()
    {
        var on = _writer.SetGoalMaintained("# kit:repo o/r@dev\n\n" + Plain, true);

        Assert.True(on.Ok);
        Assert.Equal(Maintained, on.Text);
        Assert.Equal("# kit:repo o/r@dev\n\n" + Plain, _writer.SetGoalMaintained(on.Text!, false).Text);
        Assert.Equal(Plain, _writer.SetGoalMaintained(Plain, false).Text);
        Assert.Equal(Maintained, _writer.SetGoalMaintained(Maintained, true).Text);
    }

    [Fact]
    public void BEH_NARR_2_the_operator_marks_and_clears_it_over_the_route()
    {
        var (dir, router, viewer) = Rig(false, ("app", Plain));
        var file = Path.Combine(dir, "app.beh");

        Assert.Equal(200, router.Route("POST", "/api/projects/app/goal", body: Json(new { maintained = true })).Status);
        Assert.Equal("maintained", GoalOf(viewer, "app"));
        Assert.Contains("# kit:goal maintained", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/goal", body: Json(new { maintained = true })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:goal").Length - 1);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/goal", body: Json(new { maintained = false })).Status);
        Assert.Null(GoalOf(viewer, "app"));
        Assert.Equal(Plain, File.ReadAllText(file));
    }

    [Fact]
    public void BEH_NARR_2_a_signed_in_user_who_is_not_the_operator_is_refused_and_nothing_is_written()
    {
        var (dir, router, viewer) = Rig(true, ("app", Plain));
        var ann = SignIn(router, "ann", AnnPassword);

        var refused = router.Route("POST", "/api/projects/app/goal", ann, body: Json(new { maintained = true }));

        Assert.Equal(403, refused.Status);
        Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "app.beh")));
        Assert.Null(GoalOf(viewer, "app"));

        var him = SignIn(router, null, Operator);
        Assert.Equal(200, router.Route("POST", "/api/projects/app/goal", him, body: Json(new { maintained = true })).Status);
        Assert.Equal("maintained", GoalOf(viewer, "app"));
    }

    [Fact]
    public void BEH_NARR_2_a_secondary_file_of_a_project_is_refused_saying_where_to_set_it()
    {
        var (dir, router, _) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        var r = router.Route("POST", "/api/projects/kit-ui/goal", body: Json(new { maintained = true }));

        Assert.Equal(409, r.Status);
        Assert.Equal("not-primary", Assert.IsType<ApiError>(r.Body).Error);
        Assert.DoesNotContain("kit:goal", File.ReadAllText(Path.Combine(dir, "kit-ui.beh")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"maintained\":\"yes\"}")]
    [InlineData("{\"maintained\":null}")]
    public void BEH_NARR_2_the_body_must_say_true_or_false(string body)
    {
        var (_, router, _) = Rig(false, ("app", Plain));

        var r = router.Route("POST", "/api/projects/app/goal", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
    }
}
