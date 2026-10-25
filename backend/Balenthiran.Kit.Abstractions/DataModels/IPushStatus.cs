namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of <c>GET /api/push</c> (BEH-NOTIFY-3): can this Kit push, and with which public key.</summary>
public interface IPushStatus
{
    /// <summary>False until the <c>kit-push</c> key pair is set; the page then says push is not switched on.</summary>
    bool Configured { get; }

    /// <summary>The VAPID public key, base64url, which the browser subscribes with; null when not configured.</summary>
    string? PublicKey { get; }
}
