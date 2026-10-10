using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// One named test per behaviour in <c>kit.beh</c> that <c>kit.tests.json</c> maps here — the tests
/// that mapping used to point at in <c>kit.test.js</c>, ported when the Node engine went (kit#119).
/// </summary>
/// <remarks>
/// The conformance goldens already prove these paths reproduce Node over whole outputs. That is
/// parity, not a claim about the behaviour: a mapping entry names the test that ASSERTS it, so each
/// one is asserted here by name. Every corpus is inline — a fixture read from <c>behaviours/</c>
/// would go red on the next edit to a live corpus (kit#182).
/// </remarks>
public class EngineBehaviourTests
{
    private const string Corpus =
        "# a header the parser does not keep\n"
        + "# and a second line of it\n"
        + "\n"
        + "behaviour BEH-EDIT-1 \"the first\"\n"
        + "  # a comment inside a block\n"
        + "  when opens page:Home\n"
        + "\n"
        + "# between the blocks\n"
        + "behaviour BEH-EDIT-2 \"the second\"\n"
        + "  when opens page:Home\n";

    private static readonly JsonObject Bind = new()
    {
        ["page:Home"] = new JsonObject { ["route"] = "./" },
        ["page:Editor"] = new JsonObject { ["route"] = "./editor/:id", ["urlPattern"] = "/editor/x$" },
        ["button:Go"] = new JsonObject { ["role"] = "button", ["name"] = "Go" },
    };

    private readonly CorpusParser _parser = new();

    private readonly BehaviourResolver _resolver = new();

    // ── kit check (BEH-GATE-*, BEH-READ-1) ────────────────────────────────────

