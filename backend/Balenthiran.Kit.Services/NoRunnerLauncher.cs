using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's only <see cref="IAgentRunLauncher"/> for now: starts nothing and leaves the run queued.
/// A real one (a Kubernetes Job) comes later and needs the owner's approval.
/// </summary>
public sealed class NoRunnerLauncher : IAgentRunLauncher
{
    /// <summary>What the operator is told.</summary>
    public const string Note = "no runner is configured, so the run stays queued";

    /// <inheritdoc />
    public Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentLaunch(false, Note));
}
