using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Which pages Kit trusts (<c>ui.js</c>'s <c>originAllowed()</c>, <c>isLoopback()</c> and
/// <c>crossOriginWrite()</c>): loopback, and the one configured public origin compared
/// as a whole parsed origin. Asked by CORS (who may READ a reply) and by the write gate
/// (who may WRITE) — one answer, so the two cannot disagree.
/// </summary>
public interface IOriginPolicy
{
    bool Allowed(string origin);

    /// <summary>The 403 body for a write from a page this server does not serve, or null when the Origin is absent or allowed.</summary>
    IApiError? RefuseWrite(string? origin);
}
