namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Kit's feature switches as seen by the current session (BEH-FLAGS-1, BEH-FLAGS-2).</summary>
public interface IFeatureSwitches
{
    /// <summary>The caller's group: <c>operator</c> or <c>testers</c>.</summary>
    string Group { get; }

    /// <summary>The ids of Kit's features that are switched off for the caller's group.</summary>
    IReadOnlyList<string> Off { get; }

    /// <summary>Every feature with both switches. Served to the operator only; null for a test user.</summary>
    IReadOnlyList<IFeatureSwitch>? Features { get; }
}
