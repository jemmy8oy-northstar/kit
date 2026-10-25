using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-NOTIFY-3: delivers one encrypted payload to one device through its push service. A fake stands in for the network in tests.</summary>
public interface IPushSender
{
    /// <summary>Sends <paramref name="payload"/> (JSON text). Never throws for a push service's refusal: that is a <see cref="PushOutcome"/>.</summary>
    Task<PushOutcome> SendAsync(IPushSubscription subscription, string payload, CancellationToken cancellationToken = default);
}
