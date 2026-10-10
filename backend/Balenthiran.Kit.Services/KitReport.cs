using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c>kit report</c> and <c>kit sheet</c> — port of <c>kit.js</c>'s command line. Output and exit codes
/// are Node's byte for byte (<c>Fixtures/Check/report-goldens.json</c>), but for the command's name.
/// </summary>
/// <remarks>
/// Where Node would CRASH with a stack trace — a corpus that does not parse, a bindings file that
/// is not a JSON object — this answers exit 2 with a <c>cannot look</c> line. No golden records a crash.
/// </remarks>
public sealed class KitReport(
    ICorpusParser parser,
    IBehaviourResolver resolver,
    ITestGenerator generator,
    IProjectReporter reporter,
    IQuestionSheet sheets,
    ICheckFileSystem disk) : IKitReport
{
    public const string Usage = "usage: kit report|sheet [<corpus-name>] [--rev <rev>] [--dir <corpus-dir>]";

    private const string BindingsSuffix = ".bindings.json";

    // Node's default was the script's own `behaviours/`; a CLI has no script directory.
    private const string DefaultDir = "behaviours";

    private static readonly HashSet<string> ValueFlags = new(StringComparer.Ordinal) { "--rev", "--dir" };

    /// <inheritdoc />
    public ICommandRun Run(IReadOnlyList<string> args, bool sheet)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var code = Main(args, sheet, stdout, stderr);
        return new CommandRun { ExitCode = code, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
    }

    private static bool LooksLikeAFlag(string s) => s.Length > 1 && s[0] == '-';

    // 🔑 A NAME THAT NAMES TWO CORPORA IS A REFUSAL, never a silent merge: an exact name wins, then a
    // unique substring, else refuse and name the candidates. `kit report kit` once merged kit.beh and
    // kit-ui.beh and turned kit.beh's load-bearing 0% into 16% without a word. An EMPTY name is a name
    // given and empty — not "no name" — or `"$CORPUS"` unset reports on everything.
    private static (List<string> Files, string? Kind, string? Error) SelectCorpora(IEnumerable<string> entries, string? only)
    {
        var beh = entries.Where(f => f.EndsWith(".beh", StringComparison.Ordinal)).ToList();
        if (only is null)
        {
            return (beh, null, null);
        }

        if (only.Length == 0)
        {
            return ([], "empty", "an empty corpus name was given — name one, or pass no name at all to report on every corpus");
        }

        var exact = beh.Where(f => f == $"{only}.beh").ToList();
        if (exact.Count > 0)
        {
            return (exact, null, null);
        }

        var matches = beh.Where(f => f.Contains(only, StringComparison.Ordinal)).ToList();
        if (matches.Count == 0)
        {
            return ([], "none", $"no corpus matching \"{only}\"");
        }

        if (matches.Count > 1)
        {
            var names = string.Join(", ", matches.Select(f => f[..^".beh".Length]));
            return ([], "ambiguous", $"\"{only}\" names {matches.Count} corpora — {names}. Name one: a measurement merged over unrelated corpora tells you about neither");
        }

        return (matches, null, null);
    }

    // `bindings.js`'s corpusOf: `at` is `<file>:<line>`, the only per-behaviour provenance. Never by
    // id — ids collide across corpora (kit.beh and kit-ui.beh both use BEH-UI-*).
    private static string? CorpusOf(IBehaviour b)
    {
        // `slice(0, lastIndexOf(':'))`: with no colon that is slice(0, -1), the last character dropped.
        var colon = b.At.LastIndexOf(':');
        var file = b.At[..(colon >= 0 ? colon : Math.Max(b.At.Length - 1, 0))];
        return file.EndsWith(".beh", StringComparison.Ordinal) ? file[..^".beh".Length] : null;
    }

    private static List<string> NounsOf(Behaviour b) =>
        b.Steps.SelectMany(s => s.Refs).Where(r => r.Kind != "literal").Select(r => $"{r.Kind}:{r.Name}").ToList();

    // `Math.round`: halves go UP, where .NET's default goes to even.
    private static string Percent(int part, int whole)
    {
        var ratio = part / (double)whole * 100;
        return double.IsNaN(ratio) ? "NaN" : Math.Floor(ratio + 0.5).ToString(CultureInfo.InvariantCulture);
    }

    private int Main(IReadOnlyList<string> argv, bool sheetMode, StringBuilder stdout, StringBuilder stderr)
    {
        void Out(string s) => stdout.Append(s).Append('\n');
        void Err(string s) => stderr.Append(s).Append('\n');

        // Positional, and every unknown flag a refusal: a dropped `--dir` once produced a confident
        // report about Kit's own corpus while naming yours.
        string? only = null;
        string? dirArg = null;
        var rev = string.Empty;
        var help = false;
        for (var i = 0; i < argv.Count; i++)
        {
            var a = argv[i];
            string? error = null;
            if (a == "--help" || a == "-h")
            {
                help = true;
            }
            else if (ValueFlags.Contains(a))
            {
                var v = i + 1 < argv.Count ? argv[i + 1] : null;
                if (v is null || LooksLikeAFlag(v))
                {
                    error = $"{a} needs a value";
                }
                else
                {
                    if (a == "--dir")
                    {
                        dirArg = v;
                    }
                    else
                    {
                        rev = v;
                    }

                    i++;
                }
            }
            else if (LooksLikeAFlag(a))
            {
                error = $"unknown option {a}";
            }
            else if (only is null)
            {
                only = a;
            }
            else
            {
                error = $"two corpus names given, \"{only}\" and \"{a}\" — this reports on one";
            }

            if (error is not null)
            {
                Err($"cannot look: {error}");
                Err(Usage);
                return 2;
            }
        }

        // Answered before anything is read, so it works where the corpus does not parse. Exit 0:
        // asking for help is not an error.
        if (help)
        {
            Out(Usage);
            return 0;
        }

        var dir = disk.Resolve(string.IsNullOrEmpty(dirArg) ? DefaultDir : dirArg);
        if (!disk.IsDirectory(dir))
        {
            Err($"cannot look: no corpus directory {dir}");
            return 2;
        }

        var entries = disk.Entries(dir).Select(e => e.Key).ToList();
        var (files, kind, refusal) = SelectCorpora(entries, only);
        if (refusal is not null)
        {
            // "Nothing is there" names the directory; "that names three" must not.
            Err($"cannot look: {refusal}{(kind == "none" ? $" in {dir}" : string.Empty)}");
            return 2;
        }

        if (files.Count == 0)
        {
            Err($"cannot look: no corpus at all in {dir}");
            return 2;
        }

        IResolution resolution;
        Dictionary<string, JsonObject> byApp;
        try
        {
            var all = files.SelectMany(f => parser.Parse(disk.ReadText(Path.Combine(dir, f)), f)).ToList();

            // One map per corpus, never a merged one: a behaviour generates against its OWN corpus's
            // bindings, so two corpora using one noun name cannot reach each other (kit#66).
            byApp = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var f in entries.Where(f => f.EndsWith(".beh", StringComparison.Ordinal)))
            {
                var app = f[..^".beh".Length];
                var path = Path.Combine(dir, app + BindingsSuffix);
                byApp[app] = disk.Exists(path)
                    ? JsonNode.Parse(disk.ReadText(path)) as JsonObject ?? throw new JsonException($"{app}{BindingsSuffix} is not a JSON object")
                    : [];
            }

            resolution = resolver.Resolve(all);
        }
        catch (Exception e) when (e is CorpusParseException or JsonException)
        {
            Err($"cannot look: {e.Message}");
            return 2;
        }

        var behaviours = resolution.Behaviours;
        var conflicts = resolution.Conflicts;

        // A file that parses to nothing is a different statement from no file: refuse rather than
        // print a table of zeros ending in NaN%.
        if (behaviours.Count == 0)
        {
            Err($"cannot look: {string.Join(", ", files)} parsed to 0 behaviours — nothing to report on yet");
            return 2;
        }

        var merged = reporter.Report(behaviours, conflicts, []);

        if (sheetMode)
        {
            // A sheet over a corpus that fails its own link check would present broken claims as questions.
            if (merged.Surface.Errors.Count > 0)
            {
                Err("── broken links — refusing to render a sheet over them ──");
                foreach (var e in merged.Surface.Errors)
                {
                    Err($"  {e}");
                }

                return 1;
            }

            var asked = sheets.Asked(behaviours, conflicts);
            var questionErrors = sheets.Errors(merged.Questions.Concat(asked));
            if (questionErrors.Count > 0)
            {
                Err("── incomplete questions (exit 1) ──");
                Err("  A half-written decision is worse than a missing one: it looks worked through.");
                foreach (var e in questionErrors)
                {
                    Err($"  {e}");
                }

                return 1;
            }

            var name = files.Count == 1 ? files[0][..^".beh".Length] : only;
            stdout.Append(sheets.Render(string.IsNullOrEmpty(name) ? "all" : name, merged.Questions, asked, rev));
            return 0;
        }

        // Counted PER CORPUS and summed: `region:Main` in two corpora is two nouns owned by two
        // projects, and the denominator is binding TARGETS — requires' population (kit#76).
        var perApp = new List<KeyValuePair<string, List<IBehaviour>>>();
        foreach (var b in behaviours)
        {
            var app = CorpusOf(b) ?? "<inline>";
            var group = perApp.FirstOrDefault(p => p.Key == app).Value;
            if (group is null)
            {
                perApp.Add(new(app, group = []));
            }

            group.Add(b);
        }

        var boundCount = 0;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var notBindable = new List<string>();
        foreach (var (app, bs) in perApp)
        {
            var own = reporter.Report(bs, [], byApp.GetValueOrDefault(app) ?? []).Requires.Nouns;
            boundCount += own.Count(n => n.Bound);
            var ownKeys = own.Select(n => n.Noun).ToHashSet(StringComparer.Ordinal);
            foreach (var n in ownKeys)
            {
                targets.Add($"{app}\0{n}");
            }

            foreach (var n in bs.Cast<Behaviour>().SelectMany(NounsOf).Distinct())
            {
                if (!ownKeys.Contains(n) && !notBindable.Contains(n))
                {
                    notBindable.Add(n);
                }
            }
        }

        var symbols = resolution.Symbols.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        int generated = 0, contract = 0, ungenerated = 0;
        var unbound = new List<string>();
        foreach (var b in behaviours)
        {
            var test = generator.Generate(b, byApp.GetValueOrDefault(CorpusOf(b) ?? string.Empty) ?? [], symbols);
            generated += test.Stats.Generated;
            contract += test.Stats.Contract;
            ungenerated += test.Stats.Ungenerated;
            foreach (var m in test.Missing.Where(m => !unbound.Contains(m)))
            {
                unbound.Add(m);
            }

            Out(test.Code);
            if (test.Missing.Count > 0)
            {
                Out($"// unbound noun(s): {string.Join(", ", test.Missing)}");
            }

            if (b.Open!.Count > 0)
            {
                Out($"// OPEN unknown(s): {string.Join(", ", b.Open.Select(u => u.Key))}");
            }

            if (b.Filled!.Count > 0)
            {
                Out($"// filled from elsewhere: {string.Join(", ", b.Filled.Select(u => $"{u.Key} = [{string.Join(',', u.Value)}] via {string.Join('+', u.From)}"))}");
            }

            Out(string.Empty);
        }

        if (conflicts.Count > 0)
        {
            Out("── structural conflicts (the \"supersede?\" case, no LLM involved) ──");
            foreach (var c in conflicts)
            {
                Out($"  {c.Key}: {string.Join('+', c.Holders)} say [{string.Join(',', c.Held)}]; "
                    + string.Join("; ", c.Challengers.Select(x => $"{x.From} says [{string.Join(',', x.Value)}]")));
            }

            Out(string.Empty);
        }

        var adj = merged.Adjudication;
        Out("── adjudication (James, #68: \"default included but marked unreviewed\") ──");
        Out($"  defined by a human    {adj.Defined}");
        Out($"  inferred by the model {adj.Inferred}");
        Out($"  NEVER ADJUDICATED     {adj.Unreviewed.Count}   {string.Join(", ", adj.Unreviewed)}");
        if (adj.Denied.Count > 0)
        {
            Out($"  denied w/ correction  {adj.Denied.Count}");
        }

        if (adj.Untraceable.Count > 0)
        {
            Out($"  ⚠️  UNTRACEABLE        {adj.Untraceable.Count}   no source ref: {string.Join(", ", adj.Untraceable)}");
        }

        Out(string.Empty);

        var surf = merged.Surface;
        var unserved = string.Join(", ", surf.Unserved);
        Out("── displayed surface (James, kit#3: \"expose only what is required to display\") ──");
        Out($"  serves a documented behaviour   {surf.Served.Count}");
        Out($"  NOTHING DOCUMENTED DISPLAYS IT  {surf.Unserved.Count}   {(unserved.Length > 0 ? unserved : "—")}");
        if (surf.Unserved.Count > 0)
        {
            Out("  ⇒ each is a documentation gap or surface to delete. Kit will not guess which.");
        }

        Out(string.Empty);

        var steps = generated + contract + ungenerated;
        var notBindableList = string.Join(", ", notBindable);
        Out("── measured ──");
        Out($"  read from             {dir}   ({(files.Count == 1 ? "1 corpus" : $"{files.Count} corpora")}: {string.Join(", ", files)})");
        Out($"  behaviours            {behaviours.Count}");
        Out($"  nouns bound           {boundCount}/{targets.Count}   in THIS corpus (Cucumber would need one step definition per step phrasing, i.e. {steps})");
        Out($"  not bindable          {notBindable.Count}   {(notBindableList.Length > 0 ? notBindableList : "—")}{(notBindable.Count > 0 ? "   named by a step, but no step Kit generates binds them (a form binds its fields)" : string.Empty)}");
        Out($"  generated lines       {generated}");
        Out($"  wire contracts        {contract}   not expressible as a behaviour — something else must own these");
        Out($"  ungenerated           {ungenerated}   refused rather than guessed");
        Out($"  unbound nouns         {unbound.Count}   {string.Join(", ", unbound)}");
        Out($"  generated / total     {generated}/{steps} = {Percent(generated, steps)}%");

        // The one thing here that GATES: a broken `serves` link is a corpus that lies about itself.
        if (surf.Errors.Count > 0)
        {
            Out(string.Empty);
            Out("── broken links (exit 1) ──");
            foreach (var e in surf.Errors)
            {
                Out($"  {e}");
            }

            return 1;
        }

        return 0;
    }
}
