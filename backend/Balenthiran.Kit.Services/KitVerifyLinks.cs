using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c>kit verify-links</c> (BEH-HEALTH-4, first slice: Kit's own .NET tests). <c>record</c> runs each
/// link's test under coverage and writes beside the link the hash of the source it touched;
/// <c>check</c> re-hashes those files — no coverage run, so it is cheap — and flags a link whose
/// source has changed as POSSIBLY BROKEN. Exit 0 nothing flagged, 1 something is, 2 it could not look.
/// </summary>
/// <remarks>
/// The coverage run is a delegate, not an interface: it is the one slow, process-spawning step and
/// the tests swap it for a canned Cobertura report. It takes the test project directory and the test
/// method name, and returns the report text (throwing if the run failed).
/// </remarks>
public sealed class KitVerifyLinks(Func<string, string, string>? coverage = null)
{
    private const string Usage = "usage: kit verify-links record|check <app> --repo <path-to-app-repo> [--dir <corpus-dir>]";

    private readonly Func<string, string, string> runCoverage = coverage ?? DotnetCoverage;

    /// <summary>Run <c>record</c> or <c>check</c>; <paramref name="args"/> start at the subcommand.</summary>
    public ICommandRun Run(IReadOnlyList<string> args)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var code = Go(args, stdout, stderr);
        return new CommandRun { ExitCode = code, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
    }

    private int Go(IReadOnlyList<string> args, StringBuilder stdout, StringBuilder stderr)
    {
        void Out(string s) => stdout.Append(s).Append('\n');
        void Err(string s) => stderr.Append(s).Append('\n');

        string? sub = null, app = null, repo = null;
        var dir = "behaviours";
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "--repo" or "--dir")
            {
                if (i + 1 >= args.Count)
                {
                    Err($"cannot look: {a} needs a value");
                    return 2;
                }

                if (a == "--repo")
                {
                    repo = args[++i];
                }
                else
                {
                    dir = args[++i];
                }
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Err($"cannot look: unknown option {a}");
                return 2;
            }
            else if (sub is null)
            {
                sub = a;
            }
            else if (app is null)
            {
                app = a;
            }
            else
            {
                Err($"cannot look: unexpected argument {a}");
                return 2;
            }
        }

        if (sub is not ("record" or "check") || string.IsNullOrEmpty(app) || string.IsNullOrEmpty(repo))
        {
            Err(Usage);
            return 2;
        }

        if (!Directory.Exists(repo))
        {
            Err($"cannot look: no such repo {repo}");
            return 2;
        }

        var mapPath = Path.Combine(dir, $"{app}.tests.json");
        if (!File.Exists(mapPath))
        {
            Err($"cannot look: no mapping {mapPath}");
            return 2;
        }

        var text = File.ReadAllText(mapPath);
        JsonObject map;
        try
        {
            map = JsonNode.Parse(text) as JsonObject ?? throw new JsonException("not an object");
        }
        catch (JsonException)
        {
            Err($"cannot look: {mapPath} is not a JSON object");
            return 2;
        }

        var links = Links(map).ToList();
        if (links.Count == 0)
        {
            Err($"cannot look: {mapPath} has no test links");
            return 2;
        }

        Out($"── kit verify-links {sub}: {app} ──");
        if (sub == "check")
        {
            return Check(links, repo, Out);
        }

        var code = Record(links, repo, Out, Err);
        File.WriteAllText(mapPath, Serialize(map, text.EndsWith('\n')));
        return code;
    }

    private static IEnumerable<(string Id, JsonObject Link)> Links(JsonObject map) => LinkVerification.Links(map);

    private static string? Text(JsonObject o, string key) => LinkVerification.Text(o, key);

    private int Record(List<(string Id, JsonObject Link)> links, string repo, Action<string> Out, Action<string> Err)
    {
        var failed = 0;
        foreach (var (id, link) in links)
        {
            var (file, title) = (Text(link, "file"), Text(link, "title"));
            if (file is null || title is null || !file.EndsWith(".cs", StringComparison.Ordinal))
            {
                Out($"   ◌ {id}: {title ?? file ?? "?"} — not a .NET test, skipped");
                continue;
            }

            try
            {
                var xml = runCoverage(Path.Combine(repo, Path.GetDirectoryName(file) ?? string.Empty), title);
                // The test's own file is part of what is hashed: a test edited so it no longer
                // asserts the behaviour is as much a broken link as a change to the code it runs.
                if (!File.Exists(Path.Combine(repo, file)))
                {
                    throw new IOException($"the test file {file} is gone");
                }

                var files = LinkHasher.ExercisedFiles(xml, repo).Append(file).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

                link["verified"] = new JsonObject
                {
                    ["hash"] = LinkHasher.Hash(repo, files),
                    ["files"] = new JsonArray(files.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray()),
                    ["on"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                };
                Out($"   ✅ {id}: {title} — {files.Count} file(s) hashed");
            }
            catch (Exception e) when (e is InvalidOperationException or IOException or System.Xml.XmlException)
            {
                failed++;
                // A hash that could not be re-recorded is no longer evidence: drop it, so the link
                // reads unrecorded rather than verified against code it was never run on.
                link.Remove("verified");
                Err($"   ✗ {id}: {title} — {e.Message}");
            }
        }

        Out($"\n   {links.Count - failed} link(s) handled, {failed} failed.");
        return failed > 0 ? 1 : 0;
    }

    private static int Check(List<(string Id, JsonObject Link)> links, string repo, Action<string> Out)
    {
        int verified = 0, broken = 0, unrecorded = 0;
        foreach (var (id, link) in links)
        {
            var title = Text(link, "title") ?? "?";
            var verdict = LinkVerification.Classify(link, repo);
            switch (verdict.State)
            {
                case LinkState.Unrecorded:
                    unrecorded++;
                    Out($"   ◌ {id}: {title} — unrecorded");
                    break;
                case LinkState.PossiblyBroken:
                    broken++;
                    Out($"   ✗ {id}: {title} — POSSIBLY BROKEN: {verdict.Reason}");
                    break;
                default:
                    verified++;
                    Out($"   ✅ {id}: {title} — verified");
                    break;
            }
        }

        Out($"\n   {verified} verified, {broken} possibly broken, {unrecorded} unrecorded.");
        return broken > 0 ? 1 : 0;
    }

    // Two-space indent, as the file is authored; the relaxed encoder keeps "—" and "'" unescaped so
    // an untouched comment key comes back byte for byte.
    private static string Serialize(JsonObject map, bool trailingNewline)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = map.ToJsonString(options);
        return trailingNewline ? json + "\n" : json;
    }

    /// <summary>
    /// The real coverage run: one test method under <c>dotnet test --no-build</c> with coverlet, into a
    /// throwaway directory. ~7s a test; <c>--no-build</c> means the caller built first.
    /// </summary>
    public static string DotnetCoverage(string testProjectDir, string testName)
    {
        var results = Path.Combine(Path.GetTempPath(), "kit-coverage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[] { "test", testProjectDir, "--no-build", "--filter", $"FullyQualifiedName~{testName}", "--collect:XPlat Code Coverage", "--results-directory", results })
            {
                start.ArgumentList.Add(a);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet could not be run");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet test exited {process.ExitCode} for {testName}");
            }

            _ = (output.Result, error.Result);
            var report = Directory.Exists(results)
                ? Directory.GetFiles(results, "coverage.cobertura.xml", SearchOption.AllDirectories).FirstOrDefault()
                : null;
            return report is null
                ? throw new InvalidOperationException($"no coverage report for {testName} — did the filter match a test?")
                : File.ReadAllText(report);
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException($"dotnet could not be run ({e.Message})");
        }
        finally
        {
            if (Directory.Exists(results))
            {
                Directory.Delete(results, true);
            }
        }
    }
}