    [Fact]
    public void The_gate_exits_1_when_a_behaviour_has_no_test_naming_it()
    {
        var run = Gate(new()
        {
            ["behaviours/app.beh"] = "behaviour BEH-1 \"tested\"\n  when opens page:Home\n\nbehaviour BEH-2 \"untested\"\n  when opens page:Home\n",
            ["behaviours/app.tests.json"] = "{\"BEH-1\": [{\"file\": \"a.spec.ts\", \"title\": \"one\"}]}",
            ["repo/a.spec.ts"] = "test('one', () => {});\n",
        });

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("✗ BEH-2: no test names this behaviour — \"untested\"", run.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void The_gate_exits_2_never_0_when_the_repo_has_no_test_files()
    {
        var run = Gate(new()
        {
            ["behaviours/app.beh"] = "behaviour BEH-1 \"a\"\n  when opens page:Home\n",
            ["behaviours/app.tests.json"] = "{}",
            ["repo/readme.md"] = "no tests here",
        });

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("cannot look: 0 test files under repo\n", run.Stderr);
    }

    [Fact]
    public void The_gate_refuses_a_mapping_naming_a_test_that_no_longer_exists()
    {
        var run = Gate(new()
        {
            ["behaviours/app.beh"] = "behaviour BEH-1 \"a\"\n  when opens page:Home\n",
            ["behaviours/app.tests.json"] = "{\"BEH-1\": [{\"file\": \"a.spec.ts\", \"title\": \"the old name\"}]}",
            ["repo/a.spec.ts"] = "test('the new name', () => {});\n",
        });

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("BEH-1: a.spec.ts has no test titled \"the old name\" — renamed or deleted?", run.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void The_gate_refuses_when_the_readers_two_counts_disagree()
    {
        // A test declared mid-line is invisible to the positional reader and visible to the count.
        var run = Gate(new()
        {
            ["behaviours/app.beh"] = "behaviour BEH-1 \"a\"\n  when opens page:Home\n",
            ["behaviours/app.tests.json"] = "{}",
            ["repo/a.spec.ts"] = "beforeEach(() => {}); test('hidden', () => {});\n",
        });

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("the two counts disagree, so the reader is losing or inventing tests", run.Stderr, StringComparison.Ordinal);
    }

    // ── generate (BEH-GEN-*) ──────────────────────────────────────────────────

    [Fact]
    public void An_unbound_noun_is_refused_and_the_missing_noun_is_named()
    {
        var test = Generate("behaviour A \"a\"\n  when activates button:Nope\n")[0];

        Assert.Contains("UNGENERATED", test.Code, StringComparison.Ordinal);
        Assert.Equal(["button:Nope"], test.Missing);
        Assert.Equal(1, test.Stats.Ungenerated);

        // Never a locator guessed from the noun's own name: that test would run and assert nothing.
        Assert.DoesNotContain("getByRole", test.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unsupplied_route_param_is_refused_rather_than_truncated()
    {
        var test = Generate("behaviour A \"a\"\n  when opens page:Editor\n")[0];
        Assert.Contains("UNGENERATED", test.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("goto", test.Code, StringComparison.Ordinal);

        // The control: the same route generates once something provides the param.
        var provided = Generate("behaviour A \"a\"\n  when opens page:Editor\nbehaviour B \"b\"\n  provides page:Editor.id = abc\n")[0];
        Assert.Contains("page.goto(\"./editor/abc\")", provided.Code, StringComparison.Ordinal);
    }

    // ── parse and resolve (BEH-PARSE-1, BEH-RESOLVE-*, BEH-ADJ-1) ─────────────

    [Fact]
    public void An_unrecognised_keyword_is_an_error_not_silently_dropped()
    {
        var e = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour B \"t\"\n  wibble page:Home"));
        Assert.Contains("unrecognised keyword", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hole_is_filled_by_a_different_behaviour()
    {
        var resolution = _resolver.Resolve(_parser.Parse(
            "behaviour A \"a\"\n  when fills form:Upload with ?fields\nbehaviour B \"b\"\n  provides form:Upload.fields = Email", "test.beh"));
        var a = resolution.Behaviours[0];

        Assert.Equal(["Email"], a.Filled!.Select(f => f.Value[0]));
        Assert.Equal(["B"], a.Filled![0].From);
        Assert.Empty(a.Open!);
    }

    [Fact]
    public void Two_behaviours_disagreeing_is_a_conflict_with_both_sides_named()
    {
        var resolution = _resolver.Resolve(_parser.Parse(
            "behaviour A \"a\"\n  provides form:U.fields = Email\nbehaviour B \"b\"\n  provides form:U.fields = Phone", "test.beh"));

        var conflict = Assert.Single(resolution.Conflicts);
        Assert.Equal(["A"], conflict.Holders);
        Assert.Equal("B", Assert.Single(conflict.Challengers).From);
    }

    [Fact]
    public void An_inference_defaults_to_unreviewed_without_anyone_writing_review()
    {
        var inferred = Assert.Single(_parser.Parse("behaviour BEH-1 \"a\"\n  source inferred tests/X.cs:name"));
        Assert.Equal("inferred", inferred.Source.Origin);
        Assert.Equal("unreviewed", inferred.Review.State);

        // The control: a version that marked EVERYTHING unreviewed would pass the half above.
        var defined = Assert.Single(_parser.Parse("behaviour BEH-1 \"a\"\n  actor visitor"));
        Assert.Equal("defined", defined.Source.Origin);
        Assert.Equal("approved", defined.Review.State);
    }

    // ── the writer (BEH-WRITE-*) ──────────────────────────────────────────────

    [Fact]
    public void The_writer_keeps_every_comment_byte_for_byte()
    {
        var r = new CorpusWriter(_parser).AddStep(Corpus, "BEH-EDIT-2", "then sees button:Save");
        Assert.True(r.Ok, r.Reason);

        static List<string> Comments(string t) => t.Split('\n').Where(l => l.TrimStart().StartsWith('#')).ToList();
        Assert.Equal(4, Comments(Corpus).Count);
        Assert.Equal(Comments(Corpus), Comments(r.Text!));
    }

    [Fact]
    public void The_writer_lands_a_step_at_the_end_of_its_own_block()
    {
        var r = new CorpusWriter(_parser).AddStep(Corpus, "BEH-EDIT-1", "then sees button:Save");
        Assert.True(r.Ok, r.Reason);

        var lines = r.Text!.Split('\n').ToList();
        var start = lines.FindIndex(l => l.StartsWith("behaviour BEH-EDIT-1 ", StringComparison.Ordinal));
        var next = lines.FindIndex(start + 1, l => l.StartsWith("behaviour ", StringComparison.Ordinal));
        var added = lines.IndexOf("  then sees button:Save");
        Assert.InRange(added, start + 1, next - 1);

        // The LAST content line of its block, so the blank separating the blocks is still blank.
        Assert.Equal(string.Empty, lines[added + 1].Trim());
    }

    [Fact]
    public void The_writer_refuses_an_edit_that_would_not_parse_and_hands_back_no_text()
    {
        foreach (var bad in new[] { "wibble sees button:Save", "source nonsense", "option \"only-a-label\"" })
        {
            var r = new CorpusWriter(_parser).AddStep(Corpus, "BEH-EDIT-1", bad);
            Assert.False(r.Ok, $"{bad} was accepted");
            Assert.Equal("would-not-parse", r.Error);
            Assert.Null(r.Text);
        }
    }

    [Fact]
    public void The_writer_refuses_an_edit_that_would_change_a_neighbouring_behaviour()
    {
        // A step that is itself a behaviour header would add a block beside its target.
        var r = new CorpusWriter(_parser).AddStep(Corpus, "BEH-EDIT-1", "behaviour BEH-NEW \"sneaky\"");
        Assert.False(r.Ok);
        Assert.Equal("collateral-change", r.Error);
        Assert.Null(r.Text);
    }

    [Fact]
    public void The_writer_has_no_path_to_git_or_a_process()
    {
        // Decision 2, checked rather than promised: the writer is text in, text out. A git
        // write-back lives in Database and is wired by the host, never reached from here.
        var source = File.ReadAllLines(Path.Combine(RepoLayout.Root, "backend", "Balenthiran.Kit.Services", "CorpusWriter.cs"))
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal));
        var forbidden = new Regex(@"System\.Diagnostics|\bProcess\b|LibGit2|\bIGit\w*|\bGit(Store|WriteBack)\b|\bgit\b|System\.IO\.File\b|\bFile\.", RegexOptions.CultureInvariant);

        Assert.Empty(source.Where(l => forbidden.IsMatch(l)));
        Assert.Equal(
            [typeof(Balenthiran.Kit.Abstractions.Services.ICorpusParser)],
            typeof(CorpusWriter).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
    }

    private static Balenthiran.Kit.Abstractions.DataModels.ICommandRun Gate(Dictionary<string, string> files)
    {
        var root = Directory.CreateTempSubdirectory("kit-gate-").FullName;
        try
        {
            foreach (var (rel, text) in files)
            {
                var path = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }

            return new KitCheck(new CorpusParser(), new BehaviourResolver(), new TestTitleReader(), new CheckFileSystem(root))
                .Run(["app", "--repo", "repo", "--dir", "behaviours"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private List<Balenthiran.Kit.Abstractions.DataModels.IGeneratedTest> Generate(string src)
    {
        var resolution = _resolver.Resolve(_parser.Parse(src, "test.beh"));
        var symbols = resolution.Symbols.ToDictionary(kv => kv.Key, kv => kv.Value);
        return resolution.Behaviours.Select(b => new TestGenerator().Generate(b, Bind, symbols)).ToList();
    }
}
