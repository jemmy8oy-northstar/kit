using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Files committed beside the engine that only <c>kit.test.js</c> read: the question sheet he was
/// handed, the bindings files and the frozen goldens. Ported when the Node engine was deleted
/// (kit#119), because each of them outlives it.
/// </summary>
public class CommittedArtefactTests
{
    /// <summary>
    /// Someone answers a question and edits the corpus, and the sheet in <c>docs/</c> keeps asking
    /// it — or the sheet is hand-edited and the corpus never learns. Either way it reads as current
    /// while being stale. Checkable only because the sheet carries no timestamp.
    /// </summary>
    [Theory]
    [InlineData("james-habits-app", "james-habits-app@e75de89")]
    [InlineData("language-vocab", "language-vocab@8228db7")]
    public void The_committed_sheet_is_byte_identical_to_what_kit_sheet_produces_now(string corpus, string rev)
    {
        var committed = Path.Combine(RepoLayout.Root, "docs", "sheets", $"{corpus}.md");
        var report = new KitReport(new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter(), new QuestionSheet(), new CheckFileSystem(RepoLayout.Root));

        var run = report.Run([corpus, "--rev", rev, "--dir", RepoLayout.Behaviours], sheet: true);

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.ReadAllText(committed) == run.Stdout,
            $"docs/sheets/{corpus}.md is stale — re-run `kit sheet {corpus} --rev {rev}` and commit its output");
    }

    /// <summary>
    /// A bindings file with no <c>.beh</c> beside it is an ORPHAN — bindings for a corpus that no
    /// longer exists, which nothing else in Kit would ever mention. And the map is keyed by noun,
    /// or the generator's lookups miss without a word.
    /// </summary>
    [Fact]
    public void Every_bindings_file_is_keyed_by_noun_and_belongs_to_a_corpus_beside_it()
    {
        var files = Directory.GetFiles(RepoLayout.Behaviours, "*.bindings.json");
        Assert.True(files.Length >= 3, $"could not look: only {files.Length} bindings file(s)");

        var total = 0;
        foreach (var file in files)
        {
            var app = Path.GetFileName(file)[..^".bindings.json".Length];
            Assert.True(File.Exists(Path.Combine(RepoLayout.Behaviours, $"{app}.beh")),
                $"{app}.bindings.json has no corpus beside it — it binds nouns nothing can reference");

            var real = JsonNode.Parse(File.ReadAllText(file))!.AsObject().Where(kv => !kv.Key.StartsWith('_')).ToList();
            Assert.True(real.Count >= 1, $"{app}.bindings.json binds nothing — delete it rather than ship an empty one");
            foreach (var (noun, value) in real)
            {
                Assert.Matches(@"^[a-z]+:[A-Za-z][\w.]*$", noun);
                Assert.True(value is JsonObject, $"{app}: {noun} is not an object");
            }

            total += real.Count;
        }

        Assert.True(total >= 20, $"only {total} bindings across the estate to reason about");
    }

    /// <summary>
    /// The goldens are frozen now — nothing re-records them, so a corpus change means editing a
    /// golden by hand. A date pasted in makes it differ every day; an absolute path makes it pass
    /// here and fail on a runner. Both are quiet, so both are checked over every golden.
    /// </summary>
    [Fact]
    public void No_golden_carries_a_timestamp_or_an_absolute_path()
    {
        var goldens = Directory.GetFiles(RepoLayout.Conformance, "*.json", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(RepoLayout.Fixtures, "*.json", SearchOption.AllDirectories))
            .ToList();
        Assert.True(goldens.Count >= 10, $"could not look: only {goldens.Count} golden(s)");

        var offenders = goldens
            .Select(g => (g, text: File.ReadAllText(g)))
            .Where(x => Regex.IsMatch(x.text, @"\b20\d\d-\d\d-\d\dT\d\d:") || Regex.IsMatch(x.text, @"(/data|/home|/tmp|/Users)/"))
            .Select(x => Path.GetRelativePath(RepoLayout.Root, x.g))
            .ToList();
        Assert.True(offenders.Count == 0, $"these goldens carry a timestamp or an absolute path: {string.Join(", ", offenders)}");
    }
}
