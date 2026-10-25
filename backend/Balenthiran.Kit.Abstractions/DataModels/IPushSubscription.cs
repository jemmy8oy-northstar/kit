namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One phone's push subscription (BEH-NOTIFY-3), as the browser's <c>PushSubscription.toJSON()</c> gives it.</summary>
public interface IPushSubscription
{
    /// <summary>The push service's URL for this device; the key a subscription is stored and removed by.</summary>
    string Endpoint { get; }

    /// <summary>The device's public key, base64url, which the payload is encrypted to.</summary>
    string P256dh { get; }

    /// <summary>The device's authentication secret, base64url.</summary>
    string Auth { get; }
}
