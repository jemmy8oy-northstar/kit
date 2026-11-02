using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-SPECAGENT-4's routes: talking an Inbox card through with the spec agent. The chat is held by
/// the page and sent whole each turn, so the server keeps nothing but today's count against the cap.
/// Without the optional <c>kit-anthropic</c> key <c>GET /api/triage</c> says <c>configured: false</c>
/// and a turn answers 503, so the Inbox offers no chat.
/// </summary>
public sealed partial class KitRouter
{
    /// <summary>
    /// Replies a day when <c>KIT_TRIAGE_DAILY_LIMIT</c> is unset. My assumption, his to change: a turn on
    /// Haiku re-reads about 10k cached tokens of spec, so a hundred is cents to a few dollars a day at most.
    /// </summary>
    public const int DefaultTriageLimit = 100;

    /// <summary>The most messages one chat sends: past it, the card wants deciding, not more talk.</summary>
    public const int MaxTriageTurns = 40;

    /// <summary>The longest one message may be, in characters.</summary>
    public const int MaxTriageText = 4000;

    private readonly object triageGate = new();
    private DateOnly triageDay;
    private int triageUsed;

    private DateOnly Today() => DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);

    /// <summary>Replies left today; the count starts again at midnight UTC.</summary>
    private int TriageRemaining()
    {
        lock (triageGate)
        {
            return triageDay == Today() ? Math.Max(0, triageLimit - triageUsed) : triageLimit;
        }
    }

    /// <summary>Takes one reply from today's allowance, or says there is none left.</summary>
    private bool TakeTriageTurn()
    {
        lock (triageGate)
        {
            if (triageDay != Today())
            {
                triageDay = Today();
                triageUsed = 0;
            }

            if (triageUsed >= triageLimit)
            {
                return false;
            }

            triageUsed++;
            return true;
        }
    }

    /// <summary>A turn that failed upstream gives its reply back: a refusal must not spend his allowance.</summary>
    private void ReturnTriageTurn()
    {
        lock (triageGate)
        {
            if (triageDay == Today() && triageUsed > 0)
            {
                triageUsed--;
            }
        }
    }

    /// <summary><c>GET /api/triage</c>: whether the chat is on, its model and today's allowance. Another user gets none (BEH-USERS-8).</summary>
    private KitResponse TriageState(string? cookie)
    {
        var on = triage is not null && !IsOtherUser(cookie);
        return Json(200, new TriageStatus
        {
            Configured = on,
            Model = on ? triage!.Model : null,
            Remaining = on ? TriageRemaining() : null,
        });
    }

    /// <summary>
    /// <c>POST /api/projects/:app/triage</c> with <c>{ behaviour, messages: [{ from: "user"|"agent", text }] }</c>:
    /// the spec agent's next reply. Past the write gates, so only the operator reaches here. It writes nothing.
    /// </summary>
    private KitResponse TriageTurn(string app, JsonElement? body)
    {
        if (triage is null)
        {
            return Json(503, new ApiError
            {
                Error = "triage-not-configured",
                Reason = "the spec agent is not switched on on this server yet: the kit-anthropic secret (Anthropic__ApiKey) is not set",
            });
        }

        if (body is not { ValueKind: JsonValueKind.Object } b)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the body must be a JSON object" });
        }

        if (Str(b, "behaviour") is not { Length: > 0 } id)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "behaviour must name the card being talked through" });
        }

        if (Turns(b) is not { } turns)
        {
            return Json(400, new ApiError
            {
                Error = "bad-request",
                Reason = $"messages must be 1 to {MaxTriageTurns} turns of {{ from, text }}, starting and ending with the user's and taking turns, each text 1 to {MaxTriageText} characters",
            });
        }

        if (TriageContext.Build(app, id, corpora.ReadText(app)) is not { } context)
        {
            return Json(404, new ApiError { Error = "no-such-behaviour", Reason = $"'{app}' has no behaviour {id}" });
        }

        if (!TakeTriageTurn())
        {
            return Json(429, new ApiError
            {
                Error = "triage-cap-reached",
                Reason = $"today's {triageLimit} spec-agent replies are used up; the count starts again at midnight UTC",
            });
        }

        // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
        TriageReply reply;
        try
        {
            reply = triage.ReplyAsync(context, turns).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            // Whatever the client did not foresee is still a turn with no reply: it gives the allowance back.
            Console.Error.WriteLine($"kit: a triage turn threw {e.GetType().Name}");
            reply = new TriageReply(null, "the model could not be asked, so there is no reply; try again in a minute");
        }

        if (reply.Text is not { } text)
        {
            ReturnTriageTurn();
            return Json(502, new ApiError { Error = "triage-failed", Reason = reply.Failure ?? "the model gave no reply" });
        }

        return Json(200, new TriageAnswer { Reply = text, Remaining = TriageRemaining() });
    }

    /// <summary>The chat, or null when it is not one: the Messages API wants the user first and last, taking turns.</summary>
    private static List<TriageTurn>? Turns(JsonElement body)
    {
        if (Field(body, "messages") is not { ValueKind: JsonValueKind.Array } messages)
        {
            return null;
        }

        var turns = new List<TriageTurn>();
        foreach (var m in messages.EnumerateArray())
        {
            var from = Str(m, "from");
            if (from is not ("user" or "agent")
                || Str(m, "text") is not { } raw
                || raw.Trim() is not { Length: > 0 and <= MaxTriageText } text)
            {
                return null;
            }

            var user = from == "user";
            if (user != (turns.Count % 2 == 0))
            {
                return null;
            }

            turns.Add(new TriageTurn(user, text));
        }

        return turns.Count is > 0 and <= MaxTriageTurns && turns[^1].FromUser ? turns : null;
    }
}
