using System.Diagnostics;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The image and the script that starts it — <c>kit.test.js</c>'s <c>docker:</c> and
/// <c>entrypoint:</c> tests, ported when the Node engine was deleted (kit#119). Neither file is
/// executed anywhere else before a push to <c>main</c>: there is no container builder where the
/// suite runs, so this suite is the only thing that reads the Dockerfile beside the tree it
/// copies, and the only thing that runs the entrypoint against a real remote.
/// </summary>
public class ContainerTests
{
    /// <summary>
    /// A COPY source the build stage maps from the repo, and the ONLY paths allowed to be absent
    /// from git: build outputs. The value is the RUN command that writes it, and the test checks
    /// the Dockerfile still runs it, so an exemption cannot outlive the step that earns it.
    /// </summary>
    private static readonly Dictionary<string, string> DockerfileBuildOutputs = new()
    {
        ["prototypes/behaviour-ast/ui/dist"] = "npm --prefix prototypes/behaviour-ast/ui run build",
    };

    /// <summary>
    /// An explicit (non-glob) COPY of an absent path is a HARD docker failure, and it fails LATE —
    /// after the frontend build and a registry login — so it reads as a credential fault. Read from
    /// DISK, not <c>HEAD:Dockerfile</c>, so an uncommitted bad line is red while its author looks.
    /// </summary>
    [Fact]
    public void Every_explicit_COPY_source_exists_because_an_absent_one_fails_the_build_late()
    {
        var dockerfile = Path.Combine(RepoLayout.Root, "Dockerfile");
        Assert.True(File.Exists(dockerfile), $"could not look: no Dockerfile at {dockerfile}");
        var df = File.ReadAllText(dockerfile);

        // `/src` is the build stage's WORKDIR, populated by `COPY . .`, so a `--from=build
        // /src/<p>` source is the repo's `<p>`. A COPY from any other stage is not a claim about
        // this repo and is deliberately not checked.
        var sources = df.Split('\n')
            .Select(l => Regex.Match(l, @"^\s*COPY\s+--from=build\s+(?:--[^\s]+\s+)*(/src/\S+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value["/src/".Length..])
            .ToList();
        // A regex that stops matching is how this gate would go quietly inert.
        Assert.True(sources.Count >= 2,
            $"could not look: matched {sources.Count} COPY --from=build line(s) — the regex has stopped matching");

        // `ls-files`, not File.Exists: the question is what a fresh clone gets.
        var missing = sources
            .Where(rel => !rel.Contains('*') && !DockerfileBuildOutputs.ContainsKey(rel))
            .Where(rel => Git("ls-files", "--", rel, $"{rel}/").Stdout.Trim() == "")
            .ToList();
        Assert.True(missing.Count == 0,
            $"Dockerfile COPYs these paths but git does not track them, so `docker build` fails after the "
            + $"frontend build has run: {string.Join(", ", missing)}. Restore the path, delete the COPY line, "
            + "or — if the build writes it — add it to DockerfileBuildOutputs with the RUN command.");

        var stale = DockerfileBuildOutputs.Where(e => !df.Contains(e.Value)).Select(e => e.Key).ToList();
        Assert.True(stale.Count == 0,
            $"DockerfileBuildOutputs excuses paths whose build command the Dockerfile no longer runs: {string.Join(", ", stale)}");

        var unused = DockerfileBuildOutputs.Keys.Where(p => !sources.Contains(p)).ToList();
        Assert.True(unused.Count == 0,
            $"DockerfileBuildOutputs names paths the Dockerfile no longer COPYs, so they excuse nothing: {string.Join(", ", unused)}");
    }

    /// <summary>
    /// A single NUL byte makes <c>grep</c> classify a file as binary and suppress its matching
    /// lines while <c>-c</c> still counts them — so a search answers the opposite of itself.
    /// <c>kit.js</c> carried one for an unknown time. Whole population, because nobody suspected
    /// that file either.
    /// </summary>
    [Fact]
    public void Every_tracked_source_file_is_plain_text()
    {
        var ls = Git("ls-files", "-z", "--", "*.js", "*.ts", "*.tsx", "*.cs", "*.beh", "*.md", "*.json", "*.yml", "*.sh");
        Assert.True(ls.Ok, $"could not look: {ls.Failure}");
        var files = ls.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        // An empty listing would pass vacuously — the exact shape of bug this is written to catch.
        Assert.True(files.Length > 50, $"could not look: git ls-files returned {files.Length} file(s)");

        var offenders = files
            .Select(rel => (rel, at: Array.IndexOf(File.ReadAllBytes(Path.Combine(RepoLayout.Root, rel)), (byte)0)))
            .Where(f => f.at != -1)
            .Select(f => $"{f.rel} (first NUL at byte {f.at})")
            .ToList();
        Assert.True(offenders.Count == 0,
            $"these tracked text files contain a raw NUL byte, so grep shows no matching lines in them: {string.Join(", ", offenders)}");
    }

    // ── the container entrypoint: a hosted edit survives a restart (kit#117) ─────────────────
    // Run against a REAL bare remote, with a fake `dotnet` first on PATH that prints what the
    // server would have been started with — so the assertions are about the environment Kit
    // actually receives, not about the script's text.

    [Fact]
    public void A_failed_clone_of_an_existing_kit_hosted_never_forks_a_fresh_one_from_the_base()
    {
        // kit#165: the first clone used to fall through to the base on ANY failure, so a network
        // blip began a new kit/hosted beside the real one and every later push was rejected.
        using var f = new Remote();
        f.Git(f.Clone, "push", "-q", "origin", "HEAD:kit/hosted");
        var realGit = Run("sh", ["-c", "command -v git"], new()).Stdout.Trim();
        var flaky = Directory.CreateTempSubdirectory("kit-flaky-").FullName;
        Executable(Path.Combine(flaky, "git"),
            "#!/bin/sh",
            "case \"$*\" in *clone*kit/hosted*) echo \"fatal: unable to access: Could not resolve host\" >&2; exit 128;; esac",
            $"exec {realGit} \"$@\"");
        var work = Path.Combine(f.Root, "work");

        var r = StartKit(new() { ["KIT_GIT_CLONE"] = f.Bare, ["KIT_GIT_TOKEN"] = "t", ["KIT_GIT_WORKTREE"] = work }, prependPath: flaky);

        Assert.Equal(0, r.Status);
        Assert.Equal("", r.Out["KIT_GIT"]); // OFF, rather than pushing to a forked branch
        Assert.False(Directory.Exists(work), "no clone of the base may be left to serve");
        Assert.Matches("kit/hosted exists", r.Stderr);
    }

    [Fact]
    public void With_no_token_it_clones_nothing_and_Kit_starts_exactly_as_before()
    {
        using var f = new Remote();
        var work = Path.Combine(f.Root, "work");
        var r = StartKit(new() { ["KIT_GIT_CLONE"] = f.Bare, ["KIT_GIT_WORKTREE"] = work });
        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal("Balenthiran.Kit.WebApi.dll --urls x", r.Out["ARGS"]);
        Assert.Equal("", r.Out["KIT_GIT"]);
        Assert.Equal("", r.Out["KIT_DIR"]);
        Assert.False(Directory.Exists(work));
    }

    [Fact]
    public void The_first_start_creates_kit_hosted_from_the_base_and_the_first_edit_pushes_it()
    {
        using var f = new Remote();
        var work = Path.Combine(f.Root, "work");
        var r = StartKit(new() { ["KIT_GIT_CLONE"] = f.Bare, ["KIT_GIT_TOKEN"] = "tok-123", ["KIT_GIT_WORKTREE"] = work });
        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal("1", r.Out["KIT_GIT"]);
        Assert.Equal("kit/hosted", r.Out["KIT_GIT_BRANCH"]);
        Assert.Equal(Path.Combine(work, "prototypes/behaviour-ast/behaviours"), r.Out["KIT_DIR"]);
        Assert.Equal("kit/hosted", f.Git(work, "rev-parse", "--abbrev-ref", "HEAD").Trim());
        // The token reaches git through the helper — and is in no URL git could echo back.
        Assert.Equal("tok-123", r.Out["password"]);
        Assert.DoesNotContain("tok-123", f.Git(work, "remote", "get-url", "origin"));

        // The edit, pushed the way GitStore does it (`git push <remote> HEAD:<branch>`).
        File.AppendAllText(Path.Combine(work, "behaviours", "demo.beh"), "  when opens page:Home\n");
        f.Git(work, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-am", "kit: add a step to BEH-1 (demo)");
        f.Git(work, "push", "-q", "origin", "HEAD:kit/hosted");
        Assert.Contains("kit/hosted", f.Git(f.Root, "--git-dir", f.Bare, "branch", "--list", "kit/hosted"));
    }

    [Fact]
    public void A_restart_clones_kit_hosted_so_an_edit_made_before_it_is_still_there()
    {
        using var f = new Remote();
        f.Git(f.Clone, "checkout", "-q", "-b", "kit/hosted");
        File.AppendAllText(f.File, "  review approved\n");
        f.Git(f.Clone, "commit", "-q", "-am", "kit: adjudicate BEH-1 (demo)");
        f.Git(f.Clone, "push", "-q", "origin", "kit/hosted");

        var work = Path.Combine(f.Root, "work");
        var r = StartKit(new() { ["KIT_GIT_CLONE"] = f.Bare, ["KIT_GIT_TOKEN"] = "t", ["KIT_GIT_WORKTREE"] = work });
        Assert.True(r.Status == 0, r.Stderr);
        Assert.Contains("review approved", File.ReadAllText(Path.Combine(work, "behaviours", "demo.beh")));
    }

    [Fact]
    public void A_clone_that_fails_still_starts_Kit_on_the_image_corpus_with_write_back_off()
    {
        using var f = new Remote();
        var r = StartKit(new()
        {
            ["KIT_GIT_CLONE"] = Path.Combine(f.Root, "no-such-remote.git"),
            ["KIT_GIT_TOKEN"] = "t",
            ["KIT_GIT_WORKTREE"] = Path.Combine(f.Root, "work"),
        });
        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal("Balenthiran.Kit.WebApi.dll --urls x", r.Out["ARGS"]);
        Assert.Equal("", r.Out["KIT_GIT"]);
        Assert.Equal("", r.Out["KIT_DIR"]);
        Assert.Contains("write-back OFF", r.Stderr);
    }

    private sealed record Ran(int Status, string Stdout, string Stderr, Dictionary<string, string> Out);

    private static Ran StartKit(Dictionary<string, string> env, string prependPath = "")
    {
        var bin = Directory.CreateTempSubdirectory("kit-bin-").FullName;
        Executable(Path.Combine(bin, "dotnet"),
            "#!/bin/sh",
            "echo \"ARGS=$*\"",
            "echo \"KIT_DIR=${KIT_DIR-}\"",
            "echo \"KIT_GIT=${KIT_GIT-}\"",
            "echo \"KIT_GIT_BRANCH=${KIT_GIT_BRANCH-}\"",
            // What git will hand a push to github.com, through the helper the entrypoint configured.
            "printf \"protocol=https\\nhost=github.com\\n\\n\" | git credential fill 2>/dev/null | grep \"^password=\" || echo \"password=<none>\"");
        var path = (prependPath == "" ? "" : prependPath + ":") + bin + ":" + Environment.GetEnvironmentVariable("PATH");
        var full = new Dictionary<string, string>
        {
            ["PATH"] = path,
            ["HOME"] = Directory.CreateTempSubdirectory("kit-home-").FullName,
            ["KIT_GIT_BASE"] = "main",
        };
        foreach (var (k, v) in env) full[k] = v;
        return Run("sh", [Path.Combine(RepoLayout.Root, "docker-entrypoint.sh"), "--urls", "x"], full, clearEnv: true);
    }

    private static Ran Run(string file, string[] args, Dictionary<string, string> env, bool clearEnv = false)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // The Node tests ran the script with ONLY these variables; inheriting the runner's
        // KIT_* or GIT_* would let the host's environment answer for the script.
        if (clearEnv) psi.Environment.Clear();
        foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi)!;
        var stderr = p.StandardError.ReadToEndAsync();
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        var outMap = stdout.Split('\n').Where(l => l.Contains('='))
            .GroupBy(l => l[..l.IndexOf('=')])
            .ToDictionary(g => g.Key, g => g.Last()[(g.Last().IndexOf('=') + 1)..]);
        return new Ran(p.ExitCode, stdout, stderr.Result, outMap);
    }

    private static void Executable(string path, params string[] lines)
    {
        File.WriteAllText(path, string.Join('\n', lines));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static GitStore.Call Git(params string[] args) => GitStore.Git(args, RepoLayout.Root);

    /// <summary>A real remote, a real clone, and one committed corpus file inside it.</summary>
    private sealed class Remote : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("kit-git-").FullName;
        public string Bare => Path.Combine(Root, "bare.git");
        public string Clone => Path.Combine(Root, "clone");
        public string File => Path.Combine(Clone, "behaviours", "demo.beh");

        public Remote()
        {
            Git(Root, "init", "-q", "--bare", "-b", "main", Bare);
            Git(Root, "clone", "-q", Bare, Clone);
            Git(Clone, "config", "user.name", "fixture");
            Git(Clone, "config", "user.email", "fixture@example.com");
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            System.IO.File.WriteAllText(File, "behaviour BEH-1 \"a thing\"\n  actor visitor\n");
            Git(Clone, "add", "-A");
            Git(Clone, "commit", "-q", "-m", "initial");
            Git(Clone, "push", "-q", "origin", "main");
        }

        public string Git(string cwd, params string[] args)
        {
            var r = GitStore.Git(args, cwd);
            if (!r.Ok) throw new InvalidOperationException($"fixture: git {string.Join(' ', args)} — {r.Failure}");
            return r.Stdout;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
