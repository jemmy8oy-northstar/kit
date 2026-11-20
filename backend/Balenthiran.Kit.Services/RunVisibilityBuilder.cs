using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-VIS-1 and BEH-VIS-2: the run page's data, read from the agent runs. Pure: the bundle's flag is passed in, and
/// the cost is read through <see cref="IStageCostRecord"/>, which this never writes to.
/// </summary>
public static class RunVisibilityBuilder
{
    /// <summary>The stage that asks for a review; a review's requester is the stage run before it on the same item.</summary>
    public const string ReviewStage = "review";

    /// <summary>
    /// Builds the page from every run (any order). <paramref name="flagOf"/> gives a work item's bundle Flag, or null.
    /// A work item is finished when its latest run has finished; its runs are listed oldest first.
    /// </summary>
    public static RunVisibility Build(IReadOnlyList<IAgentRun> runs, Func<string, string, string?> flagOf, IStageCostRecord costs)
    {
        var chronological = runs.OrderBy(r => r.Started).ToList();

        var running = chronological
            .Where(r => r.Status == AgentRunStatus.Running)
            .Select(r =>
            {
                var flag = r.WorkItem is null ? null : flagOf(r.Project, r.WorkItem);
                return (IRunningAgent)new RunningAgent
                {
                    Id = r.Id,
                    Project = r.Project,
                    WorkItem = r.WorkItem,
                    Stage = r.Stage,
                    Model = r.Model,
                    Started = r.Started,
                    Flag = flag,
                    FlaggedBy = flag is null ? null : RequestedBy(chronological, r)?.Model,
                };
            })
            .Reverse()
            .ToList();

        var finished = chronological
            .Where(r => r.WorkItem is not null)
            .GroupBy(r => (r.Project, WorkItem: r.WorkItem!), new ItemComparer())
            .Where(g => g.Last().Status == AgentRunStatus.Finished)
            .Select(g => (IFinishedRun)new FinishedRun
            {
                Project = g.Key.Project,
                WorkItem = g.Key.WorkItem,
                Stages = g.Select(r => (IFinishedStage)new FinishedStage
                {
                    RunId = r.Id,
                    Stage = r.Stage,
                    Model = r.Model,
                    CostUsd = costs.CostOf(r.Id),
                }).ToList(),
                Reviews = g.Where(r => r.Stage == ReviewStage)
                    .Select(r => (IRequestedReview)new RequestedReview
                    {
                        RunId = r.Id,
                        Stage = RequestedBy(chronological, r)?.Stage,
                        RequestedBy = RequestedBy(chronological, r)?.Model,
                    }).ToList(),
            })
            .Reverse()
            .ToList();

        return new RunVisibility { Running = running, Finished = finished };
    }

    /// <summary>The stage run before <paramref name="run"/> on the same work item, that is not itself a review; null when none.</summary>
    private static IAgentRun? RequestedBy(IReadOnlyList<IAgentRun> chronological, IAgentRun run) =>
        chronological.LastOrDefault(r =>
            r.Project == run.Project
            && r.WorkItem == run.WorkItem
            && r.Stage is not null
            && r.Stage != ReviewStage
            && r.Started < run.Started);

    private sealed class ItemComparer : IEqualityComparer<(string Project, string WorkItem)>
    {
        public bool Equals((string Project, string WorkItem) x, (string Project, string WorkItem) y) =>
            StringComparer.Ordinal.Equals(x.Project, y.Project) && StringComparer.Ordinal.Equals(x.WorkItem, y.WorkItem);

        public int GetHashCode((string Project, string WorkItem) obj) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.Project), StringComparer.Ordinal.GetHashCode(obj.WorkItem));
    }
}
