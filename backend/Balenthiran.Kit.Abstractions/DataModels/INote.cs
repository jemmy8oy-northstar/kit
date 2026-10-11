namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One open note (kit#118): free text, not a behaviour, waiting to be folded into the spec.</summary>
public interface INote
{
    /// <summary>When it was left, UTC, to the minute: <c>2026-10-10 20:41Z</c>.</summary>
    string At { get; }

    /// <summary>The behaviour it was left on, or null for one left on the project.</summary>
    string? Behaviour { get; }

    string Text { get; }
}
