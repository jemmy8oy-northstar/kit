namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>When the operator last asked the agent to wake (BEH-SETTINGS-7).</summary>
public interface IWakeSetting
{
    /// <summary>The ISO 8601 UTC time of the last press, or null when never asked.</summary>
    string? Requested { get; }
}
