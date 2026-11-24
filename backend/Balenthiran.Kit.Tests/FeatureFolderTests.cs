using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Features as folders under <c>.kit/features</c>: a folder is a feature (BEH-PARSE-5), every
/// kit or readme inside a nested feature tags its parent (BEH-PARSE-6), and a folder child that
/// tags its way into a loop or a duplicate id is refused as BEH-PARSE-2 says. The check command
/// reads the same tree, so a missing tag fails the gate (BEH-PARSE-6).
/// </summary>
public class FeatureFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kit-features-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // A `null` text is an empty folder; anything else is a file with that text.
    // Each call gets its own tree, so one call's folders never reach another's.
    private FeatureFolders Read(params (string Path, string? Text)[] entries)
    {
        var features = Path.Combine(_root, Guid.NewGuid().ToString("N"), ".kit", "features");
        Directory.CreateDirectory(features);
        foreach (var (path, text) in entries)
        {
            var full = Path.Combine(features, path);
            if (text is null)
            {
                Directory.CreateDirectory(full);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return new FeatureFolderReader(new CheckFileSystem(), new CorpusParser()).Read(features);
    }

    private static CorpusParseException Refused(Action read) => Assert.Throws<CorpusParseException>(read);

    private const string OneBehaviour = "behaviour BEH-B \"b\"\n  when opens page:Home\n";

    // ── BEH-PARSE-5: a folder is a feature ──────────────────────────────────

    [Fact]
    public void BEH_PARSE_5_a_kit_and_a_readme_under_feature_folders_parse_to_features_reported_from_folders()
    {
        var c = Read(
            ("Alpha/readme.md", "Alpha is the first area."),
            ("Alpha/Beta/beta.kit", "in Alpha\n\n" + OneBehaviour));

        Assert.Equal(["Alpha", "Beta"], c.Features.Select(f => f.Id));
        Assert.Null(c.Features[0].Parent);
        Assert.Equal("Alpha", c.Features[1].Parent);
        Assert.Equal(["BEH-B"], c.Behaviours.Select(b => b.Id));
    }

    [Fact]
    public void BEH_PARSE_5_a_readme_is_the_feature_description_and_is_never_parsed_as_a_spec()
    {
        var c = Read(("Alpha/readme.md", "Alpha is the first area.\nbehaviour BEH-FAKE \"from a readme\"\n  when opens page:Home\n"));

        Assert.Empty(c.Behaviours);
        Assert.Contains("Alpha is the first area.", c.Features[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_5_an_empty_folder_under_features_is_skipped_and_does_not_fail_the_parse()
    {
        var c = Read(("Empty/Nested", null), ("Alpha/readme.md", "Alpha."));

        Assert.Equal(["Alpha"], c.Features.Select(f => f.Id));
    }

    [Fact]
    public void BEH_PARSE_5_a_folder_holding_only_child_folders_is_a_feature_and_its_child_features_parse()
    {
        var c = Read(("Alpha/Beta/readme.md", "in Alpha\nBeta describes itself."));

        Assert.Equal(["Alpha", "Beta"], c.Features.Select(f => f.Id));
        Assert.Null(c.Features[0].Description);
        Assert.Equal("Alpha", c.Features[1].Parent);
    }

    // ── BEH-PARSE-2: the refusals, for folder children ─────────────────────

    [Fact]
    public void BEH_PARSE_2_a_kit_or_readme_in_a_feature_folder_whose_in_names_no_feature_is_refused_naming_its_file_and_line()
    {
        var e = Refused(() => Read(
            ("Alpha/readme.md", "Alpha."),
            ("Alpha/Beta/beta.kit", "in Alpha\nin Nowhere\n" + OneBehaviour)));

        Assert.Equal("BadFeatureTag", e.Refusal);
        Assert.Contains("Alpha/Beta/beta.kit:2", e.Message, StringComparison.Ordinal);
        Assert.Contains("Nowhere", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_2_a_folder_child_whose_in_makes_its_feature_its_own_ancestor_is_refused_naming_its_line()
    {
        // Alpha holds Beta, and Alpha's readme tags Beta: Alpha is then its own ancestor.
        var e = Refused(() => Read(
            ("Alpha/readme.md", "Alpha.\nin Beta"),
            ("Alpha/Beta/readme.md", "in Alpha\nBeta.")));

        Assert.Equal("BadFeatureTag", e.Refusal);
        Assert.Contains("Alpha/readme.md:2", e.Message, StringComparison.Ordinal);
        Assert.Contains("its own ancestor", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_2_a_feature_id_declared_by_two_folders_or_by_a_folder_and_a_kit_file_is_refused_naming_both()
    {
        var twoFolders = Refused(() => Read(
            ("Alpha/Gamma/readme.md", "in Alpha\nGamma."),
            ("Beta/Gamma/readme.md", "in Beta\nGamma again.")));

        Assert.Equal("BadFeatureTag", twoFolders.Refusal);
        Assert.Contains("Alpha/Gamma", twoFolders.Message, StringComparison.Ordinal);
        Assert.Contains("Beta/Gamma", twoFolders.Message, StringComparison.Ordinal);

        var folderAndFile = Refused(() => Read(
            ("FEAT-A/readme.md", "FEAT-A."),
            ("Other/spec.kit", "feature FEAT-A \"A\"\n\n" + OneBehaviour)));

        Assert.Equal("BadFeatureTag", folderAndFile.Refusal);
        Assert.Contains("Other/spec.kit:1", folderAndFile.Message, StringComparison.Ordinal);
        Assert.Contains("FEAT-A", folderAndFile.Message, StringComparison.Ordinal);
    }

    // ── BEH-PARSE-6: the parent tag ─────────────────────────────────────────

    [Fact]
    public void BEH_PARSE_6_a_kit_file_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file()
    {
        var e = Refused(() => Read(
            ("Alpha/readme.md", "Alpha."),
            ("Alpha/Beta/beta.kit", OneBehaviour)));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("Alpha/Beta/beta.kit", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_readme_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file()
    {
        var e = Refused(() => Read(("Alpha/Beta/readme.md", "Beta, with no tag.")));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("Alpha/Beta/readme.md", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_kit_file_whose_only_in_names_a_feature_other_than_its_parent_is_refused_naming_the_file()
    {
        // Gamma exists, so the `in` is not a BadFeatureTag: it is just not the parent.
        var e = Refused(() => Read(
            ("Gamma/readme.md", "Gamma."),
            ("Alpha/readme.md", "Alpha."),
            ("Alpha/Beta/beta.kit", "in Gamma\n" + OneBehaviour)));

        Assert.Equal("MissingParentTag", e.Refusal);
        Assert.Contains("Alpha/Beta/beta.kit", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_PARSE_6_a_child_that_tags_its_parent_and_another_feature_parses_and_the_extra_tag_is_display_only()
    {
        var c = Read(
            ("Gamma/readme.md", "Gamma."),
            ("Alpha/readme.md", "Alpha."),
            ("Alpha/Beta/beta.kit", "in Alpha\nin Gamma\n" + OneBehaviour));

        var beta = c.Features.Single(f => f.Id == "Beta");
        Assert.Equal("Alpha", beta.Parent);
        Assert.Equal(["Alpha", "Gamma"], beta.In.Select(t => t.Name));
        Assert.Empty(c.Behaviours.Single().DependsOn);
    }

    [Fact]
    public void BEH_PARSE_6_a_missing_parent_tag_fails_the_check_with_a_non_zero_exit_as_a_behaviour_with_no_test_does()
    {
        var files = new Dictionary<string, string>
        {
            ["behaviours/app.beh"] = "behaviour BEH-B \"b\"\n  when opens page:Home\n",
            ["behaviours/app.tests.json"] = "{\"BEH-B\": [{\"file\": \"a.spec.ts\", \"title\": \"one\"}]}",
            ["repo/a.spec.ts"] = "test('one', () => {});\n",
            ["repo/.kit/features/Alpha/readme.md"] = "Alpha.",
            ["repo/.kit/features/Alpha/Beta/beta.kit"] = OneBehaviour,
        };

        var run = Gate(files);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("Alpha/Beta/beta.kit", run.Stderr, StringComparison.Ordinal);

        // The same tree with the parent tag written: the gate passes, so the tag is what it refused.
        files["repo/.kit/features/Alpha/Beta/beta.kit"] = "in Alpha\n" + OneBehaviour;
        Assert.Equal(0, Gate(files).ExitCode);
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
                .Run(["app", "--repo", "repo", "--dir", "behaviours"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
