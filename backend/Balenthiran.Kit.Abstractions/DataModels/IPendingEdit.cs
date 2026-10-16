namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// One commit on the edits branch that the base does not have yet (BEH-COMMIT-5) — one edit made
/// in the hosted Kit, read back from the message <c>GitStore</c> wrote for it
/// (<c>kit: adjudicate BEH-ORDER-1 (james-habits-app)</c>).
/// </summary>
public interface IPendingEdit
{
    /// <summary>The commit's short sha, enough to find it on GitHub.</summary>
    string Sha { get; }

    /// <summary>The message without its <c>kit: </c> prefix or its <c>(app)</c> suffix: <c>adjudicate BEH-ORDER-1</c>.</summary>
    string Summary { get; }

    /// <summary>The project the edit was made in, or null for a commit Kit did not write.</summary>
    string? App { get; }

    /// <summary>The first behaviour id the message names, or null (a note names none).</summary>
    string? Behaviour { get; }

    /// <summary>When it was committed, as GitHub reports it (ISO 8601).</summary>
    string? When { get; }
}
