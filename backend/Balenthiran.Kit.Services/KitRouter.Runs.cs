using System.Text.RegularExpressions;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's operator routes: starting an agent run on a project and listing runs with what they
/// cost. Both are the operator's: a run spends Kit's own Anthropic credit. The model proxy a run calls
/// is not here (a stream cannot be a <see cref="KitResponse"/>): it is <see cref="AgentProxy"/>, mounted by the host.
/// </summary>
public sealed partial class KitRouter
{
    /// <summary>The model a run uses when the request names none: Sonnet, half Opus's price per token.</summary>
    public const string DefaultRunModel = "claude-sonnet-5-5";

    private static readonly Regex RunsRoute = new(@"^/api/projects/([^/]+)/runs\z", RegexOptions.Compiled);
    private static readonly Regex ModelName = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.Compiled);

    /// <summary><c>GET /api/runs</c>: every run newest first, with tokens and cost. Operator only: 401 with no session, 403 for another user.</summary>
    private KitResponse RunsList(string? cookie)
    {
        if (RunsRefusal(cookie) is { } refused)
        {
            return refused;
        }

        return Json(200, new AgentRunList { CapUsd = agentRunCap, Runs = agentRuns.List() });
    }

    private KitResponse? RunsRefusal(string? cookie)
    {
        if (lockRequired)
        {
            return UserOf(cookie) switch
            {
                null => Json(401, new ApiError { Error = "not-signed-in", Reason = "agent runs are listed only to a signed-in session" }),
                ISessionStore.Operator => null,
                _ => Json(403, new ApiError { Error = "not-yours", Reason = "agent runs are the operator's: they spend Kit's own credit" }),
            };
        }

        return OriginPolicy.IsLoopback(host)
            ? null
            : Json(401, new ApiError { Error = "not-signed-in", Reason = "agent runs are served only to loopback on a Kit with no password" });
    }

    /// <summary>
    /// <c>POST /api/projects/:project/runs</c> with an optional <c>{ model }</c>, past the write gates (so the
    /// operator only): records a queued run, mints its token, hands both to the launcher, and answers with the
    /// token this once. Only the token's SHA-256 is stored.
    /// </summary>
    private KitResponse StartRun(string owner, string rawProject, JsonElement? body)
    {
        if (DecodeUriComponent(rawProject) is not { } project)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the project name is not valid percent-encoding" });
        }

        var known = corpora.Corpora();
        if (!known.Contains(project, StringComparer.Ordinal))
        {
            return Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{project}'", Known = known.ToList() });
        }

        var model = DefaultRunModel;
        if (body is { ValueKind: JsonValueKind.Object } b && Field(b, "model") is { } given && given.ValueKind != JsonValueKind.Null)
        {
            if (given.ValueKind != JsonValueKind.String || !ModelName.IsMatch(given.GetString()!))
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "model, when given, must be a model id such as claude-sonnet-5-5" });
            }

            model = given.GetString()!;
        }
        else if (body is { } other && other.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the body must be a JSON object, or empty" });
        }

        var token = AgentTokens.Mint();
        var run = agentRuns.Create(owner, project, model, AgentTokens.Hash(token), (clock ?? TimeProvider.System).GetUtcNow());

        // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
        AgentLaunch launch;
        try
        {
            launch = agentLauncher.LaunchAsync(run, token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            // The type only: the exception may carry what the launcher was told, and the token is in it.
            Console.Error.WriteLine($"kit: launching an agent run threw {e.GetType().Name}");
            agentRuns.SetStatus(run.Id, AgentRunStatus.Failed, (clock ?? TimeProvider.System).GetUtcNow());
            launch = new AgentLaunch(false, "the runner failed to start the run");
        }

        // The answer carries the run token once, so nothing may keep a copy of it.
        return new KitResponse
        {
            Status = 201,
            ContentType = "application/json",
            CacheControl = "no-store",
            Body = new AgentRunStarted { Run = agentRuns.ByTokenHash(AgentTokens.Hash(token)) ?? run, Token = token, Launch = launch.Note },
        };
    }
}
