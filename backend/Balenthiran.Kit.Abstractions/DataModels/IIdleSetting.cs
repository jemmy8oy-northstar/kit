namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The agent's idle work, in his order (BEH-SETTINGS-6; BEH-SETTINGS-5's <c>idle</c> still served).</summary>
public interface IIdleSetting
{
    /// <summary>The first item on, or <c>idle</c> when none is.</summary>
    string Idle { get; }

    /// <summary>The items switched on, <c>health</c> and/or <c>ideas</c>, highest priority first.</summary>
    IReadOnlyList<string> Order { get; }
}
