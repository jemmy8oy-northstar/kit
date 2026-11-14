using System.Diagnostics;
using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

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

    [Theory]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(null)]
    public void BEH_ORCH_5_The_pool_gives_a_run_the_claims_turn_limit_and_none_when_the_claim_has_none(int? maxTurns)
    {
        using var rig = new Rig();
        // The claim body is what the server itself writes for this PoolClaim, not typed by hand.
        var claim = new PoolClaim("r1", "tok", "o/r", "t", "", "sonnet", maxTurns);
        rig.PoolStubs([200], writesUsage: false, claimJson: JsonSerializer.Serialize(claim, new EngineJsonSerialiser().Options));

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        var seen = rig.KitRunEnv();
        if (maxTurns is null)
        {
            Assert.False(seen.ContainsKey("KIT_MAX_TURNS"), "a run with no limit must not be given an empty one");
        }
        else
        {
            Assert.Equal(maxTurns.ToString(), seen["KIT_MAX_TURNS"]);
        }
    }

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

    [Fact]
    public void A_failed_run_reports_the_step_and_exit_code_its_runner_wrote()
    {
        using var rig = new Rig();
        rig.PoolStubs([200], writesUsage: false, runExit: 128, writesFailure: "clone exit 128");

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"failed\",\"failure\":\"clone exit 128\"}"], rig.Finished());
    }

    [Fact]
    public void A_failed_run_that_wrote_no_failure_is_reported_as_the_run_failing_with_its_status()
    {
        using var rig = new Rig();
        rig.PoolStubs([200], writesUsage: false, runExit: 3);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"failed\",\"failure\":\"run exit 3\"}"], rig.Finished());
    }

    [Fact]
    public void A_refused_failed_report_is_sent_again_as_the_status_alone()
    {
        using var rig = new Rig();
        rig.PoolStubs([400, 200], writesUsage: false, runExit: 3);

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["{\"status\":\"failed\",\"failure\":\"run exit 3\"}", "{\"status\":\"failed\"}"], rig.Finished());
    }

    // ── entrypoint.sh: where a failed run stopped ─────────────────────────────────────────────

    [Fact]
    public void A_run_failing_at_the_clone_writes_only_the_step_and_the_exit_code()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        Executable(System.IO.Path.Combine(rig.Bin, "git"),
            "#!/bin/sh",
            "echo 'fatal: could not read from https://x-access-token:SECRET@github.com/o/r.git' >&2",
            "exit 128");
        var failure = Path.Combine(rig.Rec, "failure.txt");

        var r = rig.Entrypoint(new() { ["KIT_FAILURE_FILE"] = failure });

        Assert.Equal(128, r.Status);
        Assert.Equal("clone exit 128\n", File.ReadAllText(failure));
    }

    // ── entrypoint.sh: the credential helper ───────────────────────────────────────────────────

    [Fact]
    public void The_credential_helper_gives_git_the_token_from_the_answer_Kit_actually_serialises()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        // The grant exactly as the server writes it: through the engine's options, which indent (kit#714).
        var grant = new Balenthiran.Kit.Services.EngineJsonSerialiser()
            .Serialise(new Balenthiran.Kit.Services.GitHubTokenGrant("ghs_minted", DateTimeOffset.Parse("2026-11-06T18:00:00Z")));
        Assert.Contains("\"token\": \"ghs_minted\"", grant);
        File.WriteAllText(Path.Combine(rig.Rec, "grant.json"), grant);
        var cred = Path.Combine(rig.Rec, "cred.out");
        Executable(System.IO.Path.Combine(rig.Bin, "git"),
            "#!/bin/sh",
            "case \"$1\" in",
            "  clone) printf 'protocol=https\\nhost=github.com\\n\\n' | sh -c \"${GIT_CONFIG_VALUE_0#!} get\" > '" + cred + "' || exit 128",
            "         for a; do d=\"$a\"; done; mkdir -p \"$d\";;",
            "  rev-list) echo 0;;",
            "esac",
            "exit 0");
        Executable(System.IO.Path.Combine(rig.Bin, "curl"),
            "#!/bin/sh",
            "url=",
            "while [ $# -gt 0 ]; do",
            "  case \"$1\" in",
            "    --config) cat > '" + Path.Combine(rig.Rec, "curl.config") + "';;",
            "    --output|-o) echo 'the process' > \"$2\"; shift;;",
            "    *) url=\"$1\";;",
            "  esac",
            "  shift",
            "done",
            "case \"$url\" in */api/runs/github-token) cat '" + Path.Combine(rig.Rec, "grant.json") + "';; esac",
            "exit 0");

        var r = rig.Entrypoint(new());

        Assert.True(r.Status == 0, r.Stderr);
        Assert.Equal(["username=x-access-token", "password=ghs_minted"], File.ReadAllLines(cred));
        Assert.Contains("x-api-key: run-secret", File.ReadAllText(Path.Combine(rig.Rec, "curl.config")));
    }

    [Fact]
    public void The_credential_helper_gives_git_nothing_and_says_so_when_Kit_answers_without_a_token()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        var cred = Path.Combine(rig.Rec, "cred.out");
        Executable(System.IO.Path.Combine(rig.Bin, "git"),
            "#!/bin/sh",
            "case \"$1\" in",
            "  clone) printf 'protocol=https\\nhost=github.com\\n\\n' | sh -c \"${GIT_CONFIG_VALUE_0#!} get\" > '" + cred + "' || exit 128;;",
            "esac",
            "exit 0");
        Executable(System.IO.Path.Combine(rig.Bin, "curl"),
            "#!/bin/sh",
            "cat > /dev/null",
            "printf '{\\n  \"expiresAt\": \"x\"\\n}'",
            "exit 0");

        var r = rig.Entrypoint(new());

        Assert.Equal(128, r.Status);
        Assert.Equal("", File.ReadAllText(cred));
        Assert.Contains("kit-run: Kit's github-token answer held no token", r.Stderr);
    }

    [Fact]
    public void The_credential_helper_says_which_refusal_Kit_gave_when_it_will_not_mint()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        var cred = Path.Combine(rig.Rec, "cred.out");
        Executable(System.IO.Path.Combine(rig.Bin, "git"),
            "#!/bin/sh",
            "case \"$1\" in",
            "  clone) printf 'protocol=https\\nhost=github.com\\n\\n' | sh -c \"${GIT_CONFIG_VALUE_0#!} get\" > '" + cred + "' || exit 128;;",
            "esac",
            "exit 0");
        // What curl --fail-with-body does with a 403: the body on stdout, exit 22 (kit#714).
        Executable(System.IO.Path.Combine(rig.Bin, "curl"),
            "#!/bin/sh",
            "cat > /dev/null",
            "printf '{\\n  \"error\": \"repo-unknown\",\\n  \"reason\": \"Kit does not know which repo this run is for\"\\n}'",
            "echo 'curl: (22) The requested URL returned error: 403' >&2",
            "exit 22");

        var r = rig.Entrypoint(new());

        Assert.Equal(128, r.Status);
        Assert.Equal("", File.ReadAllText(cred));
        Assert.Contains("kit-run: Kit refused a GitHub token: repo-unknown (Kit does not know which repo this run is for)", r.Stderr);
    }

    [Fact]
    public void A_failing_agent_writes_the_claude_step()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 3);
        var failure = Path.Combine(rig.Rec, "failure.txt");

        rig.Entrypoint(new() { ["KIT_FAILURE_FILE"] = failure });

        Assert.Equal("claude exit 3\n", File.ReadAllText(failure));
    }

    [Fact]
    public void A_missing_env_var_writes_the_setup_step_and_a_success_writes_nothing()
    {
        using var rig = new Rig();
        rig.EntrypointStubs(Plain, claudeExit: 0);
        var failure = Path.Combine(rig.Rec, "failure.txt");

        var bad = rig.Entrypoint(new() { ["KIT_FAILURE_FILE"] = failure, ["KIT_REPO"] = "norepo" });
        Assert.Equal(64, bad.Status);
        Assert.Equal("setup exit 64\n", File.ReadAllText(failure));

        File.Delete(failure);
        var ok = rig.Entrypoint(new() { ["KIT_FAILURE_FILE"] = failure });
        Assert.True(ok.Status == 0, ok.Stderr);
        Assert.False(File.Exists(failure));
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

    // ── entrypoint.sh: a test run's verdict is checked by the runner (BEH-ORCH-12) ────────────

    private sealed record Tested(Ran Ran, string Sha, string Failure, string Pushed, bool BundlesWritten);

    // Real git and a real origin (as BundleStubs); the stub claude prints message(sha) as its final message, then runs `leave`
    // (shell, inside the clone) as the agent's leftovers. The task names the sha unless task says otherwise.
    private static Tested TestRun_(Rig rig, string? stage, Func<string, string> message, string leave = "true", Func<string, string>? task = null)
    {
        rig.BundleStubs();
        var origin = System.IO.Path.Combine(rig.Root, "origin.git");
        var env = new Dictionary<string, string> { ["PATH"] = rig.Bin + ":" + Environment.GetEnvironmentVariable("PATH"), ["HOME"] = rig.Home };
        var sha = Run("git", ["--git-dir", origin, "rev-parse", "main"], env).Stdout.Trim();
        File.WriteAllText(System.IO.Path.Combine(rig.Rec, "claude.json"), JsonSerializer.Serialize(new { result = message(sha) }));
        Executable(System.IO.Path.Combine(rig.Bin, "claude"), "#!/bin/sh", "cat '" + System.IO.Path.Combine(rig.Rec, "claude.json") + "'", leave);
        var failure = System.IO.Path.Combine(rig.Rec, "failure.txt");
        var bundles = System.IO.Path.Combine(rig.Rec, "bundles.json");
        var extra = new Dictionary<string, string> { ["KIT_TASK"] = task?.Invoke(sha) ?? $"run the tests at {sha}", ["KIT_FAILURE_FILE"] = failure, ["KIT_BUNDLES_FILE"] = bundles };
        if (stage is not null) extra["KIT_STAGE"] = stage;
        var ran = rig.Entrypoint(extra);
        var pushed = Run("git", ["--git-dir", origin, "for-each-ref", "refs/heads/kit/run-r1"], env).Stdout.Trim();
        return new Tested(ran, sha, File.Exists(failure) ? File.ReadAllText(failure) : "", pushed, File.Exists(bundles));
    }

    private static string Pass(string sha) => $"Ran everything.\nKIT-TEST: PASS {sha}\n";

    [Fact]
    public void BEH_ORCH_12_a_test_run_with_the_pass_line_at_the_named_commit_succeeds_and_pushes_nothing_even_from_a_dirty_tree()
    {
        using var rig = new Rig();

        var t = TestRun_(rig, "test", Pass, "echo left > dirty.txt");

        Assert.True(t.Ran.Status == 0, t.Ran.Stderr);
        Assert.Equal("", t.Pushed);
        Assert.False(t.BundlesWritten, "a test run reports no bundles");
        Assert.Equal("", t.Failure);
    }

    [Fact]
    public void BEH_ORCH_12_a_test_run_whose_agent_said_FAIL_or_failures_in_prose_fails_at_the_verdict_step()
    {
        foreach (var message in new Func<string, string>[] { _ => "3 tests failed.\nKIT-TEST: FAIL\n", _ => "Ran the suite: 3 failures, sorry.\n" })
        {
            using var rig = new Rig();

            var t = TestRun_(rig, "test", message);

            Assert.Equal(1, t.Ran.Status);
            Assert.Equal("verdict exit 1\n", t.Failure);
            Assert.Equal("", t.Pushed);
        }
    }

    [Fact]
    public void BEH_ORCH_12_a_pass_line_that_is_not_the_last_or_not_exact_fails()
    {
        foreach (var message in new Func<string, string>[]
        {
            sha => $"KIT-TEST: PASS {sha}\nactually, one test failed.\n",
            sha => $"KIT-TEST: PASS {sha} \n",
            sha => $"kit-test: pass {sha}\n",
            sha => $"KIT-TEST: PASS {sha[..39]}\n",
            sha => $"KIT-TEST: PASS {sha.ToUpperInvariant()}\n",
        })
        {
            using var rig = new Rig();

            var t = TestRun_(rig, "test", message);

            Assert.Equal(1, t.Ran.Status);
            Assert.Equal("verdict exit 1\n", t.Failure);
        }
    }

    [Fact]
    public void BEH_ORCH_12_a_pass_for_a_commit_the_task_did_not_name_fails()
    {
        using var rig = new Rig();

        // HEAD is that commit, but the agent was asked about another one.
        var t = TestRun_(rig, "test", Pass, task: _ => "run the tests at " + new string('a', 40));

        Assert.Equal(1, t.Ran.Status);
        Assert.Equal("verdict exit 1\n", t.Failure);
    }

    [Fact]
    public void BEH_ORCH_12_a_pass_while_the_working_directory_is_at_another_commit_fails_and_pushes_nothing()
    {
        using var rig = new Rig();

        var t = TestRun_(rig, "test", Pass, "git -c user.name=t -c user.email=t@t commit -q --allow-empty -m moved");

        Assert.Equal(1, t.Ran.Status);
        Assert.Equal("verdict exit 1\n", t.Failure);
        Assert.Equal("", t.Pushed);
    }

    [Fact]
    public void BEH_ORCH_12_a_test_run_with_no_result_text_fails()
    {
        using var rig = new Rig();

        var t = TestRun_(rig, "test", _ => "");

        Assert.Equal(1, t.Ran.Status);
        Assert.Equal("verdict exit 1\n", t.Failure);
    }

    [Fact]
    public void BEH_ORCH_12_a_run_of_another_stage_or_none_is_unchanged_and_still_pushes_what_it_left()
    {
        foreach (var stage in new string?[] { "build", null })
        {
            using var rig = new Rig();

            var t = TestRun_(rig, stage, Pass, "echo left > dirty.txt");

            Assert.True(t.Ran.Status == 0, t.Ran.Stderr);
            Assert.Contains("refs/heads/kit/run-r1", t.Pushed);
            Assert.True(t.BundlesWritten);
        }
    }

    [Theory]
    [InlineData("test")]
    [InlineData(null)]
    public void BEH_ORCH_12_The_pool_gives_a_run_its_stage_only_when_the_claim_has_one(string? stage)
    {
        using var rig = new Rig();
        var claim = new PoolClaim("r1", "tok", "o/r", "t", "", "sonnet", null, stage);
        rig.PoolStubs([200], writesUsage: false, claimJson: JsonSerializer.Serialize(claim, new EngineJsonSerialiser().Options));

        var r = rig.Pool();

        Assert.True(r.Status == 0, r.Stderr);
        var seen = rig.KitRunEnv();
        if (stage is null)
        {
            Assert.False(seen.ContainsKey("KIT_STAGE"), "a run started by hand must not be given an empty stage");
        }
        else
        {
            Assert.Equal(stage, seen["KIT_STAGE"]);
        }
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
        public void PoolStubs(int[] answers, bool writesUsage, string? bundlesJson = null, int runExit = 0, string? writesFailure = null, string? claimJson = null)
        {
            Executable(System.IO.Path.Combine(Bin, "kit-run"),
                "#!/bin/sh",
                "env > '" + System.IO.Path.Combine(Rec, "kit-run.env") + "'",
                writesUsage
                    ? "printf '%s' '{\"input_tokens\":1,\"output_tokens\":2,\"cache_creation_input_tokens\":3,\"cache_read_input_tokens\":4}' > \"$KIT_USAGE_FILE\""
                    : "true",
                bundlesJson is null ? "true" : "printf '%s' '" + bundlesJson + "' > \"$KIT_BUNDLES_FILE\"",
                writesFailure is null ? "true" : "printf '%s\\n' '" + writesFailure + "' > \"$KIT_FAILURE_FILE\"",
                "exit " + runExit);
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
                "  */next)",
                "    printf '%s' '" + (claimJson ?? "{\"id\":\"r1\",\"token\":\"tok\",\"repo\":\"o/r\",\"task\":\"t\",\"baseBranch\":\"\",\"model\":\"sonnet\"}") + "' > \"$out\"",
                "    printf 200;;",
                "  */finished)",
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
            // No pool token: the runner container does not have one (kit#701); the sidecar does.
            ["KIT_CLAIMER_URL"] = "http://claimer.test",
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
