using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's machine-to-machine routes, <c>POST /api/runs/claim</c> and <c>POST /api/runs/finished</c>. Like
/// <see cref="AgentProxy"/> they are mounted by the host ahead of the router: the router sees no request headers
/// and cannot wait asynchronously, and neither route takes a session, an Origin or a cookie. Claim is
/// authenticated by the pool token alone, finished by the run's own token.
/// </summary>
/// <param name="poolToken">The configured pool token (<c>KIT_RUNNER_POOL_TOKEN</c>), or null/blank when there is no pool. Never logged or echoed.</param>
/// <param name="claimWait">How long a claim waits for work before answering 204.</param>
public sealed class RunnerPoolRoutes(RunnerPool pool, IAgentRunStore store, string? poolToken, TimeSpan claimWait, TimeProvider? clock = null)
{
    /// <summary>The header a runner pod authenticates a claim with.</summary>
    public const string PoolHeader = "x-kit-runner-pool";

    /// <summary>How long a claim waits when nothing says otherwise.</summary>
    public static readonly TimeSpan DefaultClaimWait = TimeSpan.FromSeconds(25);

    /// <summary>Answers a claim: 204 when nothing is waiting, else the run's token and work, never cached.</summary>
    public async Task<KitResponse> ClaimAsync(string? presented, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poolToken))
        {
            return Error(401, "no-pool", "this Kit has no runner pool configured");
        }

        if (presented is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(poolToken)))
        {
            return Error(401, "not-a-runner", "the runner pool header is missing or wrong");
        }

        if (await pool.ClaimAsync(claimWait, cancellationToken) is not { } claim)
        {
            return new KitResponse { Status = 204, ContentType = "application/json", CacheControl = "no-store" };
        }

        return new KitResponse { Status = 200, ContentType = "application/json", CacheControl = "no-store", Body = claim };
    }

    /// <summary>Answers a runner's report that its run ended: <c>{ status: "finished" | "failed" }</c>, authenticated by the run token.</summary>
    public KitResponse Finished(string? runToken, JsonElement? body)
    {
        var run = runToken is { Length: > 0 } ? store.ByTokenHash(AgentTokens.Hash(runToken)) : null;
        if (run is null)
        {
            return Error(401, "not-a-run", "this is not a run token Kit knows");
        }

        if (body is not { ValueKind: JsonValueKind.Object } b
            || !b.TryGetProperty("status", out var s)
            || s.ValueKind != JsonValueKind.String
            || s.GetString() is not (AgentRunStatus.Finished or AgentRunStatus.Failed))
        {
            return Error(400, "bad-request", "the body must be { \"status\": \"finished\" } or { \"status\": \"failed\" }");
        }

        if (AgentRunStatus.IsOver(run.Status))
        {
            return Error(409, "already-over", $"this run is already {run.Status}");
        }

        store.SetStatus(run.Id, s.GetString()!, (clock ?? TimeProvider.System).GetUtcNow());
        pool.Signal();
        return new KitResponse
        {
            Status = 200,
            ContentType = "application/json",
            CacheControl = "no-store",
            Body = store.ByTokenHash(AgentTokens.Hash(runToken!)) ?? run,
        };
    }

    private static KitResponse Error(int status, string error, string reason) =>
        new() { Status = status, ContentType = "application/json", CacheControl = "no-store", Body = new ApiError { Error = error, Reason = reason } };
}
