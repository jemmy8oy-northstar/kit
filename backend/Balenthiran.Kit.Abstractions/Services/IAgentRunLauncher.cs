using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-USERS-9's seam to whatever runs the agent (later, a Kubernetes Job). It is handed the run and the
/// run token, which is the only moment the token exists outside the response that minted it.
/// </summary>
public interface IAgentRunLauncher
{
    /// <summary>Starts <paramref name="run"/>, or says why it did not.</summary>
    Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default);
}
