using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Database;
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
/// <param name="github">Mints a run's repo-scoped token; null when no GitHub App is configured.</param>
/// <param name="maxAge">How long after its start a run may still ask for a GitHub token; null is <see cref="AgentProxy.DefaultMaxAge"/>.</param>
/// <param name="pipeline">Told when a run ends, so its next stage can start (BEH-ORCH-3); null when <c>KIT_PIPELINE</c> is off.</param>
public sealed class RunnerPoolRoutes(RunnerPool pool, IAgentRunStore store, string? poolToken, TimeSpan claimWait, TimeProvider? clock = null, GitHubAppTokenSource? github = null, TimeSpan? maxAge = null, Pipeline? pipeline = null)
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

    /// <summary>
    /// Answers a runner's report that its run ended: <c>{ status: "finished" | "failed", usage?, bundles?, failure? }</c>,
    /// authenticated by the run token. <c>bundles</c> are what a triage run pushed (<see cref="ReadBundles"/>), handed
    /// to the pipeline. <c>usage</c> is Claude Code's own count, with its field names (<c>input_tokens</c>,
    /// <c>output_tokens</c>, <c>cache_creation_input_tokens</c>, <c>cache_read_input_tokens</c>). A run on James's
    /// subscription reaches Anthropic directly, so the model proxy never counts it, and this is the only count Kit gets.
    /// It is priced at list price for the run's model, as the proxy prices a call, and it is ignored when the proxy has
    /// already counted the run, so no run is counted twice. Everything is checked before anything is recorded.
    /// </summary>
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

        AgentUsage? usage = null;
        if (b.TryGetProperty("usage", out var u) && u.ValueKind != JsonValueKind.Null)
        {
            if (ReadUsage(u) is not { } read)
            {
                return Error(400, "bad-usage", "usage must be an object of whole, non-negative token counts: input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens");
            }

            usage = read;
        }

        // Only a failed report may say why, and only as a step and an exit code: nothing a run could write beyond that is kept.
        string? failure = null;
        if (b.TryGetProperty("failure", out var f) && f.ValueKind != JsonValueKind.Null)
        {
            if (s.GetString() != AgentRunStatus.Failed || f.ValueKind != JsonValueKind.String
                || f.GetString() is not { Length: <= 40 } text || !FailurePattern.IsMatch(text))
            {
                return Error(400, "bad-failure", "failure is only for a failed report, and is a step and an exit code, like \"clone exit 128\"");
            }

            failure = text;
        }

        if (AgentRunStatus.IsOver(run.Status))
        {
            return Error(409, "already-over", $"this run is already {run.Status}");
        }

        // A failed run's bundles are never used, so they are not read: a bad list must not keep a failed run from ending.
        IReadOnlyList<ReportedBundle>? bundles = null;
        if (s.GetString() == AgentRunStatus.Finished && ReadBundles(b, out bundles) is { } badBundles)
        {
            return badBundles;
        }

        var proxyCounted = run.InputTokens + run.OutputTokens + run.CacheWriteTokens + run.CacheReadTokens > 0;
        if (usage is { } x && !proxyCounted)
        {
            // Claude Code's total does not split cache writes by lifetime; it writes 5 minute ones, so they are priced as those.
            store.AddUsage(run.Id, x, AgentPrices.Cost(run.Model, x.Input, x.Output, x.CacheWrite, 0, x.CacheRead));
        }

        store.SetStatus(run.Id, s.GetString()!, (clock ?? TimeProvider.System).GetUtcNow(), failure);
        pool.Signal();
        pipeline?.RunEnded(run.Id, s.GetString() == AgentRunStatus.Finished, bundles);
        return new KitResponse
        {
            Status = 200,
            ContentType = "application/json",
            CacheControl = "no-store",
            Body = store.ByTokenHash(AgentTokens.Hash(runToken!)) ?? run,
        };
    }

    /// <summary>
    /// Answers a runner's ask for a GitHub token: <c>{ token, expiresAt }</c>, minted now for the ONE repo this run
    /// was handed, with contents:write and pull_requests:write only. A run carries no GitHub token; it asks each
    /// time git needs one, so a run that is over, too old or unknown gets nothing. The token is never logged.
    /// </summary>
    public async Task<KitResponse> GitHubTokenAsync(string? runToken, CancellationToken cancellationToken = default)
    {
        var run = runToken is { Length: > 0 } ? store.ByTokenHash(AgentTokens.Hash(runToken)) : null;
        if (run is null)
        {
            return Error(401, "not-a-run", "this is not a run token Kit knows");
        }

        if (run.Status != AgentRunStatus.Running)
        {
            return Error(403, "run-not-running", $"this run is {run.Status}, so it cannot be given a GitHub token");
        }

        var limit = maxAge ?? AgentProxy.DefaultMaxAge;
        if ((clock ?? TimeProvider.System).GetUtcNow() - run.Started > limit)
        {
            return Error(403, "run-too-old", $"this run is more than {limit.TotalHours:0.##} hours old, so it can no longer be given a GitHub token");
        }

        // The repo was checked as owner/name when the run was started.
        if (pool.RepoOf(run.Id) is not { } repo)
        {
            return Error(403, "repo-unknown", "Kit does not know which repo this run is for (it was not handed out by this Kit process)");
        }

        if (github is null)
        {
            return Error(503, "no-github-app", "this Kit has no GitHub App configured, so it cannot mint a repo token");
        }

        try
        {
            if (await github.MintForRepoAsync(repo[(repo.IndexOf('/') + 1)..],cancellationToken) is not { } minted)
            {
                return Error(403, "repo-not-installed", $"the GitHub App installation does not reach {repo}");
            }

            return new KitResponse
            {
                Status = 200,
                ContentType = "application/json",
                CacheControl = "no-store",
                Body = new GitHubTokenGrant(minted.Token, minted.ExpiresAt),
            };
        }
        catch (GitHubTokenException e)
        {
            return Error(502, "github-token", e.Message);
        }
    }

    /// <summary>Claude Code's usage object as Kit's counts; null when any count is missing, not a whole number, or negative.</summary>
    private static AgentUsage? ReadUsage(JsonElement u)
    {
        if (u.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        long Count(string name) =>
            u.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) && n >= 0 ? n : -1;

        var (input, output, write, read) = (Count("input_tokens"), Count("output_tokens"), Count("cache_creation_input_tokens"), Count("cache_read_input_tokens"));
        return input < 0 || output < 0 || write < 0 || read < 0 ? null : new AgentUsage(input, output, write, read);
    }

    private static readonly System.Text.RegularExpressions.Regex FailurePattern = new("^[a-z]+ exit [0-9]{1,3}$");

    /// <summary>Most bundles one report may carry.</summary>
    public const int MaxBundles = 50;

    /// <summary>
    /// The optional <c>bundles</c> of a finished report (BEH-ORCH-3): <c>[{ slug, text }]</c>, the bundles a triage
    /// run pushed, <c>text</c> null for one too large to send. A 400 when they are malformed, else null.
    /// </summary>
    private static KitResponse? ReadBundles(JsonElement body, out IReadOnlyList<ReportedBundle>? bundles)
    {
        bundles = null;
        if (!body.TryGetProperty("bundles", out var list) || list.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var bad = Error(400, "bad-bundles", $"bundles must be at most {MaxBundles} of {{ \"slug\": <a work-item slug>, \"text\": <string or null> }}, each slug once");
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > MaxBundles)
        {
            return bad;
        }

        var read = new List<ReportedBundle>();
        foreach (var e in list.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object
                || !e.TryGetProperty("slug", out var slug) || slug.ValueKind != JsonValueKind.String
                || slug.GetString() is not { Length: > 0 and <= ContextBundle.MaxSlug } s || !ContextBundle.SlugPattern.IsMatch(s)
                || read.Any(r => r.Slug == s))
            {
                return bad;
            }

            var text = e.TryGetProperty("text", out var t) ? t : default;
            if (text.ValueKind is not (JsonValueKind.String or JsonValueKind.Null or JsonValueKind.Undefined))
            {
                return bad;
            }

            read.Add(new ReportedBundle(s, text.ValueKind == JsonValueKind.String ? text.GetString() : null));
        }

        bundles = read;
        return null;
    }

    private static KitResponse Error(int status, string error, string reason) =>
        new() { Status = status, ContentType = "application/json", CacheControl = "no-store", Body = new ApiError { Error = error, Reason = reason } };
}
