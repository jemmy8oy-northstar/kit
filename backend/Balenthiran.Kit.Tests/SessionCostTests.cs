using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-COST-1</c>: what each Claude Code session Kit runs costs, recorded in Kit's database beside the
/// per-user store, from the dollar cost the run's own result reports, and nothing else. Past sessions on a dev machine
/// are summed by week from their reported costs; token-only records are skipped, never priced.
/// </summary>
public class SessionCostTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero); // a Monday

    private const string PoolToken = "pool-secret";

    // ── a session Kit starts ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BEH_COST_1_a_finished_run_writes_one_record_with_its_date_project_model_and_the_reported_cost_and_no_estimate()
    {
        var rig = new CostRig();
        var run = rig.StartRun("octo/cat", "sonnet");

        // A million input tokens would list at more than 0.5 USD; the record keeps what the run reported, not a price.
        var r = rig.Finished(run.Token, """{"status":"finished","costUsd":0.5,"usage":{"input_tokens":1000000,"output_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}""");

        Assert.Equal(200, r.Status);
        var record = Assert.Single(rig.Costs.List());
        Assert.Equal(run.Id, record.RunId);
        Assert.Equal(Noon, record.Date);
        Assert.Equal("octo/cat", record.Project);
        Assert.Equal("sonnet", record.Model);
        Assert.Equal(0.5m, record.CostUsd);
    }

    [Fact]
    public void BEH_COST_1_the_record_is_in_the_store_before_the_report_is_answered_and_still_exists_once_claude_home_is_removed()
    {
        var claudeHome = Directory.CreateTempSubdirectory("kit-claude-home-").FullName;
        try
        {
            var projects = Path.Combine(claudeHome, "projects", "p");
            Directory.CreateDirectory(projects);
            File.WriteAllText(Path.Combine(projects, "s.jsonl"), """{"timestamp":"2026-11-02T12:00:00Z","costUSD":0.42}""" + "\n");
            var rig = new CostRig();
            var run = rig.StartRun("octo/cat", "sonnet");

            var r = rig.Finished(run.Token, """{"status":"finished","costUsd":0.42}""");

            // Answered 200 means the record is already written: the runner exits only after this answer.
            Assert.Equal(200, r.Status);
            Assert.Single(rig.Costs.List());
            Directory.Delete(claudeHome, recursive: true);
            Assert.Equal(0.42m, Assert.Single(rig.Costs.List()).CostUsd);
        }
        finally
        {
            if (Directory.Exists(claudeHome))
            {
                Directory.Delete(claudeHome, recursive: true);
            }
        }
    }

    [Fact]
    public void BEH_COST_1_no_file_is_written_under_the_project_repo_for_a_cost_record()
    {
        var rig = new CostRig();
        var run = rig.StartRun("octo/cat", "sonnet");
        var before = Snapshot(RepoLayout.Root);

        Assert.Equal(200, rig.Finished(run.Token, """{"status":"finished","costUsd":0.5}""").Status);

        Assert.Equal(before, Snapshot(RepoLayout.Root));
    }

    [Fact]
    public void BEH_COST_1_a_new_sessions_cost_is_the_one_the_run_reported_and_claude_home_is_never_read()
    {
        // A session record under a Claude home says 9.99; the run's own result says 0.42. Only the result counts.
        var claudeHome = Directory.CreateTempSubdirectory("kit-claude-home-").FullName;
        try
        {
            var projects = Path.Combine(claudeHome, "projects", "p");
            Directory.CreateDirectory(projects);
            File.WriteAllText(Path.Combine(projects, "s.jsonl"), """{"timestamp":"2026-11-02T12:00:00Z","costUSD":9.99}""" + "\n");
            var rig = new CostRig();
            var run = rig.StartRun("octo/cat", "sonnet");

            Assert.Equal(200, rig.Finished(run.Token, """{"status":"finished","costUsd":0.42}""").Status);

            Assert.Equal(0.42m, Assert.Single(rig.Costs.List()).CostUsd);
        }
        finally
        {
            Directory.Delete(claudeHome, recursive: true);
        }
    }

    [Fact]
    public void BEH_COST_1_a_failed_run_that_reports_a_cost_is_recorded_too()
    {
        var rig = new CostRig();
        var run = rig.StartRun("octo/cat", "sonnet");

        Assert.Equal(200, rig.Finished(run.Token, """{"status":"failed","failure":"claude exit 3","costUsd":0.2}""").Status);

        var record = Assert.Single(rig.Costs.List());
        Assert.Equal(0.2m, record.CostUsd);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("\"0.5\"")]
    public void BEH_COST_1_a_cost_that_is_not_a_non_negative_number_is_refused_and_nothing_is_recorded(string cost)
    {
        var rig = new CostRig();
        var run = rig.StartRun("octo/cat", "sonnet");

        var r = rig.Finished(run.Token, "{\"status\":\"finished\",\"costUsd\":" + cost + "}");

        Assert.Equal(400, r.Status);
        Assert.Equal("bad-cost", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(rig.Costs.List());
        Assert.Equal(AgentRunStatus.Running, rig.Store.ByTokenHash(AgentTokens.Hash(run.Token))!.Status);
    }

    [Fact]
    public void BEH_COST_1_a_run_reported_twice_is_recorded_once()
    {
        var rig = new CostRig();
        var run = rig.StartRun("octo/cat", "sonnet");

        Assert.Equal(200, rig.Finished(run.Token, """{"status":"finished","costUsd":0.5}""").Status);
        Assert.Equal(409, rig.Finished(run.Token, """{"status":"finished","costUsd":0.5}""").Status);

        Assert.Single(rig.Costs.List());
    }

    [Fact]
    public void BEH_COST_1_the_subscription_price_is_not_a_field_of_the_record()
    {
        var names = typeof(SessionCost).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(["CostUsd", "Date", "Model", "Project", "RunId"], names.Order());
        Assert.DoesNotContain(names, n => n.Contains("Subscription", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Plan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BEH_COST_1_the_postgres_store_keeps_one_record_per_run()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresSessionCostStore(connection);
        var run = $"run-{Guid.NewGuid():N}";
        store.Record(new SessionCost { RunId = run, Date = Noon, Project = "octo/cat", Model = "sonnet", CostUsd = 0.5m });
        store.Record(new SessionCost { RunId = run, Date = Noon, Project = "octo/cat", Model = "sonnet", CostUsd = 9m });

        var kept = Assert.Single(store.List(), c => c.RunId == run);
        Assert.Equal((Noon, "octo/cat", "sonnet", 0.5m), (kept.Date, kept.Project, kept.Model, kept.CostUsd));
    }

    // ── past sessions on a dev machine ───────────────────────────────────────────────────────

    [Fact]
    public void BEH_COST_1_past_sessions_with_a_reported_cost_are_summed_by_week_and_match_those_costs()
    {
        var home = Directory.CreateTempSubdirectory("kit-past-").FullName;
        try
        {
            // Mon 2 and Wed 4 Nov share a week; Mon 9 Nov starts the next. One session spans two lines.
            Session(home, "a", ("2026-11-02T09:00:00Z", 0.10), ("2026-11-02T09:05:00Z", 0.05));
            Session(home, "b", ("2026-11-04T18:00:00Z", 0.25));
            Session(home, "c", ("2026-11-09T10:00:00Z", 0.50));

            var report = PastSessionCosts.Read(home);

            Assert.Equal(0.40m, report.WeekTotals[new DateOnly(2026, 11, 2)]);
            Assert.Equal(0.50m, report.WeekTotals[new DateOnly(2026, 11, 9)]);
            Assert.Equal(2, report.WeekTotals.Count);
            Assert.Equal(3, report.Counted);
            Assert.Equal(0, report.Skipped);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void BEH_COST_1_a_past_session_with_only_token_usage_is_skipped_and_counted_and_never_priced_from_its_tokens()
    {
        var home = Directory.CreateTempSubdirectory("kit-past-").FullName;
        try
        {
            Session(home, "reported", ("2026-11-02T09:00:00Z", 0.10));
            WriteSession(home, "tokens-only", """{"timestamp":"2026-11-02T10:00:00Z","message":{"model":"claude-sonnet-5-5","usage":{"input_tokens":1000000,"output_tokens":1000000}}}""");

            var report = PastSessionCosts.Read(home);

            Assert.Equal(1, report.Skipped);
            Assert.Equal(1, report.Counted);
            Assert.Equal(0.10m, Assert.Single(report.WeekTotals).Value);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void BEH_COST_1_no_import_of_past_runner_sessions_is_attempted_by_the_runner_or_the_server()
    {
        // Only a dev machine reads its own Claude home. The runner scripts and the server's run routes never do.
        var runner = Directory.GetFiles(Path.Combine(RepoLayout.Root, "runner"));
        foreach (var file in runner)
        {
            Assert.DoesNotContain(".claude", File.ReadAllText(file), StringComparison.Ordinal);
        }

        var servers = Directory.GetFiles(Path.Combine(RepoLayout.Root, "backend", "Balenthiran.Kit.Services"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(RepoLayout.Root, "backend", "Balenthiran.Kit.WebApi"), "*.cs"))
            .Where(f => !f.EndsWith("PastSessionCosts.cs", StringComparison.Ordinal));
        foreach (var file in servers)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("PastSessionCosts", text, StringComparison.Ordinal);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A session file: one line per (timestamp, reported dollar cost) pair, each with a message.</summary>
    private static void Session(string home, string name, params (string Timestamp, double Cost)[] lines)
    {
        var text = string.Join("\n", lines.Select(l =>
            $"{{\"timestamp\":\"{l.Timestamp}\",\"costUSD\":{l.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"message\":{{\"model\":\"claude-sonnet-5-5\"}}}}"));
        WriteSession(home, name, text);
    }

    private static void WriteSession(string home, string name, string text)
    {
        var dir = Path.Combine(home, "projects", "-work-repo");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".jsonl"), text + "\n");
    }

    private static List<string> Snapshot(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => $"{Path.GetRelativePath(root, f)}|{new FileInfo(f).Length}|{File.GetLastWriteTimeUtc(f).Ticks}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private sealed record StartedRun(string Id, string Token);

    /// <summary>The runner pool's routes over an in-memory run store and cost store, with a fixed clock.</summary>
    private sealed class CostRig
    {
        public MemoryAgentRunStore Store { get; } = new();

        public MemorySessionCostStore Costs { get; } = new();

        private RunnerPoolRoutes Routes { get; }

        public CostRig()
        {
            Routes = new RunnerPoolRoutes(new RunnerPool(Store), Store, PoolToken, TimeSpan.Zero, new FixedClock(Noon), costs: Costs);
        }

        public StartedRun StartRun(string project, string model)
        {
            var token = AgentTokens.Mint();
            var run = Store.Create("james", project, model, AgentTokens.Hash(token), Noon.AddMinutes(-5));
            Assert.True(Store.StartIfQueued(run.Id));
            return new StartedRun(run.Id, token);
        }

        public KitResponse Finished(string token, string body)
        {
            using var doc = JsonDocument.Parse(body);
            return Routes.Finished(token, doc.RootElement.Clone());
        }
    }

    private sealed class FixedClock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }
}
