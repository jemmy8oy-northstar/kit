using System.Diagnostics;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The runner's two scripts, <c>runner/entrypoint.sh</c> (one agent run) and <c>runner/pool.sh</c>
/// (the pod loop), run for real with stub <c>claude</c>, <c>git</c>, <c>curl</c> and <c>kit-run</c>
/// first on PATH (kit-ui.beh BEH-USERS-9, kit#708). The assertions are about what the agent
/// process and Kit's API actually receive: which credentials reach the model, and what usage
/// is reported when a run is on the subscription. Every directory the scripts could delete from
/// is a temp dir, never <c>/work</c> or <c>/home/runner</c>.
/// </summary>
public class RunnerScriptTests
{
    private const string Plain = "{\"type\":\"result\",\"result\":\"done\",\"usage\":{\"input_tokens\":11,\"output_tokens\":22,"
        + "\"cache_creation_input_tokens\":33,\"cache_read_input_tokens\":44,\"server_tool_use\":{\"web_search_requests\":1},\"service_tier\":\"standard\"}}";

    // ── entrypoint.sh ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Without_the_OAuth_token_the_agent_goes_through_Kit_with_the_run_token_and_no_usage_is_written()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        var usage = Path.Combine(rig.Rec, "usage.json");

        var r = rig.Entrypoint(new() { ["KIT_USAGE_FILE"] = usage });

