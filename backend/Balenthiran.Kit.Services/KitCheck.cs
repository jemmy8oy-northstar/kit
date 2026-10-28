using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c>kit check</c> — port of <c>check.js</c>, Stage 0's deliverable: a behaviour with no test
/// naming it fails the build. Output and exit codes are Node's, byte for byte, scored by
/// <c>Fixtures/Check/goldens.json</c>; the one intended difference is the usage line's
/// command name.
/// </summary>
/// <remarks>
/// Exit codes, and the distinction matters more than the gate: 0 every built behaviour is
/// named by a test; 1 it looked and something is wrong; 2 IT COULD NOT LOOK — no corpus, no
/// repo, zero test files, or a reader whose two counts disagree. A gate that reads nothing and
/// exits 0 is indistinguishable in CI from one that passed.
///
/// WHAT A PASS MEANS: someone LINKED each behaviour to a test — a marker or a mapping entry. Not
/// that the test asserts it. The output says so, because a gate quoted second-hand loses its
/// caveats first.
///
/// Where Node would CRASH (a corpus that does not parse, a mapping that is not a JSON object of
/// arrays) this answers exit 2 with a <c>cannot look</c> line instead of a stack trace. That is
/// the only behavioural difference, and no golden records a crash.
/// </remarks>
public sealed class KitCheck(
    ICorpusParser parser,
    IBehaviourResolver resolver,
    ITestTitleReader reader,
    ICheckFileSystem disk,
    ICoverageCounter? counter = null) : IKitCheck
{
    // The counting is shared with hosted Kit (BEH-HEALTH-1); the gate decides only what to print.
    private readonly ICoverageCounter counts = counter ?? new CoverageCounter(reader);

    private const string Usage = "usage: kit check <app> --repo <path-to-app-repo> [--via mapping|markers] [--dir <corpus-dir>]";

    // Node's default was the script's own `behaviours/`; a CLI has no script directory, so it
    // is the one in the directory it runs from.
    private const string DefaultDir = "behaviours";

    private static readonly string[] Layers = ["ux", "technical", "ui"];

    private static readonly HashSet<string> ValueFlags = new(StringComparer.Ordinal) { "--repo", "--via", "--dir" };

    // A Playwright spec — the suite that walks the app in a browser. Every other file the
    // reader accepts is a unit test.
    private static readonly Regex E2EFile = new(@"\.spec\.(ts|tsx|js|jsx)\z", RegexOptions.CultureInvariant);

    private static readonly Regex Marker = new(@"\[([A-Z][A-Z0-9-]*)\]", RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public ICommandRun Run(IReadOnlyList<string> args)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var code = Gate(args, stdout, stderr);
        return new CommandRun { ExitCode = code, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
    }

    // `cli.js`'s one predicate for "a flag, not a value": a lone `-` is a value (stdin by convention).
    private static bool LooksLikeAFlag(string s) => s.Length > 1 && s[0] == '-';

    // Node's `path.join`: join with `/`, then normalise — `.` dropped, `..` folded, `//` collapsed.
    private static string Join(params string[] parts)
    {
        var joined = string.Join('/', parts.Where(p => p.Length > 0));
        if (joined.Length == 0)
        {
            return ".";
        }

        var absolute = joined[0] == '/';
        var trailing = joined[^1] == '/';
        var stack = new List<string>();
        foreach (var seg in joined.Split('/'))
        {
            if (seg.Length == 0 || seg == ".")
            {
                continue;
            }

            if (seg == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..")
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                else if (!absolute)
                {
                    stack.Add(seg);
                }

                continue;
            }

            stack.Add(seg);
        }

        var path = string.Join('/', stack);
        if (path.Length == 0 && !absolute)
        {
            path = ".";
        }

        if (path.Length > 0 && trailing)
        {
            path += "/";
        }

        return absolute ? "/" + path : path;
    }

    private int Gate(IReadOnlyList<string> argv, StringBuilder stdout, StringBuilder stderr)
    {
        void Out(string s) => stdout.Append(s).Append('\n');
        void Err(string s) => stderr.Append(s).Append('\n');

        // Positional, never `indexOf(flag) + 1`, and every rejection is exit 2: a typo'd `--dir`
        // that fell back to a default would gate a different corpus and report a clean pass.
        string? app = null, repo = null;
        var via = "mapping";
        var dir = DefaultDir;
        var help = false;
        for (var i = 0; i < argv.Count; i++)
        {
            var a = argv[i];
            string? error = null;

            // kit#103: --help is an answer, not a typo — the same as `kit report --help`.
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
                    if (a == "--repo")
                    {
                        repo = v;
                    }
                    else if (a == "--via")
                    {
                        via = v;
                    }
                    else
                    {
                        dir = v;
                    }

                    i++;
                }
            }
            else if (LooksLikeAFlag(a))
            {
                error = $"unknown option {a}";
            }
            else if (app is null)
            {
                app = a;
            }
            else
            {
                error = $"two app names given, \"{app}\" and \"{a}\" — this gate checks one corpus";
            }

            if (error is not null)
            {
                Err($"cannot look: {error}");
                Err(Usage);
                return 2;
            }
        }

        if (help)
        {
            Out(Usage);
            return 0;
        }

        if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(repo))
        {
            Err(Usage);
            return 2;
        }

        if (via != "mapping" && via != "markers")
        {
            Err($"--via must be \"mapping\" (option C, default) or \"markers\" (option A), got \"{via}\"");
            return 2;
        }

        if (!disk.Exists(repo))
        {
            Err($"cannot look: no such repo {repo}");
            return 2;
        }

        var behPath = Join(dir, $"{app}.beh");
        if (!disk.Exists(behPath))
        {
            Err($"cannot look: no corpus {behPath}");
            return 2;
        }

        IReadOnlyList<IBehaviour> behaviours;
        try
        {
            behaviours = resolver.Resolve(parser.Parse(disk.ReadText(behPath), $"{app}.beh")).Behaviours;
        }
        catch (CorpusParseException e)
        {
            Err($"cannot look: {e.Message}");
            return 2;
        }

        if (behaviours.Count == 0)
        {
            Err($"cannot look: {app}.beh resolved to 0 behaviours");
            return 2;
        }

        var (files, titles, sources, fatal) = ReadTests(repo);
        if (fatal is not null)
        {
            Err($"cannot look: {fatal}");
            return 2;
        }

        if (files.Count == 0)
        {
            Err($"cannot look: 0 test files under {repo}");
            return 2;
        }

        IReadOnlyList<IBehaviour> covered, notBuilt, missing;
        List<string> errors;
        Func<string, IEnumerable<string>> filesOf;
        if (via == "markers")
        {
            var named = new List<string>();
            foreach (var src in sources)
            {
                foreach (Match m in Marker.Matches(src))
                {
                    if (!named.Contains(m.Groups[1].Value))
                    {
                        named.Add(m.Groups[1].Value);
                    }
                }
            }

            // kit#155: a pending behaviour with no test is expected; one a test already names is the
            // slip of a branch that built it and forgot to delete the marker.
            covered = behaviours.Where(b => named.Contains(b.Id)).ToList();
            var unnamed = behaviours.Where(b => !named.Contains(b.Id)).ToList();
            notBuilt = unnamed.Where(b => b.Pending).ToList();
            missing = unnamed.Where(b => !b.Pending).ToList();

            // An id a test names that no behaviour claims: the corpus lost one the tests still name.
            errors = named.Where(id => !behaviours.Any(b => b.Id == id))
                .Select(id => $"[{id}] is named by a test but is not a behaviour in this corpus").ToList();
            filesOf = id => files.Where((f, i) => sources[i].Contains($"[{id}]", StringComparison.Ordinal));
        }
        else
        {
            // The same `dir`, deliberately: a mapping left behind would read one repo's
            // behaviours against another's claims about its tests, and both files exist.
            var mapPath = Join(dir, $"{app}.tests.json");
            if (!disk.Exists(mapPath))
            {
                Err($"cannot look: no mapping {mapPath} (--via mapping)");
                return 2;
            }

            JsonObject map;
            try
            {
                map = JsonNode.Parse(disk.ReadText(mapPath)) as JsonObject
                    ?? throw new JsonException("not an object");
            }
            catch (JsonException)
            {
                Err($"cannot look: {mapPath} is not a JSON object");
                return 2;
            }

            var mapped = counts.Count(behaviours, map, titles, mapPath);
            if (mapped.Refusal is not null)
            {
                Err($"cannot look: {mapped.Refusal}");
                return 2;
            }

            var linkedFiles = mapped.Linked;
            covered = mapped.Covered;
            notBuilt = mapped.Pending;
            missing = mapped.Uncovered;
            errors = [.. mapped.Errors];
            filesOf = id => linkedFiles.TryGetValue(id, out var linked) ? linked : [];
        }

        foreach (var b in covered.Where(x => x.Pending))
        {
            errors.Add($"{b.Id}: a test names this behaviour, but it is still marked pending — delete the `pending` line");
        }

        var built = behaviours.Count - notBuilt.Count;

        // kit#89: the layer decides whose evidence counts. A technical behaviour needs a unit
        // test; a ui behaviour has no evidence yet. A pending one is exempt — nothing to judge.
        var coveredIds = covered.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var b in behaviours.Where(b => !b.Pending))
        {
            if (b.Layer == "ui")
            {
                errors.Add($"{b.Id}: a ui behaviour cannot be satisfied yet — visual checks are not designed (kit#89). Mark it pending, or give it another layer");
            }
            else if (b.Layer == "technical" && coveredIds.Contains(b.Id))
            {
                var evidence = filesOf(b.Id).ToList();
                if (evidence.All(f => E2EFile.IsMatch(f)))
                {
                    errors.Add($"{b.Id}: a technical behaviour needs a unit test, but only e2e specs name it ({string.Join(", ", evidence)})");
                }
            }
        }

        var uiRefused = behaviours.Where(b => b.Layer == "ui" && !b.Pending).Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var layers = Layers.Select(l => (Layer: l, Count: behaviours.Count(b => b.Layer == l))).Where(l => l.Count > 0).ToList();

        // Name the corpus that was read, not just the app: once two directories answer to one
        // name, "kit check: snip-it ✅" no longer says which went green.
        Out($"── kit check: {app} (via {via}) ──");
        Out($"   {behaviours.Count} behaviour(s) read from {behPath}");
        Out($"   {files.Count} test file(s), {titles.Count} test(s) read from {repo}");
        Out($"   {built - missing.Count}/{built} behaviour(s) have a test naming them");
        if (notBuilt.Count > 0)
        {
            Out($"   {notBuilt.Count} pending behaviour(s): spec'd, not built — not counted above");
        }

        if (layers.Any(l => l.Layer != "ux"))
        {
            Out($"   layers: {string.Join(", ", layers.Select(l => $"{l.Layer} {l.Count}"))}");
        }

        Out(string.Empty);
        foreach (var e in errors)
        {
            Out($"   ✗ {e}");
        }

        // A ui behaviour already has its own line; "no test names it" would send the reader
        // to write a test that cannot satisfy it.
        var untested = missing.Where(b => !uiRefused.Contains(b.Id)).ToList();
        foreach (var b in untested)
        {
            Out($"   ✗ {b.Id}: no test names this behaviour — \"{b.Title}\"");
        }

        foreach (var b in notBuilt)
        {
            Out($"   ◌ {b.Id}: pending — spec'd, not built — \"{b.Title}\"");
        }

        if (errors.Count == 0 && missing.Count == 0)
        {
            Out(notBuilt.Count > 0 ? "   ✅ every built behaviour is named by a test." : "   ✅ every behaviour is named by a test.");
            Out("   ⚠️  this proves someone LINKED each behaviour to a test, not that the test");
            Out("      asserts it. See docs/design/tagging.md before quoting this as coverage.");
            return 0;
        }

        Out($"\n   {errors.Count + untested.Count} problem(s). This is what \"fails the build\" means.");
        return 1;
    }

    // `readTests`: every test file under the repo, its titles, and its text. A reader whose two
    // counts disagree is losing or inventing tests, so that is a refusal, never a number.
    private (List<string> Files, IReadOnlyList<ITestTitle> Titles, List<string> Sources, string? Fatal) ReadTests(string repo)
    {
        var files = new List<string>();
        Walk(repo, string.Empty, files);
        var sources = new List<string>();
        var read = counts.ReadTitles(files.Select(f =>
        {
            var src = disk.ReadText(Join(repo, f));
            sources.Add(src);
            return (f, src);
        }));
        return (files, read.Titles, sources, read.Refusal);
    }

    private void Walk(string root, string rel, List<string> output)
    {
        foreach (var (name, isDirectory) in disk.Entries(Join(root, rel)))
        {
            if (isDirectory)
            {
                if (!counts.SkipsDirectory(name))
                {
                    Walk(root, Join(rel, name), output);
                }
            }
            else if (counts.IsTestPath(name))
            {
                output.Add(Join(rel, name));
            }
        }
    }

}
