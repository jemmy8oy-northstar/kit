using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-4's two inputs to a user's agent: the one generic dev process every user shares
/// (<c>GET /api/process</c>, the same text for every caller) and that user's own memory
/// (<c>/api/me/memory</c>). The memory routes act on the signed-in account's id and read no id
/// from the request, so there is no way to ask for another user's.
/// </summary>
public sealed partial class KitRouter
{
    /// <summary>The most a user's memory may hold, in UTF-8 bytes.</summary>
    public const int MaxMemoryBytes = 64 * 1024;

    private const string ProcessResource = "process/agent.md";

    /// <summary><c>GET /api/process</c>: the shared dev process, embedded in the build.</summary>
    private static KitResponse Process()
    {
        using var stream = typeof(KitRouter).Assembly.GetManifestResourceStream(ProcessResource)
            ?? throw new InvalidOperationException($"{ProcessResource} is not embedded in the build");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return new KitResponse { Status = 200, ContentType = "text/markdown; charset=utf-8", CacheControl = "no-store", Raw = copy.ToArray() };
    }

    /// <summary>
    /// <c>GET /api/me/memory</c>: the signed-in user's memory ("" before they write any). With a
    /// password, the session says who; with none, only loopback reaches it and it is the operator's.
    /// </summary>
    private KitResponse MemoryRead(string? cookie) =>
        MemoryOwner(cookie) is { } me
            ? Json(200, new UserMemory { Body = userMemory.Read(me) ?? string.Empty })
            : MemoryRefused();

    /// <summary><c>POST /api/me/memory</c> with <c>{ body }</c> replaces the signed-in user's memory; past the write gates.</summary>
    private KitResponse MemoryWrite(string me, JsonElement? body)
    {
        if (body is not { ValueKind: JsonValueKind.Object } b || Str(b, "body") is not { } text)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the body must be a JSON object with a string 'body'" });
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxMemoryBytes)
        {
            return Json(413, new ApiError { Error = "too-large", Reason = $"memory holds at most {MaxMemoryBytes / 1024} KB" });
        }

        userMemory.Write(me, text);
        return Json(200, new UserMemory { Body = text });
    }

    private string? MemoryOwner(string? cookie) =>
        lockRequired ? UserOf(cookie) : OriginPolicy.IsLoopback(host) ? ISessionStore.Operator : null;

    private static KitResponse MemoryRefused() => Json(401, new ApiError
    {
        Error = "not-signed-in",
        Reason = "a user's memory is served only to that user's own signed-in session",
    });
}
