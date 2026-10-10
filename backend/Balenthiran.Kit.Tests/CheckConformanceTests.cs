using System.Text.Json.Nodes;
using Balenthiran.Kit.Cli;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>kit check</c> scored against NODE. <c>Fixtures/Check/goldens.json</c> is what
/// <c>check.js</c> printed and how it exited, as a real process, for every case — and what
/// <c>testTitles</c>/<c>expectedTestCount</c> returned for every source. The expected side never
/// passes through C#. Regenerate with <c>node prototypes/behaviour-ast/check-goldens.js</c>.
/// </summary>
/// <remarks>
/// The cases' files live inside the golden as strings and are written to a temp directory per
/// run: committed under their own names, a fixture <c>*.spec.ts</c> would be read by Kit's own
/// gate, and a fixture <c>*Tests.cs</c> would be compiled into this project.
/// </remarks>
public class CheckConformanceTests
{
    private static readonly JsonNode Golden =
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "Check", "goldens.json")))!;

    public static TheoryData<string> Checks()
    {
        var data = new TheoryData<string>();
        foreach (var c in Golden["checks"]!.AsArray())
        {
            data.Add(c!["name"]!.GetValue<string>());
        }

        return data;
    }

    public static TheoryData<int> Titles()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Golden["titles"]!.AsArray().Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    /// <summary>A theory over an empty population reports green. These are the floors the golden was recorded with.</summary>
    [Fact]
    public void The_golden_still_holds_its_cases()
    {
        Assert.True(Golden["checks"]!.AsArray().Count >= 29);
        Assert.True(Golden["titles"]!.AsArray().Count >= 37);
        var codes = Golden["checks"]!.AsArray().Select(c => c!["exitCode"]!.GetValue<int>()).ToHashSet();
        Assert.Equal([0, 1, 2], codes.Order());
    }

    [Theory]
    [MemberData(nameof(Checks))]
    public void The_gate_prints_and_exits_as_Node_did(string name)
    {
        var c = Golden["checks"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == name)!;
        var root = Directory.CreateTempSubdirectory("kit-check-").FullName;
        try
        {
            foreach (var (rel, text) in c["files"]!.AsObject())
            {
                var path = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text!.GetValue<string>());
            }

            var check = new KitCheck(new CorpusParser(), new BehaviourResolver(), new TestTitleReader(), new CheckFileSystem(root));
            var run = check.Run(c["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToList());

            // The one intended difference: the usage line names the command, not the script.
            var stderr = c["stderr"]!.GetValue<string>().Replace("usage: node check.js", "usage: kit check", StringComparison.Ordinal);
            Assert.Equal(c["stdout"]!.GetValue<string>(), run.Stdout);
            Assert.Equal(stderr, run.Stderr);
            Assert.Equal(c["exitCode"]!.GetValue<int>(), run.ExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(Titles))]
    public void The_reader_reads_titles_as_Node_did(int index)
    {
        var t = Golden["titles"]!.AsArray()[index]!;
        var file = t["file"]!.GetValue<string>();
        var src = t["src"]!.GetValue<string>();
        var reader = new TestTitleReader();

        var expected = t["titles"]!.AsArray()
            .Select(x => $"{x!["file"]}|{x["line"]}|{x["raw"]!.GetValue<string>()}|{x["style"]}")
            .ToList();
        var actual = reader.Titles(file, src).Select(x => $"{x.File}|{x.Line}|{x.Raw}|{x.Style}").ToList();

        Assert.Equal(expected, actual);
        Assert.Equal(t["expectedCount"]!.GetValue<int>(), reader.ExpectedCount(file, src));
    }

    [Fact]
    public void A_name_the_reader_accepts_is_one_Node_accepts()
    {
        var reader = new TestTitleReader();
        string[] yes = ["a.spec.ts", "a.test.tsx", "b.spec.js", "c.test.jsx", "WidgetTests.cs", "WidgetTest.cs"];
        string[] no = ["a.spec.mjs", "a.ts", "spec.ts", "Widgets.cs", "WidgetTests.cs.txt", "a.spec.ts\n"];
        Assert.All(yes, n => Assert.True(reader.IsTestFile(n), n));
        Assert.All(no, n => Assert.False(reader.IsTestFile(n), n));
    }

    [Fact]
    public void No_command_and_an_unknown_command_cannot_look()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, KitCli.Run([], output, error));
        Assert.StartsWith("usage: kit <command>", error.ToString(), StringComparison.Ordinal);

        error = new StringWriter();
        Assert.Equal(2, KitCli.Run(["chek", "kit"], output, error));
        Assert.StartsWith("unknown command chek\n", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>Where Node would crash with a stack trace, the port says it could not look.</summary>
    [Fact]
    public void A_mapping_that_is_not_an_object_cannot_look()
    {
        var root = Directory.CreateTempSubdirectory("kit-check-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "behaviours"));
            Directory.CreateDirectory(Path.Combine(root, "repo"));
            File.WriteAllText(Path.Combine(root, "behaviours", "app.beh"), "behaviour BEH-A1 \"a\"\n  when opens page:Home\n");
            File.WriteAllText(Path.Combine(root, "behaviours", "app.tests.json"), "[1, 2]");
            File.WriteAllText(Path.Combine(root, "repo", "a.spec.ts"), "test('[BEH-A1] a', () => {});\n");

            var run = new KitCheck(new CorpusParser(), new BehaviourResolver(), new TestTitleReader(), new CheckFileSystem(root))
                .Run(["app", "--repo", "repo", "--dir", "behaviours"]);

            Assert.Equal(2, run.ExitCode);
            Assert.Equal("cannot look: behaviours/app.tests.json is not a JSON object\n", run.Stderr);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
