using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// claude-code-bot#151, slice 2: a corpus directory holding <c>features/**/*.kit</c> serves the tree as one
/// app, and every edit Kit makes goes back to the one file it changed. A flat directory is untouched by it.
/// </summary>
public sealed class FeatureTreeDirectoryTests : IDisposable
{
    private static readonly CorpusWriter Writer = new(new CorpusParser());

    private static readonly Dictionary<string, string> Tree = new()
    {
        ["project.kit"] = "# kit:project tree\n",
        ["notes.md"] = "## 2026-11-25 a note\n",
        ["features/notes/README.md"] = "# Notes\n\nWhat a note is.\n",
        ["features/notes/note.kit"] = "behaviour BEH-NOTE-1 \"A note is kept\"\n  in FEAT-NOTES\n  when opens page:Home\n",
        ["features/notes/note.tests.json"] = "{\"BEH-NOTE-1\": [{\"file\": \"a.test.ts\", \"title\": \"kept\"}]}",
        ["features/pipeline/README.md"] = "# Pipeline\n",
        ["features/pipeline/triage/README.md"] = "# Triage\nin FEAT-PIPELINE\n",
        ["features/pipeline/triage/orch.kit"] = "\ufeff# kit:layer technical\r\nbehaviour BEH-ORCH-1 \"Triage reads notes\"\r\n  in FEAT-TRIAGE\r\n  when opens page:Home\r\n",
        ["features/pipeline/triage/orch.tests.json"] = "{\"BEH-ORCH-1\": [{\"file\": \"b.test.ts\", \"title\": \"reads\"}]}",
    };

    private readonly string root = Directory.CreateTempSubdirectory("kit-tree-dir-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    private string Kit => Path.Combine(root, ".kit");

    private CorpusDirectory Load(Dictionary<string, string> files)
    {
        foreach (var (rel, text) in files)
        {
            var full = Path.Combine(Kit, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, new System.Text.UTF8Encoding(false).GetBytes(text));
        }

        return new CorpusDirectory(Kit, root);
    }

    // Every file under .kit/, by path, as bytes.
    private Dictionary<string, byte[]> Snapshot() =>
        Directory.EnumerateFiles(Kit, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(Kit, f).Replace('\\', '/'), File.ReadAllBytes);

    private List<string> ChangedSince(Dictionary<string, byte[]> before)
    {
        var after = Snapshot();
        return after.Keys.Union(before.Keys)
            .Where(k => !before.TryGetValue(k, out var b) || !after.TryGetValue(k, out var a) || !a.SequenceEqual(b))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void A_tree_is_one_app_named_by_its_project_file_with_its_cards_tests_and_notes()
    {
        var d = Load(Tree);

        Assert.Equal(["tree"], d.Corpora());
        var c = new CorpusParser().ParseCorpus(d.Read("tree"), "tree");
        Assert.Equal(["BEH-NOTE-1", "BEH-ORCH-1"], c.Behaviours.Select(b => b.Id));
        Assert.Equal("technical", c.Behaviours.Single(b => b.Id == "BEH-ORCH-1").Layer);
        Assert.Contains("BEH-NOTE-1", d.ReadTestsText("tree"), StringComparison.Ordinal);
        Assert.Contains("BEH-ORCH-1", d.ReadTestsText("tree"), StringComparison.Ordinal);
        Assert.Equal(Tree["notes.md"], d.ReadNotesText("tree"));
    }

    [Fact]
    public void Editing_a_card_rewrites_only_that_cards_file()
    {
        var d = Load(Tree);
        var before = Snapshot();

        var edit = Writer.SetPriority(d.ReadText("tree"), "BEH-NOTE-1", 2);
        Assert.True(edit.Ok, edit.Reason);
        d.WriteText("tree", edit.Text!);

        Assert.Equal(["features/notes/note.kit"], ChangedSince(before));
        Assert.Contains("  priority 2", File.ReadAllText(Path.Combine(Kit, "features/notes/note.kit")), StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_a_card_in_a_BOM_and_CRLF_file_keeps_both_and_touches_no_other_file()
    {
        var d = Load(Tree);
        var before = Snapshot();

        var edit = Writer.SetPriority(d.ReadText("tree"), "BEH-ORCH-1", 3);
        Assert.True(edit.Ok, edit.Reason);
        d.WriteText("tree", edit.Text!);

        Assert.Equal(["features/pipeline/triage/orch.kit"], ChangedSince(before));
        // Bytes, not File.ReadAllText: that strips the BOM this test is about.
        var text = new System.Text.UTF8Encoding(false).GetString(File.ReadAllBytes(Path.Combine(Kit, "features/pipeline/triage/orch.kit")));
        Assert.StartsWith("\ufeff# kit:layer technical\r\n", text, StringComparison.Ordinal);
        Assert.Contains("priority 3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_a_card_and_setting_a_project_directive_each_touch_only_their_own_file()
    {
        var d = Load(Tree);
        var before = Snapshot();

        var removed = Writer.RemoveBehaviour(d.ReadText("tree"), "BEH-ORCH-1");
        Assert.True(removed.Ok, removed.Reason);
        d.WriteText("tree", removed.Text!);
        Assert.Equal(["features/pipeline/triage/orch.kit"], ChangedSince(before));
        Assert.DoesNotContain("BEH-ORCH-1", d.Read("tree"), StringComparison.Ordinal);

        before = Snapshot();
        var promote = Writer.SetPromote(d.ReadText("tree"), true);
        Assert.True(promote.Ok, promote.Reason);
        d.WriteText("tree", promote.Text!);
        Assert.Equal(["project.kit"], ChangedSince(before));
        Assert.Contains("# kit:promote self", File.ReadAllText(Path.Combine(Kit, "project.kit")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_card_lands_in_the_file_for_its_area_in_its_features_folder()
    {
        var d = Load(Tree);
        var before = Snapshot();

        var added = Writer.AddBehaviour(d.ReadText("tree"), "BEH-NOTE-2", "A note is dated", steps: ["in FEAT-NOTES", "when opens page:Home"]);
        Assert.True(added.Ok, added.Reason);
        d.WriteText("tree", added.Text!);

        Assert.Equal(["features/notes/note.kit"], ChangedSince(before));
        Assert.Contains("behaviour BEH-NOTE-2", File.ReadAllText(Path.Combine(Kit, "features/notes/note.kit")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_flat_directory_reads_and_writes_exactly_as_before_even_beside_a_features_folder_with_no_kit_file()
    {
        // `features/` with only a README is not a tree, so the flat app is all there is.
        const string flat = "\ufeff# kit:layer technical\r\nbehaviour BEH-A \"a\"\r\n  when opens page:Home\r\n";
        var d = Load(new Dictionary<string, string>
        {
            ["app.beh"] = flat,
            ["app.tests.json"] = "{}",
            ["features/notes/README.md"] = "# Notes\n",
        });
        var before = Snapshot();

        Assert.Equal(["app"], d.Corpora());
        Assert.Equal(flat, d.ReadText("app"));
        Assert.Equal("{}", d.ReadTestsText("app"));
        Assert.Equal(".kit/app.beh", d.RelativePath("app"));

        var edit = Writer.SetPriority(d.ReadText("app"), "BEH-A", 2);
        Assert.True(edit.Ok, edit.Reason);
        d.WriteText("app", edit.Text!);

        Assert.Equal(["app.beh"], ChangedSince(before));
        Assert.Equal(new System.Text.UTF8Encoding(false).GetBytes(edit.Text!), File.ReadAllBytes(Path.Combine(Kit, "app.beh")));
    }
}