        Assert.True(r.Status == 0, r.Stderr);
        var seen = rig.ClaudeEnv();
        Assert.Equal("http://kit.test/kit/api/agent", seen["ANTHROPIC_BASE_URL"]);
        Assert.Equal("run-secret", seen["ANTHROPIC_API_KEY"]);
        Assert.False(seen.ContainsKey("CLAUDE_CODE_OAUTH_TOKEN"), "the subscription token must not exist on the Kit path");
        Assert.False(File.Exists(usage), "Kit's proxy counts this run; nothing is reported from the run");
    }

    [Fact]
    public void Without_the_OAuth_token_a_parent_token_is_dropped_so_it_cannot_leak_into_a_proxied_run()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);

        var r = rig.Entrypoint(new() { ["ANTHROPIC_AUTH_TOKEN"] = "stray" });

        Assert.True(r.Status == 0, r.Stderr);
        Assert.False(rig.ClaudeEnv().ContainsKey("ANTHROPIC_AUTH_TOKEN"));
    }

    [Fact]
    public void With_the_OAuth_token_the_agent_gets_it_and_no_API_key_or_base_url_and_the_four_counts_are_written()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        var usage = Path.Combine(rig.Rec, "usage.json");

        var r = rig.Entrypoint(new()
        {
            ["CLAUDE_CODE_OAUTH_TOKEN"] = "oauth-secret",
            ["ANTHROPIC_API_KEY"] = "parent-key",
            ["ANTHROPIC_BASE_URL"] = "http://parent.invalid",
            ["KIT_USAGE_FILE"] = usage,
        });

        Assert.True(r.Status == 0, r.Stderr);
        var seen = rig.ClaudeEnv();
        Assert.Equal("oauth-secret", seen["CLAUDE_CODE_OAUTH_TOKEN"]);
        Assert.False(seen.ContainsKey("ANTHROPIC_API_KEY"), "an API key would win over the OAuth token");
        Assert.False(seen.ContainsKey("ANTHROPIC_BASE_URL"), "the subscription goes straight to Anthropic");
        // Exactly the four counts, in this order; server_tool_use and service_tier are dropped.
        Assert.Equal("{\"input_tokens\":11,\"output_tokens\":22,\"cache_creation_input_tokens\":33,\"cache_read_input_tokens\":44}",
            File.ReadAllText(usage));
    }

    [Fact]
    public void A_missing_negative_or_non_integer_count_is_written_as_zero()
    {
        using var rig = new Rig();
        rig.EntrypointStubs("{\"type\":\"result\",\"result\":\"done\",\"usage\":{\"input_tokens\":5,\"output_tokens\":-1,"
            + "\"cache_creation_input_tokens\":2.5,\"extra\":9}}", claudeExit: 0);
        var usage = Path.Combine(rig.Rec, "usage.json");

        var r = rig.Entrypoint(new() { ["CLAUDE_CODE_OAUTH_TOKEN"] = "t", ["KIT_USAGE_FILE"] = usage });

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal("{\"input_tokens\":5,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}",
            File.ReadAllText(usage));
    }

    [Fact]
    public void A_string_count_is_written_as_zero_rather_than_trusted()
    {
        using var rig = new Rig();
        rig.EntrypointStubs("{\"result\":\"x\",\"usage\":{\"input_tokens\":\"7\",\"output_tokens\":3}}", claudeExit: 0);
        var usage = Path.Combine(rig.Rec, "usage.json");

        rig.Entrypoint(new() { ["CLAUDE_CODE_OAUTH_TOKEN"] = "t", ["KIT_USAGE_FILE"] = usage });

        Assert.Equal("{\"input_tokens\":0,\"output_tokens\":3,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}",
            File.ReadAllText(usage));
    }

    [Fact]
    public void The_agents_result_text_is_printed_on_stdout()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);

        var r = rig.Entrypoint(new());

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Contains("\ndone\n", "\n" + r.Stdout);
    }

    [Fact]
    public void A_failing_agent_makes_the_script_exit_with_its_code_and_its_usage_is_still_written()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 3);
        var usage = Path.Combine(rig.Rec, "usage.json");

        var r = rig.Entrypoint(new() { ["CLAUDE_CODE_OAUTH_TOKEN"] = "t", ["KIT_USAGE_FILE"] = usage });

        Assert.Equal(3, r.Status);
        Assert.Contains("claude exited 3", r.Stderr);
        Assert.Equal("{\"input_tokens\":11,\"output_tokens\":22,\"cache_creation_input_tokens\":33,\"cache_read_input_tokens\":44}",
            File.ReadAllText(usage));
    }

    // ── pool.sh ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_subscription_run_is_reported_finished_with_its_usage_and_never_sees_the_pool_token()
    {
        using var rig = new Rig();
        rig.PoolStubs(finishedWithUsage: 200, writesUsage: true);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        var reports = rig.Finished();
        Assert.Single(reports);
        Assert.Equal("{\"status\":\"finished\",\"usage\":{\"input_tokens\":1,\"output_tokens\":2,"
            + "\"cache_creation_input_tokens\":3,\"cache_read_input_tokens\":4}}", reports[0]);
        var seen = rig.KitRunEnv();
        Assert.False(seen.ContainsKey("KIT_RUNNER_POOL_TOKEN"), "a run must never see the pool credential");
        Assert.Equal("r1", seen["KIT_RUN_ID"]);
        Assert.Equal("tok", seen["KIT_RUN_TOKEN"]);
        Assert.Equal("o/r", seen["KIT_REPO"]);
        Assert.Equal("sonnet", seen["KIT_MODEL"]);
        Assert.True(seen.ContainsKey("KIT_USAGE_FILE"));
    }

    [Fact]
    public void When_Kit_refuses_a_report_that_carries_usage_it_is_sent_again_without_it()
    {
        using var rig = new Rig();
        rig.PoolStubs(finishedWithUsage: 400, writesUsage: true);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        var reports = rig.Finished();
        Assert.Equal(2, reports.Length);
        Assert.Contains("\"usage\"", reports[0]);
        Assert.Equal("{\"status\":\"finished\"}", reports[1]);
        Assert.Contains("reporting it without", r.Stderr);
    }

    [Fact]
    public void A_run_that_wrote_no_usage_is_reported_finished_exactly_once_and_plain()
    {
        using var rig = new Rig();
        rig.PoolStubs(finishedWithUsage: 200, writesUsage: false);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"finished\"}"], rig.Finished());
    }

    // ── the rig ──────────────────────────────────────────────────────────────────────────────

    private sealed record Ran(int Status, string Stdout, string Stderr);

    /// <summary>A temp tree: stubs on PATH, scratch dirs the scripts may delete from, and a place stubs record to.</summary>
    private sealed class Rig : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("kit-runner-").FullName;
        public string Bin => Path.Combine(Root, "bin");
        public string Work => Path.Combine(Root, "work");
        public string Home => Path.Combine(Root, "home");
        public string Rec => Path.Combine(Root, "rec");

        public Rig()
        {
            foreach (var d in new[] { Bin, Work, Home, Rec }) Directory.CreateDirectory(d);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private string Path_ => Bin + ":" + Environment.GetEnvironmentVariable("PATH");

        public void EntrypointStubs(string claudeJson, int claudeExit)
        {
            File.WriteAllText(System.IO.Path.Combine(Rec, "claude.json"), claudeJson);
            Executable(System.IO.Path.Combine(Bin, "claude"),
                "#!/bin/sh",
                "env > '" + System.IO.Path.Combine(Rec, "claude.env") + "'",
                "cat '" + System.IO.Path.Combine(Rec, "claude.json") + "'",
                "exit " + claudeExit);
            Executable(System.IO.Path.Combine(Bin, "git"),
                "#!/bin/sh",
                "case \"$1\" in",
                "  clone) for a; do d=\"$a\"; done; mkdir -p \"$d\";;",
                "  rev-list) echo 0;;",
                "  status) ;;",
                "esac",
                "exit 0");
            Executable(System.IO.Path.Combine(Bin, "curl"),
                "#!/bin/sh",
                "while [ $# -gt 0 ]; do",
                "  case \"$1\" in --output|-o) echo 'the process' > \"$2\"; shift;; esac",
                "  shift",
                "done",
                "exit 0");
        }

        public Ran Entrypoint(Dictionary<string, string> extra)
        {
            var env = new Dictionary<string, string>
            {
                ["PATH"] = Path_,
                ["HOME"] = Home,
                ["KIT_WORK_DIR"] = Work,
                ["KIT_RUN_ID"] = "r1",
                ["KIT_URL"] = "http://kit.test/kit",
                ["KIT_RUN_TOKEN"] = "run-secret",
                ["KIT_REPO"] = "o/r",
                ["KIT_TASK"] = "do a thing",
            };
            foreach (var (k, v) in extra) env[k] = v;
            return Run("bash", [System.IO.Path.Combine(RepoLayout.Root, "runner", "entrypoint.sh")], env);
        }

        public void PoolStubs(int finishedWithUsage, bool writesUsage)
        {
            Executable(System.IO.Path.Combine(Bin, "kit-run"),
                "#!/bin/sh",
                "env > '" + System.IO.Path.Combine(Rec, "kit-run.env") + "'",
                writesUsage
                    ? "printf '%s' '{\"input_tokens\":1,\"output_tokens\":2,\"cache_creation_input_tokens\":3,\"cache_read_input_tokens\":4}' > \"$KIT_USAGE_FILE\""
                    : "true",
                "exit 0");
            // Args: the URL is the last one; --output and --data take a value; --config reads stdin.
            Executable(System.IO.Path.Combine(Bin, "curl"),
                "#!/bin/sh",
                "out=; data=; url=",
                "while [ $# -gt 0 ]; do",
                "  case \"$1\" in",
                "    --config) cat > /dev/null; shift;;",
                "    --output) out=\"$2\"; shift;;",
                "    --data) data=\"$2\"; shift;;",
                "    *) url=\"$1\";;",
                "  esac",
                "  shift",
                "done",
                "case \"$url\" in",
                "  */api/runs/claim)",
                "    printf '%s' '{\"id\":\"r1\",\"token\":\"tok\",\"repo\":\"o/r\",\"task\":\"t\",\"baseBranch\":\"\",\"model\":\"sonnet\"}' > \"$out\"",
                "    printf 200;;",
                "  */api/runs/finished)",
                "    printf '%s\\n' \"$data\" >> '" + System.IO.Path.Combine(Rec, "finished.log") + "'",
                "    case \"$data\" in",
                "      *'\"usage\"'*) printf " + finishedWithUsage + ";;",
                "      *) printf 200;;",
                "    esac;;",
                "  *) printf 404;;",
                "esac",
                "exit 0");
        }

        public Ran Pool() => Run("bash", [System.IO.Path.Combine(RepoLayout.Root, "runner", "pool.sh")], new()
        {
            ["PATH"] = Path_,
            ["HOME"] = Home,
            ["KIT_URL"] = "http://kit.test/kit",
            ["KIT_RUNNER_POOL_TOKEN"] = "pool-secret",
            ["KIT_RUNS_PER_CONTAINER"] = "1",
            ["KIT_RUN_BIN"] = System.IO.Path.Combine(Bin, "kit-run"),
            // The loop deletes the contents of these; they must never default to /work or /home/runner.
            ["KIT_WORK_DIR"] = Work,
            ["KIT_HOME_DIR"] = Home,
        });

        public Dictionary<string, string> ClaudeEnv() => ReadEnv("claude.env");

        public Dictionary<string, string> KitRunEnv() => ReadEnv("kit-run.env");

        public string[] Finished() =>
            File.ReadAllLines(System.IO.Path.Combine(Rec, "finished.log"));

        private Dictionary<string, string> ReadEnv(string name)
        {
            var file = System.IO.Path.Combine(Rec, name);
            Assert.True(File.Exists(file), $"the stub never ran: {name} was not written");
            return File.ReadAllLines(file).Where(l => l.Contains('='))
                .GroupBy(l => l[..l.IndexOf('=')])
                .ToDictionary(g => g.Key, g => g.Last()[(g.Last().IndexOf('=') + 1)..]);
        }
    }

    // Copied from ContainerTests (private there): the environment is cleared, so only what the rig
    // names reaches the script and the host's KIT_* or ANTHROPIC_* cannot answer for it.
    private static Ran Run(string file, string[] args, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment.Clear();
        foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var stderr = p.StandardError.ReadToEndAsync();
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return new Ran(p.ExitCode, stdout, stderr.Result);
    }

    // See ContainerTests.Executable: a stub is exec'd directly until the kernel accepts it (ETXTBSY),
    // because dash would otherwise fall through to the real binary on PATH.
    private static void Executable(string path, params string[] lines)
    {
        if (!lines[0].StartsWith("#!")) throw new ArgumentException("a stub starts with its shebang", nameof(lines));
        string[] probed = [lines[0], "[ \"${1-}\" = --exec-probe ] && exit 0", .. lines[1..]];
        File.WriteAllText(path, string.Join('\n', probed) + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var budget = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(path, "--exec-probe") { RedirectStandardOutput = true, RedirectStandardError = true })!;
                p.WaitForExit();
                if (p.ExitCode != 0) throw new InvalidOperationException($"stub {path} failed its probe: exit {p.ExitCode}");
                return;
            }
            catch (System.ComponentModel.Win32Exception) when (budget.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(10);
            }
        }
    }
}
