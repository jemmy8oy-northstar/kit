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
        rig.PoolStubs([200], writesUsage: true);

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
        rig.PoolStubs([400, 200], writesUsage: true);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        var reports = rig.Finished();
        Assert.Equal(2, reports.Length);
        Assert.Contains("\"usage\"", reports[0]);
        Assert.Equal("{\"status\":\"finished\"}", reports[1]);
        Assert.Contains("Kit answered 400 to r1's report, sending less", r.Stderr);
    }

    [Fact]
    public void A_run_that_wrote_no_usage_is_reported_finished_exactly_once_and_plain()
    {
        using var rig = new Rig();
        rig.PoolStubs([200], writesUsage: false);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"finished\"}"], rig.Finished());
    }

    private const string UsageJson = "{\"input_tokens\":1,\"output_tokens\":2,\"cache_creation_input_tokens\":3,\"cache_read_input_tokens\":4}";
    private const string BundlesJson = "[{\"slug\":\"a\",\"text\":\"hi\"},{\"slug\":\"b\",\"text\":null}]";

    [Fact]
    public void A_run_with_usage_and_bundles_sends_one_report_carrying_both()
    {
        using var rig = new Rig();
        rig.PoolStubs([200], writesUsage: true, BundlesJson);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"finished\",\"usage\":" + UsageJson + ",\"bundles\":" + BundlesJson + "}"], rig.Finished());
    }

    [Fact]
    public void A_refused_report_is_sent_again_without_usage_but_with_bundles_and_then_with_the_status_alone()
    {
        using var rig = new Rig();
        rig.PoolStubs([400, 400, 200], writesUsage: true, BundlesJson);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(
        [
            "{\"status\":\"finished\",\"usage\":" + UsageJson + ",\"bundles\":" + BundlesJson + "}",
            "{\"status\":\"finished\",\"bundles\":" + BundlesJson + "}",
            "{\"status\":\"finished\"}",
        ], rig.Finished());
    }

    [Fact]
    public void Bundles_without_usage_refused_once_are_followed_by_the_status_alone_not_by_the_same_body_twice()
    {
        // body.2 would equal body.1 (there is no usage to drop), so it is skipped.
        using var rig = new Rig();
        rig.PoolStubs([400, 200], writesUsage: false, BundlesJson);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"finished\",\"bundles\":" + BundlesJson + "}", "{\"status\":\"finished\"}"], rig.Finished());
    }

    // ── entrypoint.sh: the bundles a triage pushed (BEH-ORCH-3) ──────────────────────────────

    private static string Bundles_(Rig rig, string[] setup, out Ran ran)
    {
        rig.BundleStubs(setup);
        var file = Path.Combine(rig.Rec, "bundles.json");
        ran = rig.Entrypoint(new() { ["KIT_BUNDLES_FILE"] = file });
        return file;
    }

    [Fact]
    public void Pushed_work_branches_are_sent_with_the_exact_text_of_their_bundle_files()
    {
        using var rig = new Rig();
        var file = Bundles_(rig, ["mk alpha 'first text'", "mk beta \"$(printf 'two\\nlines')\""], out var r);

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal([("alpha", "first text"), ("beta", "two\nlines")], rig.Bundles(file));
    }

    [Fact]
    public void A_branch_whose_tip_differs_from_origin_or_that_was_never_pushed_is_left_out()
    {
        using var rig = new Rig();
        var file = Bundles_(rig,
        [
            "mk good 'kept'",
            "mk moved 'pushed then moved'",
            "git checkout -q kit/work/moved",
            "git -c user.name=t -c user.email=t@t commit -q --allow-empty -m local-only",
            "git checkout -q kit/run-r1",
            "mk unpushed 'never pushed' no",
        ], out var r);

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal([("good", "kept")], rig.Bundles(file));
    }

    [Fact]
    public void A_pushed_branch_with_no_bundle_file_has_empty_text()
    {
        using var rig = new Rig();
        var file = Bundles_(rig, ["mk bare ''"], out var r);

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal([("bare", "")], rig.Bundles(file));
    }

    [Fact]
    public void A_bundle_over_8000_bytes_is_withheld_as_null()
    {
        using var rig = new Rig();
        var file = Bundles_(rig, ["mk big \"$(head -c 9000 /dev/zero | tr '\\0' a)\"", "mk small ok"], out var r);

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal([("big", (string?)null), ("small", "ok")], rig.Bundles(file));
    }

    [Fact]
    public void Bundles_past_48000_bytes_in_total_have_the_later_ones_withheld()
    {
        using var rig = new Rig();
        // Each is ~7930 bytes as sent, under the 8000 each-limit; six fit in 48000, the seventh does not.
        var setup = Enumerable.Range(1, 7).Select(i => $"mk b{i} \"$(head -c 7900 /dev/zero | tr '\\0' a)\"").ToArray();
        var file = Bundles_(rig, setup, out var r);

        Assert.True(r.Status == 0, r.Stderr);
        var got = rig.Bundles(file);
        Assert.Equal(7, got.Count);
        Assert.All(got.Take(6), b => Assert.NotNull(b.Text));
        Assert.Null(got[6].Text);
    }

    [Fact]
    public void A_branch_with_an_invalid_slug_is_left_out()
    {
        using var rig = new Rig();
        var file = Bundles_(rig, ["mk Bad_Slug nope", "mk fine-slug yes"], out var r);

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal([("fine-slug", "yes")], rig.Bundles(file));
    }

    [Fact]
    public void With_no_bundles_file_named_nothing_is_written()
    {
        using var rig = new Rig();
        rig.BundleStubs("mk alpha 'x'");

        var r = rig.Entrypoint(new());

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Empty(Directory.GetFiles(rig.Rec, "bundles*"));
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

        /// <summary>
        /// <paramref name="answers"/> is what Kit says to the 1st, 2nd, ... finished report (the last repeats).
        /// The body is read from the <c>@file</c> given to <c>--data-binary</c>, as curl would send it.
        /// </summary>
        public void PoolStubs(int[] answers, bool writesUsage, string? bundlesJson = null)
        {
            Executable(System.IO.Path.Combine(Bin, "kit-run"),
                "#!/bin/sh",
                "env > '" + System.IO.Path.Combine(Rec, "kit-run.env") + "'",
                writesUsage
                    ? "printf '%s' '{\"input_tokens\":1,\"output_tokens\":2,\"cache_creation_input_tokens\":3,\"cache_read_input_tokens\":4}' > \"$KIT_USAGE_FILE\""
                    : "true",
                bundlesJson is null ? "true" : "printf '%s' '" + bundlesJson + "' > \"$KIT_BUNDLES_FILE\"",
                "exit 0");
            var counter = System.IO.Path.Combine(Rec, "finished.count");
            var cases = answers.Select((a, i) => "      " + (i + 1) + ") printf " + a + ";;").ToList();
            cases.Add("      *) printf " + answers[^1] + ";;");
            // Args: the URL is the last one; --output and --data-binary take a value; --config reads stdin.
            Executable(System.IO.Path.Combine(Bin, "curl"),
                ["#!/bin/sh",
                "out=; data=; url=",
                "while [ $# -gt 0 ]; do",
                "  case \"$1\" in",
                "    --config) cat > /dev/null; shift;;",
                "    --output) out=\"$2\"; shift;;",
                "    --data-binary) data=\"$(cat \"${2#@}\")\"; shift;;",
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
                "    n=$(cat '" + counter + "' 2>/dev/null || echo 0); n=$((n+1)); echo $n > '" + counter + "'",
                "    case $n in",
                .. cases,
                "    esac;;",
                "  *) printf 404;;",
                "esac",
                "exit 0"]);
        }

        /// <summary>
        /// A claude stub that, from inside the clone, runs <paramref name="setup"/> (shell) to leave work branches
        /// behind; origin is a REAL bare repo that "https://github.com/o/r.git" is rewritten to, and git is real.
        /// </summary>
        public void BundleStubs(params string[] setup)
        {
            var origin = System.IO.Path.Combine(Root, "origin.git");
            var seed = System.IO.Path.Combine(Root, "seed");
            var id = "-c user.name=t -c user.email=t@t";
            var seeded = Run("sh", ["-c", string.Join(" && ",
                $"git init -q --bare -b main '{origin}'",
                $"git init -q -b main '{seed}'",
                $"cd '{seed}'",
                "echo hi > README",
                "git add -A",
                $"git {id} commit -q -m seed",
                $"git push -q '{origin}' main")], new() { ["PATH"] = Path_, ["HOME"] = Home });
            Assert.True(seeded.Status == 0, "fixture: " + seeded.Stderr);
            File.WriteAllText(System.IO.Path.Combine(Home, ".gitconfig"),
                $"[url \"file://{origin}\"]\n\tinsteadOf = https://github.com/o/r.git\n");
            File.WriteAllLines(System.IO.Path.Combine(Rec, "setup.sh"), [
                "set -e",
                "mk() { # slug, text (empty = no bundle file), push yes/no",
                "  git checkout -q -b \"kit/work/$1\" kit/run-r1",
                "  if [ -n \"$2\" ]; then mkdir -p .kit/bundles; printf %s \"$2\" > \".kit/bundles/$1.md\"; git add -A; fi",
                $"  git {id} commit -q --allow-empty -m \"$1\"",
                "  [ \"$3\" = no ] || git push -q origin \"kit/work/$1\"",
                "  git checkout -q kit/run-r1",
                "}",
                .. setup]);
            Executable(System.IO.Path.Combine(Bin, "claude"),
                "#!/bin/sh",
                "echo '{\"result\":\"done\"}'",
                "sh '" + System.IO.Path.Combine(Rec, "setup.sh") + "' >&2");
            Executable(System.IO.Path.Combine(Bin, "curl"),
                "#!/bin/sh",
                "while [ $# -gt 0 ]; do",
                "  case \"$1\" in --output|-o) echo 'the process' > \"$2\"; shift;; esac",
                "  shift",
                "done",
                "exit 0");
        }

        /// <summary>The (slug, text) pairs the script wrote to the bundles file; text is null where it was withheld.</summary>
        public List<(string Slug, string? Text)> Bundles(string file)
        {
            Assert.True(File.Exists(file), "no bundles file was written");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.EnumerateArray()
                .Select(e => (e.GetProperty("slug").GetString()!, e.GetProperty("text").GetString())).ToList();
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
