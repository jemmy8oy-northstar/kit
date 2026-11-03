using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:depth goals|behaviours|architecture</c> (BEH-ARCH-3): how involved the user chooses to be —
/// read into the project list, written by the operator alone, and honoured by the spec agent and the
/// shared process. Behaviours, the default, is no line at all.
/// </summary>
public class DepthModeTests
{
    private const string Plain = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Goals = "# kit:repo o/r@dev\n# kit:depth goals\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router, ProjectViewer Viewer, DepthModel Model) Rig(bool accounts, params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-depth-").FullName;
        foreach (var (app, text) in files)
        {
            File.WriteAllText(Path.Combine(dir, app + ".beh"), text);
        }

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        var model = new DepthModel();
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), accounts ? Operator : null,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: accounts ? users : null,
            triage: model);
        return (dir, router, viewer, model);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string? DepthOf(ProjectViewer viewer, string app) => ((IProjectSummary)viewer.Summary(app)).Depth;

    private static JsonElement Chat() => Json(new { messages = new[] { new { from = "user", text = "hello" } } });

    [Fact]
    public void BEH_ARCH_3_goals_and_architecture_are_read_into_the_summary_and_behaviours_is_no_key()
    {
        var (_, _, viewer, _) = Rig(false, ("a", Goals), ("b", Goals.Replace("goals", "architecture", StringComparison.Ordinal)), ("c", Plain));

        Assert.Equal("goals", DepthOf(viewer, "a"));
        Assert.Equal("architecture", DepthOf(viewer, "b"));
        Assert.Equal("goals", JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("a")))!["depth"]!.GetValue<string>());
        Assert.Null(DepthOf(viewer, "c"));
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(viewer.Summary("c")))!.AsObject().ContainsKey("depth"));
    }

    [Theory]
    [InlineData("# kit:depth\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:depth deep\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:depthx goals\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# not kit:depth goals\nbehaviour BEH-1 \"a\"\n")]
    public void BEH_ARCH_3_only_an_exact_choice_counts_and_anything_else_is_behaviours(string text)
    {
        Assert.Null(DepthOf(Rig(false, ("app", text)).Viewer, "app"));
    }

    [Fact]
    public void BEH_ARCH_3_only_the_primary_file_of_a_project_can_choose()
    {
        var (_, _, viewer, _) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\n# kit:depth goals\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        Assert.Null(DepthOf(viewer, "kit"));
        Assert.Null(DepthOf(viewer, "kit-ui"));
    }

    [Fact]
    public void BEH_ARCH_3_the_writer_adds_replaces_and_removes_the_line_and_behaviours_restores_the_text()
    {
        var goals = _writer.SetDepth("# kit:repo o/r@dev\n\n" + Plain, "goals");
        Assert.True(goals.Ok);
        Assert.Equal(Goals, goals.Text);

        var architecture = _writer.SetDepth(goals.Text!, "architecture");
        Assert.Contains("# kit:depth architecture", architecture.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("depth goals", architecture.Text, StringComparison.Ordinal);

        Assert.Equal("# kit:repo o/r@dev\n\n" + Plain, _writer.SetDepth(architecture.Text!, "behaviours").Text);
        Assert.Equal(Goals, _writer.SetDepth(Goals, "goals").Text);
        Assert.Equal(Plain, _writer.SetDepth(Plain, "behaviours").Text);
    }

    [Fact]
    public void BEH_ARCH_3_a_depth_that_is_not_goals_behaviours_or_architecture_is_refused()
    {
        var r = _writer.SetDepth(Plain, "deep");

        Assert.False(r.Ok);
        Assert.Equal("bad-mode", r.Error);
    }

    [Fact]
    public void BEH_ARCH_3_the_operator_chooses_over_the_route_and_it_is_a_commit_shaped_edit()
    {
        var (dir, router, viewer, _) = Rig(false, ("app", Plain));
        var file = Path.Combine(dir, "app.beh");

        Assert.Equal(200, router.Route("POST", "/api/projects/app/depth", body: Json(new { mode = "architecture" })).Status);
        Assert.Equal("architecture", DepthOf(viewer, "app"));
        Assert.Contains("# kit:depth architecture", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/depth", body: Json(new { mode = "architecture" })).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:depth").Length - 1);

        Assert.Equal(200, router.Route("POST", "/api/projects/app/depth", body: Json(new { mode = "behaviours" })).Status);
        Assert.Null(DepthOf(viewer, "app"));
        Assert.Equal(Plain, File.ReadAllText(file));
    }

    [Fact]
    public void BEH_ARCH_3_a_signed_in_user_who_is_not_the_operator_is_refused_and_nothing_is_written()
    {
        var (dir, router, viewer, _) = Rig(true, ("app", Plain));
        var signIn = router.Route("POST", "/api/session", body: Json(new { username = "ann", password = AnnPassword }));
        var ann = signIn.SetCookie!.Split(';')[0];

        var refused = router.Route("POST", "/api/projects/app/depth", ann, body: Json(new { mode = "goals" }));

        Assert.Equal(403, refused.Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "app.beh")));
        Assert.Null(DepthOf(viewer, "app"));
    }

    [Fact]
    public void BEH_ARCH_3_a_secondary_file_of_a_project_is_refused_saying_where_to_set_it()
    {
        var (dir, router, _, _) = Rig(
            false,
            ("kit", "# kit:project kit\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        var r = router.Route("POST", "/api/projects/kit-ui/depth", body: Json(new { mode = "goals" }));

        Assert.Equal(409, r.Status);
        Assert.Equal("not-primary", Assert.IsType<ApiError>(r.Body).Error);
        Assert.DoesNotContain("kit:depth", File.ReadAllText(Path.Combine(dir, "kit-ui.beh")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"mode\":\"deep\"}")]
    [InlineData("{\"mode\":true}")]
    [InlineData("{\"mode\":null}")]
    public void BEH_ARCH_3_the_body_must_name_goals_behaviours_or_architecture(string body)
    {
        var (_, router, _, _) = Rig(false, ("app", Plain));

        var r = router.Route("POST", "/api/projects/app/depth", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
    }

    [Fact]
    public void BEH_ARCH_3_the_spec_agent_is_told_the_depth_a_project_chose_and_nothing_for_the_default()
    {
        var (_, router, _, model) = Rig(
            false, ("plain", Plain), ("goalsy", Goals), ("archy", Goals.Replace("goals", "architecture", StringComparison.Ordinal)));

        foreach (var app in new[] { "plain", "goalsy", "archy" })
        {
            Assert.Equal(200, router.Route("POST", $"/api/projects/{app}/chat", body: Chat()).Status);
        }

        Assert.Equal(TriageContext.ChatInstructions, model.Instructions[0]);
        Assert.Equal(
            TriageContext.ChatInstructions + " This user works at the level of goals: do not ask them to decide individual behaviours; propose them yourself, "
            + "saved as notes for the coding agent to file as unreviewed suggestions, and only ask about goals and priorities.",
            model.Instructions[1]);
        Assert.Equal(
            TriageContext.ChatInstructions + " This user also wants to decide architecture: raise stack and design choices with them rather than deciding them.",
            model.Instructions[2]);
    }

    [Fact]
    public void BEH_ARCH_3_a_secondary_app_of_a_project_is_told_the_depth_its_primary_file_chose()
    {
        var (_, router, _, model) = Rig(
            false,
            ("kit", "# kit:project kit\n# kit:depth goals\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n"),
            ("kit-ui", "# kit:project kit\nbehaviour BEH-2 \"b\"\n  when opens page:Home\n"));

        Assert.Equal(200, router.Route("POST", "/api/projects/kit-ui/chat", body: Chat()).Status);

        Assert.Contains("works at the level of goals", Assert.Single(model.Instructions), StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_ARCH_3_the_shared_process_tells_the_coding_agent_how_to_honour_the_depth()
    {
        var (_, router, _, _) = Rig(false, ("app", Plain));
        var text = System.Text.Encoding.UTF8.GetString(router.Route("GET", "/api/process").Raw!);

        Assert.Contains("## How involved the user is", text, StringComparison.Ordinal);
        Assert.Contains("`# kit:depth`", text, StringComparison.Ordinal);
        Assert.Contains("the user leaves the behaviours to you", text, StringComparison.Ordinal);
        Assert.Contains("unreviewed suggestions, and build them", text, StringComparison.Ordinal);
        Assert.Contains("Raise each", text, StringComparison.Ordinal);
        Assert.Contains("review request before you build", text, StringComparison.Ordinal);
    }

    private sealed class DepthModel : ITriageChat
    {
        public List<string?> Instructions { get; } = [];

        public string Model => "fake-model";

        public Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, bool actions = false, Func<IReadOnlyList<string>, string>? read = null, CancellationToken cancellationToken = default)
        {
            Instructions.Add(instructions);
            return Task.FromResult(new TriageReply("ok", null));
        }
    }
}
