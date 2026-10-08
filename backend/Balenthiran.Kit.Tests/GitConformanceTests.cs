using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Git write-back's score: every scenario in <c>conformance/routes/git.json</c>, which
/// <c>conformance.js</c> records from <c>ui.js</c> + <c>git-store.js</c>, rebuilt here — a REAL
/// bare remote and a REAL clone each — and answered through <see cref="KitHost"/> with a
/// <see cref="GitStore"/> switched on. Scored on the answer AND on what the remote then holds,
/// because a commit that never left the clone is exactly what must not pass as a push.
/// </summary>
public class GitConformanceTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var s in Golden()["scenarios"]!.AsArray())
        {
            data.Add(s!["name"]!.GetValue<string>());
        }

        return data;
    }

    [Fact]
    public void The_golden_reaches_every_outcome()
    {
        var names = Golden()["scenarios"]!.AsArray().Select(s => s!["name"]!.GetValue<string>()).ToList();
        Assert.True(names.Count >= 10, $"only {names.Count} git scenarios — the gate is inert");

        // Pushed, committed-not-pushed, and neither: the three states gitOutcome() keeps apart.
        var states = Golden()["scenarios"]!.AsArray()
            .Select(s => (s!["response"]!["body"]!["committed"]!.GetValue<bool>(), s["response"]!["body"]!["pushed"]!.GetValue<bool>()))
            .ToHashSet();
        Assert.Equal(3, states.Count);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_write_answers_and_reaches_the_remote_as_ui_js_does(string name)
    {
        var s = Golden()["scenarios"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == name)!;
        var root = Directory.CreateTempSubdirectory("kit-git-").FullName;
        try
        {
            var bare = Path.Combine(root, "bare.git");
            var clone = Path.Combine(root, "clone");
            Sh(root, "init", "-q", "--bare", "-b", "main", bare);
            Sh(root, "clone", "-q", bare, clone);
            Sh(clone, "config", "user.name", "fixture");
            Sh(clone, "config", "user.email", "fixture@example.com");
            var dir = Path.Combine(clone, "behaviours");
            Directory.CreateDirectory(dir);
            CopyFixtures(dir);
            Sh(clone, "add", "-A");
            Sh(clone, "commit", "-q", "-m", "initial");
            Sh(clone, "push", "-q", "origin", "main");

            var served = dir;
            switch (s["setup"]?.GetValue<string>())
            {
                case "remote-gone":
                    Directory.Move(bare, bare + ".gone");
                    break;
                case "detached":
                    Sh(clone, "checkout", "-q", "--detach", "HEAD");
                    break;
                case "dirty-tree":
                    File.WriteAllText(Path.Combine(dir, "unrelated.txt"), "not part of this edit\n");
                    Sh(clone, "add", "--", "behaviours/unrelated.txt");
                    break;
                case "not-a-work-tree":
                    served = Path.Combine(root, "loose");
                    Directory.CreateDirectory(served);
                    CopyFixtures(served);
                    break;
            }

            var before = Sh(clone, "rev-parse", "HEAD").Trim();
            var g = s["git"]!;
            var store = new GitStore(true, Str(g["remote"]), Str(g["branch"]), name: Str(g["name"]), email: Str(g["email"]));
            var corpora = new CorpusDirectory(served, RepoLayout.Root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var policy = new OriginPolicy(new UrlParser(), null);
            var router = new KitRouter(corpora, viewer, new UiBundle(served), null, policy, git: store);
            var host = new KitHost(router, new UrlParser(), Serialiser, policy, string.Empty);

            var a = host.Received(s["path"]!.GetValue<string>(), System.Text.Encoding.UTF8.GetBytes(s["body"]!.ToJsonString()), null, null);
            var actual = Mask(JsonNode.Parse(a.Body!)!.AsObject(), root, Path.GetRelativePath(RepoLayout.Root, served));

            var expected = s["response"]!;
            Assert.True(expected["status"]!.GetValue<int>() == a.Status, $"{name}: status {a.Status}, body {a.Body}");
            Assert.Equal(Canonical(expected["body"]), Canonical(actual));

            var branch = Str(g["branch"]) ?? "main";
            JsonNode? remote = !Directory.Exists(bare) || !Git(root, "-C", bare, "rev-parse", "--verify", "-q", $"refs/heads/{branch}").Ok
                ? null
                : new JsonObject
                {
                    ["branch"] = branch,
                    ["subject"] = Sh(bare, "log", "-1", "--format=%s", branch).Trim(),
                    ["author"] = Sh(bare, "log", "-1", "--format=%an <%ae>", branch).Trim(),
                    ["files"] = new JsonArray(Sh(bare, "show", "--name-only", "--format=", branch).Trim().Split('\n').Select(f => (JsonNode)f).ToArray()),
                };
            Assert.Equal(Canonical(s["remote"]), Canonical(remote));
            Assert.Equal(s["headMoved"]!.GetValue<bool>(), Sh(clone, "rev-parse", "HEAD").Trim() != before);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary><c>maskGit()</c>'s three rules: the hash, the fixture root, the corpus path.</summary>
    private static JsonObject Mask(JsonObject body, string root, string relDir)
    {
        var sha = body["commit"] is JsonValue c && c.TryGetValue<string>(out var v) && Regex.IsMatch(v, "^[0-9a-f]{10}$") ? v : null;
        var rel = relDir.Replace('\\', '/');
        var o = new JsonObject();
        foreach (var (k, node) in body)
        {
            if (k == "file" && node!.GetValue<string>() is { } f && f.StartsWith(rel + "/", StringComparison.Ordinal))
            {
                o[k] = "<dir>/" + f[(rel.Length + 1)..];
            }
            else if (node is JsonValue jv && jv.TryGetValue<string>(out var str))
            {
                var t = sha is null ? str : str.Replace(sha, "<sha>", StringComparison.Ordinal);
                o[k] = t.Replace(root, "<tmp>", StringComparison.Ordinal);
            }
            else
            {
                o[k] = node?.DeepClone();
            }
        }

        return o;
    }

    private static void CopyFixtures(string to)
    {
        foreach (var f in new[] { "alpha.beh", "alpha.bindings.json" })
        {
            File.Copy(Path.Combine(RepoLayout.Conformance, "writes", f), Path.Combine(to, f));
        }
    }

    private static GitStore.Call Git(string cwd, params string[] args) => GitStore.Git(args, cwd);

    private static string Sh(string cwd, params string[] args)
    {
        var r = GitStore.Git(args, cwd);
        return r.Ok ? r.Stdout : throw new InvalidOperationException($"fixture: git {string.Join(' ', args)} — {r.Failure}");
    }

    private static string? Str(JsonNode? n) => n?.GetValue<string>();

    private static JsonNode Golden() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "git.json")))!;

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);
}
