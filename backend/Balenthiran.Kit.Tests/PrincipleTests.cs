using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:principle [global] &lt;Name&gt;: &lt;what it means&gt;</c> (kit#284 slice 5): read in order
/// into the project view, absent when a file declares none, and refused when malformed.
/// </summary>
public class PrincipleTests
{
    private const string Behaviour = "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n";

    private static ProjectViewer Viewer(string text)
    {
        var dir = Directory.CreateTempSubdirectory("kit-principle-").FullName;
        File.WriteAllText(Path.Combine(dir, "app.beh"), text);
        return new ProjectViewer(new Balenthiran.Kit.Database.CorpusDirectory(dir, RepoLayout.Root), new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
    }

    [Fact]
    public void Principles_are_read_in_order_with_their_scope_and_line()
    {
        var view = Viewer("# kit:principle global Readability: a human can read it\n#\n# kit:principle  Scale later :  one language for now\n" + Behaviour).View("app");

        Assert.Collection(
            view.Principles,
            p =>
            {
                Assert.Equal("Readability", p.Name);
                Assert.Equal("a human can read it", p.Text);
                Assert.True(p.Global);
                Assert.Equal("app.beh:1", p.At);
            },
            p =>
            {
                Assert.Equal("Scale later", p.Name);
                Assert.Equal("one language for now", p.Text);
                Assert.False(p.Global);
                Assert.Equal("app.beh:3", p.At);
            });
    }

    [Fact]
    public void A_file_with_no_principles_writes_no_principles_key()
    {
        var view = (ProjectView)Viewer(Behaviour).View("app");

        Assert.Empty(((Abstractions.DataModels.IProjectView)view).Principles);
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(view))!.AsObject().ContainsKey("principles"));
    }

    [Theory]
    [InlineData("# kit:principle Readability\n", "app.beh:1: kit:principle wants \"[global] <Name>: <what it means>\", got: Readability")]
    [InlineData("# kit:principle global Readability:\n", "app.beh:1: kit:principle wants \"[global] <Name>: <what it means>\", got: global Readability:")]
    [InlineData("#\n# kit:principle\n", "app.beh:2: kit:principle wants \"[global] <Name>: <what it means>\", got: ")]
    public void A_principle_that_does_not_say_what_it_means_is_refused(string header, string message)
    {
        var ex = Assert.Throws<ProjectionFailedException>(() => Viewer(header + Behaviour).View("app"));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void A_comment_that_only_mentions_principles_is_not_one()
    {
        var view = Viewer("# kit:principles are described on kit#284\n# see the kit:principle lines in kit.beh\n" + Behaviour).View("app");

        Assert.Empty(((Abstractions.DataModels.IProjectView)view).Principles);
    }
}
