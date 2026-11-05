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

    // Every link object under a behaviour id; "_"-keys are the file's comments.
    private static IEnumerable<(string Id, JsonObject Link)> Links(JsonObject map)
    {
        foreach (var (id, node) in map)
        {
            if (id.StartsWith('_'))
            {
                continue;
            }

            var entries = node is JsonArray many ? many.OfType<JsonObject>() : node is JsonObject one ? [one] : [];
            foreach (var link in entries)
            {
                yield return (id, link);
            }
        }
    }

    private static string? Text(JsonObject o, string key) =>
        o.TryGetPropertyValue(key, out var v) && v is JsonValue s && s.TryGetValue<string>(out var t) ? t : null;

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
                var files = LinkHasher.ExercisedFiles(xml, repo);
                if (files.Count == 0)
                {
                    throw new InvalidOperationException("the coverage run touched no source files");
                }

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
            if (link["verified"] is not JsonObject v || Text(v, "hash") is not { } recorded || v["files"] is not JsonArray files)
            {
                unrecorded++;
                Out($"   ◌ {id}: {title} — unrecorded");
                continue;
            }

            var paths = files.Select(f => f?.GetValue<string>() ?? string.Empty).ToList();
            var gone = paths.FirstOrDefault(p => !File.Exists(Path.Combine(repo, p)));
            if (gone is not null)
            {
                broken++;
                Out($"   ✗ {id}: {title} — POSSIBLY BROKEN: {gone} is gone");
            }
            else if (LinkHasher.Hash(repo, paths) != recorded)
            {
                broken++;
                Out($"   ✗ {id}: {title} — POSSIBLY BROKEN: the code it exercises has changed since {Text(v, "on")}");
            }
            else
            {
                verified++;
                Out($"   ✅ {id}: {title} — verified");
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
