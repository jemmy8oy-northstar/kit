using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-VIS-1</c> and <c>BEH-VIS-2</c>: the run page's data. The builder is pure: each test passes the runs,
/// the bundle flags and a cost record, and asserts on what the page would show. The route tests are in AgentRunTests.
/// </summary>
public class RunVisibilityTests
{
    private const string Haiku = "claude-haiku-5-5";
    private const string Sonnet = "claude-sonnet-5-5";
    private const string Opus = "claude-opus-5-5";
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static readonly Func<string, string, string?> NoFlag = (_, _) => null;

    private static IAgentRun Run(string id, string status, string? workItem, string? stage, string model, int minute, string project = "kit") =>
        new AgentRun
        {
            Id = id,
            Owner = "james",
            Project = project,
            Model = model,
            Status = status,
            Started = T0.AddMinutes(minute),
            WorkItem = workItem,
            Stage = stage,
        };

    /// <summary>A cost record that answers from a table and remembers every run it was asked about.</summary>
    private sealed class RecordedCosts(IReadOnlyDictionary<string, decimal> rows) : IStageCostRecord
    {
        public List<string> Asked { get; } = [];

        public decimal? CostOf(string runId)
        {
            Asked.Add(runId);
            return rows.TryGetValue(runId, out var usd) ? usd : null;
        }
    }

    // ---- BEH-VIS-1 -------------------------------------------------------------------------------

