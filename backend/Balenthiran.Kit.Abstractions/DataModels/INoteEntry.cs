namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One section written under a note: the folder's questions, or the answer to them.</summary>
public interface INoteEntry
{
    /// <summary><c>questions</c> or <c>answer</c>.</summary>
    string Kind { get; }

    /// <summary>When it was written, if its heading says; a hand-written round of questions may not.</summary>
    string? At { get; }

    string Text { get; }
}
