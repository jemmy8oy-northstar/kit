using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Features as folders under <c>.kit/features</c>: a folder is a feature (BEH-PARSE-5), every
/// README in a nested feature and every card in a feature's folder tags its parent (BEH-PARSE-6), and a
/// folder that tags its way into a loop or a duplicate id is refused as BEH-PARSE-2 says. The check
/// command reads the same tree, so a missing tag fails the gate (BEH-PARSE-6).
/// </summary>
/// <remarks>
/// claude-code-bot#151 (docs/design/feature-tree.md): a folder's feature id is <c>FEAT-</c> and its name
/// upper-cased, and the tree is read as one corpus (<see cref="FeatureTree"/>), so a card's own <c>in</c>
/// lines are its tags.
/// </remarks>
public class FeatureFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kit-features-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Paths are relative to `.kit/`. A `null` text is an empty folder; anything else is a file with that text.
    // Each call gets its own tree, so one call's folders never reach another's.
    private IParsedCorpus Read(params (string Path, string? Text)[] entries)
    {
        var kit = Path.Combine(_root, Guid.NewGuid().ToString("N"), ".kit");
        foreach (var (path, text) in entries)
        {
            var full = Path.Combine(kit, path);
            if (text is null)
            {
                Directory.CreateDirectory(full);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return FeatureTree.Parse(FeatureTree.ReadFiles(new CheckFileSystem(), kit), new CorpusParser());
    }

    private static CorpusParseException Refused(Action read) => Assert.Throws<CorpusParseException>(read);

    private static string Card(params string[] tags) =>
        "behaviour BEH-B \"b\"\n" + string.Concat(tags.Select(t => $"  in {t}\n")) + "  when opens page:Home\n";

    // ── BEH-PARSE-5: a folder is a feature ──────────────────────────────────

    [Fact]
    public void BEH_PARSE_5_a_kit_and_a_readme_under_feature_folders_parse_to_features_reported_from_folders()
    {
        var c = Read(
            ("features/alpha/README.md", "# Alpha\n\nAlpha is the first area."),
            ("features/alpha/beta/beta.kit", Card("FEAT-BETA")));

        Assert.Equal(["FEAT-ALPHA", "FEAT-BETA"], c.Features.Select(f => f.Id));
        Assert.Equal("Alpha", c.Features[0].Title);
        Assert.Empty(c.Features[0].In);
        Assert.Equal(["FEAT-ALPHA"], c.Features[1].In.Select(t => t.Id));
        Assert.Equal(["BEH-B"], c.Behaviours.Select(b => b.Id));
        Assert.Equal("features/alpha/beta/beta.kit:1", c.Behaviours[0].At);
    }

    [Fact]
    public void BEH_PARSE_5_a_readme_is_the_feature_description_and_is_never_parsed_as_a_spec()
    {
        var c = Read(("features/alpha/README.md", "# Alpha\nAlpha is the first area.\nbehaviour BEH-FAKE \"from a readme\"\n  when opens page:Home\n"));

        Assert.Empty(c.Behaviours);
        Assert.Equal("Alpha", c.Features.Single().Title);
    }

    [Fact]
    public void BEH_PARSE_5_an_empty_folder_under_features_is_skipped_and_does_not_fail_the_parse()
    {
        var c = Read(("features/empty/nested", null), ("features/alpha/README.md", "# Alpha"));

        Assert.Equal(["FEAT-ALPHA"], c.Features.Select(f => f.Id));
    }

    [Fact]
    public void BEH_PARSE_5_a_folder_holding_only_child_folders_is_a_feature_and_its_child_features_parse()
    {
        var c = Read(("features/alpha/beta/README.md", "# Beta\nin FEAT-ALPHA\nBeta describes itself."));

        Assert.Equal(["FEAT-ALPHA", "FEAT-BETA"], c.Features.Select(f => f.Id));
        Assert.Equal("alpha", c.Features[0].Title);
        Assert.Equal(["FEAT-ALPHA"], c.Features[1].In.Select(t => t.Id));
    }

    // ── BEH-PARSE-2: the refusals, for folder children ─────────────────────

    [Fact]
    public void BEH_PARSE_2_a_kit_or_readme_in_a_feature_folder_whose_in_names_no_feature_is_refused_naming_its_file_and_line()
    {
        var e = Refused(() => Read(
            ("features/alpha/README.md", "# Alpha"),
            ("features/alpha/beta/beta.kit", Card("FEAT-BETA", "FEAT-NOWHERE"))));

        Assert.Equal("BadFeatureTag", e.Refusal);
        Assert.Contains("features/alpha/beta/beta.kit:3", e.Message, StringComparison.Ordinal);
        Assert.Contains("FEAT-NOWHERE", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_2_a_folder_child_whose_in_makes_its_feature_its_own_ancestor_is_refused_naming_its_line()
    {
        // Alpha holds Beta, and Alpha's README tags Beta: Alpha is then its own ancestor.
        var e = Refused(() => Read(
            ("features/alpha/README.md", "# Alpha\nin FEAT-BETA"),
            ("features/alpha/beta/README.md", "# Beta\nin FEAT-ALPHA")));

        Assert.Equal("BadFeatureTag", e.Refusal);
        Assert.Contains("README.md", e.Message, StringComparison.Ordinal);
        Assert.Contains("its own ancestor", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_2_a_feature_id_declared_by_two_folders_or_by_a_folder_and_a_kit_file_is_refused_naming_both()
    {
        var twoFolders = Refused(() => Read(
            ("features/alpha/gamma/README.md", "# Gamma\nin FEAT-ALPHA"),
            ("features/beta/gamma/README.md", "# Gamma again\nin FEAT-BETA")));

        Assert.Equal("BadFeatureTag", twoFolders.Refusal);
        Assert.Contains("features/alpha/gamma/README.md", twoFolders.Message, StringComparison.Ordinal);
        Assert.Contains("features/beta/gamma/README.md", twoFolders.Message, StringComparison.Ordinal);

        var folderAndFile = Refused(() => Read(
            ("features/a/README.md", "# A"),
            ("features/other/spec.kit", "feature FEAT-A \"A\"\n")));

        Assert.Equal("BadFeatureTag", folderAndFile.Refusal);
        Assert.Contains("features/other/spec.kit:1", folderAndFile.Message, StringComparison.Ordinal);
        Assert.Contains("FEAT-A", folderAndFile.Message, StringComparison.Ordinal);
    }

    // ── BEH-PARSE-6: the parent tag ─────────────────────────────────────────

    [Fact]
    public void BEH_PARSE_6_a_kit_file_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file()
    {
        var e = Refused(() => Read(
            ("features/alpha/README.md", "# Alpha"),
            ("features/alpha/beta/beta.kit", Card())));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("features/alpha/beta/beta.kit:1", e.Message, StringComparison.Ordinal);
        Assert.Contains("in FEAT-BETA", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_readme_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file()
    {
        var e = Refused(() => Read(("features/alpha/beta/README.md", "# Beta, with no tag.")));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("features/alpha/beta/README.md", e.Message, StringComparison.Ordinal);
        Assert.Contains("in FEAT-ALPHA", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_kit_file_whose_only_in_names_a_feature_other_than_its_parent_is_refused_naming_the_file()
    {
        // Gamma exists, so the `in` is not a BadFeatureTag: it is just not the parent.
        var e = Refused(() => Read(
            ("features/gamma/README.md", "# Gamma"),
            ("features/alpha/README.md", "# Alpha"),
            ("features/alpha/beta/beta.kit", Card("FEAT-GAMMA"))));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("features/alpha/beta/beta.kit", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_child_that_tags_its_parent_and_another_feature_parses_and_the_extra_tag_is_display_only()
    {
        var c = Read(
            ("features/gamma/README.md", "# Gamma"),
            ("features/alpha/README.md", "# Alpha"),
            ("features/alpha/beta/README.md", "# Beta\nin FEAT-ALPHA\nin FEAT-GAMMA"),
            ("features/alpha/beta/beta.kit", Card("FEAT-BETA", "FEAT-GAMMA")));

        Assert.Equal(["FEAT-ALPHA", "FEAT-GAMMA"], c.Features.Single(f => f.Id == "FEAT-BETA").In.Select(t => t.Id));
        Assert.Equal(["FEAT-BETA", "FEAT-GAMMA"], c.Behaviours.Single().In.Select(t => t.Id));
        Assert.Empty(c.Behaviours.Single().DependsOn);
    }

    [Fact]
    public void BEH_PARSE_6_a_missing_parent_tag_fails_the_check_with_a_non_zero_exit_as_a_behaviour_with_no_test_does()
    {
        var files = new Dictionary<string, string>
        {
            ["repo/.kit/project.kit"] = "# kit:project app\n",
            ["repo/.kit/features/alpha/README.md"] = "# Alpha",
            ["repo/.kit/features/alpha/beta/beta.kit"] = Card(),
            ["repo/.kit/features/alpha/beta/beta.tests.json"] = "{\"BEH-B\": [{\"file\": \"a.spec.ts\", \"title\": \"one\"}]}",
            ["repo/a.spec.ts"] = "test('one', () => {});\n",
        };

        var run = Gate(files);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("features/alpha/beta/beta.kit", run.Stderr, StringComparison.Ordinal);

        // The same tree with the parent tag written: the gate passes, so the tag is what it refused.
        files["repo/.kit/features/alpha/beta/beta.kit"] = Card("FEAT-BETA");
        var pass = Gate(files);
        Assert.Equal(0, pass.ExitCode);
        Assert.Contains("1 behaviour(s) read from repo/.kit/features", pass.Stdout, StringComparison.Ordinal);
    }

    private static ICommandRun Gate(Dictionary<string, string> files)
    {
        var root = Directory.CreateTempSubdirectory("kit-gate-features-").FullName;
        try
        {
            foreach (var (rel, text) in files)
            {
                var path = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }

            return new KitCheck(new CorpusParser(), new BehaviourResolver(), new TestTitleReader(), new CheckFileSystem(root))
                .Run(["app", "--repo", "repo", "--dir", "repo/.kit"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