    [Fact]
    public void BEH_VIS_1_a_running_run_lists_each_running_agent_with_its_current_stage()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, null, "triage", Haiku, 0),
            Run("s", AgentRunStatus.Running, "slug-a", "spec", Sonnet, 5),
            Run("b", AgentRunStatus.Running, null, null, Sonnet, 6),
            Run("q", AgentRunStatus.Queued, "slug-b", "build", Sonnet, 7),
        };

        var page = RunVisibilityBuilder.Build(runs, NoFlag, NoStageCostRecord.Instance);

        Assert.Equal(["b", "s"], page.Running.Select(r => r.Id)); // the queued run is not running; newest first
        var spec = page.Running.Single(r => r.Id == "s");
        Assert.Equal(("slug-a", "spec", Sonnet), (spec.WorkItem, spec.Stage, spec.Model));
        Assert.Null(page.Running.Single(r => r.Id == "b").Stage); // a run started by hand has no stage
    }

    [Fact]
    public void BEH_VIS_1_a_review_an_agent_flagged_shows_on_its_stage_with_the_agent_that_flagged_it()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("b", AgentRunStatus.Running, "slug-a", "build", Sonnet, 5),
        };
        Func<string, string, string?> flagged = (project, slug) => project == "kit" && slug == "slug-a" ? "unsure which table changes" : null;

        var page = RunVisibilityBuilder.Build(runs, flagged, NoStageCostRecord.Instance);

        var build = Assert.Single(page.Running);
        Assert.Equal("unsure which table changes", build.Flag);
        Assert.Equal(Haiku, build.FlaggedBy); // the stage run before it on the item, the triage agent, flagged it
    }

    [Fact]
    public void BEH_VIS_1_a_stage_with_no_flagged_review_shows_no_review_line_and_a_run_with_no_running_agent_shows_none()
    {
        var unflagged = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("b", AgentRunStatus.Running, "slug-a", "build", Sonnet, 5),
        };

        var withNone = RunVisibilityBuilder.Build(unflagged, NoFlag, NoStageCostRecord.Instance);
        var build = Assert.Single(withNone.Running);
        Assert.Null(build.Flag);
        Assert.Null(build.FlaggedBy);

        var idle = RunVisibilityBuilder.Build([Run("t", AgentRunStatus.Finished, null, "triage", Haiku, 0)], NoFlag, NoStageCostRecord.Instance);
        Assert.Empty(idle.Running);
    }

    // ---- BEH-VIS-2 -------------------------------------------------------------------------------

    [Fact]
    public void BEH_VIS_2_a_finished_run_shows_each_stage_with_the_cost_recorded_for_it()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("s", AgentRunStatus.Finished, "slug-a", "spec", Sonnet, 5),
        };
        var costs = new RecordedCosts(new Dictionary<string, decimal> { ["t"] = 0.02m, ["s"] = 0.10m });

        var page = RunVisibilityBuilder.Build(runs, NoFlag, costs);

        var finished = Assert.Single(page.Finished);
        Assert.Equal(("kit", "slug-a"), (finished.Project, finished.WorkItem));
        Assert.Equal(new (string?, decimal?)[] { ("triage", 0.02m), ("spec", 0.10m) }, finished.Stages.Select(s => (s.Stage, s.CostUsd)));
    }

    [Fact]
    public void BEH_VIS_2_a_stage_with_no_recorded_cost_shows_as_unknown_never_as_zero()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("s", AgentRunStatus.Finished, "slug-a", "spec", Sonnet, 5),
        };
        var costs = new RecordedCosts(new Dictionary<string, decimal> { ["t"] = 0.02m }); // the spec run has no record

        var stages = RunVisibilityBuilder.Build(runs, NoFlag, costs).Finished.Single().Stages;

        Assert.Equal(0.02m, stages[0].CostUsd);
        Assert.Null(stages[1].CostUsd);
        Assert.NotEqual(0m, stages[1].CostUsd);
    }

    [Fact]
    public void BEH_VIS_2_a_finished_run_lists_the_reviews_it_requested_with_the_stage_and_agent()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("b", AgentRunStatus.Finished, "slug-a", "build", Sonnet, 5),
            Run("r", AgentRunStatus.Finished, "slug-a", "review", Opus, 10),
        };

        var finished = Assert.Single(RunVisibilityBuilder.Build(runs, NoFlag, NoStageCostRecord.Instance).Finished);

        var review = Assert.Single(finished.Reviews);
        Assert.Equal(("r", "build", Sonnet), (review.RunId, review.Stage, review.RequestedBy));
    }

    [Fact]
    public void BEH_VIS_2_the_page_only_reads_the_cost_record_and_writes_none()
    {
        var runs = new List<IAgentRun>
        {
            Run("t", AgentRunStatus.Finished, "slug-a", "triage", Haiku, 0),
            Run("s", AgentRunStatus.Finished, "slug-a", "spec", Sonnet, 5),
        };
        var costs = new RecordedCosts(new Dictionary<string, decimal> { ["t"] = 0.02m, ["s"] = 0.10m });
        var before = runs.Select(r => (r.Id, r.CostUsd, r.Status)).ToList();

        var page = RunVisibilityBuilder.Build(runs, NoFlag, costs);

        Assert.Equal(["t", "s"], costs.Asked); // each run's cost is read once, and nothing else is asked of the record
        Assert.Equal(before, runs.Select(r => (r.Id, r.CostUsd, r.Status)).ToList()); // no run was changed
        Assert.Equal([0.02m, 0.10m], page.Finished.Single().Stages.Select(s => s.CostUsd!.Value));
    }

    // ---- BEH-UI-7, BEH-UI-8: the runs page's cards -----------------------------------------------

    [Fact]
    public void BEH_UI_7_the_recent_runs_are_newest_start_first_whatever_order_they_were_given_in()
    {
        var runs = new List<IAgentRun>
        {
            Run("mid", AgentRunStatus.Finished, null, null, Sonnet, 5),
            Run("old", AgentRunStatus.Finished, null, null, Sonnet, 0),
            Run("new", AgentRunStatus.Running, null, null, Sonnet, 9),
        };

        var page = RunVisibilityBuilder.Build(runs, NoFlag, NoStageCostRecord.Instance);

        Assert.Equal(["new", "mid", "old"], page.Recent.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void BEH_UI_8_each_recent_run_carries_the_state_the_run_record_holds()
    {
        var runs = new List<IAgentRun>
        {
            Run("q", AgentRunStatus.Queued, null, null, Sonnet, 0),
            Run("r", AgentRunStatus.Running, null, null, Sonnet, 1),
            Run("f", AgentRunStatus.Finished, null, null, Sonnet, 2),
            Run("x", AgentRunStatus.Failed, null, null, Sonnet, 3),
        };

        var page = RunVisibilityBuilder.Build(runs, NoFlag, NoStageCostRecord.Instance);

        Assert.Equal(
            [AgentRunStatus.Failed, AgentRunStatus.Finished, AgentRunStatus.Running, AgentRunStatus.Queued],
            page.Recent.Select(r => r.Status).ToArray());
    }

    [Fact]
    public void BEH_UI_8_with_no_cost_record_every_recent_run_costs_unknown()
    {
        var runs = new List<IAgentRun>
        {
            Run("a", AgentRunStatus.Finished, null, null, Sonnet, 0),
            Run("b", AgentRunStatus.Running, "slug-a", "spec", Sonnet, 1),
        };

        var page = RunVisibilityBuilder.Build(runs, NoFlag, NoStageCostRecord.Instance);

        Assert.All(page.Recent, r => Assert.Null(r.CostUsd));
    }

    [Fact]
    public void BEH_UI_8_a_run_with_no_recorded_cost_is_unknown_and_a_run_recorded_at_zero_is_zero()
    {
        var runs = new List<IAgentRun>
        {
            Run("none", AgentRunStatus.Finished, null, null, Sonnet, 0),
            Run("zero", AgentRunStatus.Finished, null, null, Sonnet, 1),
        };
        var costs = new RecordedCosts(new Dictionary<string, decimal> { ["zero"] = 0m });

        var page = RunVisibilityBuilder.Build(runs, NoFlag, costs);

        var none = page.Recent.Single(r => r.Id == "none");
        var zero = page.Recent.Single(r => r.Id == "zero");
        Assert.Null(none.CostUsd);
        Assert.Equal(0m, zero.CostUsd);
        Assert.NotEqual(none.CostUsd, zero.CostUsd);
    }
}
