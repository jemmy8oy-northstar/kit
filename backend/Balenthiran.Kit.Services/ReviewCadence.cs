namespace Balenthiran.Kit.Services;

/// <summary>How often a project's scheduled review runs (kit-ui.beh <c>BEH-ORCH-13</c>). Off is the start state of every project.</summary>
public enum ReviewCadence
{
    /// <summary>No review runs for the project.</summary>
    Off,

    /// <summary>One review a day.</summary>
    Daily,

    /// <summary>One review every two days.</summary>
    EveryTwoDays,

    /// <summary>One review a week.</summary>
    Weekly,
}
