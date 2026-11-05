using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-HEALTH-4, second slice: the classifier the CLI and the Health page share, and the project view's
/// <c>links</c> — absent when nothing is recorded, the broken ones when Kit can look, and the reason when it cannot.
/// </summary>
public sealed class LinkHealthTests : IDisposable
{
    private const string Behaviour = "behaviour BEH-X-1 \"a thing\"\n  when opens page:Home\n";

    private readonly string _root = Directory.CreateTempSubdirectory("kit-link-health-").FullName;

    public LinkHealthTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, "corpora"));
        Write("src/a.cs", "class A {}\n");
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Write(string rel, string content) => File.WriteAllText(Path.Combine(_root, rel), content);

    private JsonObject Link(bool recorded = true, string? hash = null)
    {
        var link = new JsonObject { ["file"] = "src/a.cs", ["title"] = "X_works" };
        if (recorded)
        {
            link["verified"] = new JsonObject
            {
                ["hash"] = hash ?? LinkHasher.Hash(_root, ["src/a.cs"]),
                ["files"] = new JsonArray("src/a.cs"),
                ["on"] = "2026-11-01",
            };
        }

        return link;
    }

    private ProjectView View(JsonObject? link, string header = "# kit:repo o/kit@dev\n", SourceCheckout? checkout = null)
    {
        Write("corpora/app.beh", header + Behaviour);
        if (link is not null)
        {
            Write("corpora/app.tests.json", new JsonObject { ["BEH-X-1"] = new JsonArray(link) }.ToJsonString());
        }

        var viewer = new ProjectViewer(
            new Database.CorpusDirectory(Path.Combine(_root, "corpora"), RepoLayout.Root),
            new CorpusParser(),
            new BehaviourResolver(),
            new TestGenerator(),
            new ProjectReporter(),
            checkout: checkout);
        return (ProjectView)viewer.View("app");
    }

    [Fact]
    public void A_link_with_no_recorded_hash_is_unrecorded()
    {
        Assert.Equal(LinkState.Unrecorded, LinkVerification.Classify(Link(recorded: false), _root).State);
    }

    [Fact]
    public void A_link_whose_files_still_hash_the_same_is_verified()
    {
        Assert.Equal(LinkState.Verified, LinkVerification.Classify(Link(), _root).State);
    }

    [Fact]
    public void A_recorded_file_that_is_gone_is_possibly_broken_and_named()
    {
        var link = Link();
        File.Delete(Path.Combine(_root, "src/a.cs"));

        var verdict = LinkVerification.Classify(link, _root);

        Assert.Equal(LinkState.PossiblyBroken, verdict.State);
        Assert.Equal("src/a.cs is gone", verdict.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("../outside.cs")]
    [InlineData("/etc/hostname")]
    public void A_recorded_path_that_is_not_a_file_in_the_checkout_is_possibly_broken_not_a_throw(string? path)
    {
        var link = Link();
        link["verified"]!["files"] = path is null ? new JsonArray(7) : new JsonArray(path);

        Assert.Equal(LinkState.PossiblyBroken, LinkVerification.Classify(link, _root).State);
    }

    [Fact]
    public void A_changed_hash_is_possibly_broken_and_says_since_when()
    {
        var link = Link();
        Write("src/a.cs", "class A { int changed; }\n");

        var verdict = LinkVerification.Classify(link, _root);

        Assert.Equal(LinkState.PossiblyBroken, verdict.State);
        Assert.Equal("the code it exercises has changed since 2026-11-01", verdict.Reason);
    }

    [Fact]
    public void A_project_with_no_recorded_hash_has_no_links_key()
    {
        var view = View(Link(recorded: false));

        Assert.Null(view.Links);
        Assert.False(JsonNode.Parse(new EngineJsonSerialiser().Serialise(view))!.AsObject().ContainsKey("links"));
        Assert.Null(View(null).Links);
    }

    [Fact]
    public void Held_links_read_as_available_with_nothing_broken_and_how_many_were_checked()
    {
        var links = Assert.IsType<AvailableLinkHealth>(View(Link(), checkout: new SourceCheckout(_root, "O/Kit")).Links);

        Assert.True(links.Available);
        Assert.Equal(1, links.Recorded);
        Assert.Empty(links.Broken);
    }

    [Fact]
    public void A_changed_source_file_lists_the_link_with_its_behaviour_title_and_reason()
    {
        var link = Link();
        Write("src/a.cs", "class A { int changed; }\n");

        var links = Assert.IsType<AvailableLinkHealth>(View(link, checkout: new SourceCheckout(_root, "o/kit")).Links);

        var broken = Assert.Single(links.Broken);
        Assert.Equal("BEH-X-1", broken.BehaviourId);
        Assert.Equal("X_works", broken.Title);
        Assert.Equal("the code it exercises has changed since 2026-11-01", broken.Reason);
    }

    [Fact]
    public void Links_recorded_against_another_repo_are_unavailable_and_say_which()
    {
        var links = Assert.IsType<UnavailableLinkHealth>(View(Link(), "# kit:repo o/elsewhere@dev\n", new SourceCheckout(_root, "o/kit")).Links);

        Assert.False(links.Available);
        Assert.Equal("these links are recorded against o/elsewhere, and Kit only has a checkout of o/kit", links.Reason);
    }

    [Fact]
    public void With_no_known_repo_a_recorded_file_not_in_the_checkout_is_unavailable_not_broken()
    {
        var link = Link();
        File.Delete(Path.Combine(_root, "src/a.cs"));

        var links = Assert.IsType<UnavailableLinkHealth>(View(link, "", new SourceCheckout(_root, null)).Links);

        Assert.Contains("src/a.cs is not in Kit's checkout", links.Reason);
    }

    [Fact]
    public void With_no_checkout_at_all_the_links_are_unavailable()
    {
        Assert.IsType<UnavailableLinkHealth>(View(Link()).Links);
    }

    [Fact]
    public void The_project_list_does_not_read_source_files()
    {
        View(Link());
        var viewer = new ProjectViewer(
            new Database.CorpusDirectory(Path.Combine(_root, "corpora"), RepoLayout.Root),
            new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter(),
            checkout: new SourceCheckout(_root, "o/kit"));

        Assert.IsType<ProjectSummary>(viewer.Summary("app"));
    }
}
