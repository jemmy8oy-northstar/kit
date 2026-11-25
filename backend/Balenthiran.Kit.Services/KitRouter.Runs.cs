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

    private static readonly Regex TicketsRoute = new(@"^/api/projects/([^/]+)/tickets\z", RegexOptions.Compiled);
    private static readonly Regex TicketCommentRoute = new(@"^/api/projects/([^/]+)/tickets/([^/]+)/comments\z", RegexOptions.Compiled);

    /// <summary>The longest reply on a ticket: it goes into a triage task, which is at most <see cref="MaxTask"/>.</summary>
    public const int MaxReply = 1000;
    private static readonly Regex RunsRoute = new(@"^/api/projects/([^/]+)/runs\z", RegexOptions.Compiled);
    private static readonly Regex RepoName = new(@"^[A-Za-z0-9-]+/[A-Za-z0-9._-]+\z", RegexOptions.Compiled);
    private static readonly Regex BranchName = new(@"^(?!.*\.\.)[A-Za-z0-9][A-Za-z0-9._/-]{0,99}\z", RegexOptions.Compiled);

    /// <summary>The longest task a run may be given.</summary>
    public const int MaxTask = 8000;

    public static readonly Regex ModelName = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.Compiled);

    /// <summary>
    /// <c>GET /api/runs</c>: every run newest first, with tokens and cost, and when a runner took each run still going
    /// (BEH-ORCH-17). Operator only: 401 with no session, 403 for another user.
    /// </summary>
    private KitResponse RunsList(string? cookie)
    {
        if (RunsRefusal(cookie) is { } refused)
        {
            return refused;
        }

        var claims = (runnerPool?.Claimed() ?? [])
            .Select(c => (IRunClaim)new RunClaim { RunId = c.RunId, ClaimedAt = c.ClaimedAt, GivesUpAt = c.GivesUpAt, LostAt = c.LostAt })
            .ToList();
        return Json(200, new AgentRunList { CapUsd = agentRunCap, Runs = agentRuns.List(), Claims = claims });
    }

    /// <summary>
    /// <c>GET /api/pipeline</c> (BEH-ORCH-9): the pipeline's work items since Kit last started, oldest first, each with
    /// its bundle's flag and review. Operator only, as runs are; with the pipeline off, <c>on</c> is false and none are listed.
    /// </summary>
    private KitResponse PipelineList(string? cookie)
    {
        if (RunsRefusal(cookie) is { } refused)
        {
            return refused;
        }

        var items = (pipeline?.Items() ?? []).Select(i =>
        {
            var bundle = pipeline!.Bundle(i.Project, i.Slug);
            return (IPipelineItem)new PipelineItem
            {
                Project = i.Project,
                Slug = i.Slug,
                Stage = i.Stage.ToString().ToLowerInvariant(),
                Status = i.Status.ToString().ToLowerInvariant(),
                StoppedBecause = i.StoppedBecause,
                PullRequest = i.PullRequest,
                Merged = i.Merged,
                MergeNote = pipeline!.MergeNoteOf(i),
                Flag = bundle?.Flag,
                Review = bundle?.Review?.Outcome.ToString().ToLowerInvariant(),
                ReviewNote = bundle?.Review?.Note,
            };
        }).ToList();
        return Json(200, new PipelineItemList { On = pipeline is not null, Items = items, Paused = pipeline?.PausedNote() });
    }

    /// <summary>
    /// <c>GET /api/runs/visibility</c> (BEH-VIS-1, BEH-VIS-2): the agents running now with their stage and any flagged review,
    /// and the finished work items with each stage's cost and the reviews they requested. Operator only, as runs are. No
    /// session cost record exists yet (BEH-COST-1), so every stage cost is null: unknown, never zero.
    /// </summary>
    private KitResponse RunVisibilityView(string? cookie)
    {
        if (RunsRefusal(cookie) is { } refused)
        {
            return refused;
        }

        return Json(200, RunVisibilityBuilder.Build(
            agentRuns.List(),
            (project, slug) => pipeline?.Bundle(project, slug)?.Flag,
            NoStageCostRecord.Instance));
    }

    /// <summary>
    /// <c>GET /api/projects/:project/tickets</c> (BEH-ORCH-16): the project's tickets oldest first, each with its state,
    /// its blocked tag and its comment thread. Operator only, as the pipeline is; with the pipeline off, <c>on</c> is
    /// false and none are listed.
    /// </summary>
    private KitResponse TicketList(string? cookie, string rawProject)
    {
        if (RunsRefusal(cookie) is { } refused)
        {
            return refused;
        }

        if (DecodeUriComponent(rawProject) is not { } project)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the project name is not valid percent-encoding" });
        }

        var known = corpora.Corpora();
        if (!known.Contains(project, StringComparer.Ordinal))
        {
            return Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{project}'", Known = known.ToList() });
        }

        var tickets = (pipeline?.Tickets(project) ?? []).Select(t => (ITicketView)new TicketView
        {
            Slug = t.Ticket.Slug,
            Title = t.Ticket.Title,
            State = t.Ticket.State.ToString().ToLowerInvariant(),
            Priority = t.Ticket.Priority,
            Cards = t.Ticket.Cards,
            PullRequest = t.Ticket.PullRequest,
            BlockedBecause = t.Ticket.BlockedBecause,
            Comments = t.Comments.Select(c => (ITicketCommentView)new TicketCommentView
            {
                Author = c.Author,
                Body = c.Body,
                At = c.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            }).ToList(),
        }).ToList();
        return Json(200, new TicketList { On = pipeline is not null, Tickets = tickets });
    }

    /// <summary>
    /// <c>POST /api/projects/:project/tickets/:slug/comments</c> with <c>{ body }</c> (BEH-ORCH-16 slice 3), past the write
    /// gates (so the operator only): puts his reply on the ticket's thread and, when the ticket is blocked where the first
    /// agent can act, wakes it. Answers <c>{ woke }</c>.
    /// </summary>
    private KitResponse TicketReply(string author, string rawProject, string rawSlug, JsonElement? body)
    {
        if (DecodeUriComponent(rawProject) is not { } project || DecodeUriComponent(rawSlug) is not { } slug)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the project or ticket is not valid percent-encoding" });
        }

        var known = corpora.Corpora();
        if (!known.Contains(project, StringComparer.Ordinal))
        {
            return Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{project}'", Known = known.ToList() });
        }

        if (body is not { ValueKind: JsonValueKind.Object } b || Field(b, "body") is not { ValueKind: JsonValueKind.String } given
            || given.GetString()!.Trim() is not { Length: > 0 and <= MaxReply } text)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = $"send {{ body }}: the reply, 1 to {MaxReply} characters" });
        }

        if (pipeline is null)
        {
            return Json(409, new ApiError { Error = "pipeline-off", Reason = "the pipeline is off on this Kit, so it keeps no tickets to reply on" });
        }

        return pipeline.Replied(project, slug, author, text) switch
        {
            Pipeline.Reply.NoTicket => Json(404, new ApiError { Error = "no-such-ticket", Reason = $"{project} has no ticket '{slug}'" }),
            Pipeline.Reply.NotSaved => Json(503, new ApiError { Error = "store-unavailable", Reason = "the ticket store could not be reached, so the reply was not saved; try again" }),
            var r => Json(200, new TicketReplyResult { Woke = r == Pipeline.Reply.Woke }),
        };
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

        string? repo = null;
        string? task = null;
        string? baseBranch = null;
        if (body is { ValueKind: JsonValueKind.Object } rb)
        {
            if (Field(rb, "repo") is { } r && r.ValueKind != JsonValueKind.Null)
            {
                if (r.ValueKind != JsonValueKind.String || !RepoName.IsMatch(r.GetString()!))
                {
                    return Json(400, new ApiError { Error = "bad-request", Reason = "repo, when given, must be owner/name" });
                }

                repo = r.GetString();
            }

            if (Field(rb, "task") is { } t && t.ValueKind != JsonValueKind.Null)
            {
                if (t.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(t.GetString()) || t.GetString()!.Length > MaxTask)
                {
                    return Json(400, new ApiError { Error = "bad-request", Reason = $"task, when given, must be non-empty text of at most {MaxTask} characters" });
                }

                task = t.GetString();
            }

            if (Field(rb, "baseBranch") is { } bb && bb.ValueKind != JsonValueKind.Null)
            {
                if (bb.ValueKind != JsonValueKind.String || !BranchName.IsMatch(bb.GetString()!))
                {
                    return Json(400, new ApiError { Error = "bad-request", Reason = "baseBranch, when given, must be a branch name" });
                }

                baseBranch = bb.GetString();
            }
        }

        var token = AgentTokens.Mint();
        var run = agentRuns.Create(owner, project, model, AgentTokens.Hash(token), (clock ?? TimeProvider.System).GetUtcNow());

        // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
        AgentLaunch launch;
        try
        {
            if (runnerPool is not null && repo is not null && task is not null)
            {
                runnerPool.Enqueue(run, token, repo, task, baseBranch);
                launch = new AgentLaunch(true, RunnerPool.QueuedNote);
            }
            else
            {
                launch = agentLauncher.LaunchAsync(run, token).GetAwaiter().GetResult();
            }
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
