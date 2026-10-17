using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:idea</c> (kit#284 slice 4): the Ideas file is flagged in the project list and the
/// project view, and every other corpus says nothing new, so no golden changes shape.
/// </summary>
public class IdeaTests
{
    private static ProjectViewer Viewer(params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-idea-").FullName;
        foreach (var (app, text) in files)
        {
            File.WriteAllText(Path.Combine(dir, app + ".beh"), text);
        }

        return new ProjectViewer(new Balenthiran.Kit.Database.CorpusDirectory(dir, RepoLayout.Root), new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
    }

    private const string Idea = "# Ideas\n#\n# kit:idea\n#   kept off the board\nfeature FEAT-IDEA-A \"an idea\"\n\nbehaviour BEH-IDEA-A-1 \"it does a thing\"\n  in FEAT-IDEA-A\n  pending\n";
    private const string Plain = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";

    [Fact]
    public void A_kit_idea_file_is_flagged_in_the_list_and_the_view()
    {
        var viewer = Viewer(("ideas", Idea), ("app", Plain));

        Assert.True(((IProjectSummary)viewer.Summary("ideas")).Idea);
        Assert.True(viewer.View("ideas").Idea);
        Assert.False(((IProjectSummary)viewer.Summary("app")).Idea);
        Assert.False(viewer.View("app").Idea);
    }

    [Fact]
    public void A_corpus_that_is_not_an_idea_writes_no_idea_key()
    {
        var viewer = Viewer(("app", Plain));
        var serialiser = new EngineJsonSerialiser();

        Assert.False(JsonNode.Parse(serialiser.Serialise(viewer.Summary("app")))!.AsObject().ContainsKey("idea"));
        Assert.False(JsonNode.Parse(serialiser.Serialise((ProjectView)viewer.View("app")))!.AsObject().ContainsKey("idea"));
    }

    [Theory]
    [InlineData("# kit:ideas are on kit#284\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# see kit:idea-board\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# a comment that mentions # kit:idea mid-line\nbehaviour BEH-1 \"a\"\n")]
    public void Only_the_directive_itself_flags_an_idea(string text)
    {
        Assert.False(Viewer(("app", text)).View("app").Idea);
    }
}
