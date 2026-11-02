namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of a triage turn (BEH-SPECAGENT-4): the spec agent's reply.</summary>
public interface ITriageAnswer
{
    /// <summary>What the spec agent said.</summary>
    string Reply { get; }

    /// <summary>Replies left today under the server's daily cap.</summary>
    int Remaining { get; }

    /// <summary>What the Chat page's agent asked to be done with the item in hand (BEH-CHAT-6); null for none.</summary>
    TriageAction? Action { get; }
}
