namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of <c>GET /api/triage</c> (BEH-SPECAGENT-4): can this Kit talk a card through, and with which model.</summary>
public interface ITriageStatus
{
    /// <summary>False until the <c>kit-anthropic</c> key is set; the Inbox then offers no chat.</summary>
    bool Configured { get; }

    /// <summary>The model replies come from; null when not configured.</summary>
    string? Model { get; }

    /// <summary>Replies left today under the server's daily cap; null when not configured.</summary>
    int? Remaining { get; }
}
