namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of a triage turn (BEH-SPECAGENT-4): the spec agent's reply.</summary>
public interface ITriageAnswer
{
    /// <summary>What the spec agent said.</summary>
    string Reply { get; }

    /// <summary>Replies left today under the server's daily cap.</summary>
    int Remaining { get; }
}
