namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>The states a run is in (BEH-USERS-9).</summary>
public static class AgentRunStatus
{
    /// <summary>Created; nothing has started it.</summary>
    public const string Queued = "queued";

    /// <summary>Its first model call has arrived.</summary>
    public const string Running = "running";

    /// <summary>Done; its token no longer reaches the model.</summary>
    public const string Finished = "finished";

    /// <summary>Died; its token no longer reaches the model.</summary>
    public const string Failed = "failed";

    /// <summary>Kit would not start it; its token reaches nothing.</summary>
    public const string Refused = "refused";

    /// <summary>The runs whose token no longer reaches the model.</summary>
    public static bool IsOver(string status) => status is Finished or Failed or Refused;
}
