namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One open note (kit#118): free text, not a behaviour, waiting to be folded into the spec.</summary>
public interface INote
{
    /// <summary>When it was left, UTC, to the minute: <c>2026-10-10 20:41Z</c>.</summary>
    string At { get; }

    /// <summary>
    /// <c>note</c>, a free comment; <c>request</c>, a behaviour asked for in prose (BEH-NOTE-4); or
    /// <c>resolution</c>, how a decision on the question sheet should go (BEH-NOTE-8).
    /// </summary>
    string Kind { get; }

    /// <summary>The behaviour it was left on, or null for one left on the project.</summary>
    string? Behaviour { get; }

    /// <summary>The question-sheet key a <c>resolution</c> answers; null for every other kind.</summary>
    string? Resolves { get; }

    /// <summary>What was typed, without anything written under it since.</summary>
    string Text { get; }

    /// <summary>Questions and answers written under the note, oldest first (BEH-NOTE-5/6).</summary>
    IReadOnlyList<INoteEntry> Thread { get; }

    /// <summary>
    /// The latest round of questions while nobody has answered it; empty otherwise. Non-empty is what
    /// puts a note under "Needs your answer".
    /// </summary>
    IReadOnlyList<string> Questions { get; }
}
