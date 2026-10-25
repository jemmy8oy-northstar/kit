namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What the push service answered to one push (BEH-NOTIFY-3).</summary>
public enum PushOutcome
{
    /// <summary>Accepted for delivery.</summary>
    Sent,

    /// <summary>404 or 410: the device unsubscribed or the subscription expired, so it is deleted.</summary>
    Gone,

    /// <summary>Any other failure: logged, and the subscription kept for the next push.</summary>
    Failed,
}
