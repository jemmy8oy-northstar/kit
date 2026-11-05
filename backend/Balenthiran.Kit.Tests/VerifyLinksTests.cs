using System.Text.Json.Nodes;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-HEALTH-4, first slice: the hash is a pure function of paths and contents, the coverage
/// report names the files a test ran, and <c>check</c> flags a link whose source moved. The
/// coverage run itself is a delegate here, so no test spawns <c>dotnet test</c>.
/// </summary>
public sealed class VerifyLinksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kit-verify-" + Guid.NewGuid().ToString("N"));

    public VerifyLinksTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, "tests"));
        Directory.CreateDirectory(Path.Combine(_root, "behaviours"));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Write(string rel, string content) => File.WriteAllText(Path.Combine(_root, rel), content);

    private string Report(params (string File, int Hits)[] classes) =>
        $"<coverage><sources><source>{_root}/</source></sources><packages><package><classes>"
        + string.Concat(classes.Select(c => $"<class filename=\"{c.File}\"><lines><line number=\"1\" hits=\"{c.Hits}\"/></lines></class>"))
        + "</classes></package></packages></coverage>";

    private string MapPath => Path.Combine(_root, "behaviours", "app.tests.json");

    private (int Code, string Out) Run(string sub, Func<string, string, string>? coverage = null)
    {
        var run = new KitVerifyLinks(coverage).Run([sub, "app", "--repo", _root, "--dir", Path.Combine(_root, "behaviours")]);
        return (run.ExitCode, run.Stdout + run.Stderr);
    }

    // One recorded link over src/a.cs, with a comment key and a second, .NET-less link either side of it.
    private void Recorded()
    {
        Write("src/a.cs", "class A {}\n");
        Write("src/b.cs", "class B {}\n");
        Write("behaviours/app.tests.json", "{\n  \"_\": \"a comment — kept\",\n  \"BEH-X-1\": [\n    {\n      \"file\": \"tests/XTests.cs\",\n      \"title\": \"X_works\"\n    }\n  ]\n}");
        Assert.Equal(0, Run("record", (_, _) => Report(("src/a.cs", 3), ("src/b.cs", 1))).Code);
    }

    [Fact]
    public void The_hash_is_the_same_whatever_order_the_paths_arrive_in()
    {
        Write("src/a.cs", "A");
        Write("src/b.cs", "B");

        Assert.Equal(LinkHasher.Hash(_root, ["src/a.cs", "src/b.cs"]), LinkHasher.Hash(_root, ["src/b.cs", "src/a.cs", "src/a.cs"]));
    }

    [Fact]
    public void A_Windows_checkout_hashes_the_same_as_a_Linux_one()
    {
        Write("src/a.cs", "one\ntwo\n");
        var lf = LinkHasher.Hash(_root, ["src/a.cs"]);
        Write("src/a.cs", "one\r\ntwo\r\n");

        Assert.Equal(lf, LinkHasher.Hash(_root, ["src/a.cs"]));
    }

    [Fact]
    public void A_change_to_the_content_or_the_path_changes_the_hash()
    {
        Write("src/a.cs", "one");
        Write("src/b.cs", "one");
        var a = LinkHasher.Hash(_root, ["src/a.cs"]);

        Assert.NotEqual(a, LinkHasher.Hash(_root, ["src/b.cs"]));
        Write("src/a.cs", "two");
        Assert.NotEqual(a, LinkHasher.Hash(_root, ["src/a.cs"]));
    }

    [Fact]
    public void Only_files_with_a_line_that_ran_count_as_exercised()
    {
        Write("src/a.cs", "A");
        Write("src/b.cs", "B");

        var files = LinkHasher.ExercisedFiles(Report(("src/a.cs", 2), ("src/b.cs", 0)), _root);

        Assert.Equal(["src/a.cs"], files);
    }

    [Fact]
    public void Build_output_and_files_outside_the_repo_are_not_hashed()
    {
        Write("src/a.cs", "A");
        Directory.CreateDirectory(Path.Combine(_root, "obj"));
        Write("obj/Gen.cs", "G");

        var files = LinkHasher.ExercisedFiles(Report(("src/a.cs", 1), ("obj/Gen.cs", 1), ("../elsewhere.cs", 1)), _root);

        Assert.Equal(["src/a.cs"], files);
    }

    [Fact]
    public void Record_writes_the_hash_and_files_beside_the_link_and_leaves_the_rest()
    {
        Recorded();

        var map = JsonNode.Parse(File.ReadAllText(MapPath))!.AsObject();
        var link = map["BEH-X-1"]![0]!;
        Assert.Equal("a comment — kept", (string?)map["_"]);
        Assert.Equal("X_works", (string?)link["title"]);
        Assert.Equal(LinkHasher.Hash(_root, ["src/a.cs", "src/b.cs"]), (string?)link["verified"]!["hash"]);
        Assert.Equal(["src/a.cs", "src/b.cs"], link["verified"]!["files"]!.AsArray().Select(f => (string?)f));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", (string?)link["verified"]!["on"]);
        Assert.StartsWith("{\n  \"_\": \"a comment — kept\",\n  \"BEH-X-1\": [\n    {\n      \"file\"", File.ReadAllText(MapPath));
        Assert.False(File.ReadAllText(MapPath).EndsWith('\n'));
    }

    [Fact]
    public void Check_reads_an_untouched_link_as_verified_and_exits_0()
    {
        Recorded();

        var (code, output) = Run("check");

        Assert.Equal(0, code);
        Assert.Contains("verified", output);
        Assert.DoesNotContain("POSSIBLY BROKEN", output);
    }

    [Fact]
    public void Check_flags_a_link_whose_source_changed_and_exits_1()
    {
        Recorded();
        Write("src/b.cs", "class B { int changed; }\n");

        var (code, output) = Run("check");

        Assert.Equal(1, code);
        Assert.Contains("POSSIBLY BROKEN", output);
    }

    [Fact]
    public void Check_flags_a_link_whose_source_file_is_gone_and_exits_1()
    {
        Recorded();
        File.Delete(Path.Combine(_root, "src/a.cs"));

        var (code, output) = Run("check");

        Assert.Equal(1, code);
        Assert.Contains("src/a.cs is gone", output);
    }

    [Fact]
    public void Check_reports_a_link_with_no_hash_as_unrecorded_without_failing()
    {
        Write("behaviours/app.tests.json", "{ \"BEH-X-1\": { \"file\": \"tests/XTests.cs\", \"title\": \"X_works\" } }");

        var (code, output) = Run("check");

        Assert.Equal(0, code);
        Assert.Contains("unrecorded", output);
    }

    [Fact]
    public void Check_exits_2_when_there_is_no_mapping_to_read()
    {
        var (code, output) = Run("check");

        Assert.Equal(2, code);
        Assert.Contains("cannot look", output);
    }
}
