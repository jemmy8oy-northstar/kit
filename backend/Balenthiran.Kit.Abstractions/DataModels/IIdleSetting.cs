namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>How busy the agent should be when nothing is asked of it (BEH-SETTINGS-5).</summary>
public interface IIdleSetting
{
    /// <summary>One of <c>on-change</c>, <c>health</c>, <c>ideas</c>, <c>idle</c>.</summary>
    string Idle { get; }
}
