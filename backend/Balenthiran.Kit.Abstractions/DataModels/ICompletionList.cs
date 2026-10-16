namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The body of <c>GET /api/projects/{app}/history</c> (BEH-BOARD-4): when each built behaviour was
/// completed, read from git rather than stored in the <c>.beh</c>. Apart from the project view, as
/// the notes are, so that view's shape and its goldens stay put and a history that cannot be read
/// costs the board its order, never its cards.
/// </summary>
public interface ICompletionList
{
    string App { get; }

    /// <summary>The corpus, relative to the repository root, whose history this is.</summary>
    string File { get; }

    /// <summary><c>git</c> when the history was read; <c>none</c> when it could not be, and <see cref="Reason"/> says why.</summary>
    string History { get; }

    /// <summary>Why there is no history — no work tree, no clone of a project read from GitHub, git not runnable. Null when there is.</summary>
    string? Reason { get; }

    /// <summary>Every behaviour the corpus says is built, in corpus order. Empty when <see cref="History"/> is <c>none</c>.</summary>
    IReadOnlyList<ICompletion> Completed { get; }
}
