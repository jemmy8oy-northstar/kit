using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// claude-code-bot#151 (docs/design/feature-tree.md): a project's spec as folders of features under
/// <c>.kit/</c>, composed into one corpus, split back into its files, and health-checked for parent tags.
/// </summary>
public class FeatureTreeTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private static readonly (string Path, string Text)[] Tree =
    [
        ("project.kit", "# kit:project kit\n# kit:repo jemmy8oy-northstar/kit@dev\n"),
        ("features/pipeline/README.md", "# Agent pipeline\n\nNotes become tickets, tickets become pull requests.\n\nstage ui\n"),
        ("features/pipeline/triage/README.md", "# Triage\n\nin FEAT-PIPELINE\nin FEAT-NOTES\n\nReads notes against the cards.\n"),
        ("features/pipeline/triage/orch.kit", "# kit:layer technical\nbehaviour BEH-ORCH-1 \"Triage reads notes\"\n  in FEAT-TRIAGE\n  when opens page:Home\n"),
        ("features/notes/README.md", "# Notes\n"),
        ("features/notes/note.kit", "behaviour BEH-NOTE-1 \"A note is kept\"\n  in FEAT-NOTES\n  when opens page:Home"),
        ("features/notes/notes.tests.json", "{}"),
    ];

    [Fact]
    public void A_folder_is_a_feature_named_FEAT_and_its_name()
    {
        Assert.Equal("FEAT-AGENT-PIPELINE", FeatureTree.FeatureId("agent-pipeline"));
        Assert.Equal("FEAT-NOTES", FeatureTree.FeatureId("notes"));
    }

    [Fact]
    public void The_tree_composes_into_one_project_with_features_from_its_READMEs()
    {
        var c = _parser.ParseCorpus(FeatureTree.Compose(Tree), "kit");

        Assert.Equal("kit", c.Project);
        Assert.Equal(["FEAT-NOTES", "FEAT-PIPELINE", "FEAT-TRIAGE"], c.Features.Select(f => f.Id));
        var triage = c.Features.Single(f => f.Id == "FEAT-TRIAGE");
        Assert.Equal("Triage", triage.Title);
        Assert.Equal(["FEAT-PIPELINE", "FEAT-NOTES"], triage.In.Select(t => t.Id));
        Assert.Equal(["BEH-NOTE-1", "BEH-ORCH-1"], c.Behaviours.Select(b => b.Id));
    }

    [Fact]
    public void Every_location_names_its_own_file_and_line()
    {
        var c = _parser.ParseCorpus(FeatureTree.Compose(Tree), "kit");

        Assert.Equal("features/pipeline/triage/orch.kit:2", c.Behaviours.Single(b => b.Id == "BEH-ORCH-1").At);
        Assert.Equal("features/notes/note.kit:1", c.Behaviours.Single(b => b.Id == "BEH-NOTE-1").At);
    }

    [Fact]
    public void A_layer_holds_for_its_own_file_only()
    {
        var c = _parser.ParseCorpus(FeatureTree.Compose(Tree), "kit");

        Assert.Equal("technical", c.Behaviours.Single(b => b.Id == "BEH-ORCH-1").Layer);
        Assert.Equal("ux", c.Behaviours.Single(b => b.Id == "BEH-NOTE-1").Layer);
    }

    [Fact]
    public void A_duplicate_id_in_two_files_is_refused_naming_both()
    {
        var files = Tree.Append(("features/notes/more.kit", "behaviour BEH-NOTE-1 \"again\"\n  in FEAT-NOTES\n  when opens page:Home\n")).ToArray();

        var e = Assert.Throws<CorpusParseException>(() => _parser.ParseCorpus(FeatureTree.Compose(files), "kit"));
        Assert.Contains("features/notes/more.kit:1", e.Message);
        Assert.Contains("features/notes/note.kit:1", e.Message);
    }

    [Fact]
    public void An_unedited_tree_writes_nothing_back()
    {
        Assert.Empty(FeatureTree.Changes(Tree, FeatureTree.Compose(Tree)));
    }

    [Fact]
    public void An_edit_lands_in_the_file_of_the_card_it_changed()
    {
        var text = FeatureTree.Compose(Tree).Replace("\"A note is kept\"", "\"A note is kept forever\"");

        var change = Assert.Single(FeatureTree.Changes(Tree, text));
        Assert.Equal("features/notes/note.kit", change.Path);
        // A file with no final newline gets one when it changes; nothing else about it moves.
        Assert.Equal(Tree.Single(t => t.Path == change.Path).Text.Replace("kept", "kept forever") + "\n", change.Text);
    }

    [Fact]
    public void A_new_card_goes_to_the_file_for_its_area_in_the_folder_of_its_feature()
    {
        var text = FeatureTree.Compose(Tree) + "\nbehaviour BEH-ORCH-2 \"Triage runs again\"\n  in FEAT-TRIAGE\n  when opens page:Home\n";

        var change = Assert.Single(FeatureTree.Changes(Tree, text));
        Assert.Equal("features/pipeline/triage/orch.kit", change.Path);
        Assert.EndsWith("when opens page:Home\n\nbehaviour BEH-ORCH-2 \"Triage runs again\"\n  in FEAT-TRIAGE\n  when opens page:Home\n", change.Text);
    }

    [Fact]
    public void A_new_card_with_no_feature_is_refused_rather_than_guessed_into_a_file()
    {
        var text = FeatureTree.Compose(Tree) + "\nbehaviour BEH-ORCH-2 \"Triage runs again\"\n  when opens page:Home\n";

        Assert.Throws<NotWritableException>(() => FeatureTree.Changes(Tree, text));
    }

    [Fact]
    public void An_edit_to_a_README_segment_is_refused()
    {
        var text = FeatureTree.Compose(Tree).Replace("feature FEAT-NOTES \"Notes\"", "feature FEAT-NOTES \"Notebook\"");

        var e = Assert.Throws<NotWritableException>(() => FeatureTree.Changes(Tree, text));
        Assert.Contains("features/notes/README.md", e.Message);
    }

    [Fact]
    public void A_folder_with_no_README_is_a_feature_under_its_parent()
    {
        var files = new[] { ("features/a/b/x.kit", "behaviour BEH-X-1 \"x\"\n  in FEAT-B\n  when opens page:Home\n") };

        var c = _parser.ParseCorpus(FeatureTree.Compose(files), "p");
        Assert.Equal(["FEAT-A", "FEAT-B"], c.Features.Select(f => f.Id));
        Assert.Equal(["FEAT-A"], c.Features.Single(f => f.Id == "FEAT-B").In.Select(t => t.Id));
    }

    [Fact]
    public void The_health_check_names_a_README_or_card_that_does_not_tag_its_parent()
    {
        var files = new[]
        {
            ("features/pipeline/README.md", "# Pipeline\n"),
            ("features/pipeline/triage/README.md", "# Triage\n"),
            ("features/pipeline/triage/orch.kit", "behaviour BEH-ORCH-1 \"t\"\n  in FEAT-PIPELINE\n  when opens page:Home\n"),
        };
        var c = _parser.ParseCorpus(FeatureTree.Compose(files), "p");

        var problems = FeatureTree.Problems(files, c);
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.StartsWith("features/pipeline/triage/README.md:") && p.Contains("in FEAT-PIPELINE"));
        Assert.Contains(problems, p => p.StartsWith("features/pipeline/triage/orch.kit:1:") && p.Contains("in FEAT-TRIAGE"));
        Assert.Empty(FeatureTree.Problems(Tree, _parser.ParseCorpus(FeatureTree.Compose(Tree), "kit")));
    }

    [Fact]
    public void A_flat_corpus_with_no_file_line_parses_exactly_as_before()
    {
        const string flat = "# kit:layer technical\nbehaviour BEH-A-1 \"a\"\n  when opens page:Home\n";
        var c = _parser.ParseCorpus(flat, "kit.beh");

        Assert.Equal("kit.beh:2", c.Behaviours.Single().At);
        Assert.Equal("technical", c.Behaviours.Single().Layer);
        Assert.False(FeatureTree.IsTree(["kit.beh", "kit.tests.json"]));
        Assert.True(FeatureTree.IsTree(["project.kit", "features/notes/note.kit"]));
    }
}
