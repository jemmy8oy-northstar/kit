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
    /// Kit serves every committed corpus, but CI's gates only check kit and kit-ui, so a typo in any
    /// other app's corpus shipped green and took that project's page down on hosted (whos-accountable,
    /// 2026-10-26: <c>BEH-LEADER-1"The core…</c>, a missing space).
    /// </summary>
    [Fact]
    public void Every_committed_corpus_parses()
    {
        var files = Directory.GetFiles(RepoLayout.Behaviours, "*.beh");
        Assert.True(files.Length >= 5, $"could not look: only {files.Length} corpus file(s)");

        var failures = new List<string>();
        foreach (var file in files)
        {
            try { new CorpusParser().ParseCorpus(File.ReadAllText(file), Path.GetFileName(file)); }
            catch (Exception e) { failures.Add($"{Path.GetFileName(file)}: {e.Message}"); }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
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

    private static readonly string Beh = Path.Combine(RepoLayout.Behaviours, "kit-ui.beh");
    private static readonly string Notes = Path.Combine(RepoLayout.Behaviours, "kit-ui.notes.md");
    private static readonly string Sched = Path.Combine(RepoLayout.Behaviours, "kit-ui.schedule");

    /// <summary>The lines of one behaviour: its header and the indented lines under it, up to the next top-level line.</summary>
    private static string Block(string text, string id)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith($"behaviour {id} ", StringComparison.Ordinal));
        Assert.True(start >= 0, $"no behaviour {id} in kit-ui.beh");

        var end = start + 1;
        while (end < lines.Length && (lines[end].Length == 0 || char.IsWhiteSpace(lines[end][0])))
        {
            end++;
        }

        return string.Join("\n", lines[start..end]);
    }

    /// <summary>The body of one dated section of kit-ui.notes.md, up to the next heading.</summary>
    private static string NoteSection(string heading)
    {
        var lines = File.ReadAllText(Notes).Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l == "## " + heading);
        Assert.True(start >= 0, $"no note '## {heading}' in kit-ui.notes.md");

        var end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("## ", StringComparison.Ordinal))
        {
            end++;
        }

        return string.Join("\n", lines[(start + 1)..end]);
    }

    private static IReadOnlyList<Balenthiran.Kit.Abstractions.DataModels.IBehaviour> Corpus() =>
        new CorpusParser().Parse(File.ReadAllText(Beh), "kit-ui.beh");

    // ---- BEH-USERS-5: the vault answer, recorded ---------------------------------------------

    [Fact]
    public void BEH_USERS_5_records_the_OCI_Vault_answer_with_its_source_note()
    {
        var card = Block(File.ReadAllText(Beh), "BEH-USERS-5");

        Assert.Contains("contract his answer (kit-ui.notes.md#2026-11-03-2337): OCI Vault for now", card);
        Assert.Contains("OCI Vault for now", NoteSection("2026-11-03 23:37Z"));
    }

    [Fact]
    public void BEH_USERS_5_says_Kit_writes_a_secret_but_can_never_read_it_back()
    {
        var card = Block(File.ReadAllText(Beh), "BEH-USERS-5");

        Assert.Contains("Kit can write a value but never read it back", card);
        Assert.Contains("cannot read one back", card);
    }

    [Fact]
    public void BEH_USERS_5_keeps_the_storage_choice_behind_a_seam()
    {
        var card = Block(File.ReadAllText(Beh), "BEH-USERS-5");

        Assert.Contains("The storage choice stays behind a seam", card);
    }

    // ---- BEH-USERS-5: its priority is below BEH-USERS-9's ---------------------------------------

    [Fact]
    public void BEH_USERS_5_is_pending_at_priority_3_below_BEH_USERS_9_as_the_owner_asked()
    {
        var corpus = Corpus();
        var secrets = corpus.Single(b => b.Id == "BEH-USERS-5");
        var agent = corpus.Single(b => b.Id == "BEH-USERS-9");

        Assert.True(secrets.Pending);
        Assert.Equal(3, secrets.Priority);
        Assert.Equal(2, agent.Priority);
        Assert.True(agent.Priority < secrets.Priority, "BEH-USERS-9 must be the sooner one");
    }

    // ---- BEH-USERS-9: pending at priority 2 and first on the schedule ---------------------------

    [Fact]
    public void BEH_USERS_9_is_pending_at_priority_2_and_stands_before_BEH_USERS_5_on_the_schedule()
    {
        var agent = Corpus().Single(b => b.Id == "BEH-USERS-9");
        var ids = Schedule.Parse(File.ReadAllText(Sched));

        Assert.True(agent.Pending);
        Assert.Equal(2, agent.Priority);
        Assert.Contains("BEH-USERS-9", ids);
        Assert.Contains("BEH-USERS-5", ids);
        Assert.True(ids.IndexOf("BEH-USERS-9") < ids.IndexOf("BEH-USERS-5"));
    }

    // ---- BEH-SCHED-2: a reorder changes only the order ------------------------------------------

    /// <summary>A built id may stay listed and shows as done (the schedule's own rule), so only duplicates and unknown ids are wrong.</summary>
    [Fact]
    public void BEH_SCHED_2_the_schedule_lists_each_id_once_and_each_is_a_behaviour_in_the_corpus()
    {
        var known = Corpus().Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var ids = Schedule.Parse(File.ReadAllText(Sched));
        var listed = File.ReadAllLines(Sched)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0])
            .ToList();

        Assert.Equal(listed.Count, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Contains(id, known));
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public void BEH_SCHED_2_a_reorder_of_the_real_schedule_keeps_the_same_ids(string action)
    {
        var before = Schedule.Parse(File.ReadAllText(Sched));

        var (after, error, _) = Schedule.Change(before, action, "BEH-USERS-9");

        Assert.Null(error);
        Assert.NotNull(after);
        Assert.Equal(before.OrderBy(x => x, StringComparer.Ordinal), after!.OrderBy(x => x, StringComparer.Ordinal));
    }
}
