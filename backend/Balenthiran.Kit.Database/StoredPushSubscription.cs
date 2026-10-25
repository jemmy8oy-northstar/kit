using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary>A subscription as <see cref="PostgresPushStore"/> reads it back; the Database layer has no reference to the models.</summary>
internal sealed class StoredPushSubscription : IPushSubscription
{
    public required string Endpoint { get; init; }

    public required string P256dh { get; init; }

    public required string Auth { get; init; }
}
