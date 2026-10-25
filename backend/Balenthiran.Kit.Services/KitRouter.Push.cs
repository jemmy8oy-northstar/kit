using System.Text.Json;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-NOTIFY-3's routes. Every one is signed-in only, like the feed it announces. Without a VAPID
/// key pair (the optional <c>kit-push</c> secret) <c>GET /api/push</c> says <c>configured: false</c>
/// and the subscription routes answer 503, so the page can say push is not switched on yet.
/// </summary>
public sealed partial class KitRouter
{
    private const int MaxEndpoint = 2048;
    private const int MaxKey = 256;

    private bool PushConfigured => pushStore is not null && !string.IsNullOrEmpty(pushPublicKey);

    /// <summary><c>GET /api/push</c>: can this Kit push, and with which public key.</summary>
    private KitResponse PushState(string? cookie)
    {
        // Another user has no feed (BEH-USERS-8), so nothing to push to them.
        var on = PushConfigured && !IsOtherUser(cookie);
        return Json(200, new PushStatus { Configured = on, PublicKey = on ? pushPublicKey : null });
    }

    /// <summary>
    /// <c>POST /api/push/subscriptions</c> stores the browser's subscription (idempotent on its
    /// endpoint); <c>POST /api/push/subscriptions/remove</c> forgets <c>{ endpoint }</c>. Past the
    /// write gates, so only the operator reaches here. Remove is a POST, not a DELETE, because Kit
    /// serves GET and POST only (405 otherwise, pinned by the routes conformance).
    /// </summary>
    private KitResponse PushWrite(string pathname, JsonElement? body)
    {
        var remove = pathname == "/api/push/subscriptions/remove";
        if (!remove && pathname != "/api/push/subscriptions")
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = $"nothing accepts a POST at {pathname}" });
        }

        if (!PushConfigured)
        {
            return Json(503, new ApiError
            {
                Error = "push-not-configured",
                Reason = "push is not switched on on this server yet: the kit-push secret (VAPID public-key and private-key) is not set",
            });
        }

        if (body is not { ValueKind: JsonValueKind.Object } b)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the body must be a JSON object" });
        }

        if (Str(b, "endpoint") is not { Length: > 0 and <= MaxEndpoint } endpoint
            || !endpoint.StartsWith("https://", StringComparison.Ordinal))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "endpoint must be the https URL of the browser's push subscription" });
        }

        if (remove)
        {
            pushStore!.Remove(endpoint);
            return Json(200, new PushStatus { Configured = true, PublicKey = pushPublicKey });
        }

        if (Field(b, "keys") is not { ValueKind: JsonValueKind.Object } keys
            || Str(keys, "p256dh") is not { Length: > 0 and <= MaxKey } p256dh
            || Str(keys, "auth") is not { Length: > 0 and <= MaxKey } auth)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "keys.p256dh and keys.auth must be the strings the browser's subscription holds" });
        }

        pushStore!.Add(new PushSubscription { Endpoint = endpoint, P256dh = p256dh, Auth = auth });
        return Json(200, new PushStatus { Configured = true, PublicKey = pushPublicKey });
    }
}
