namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The 200 answer to a write: what changed, where, and — every time — what was NOT done.
/// A behaviour edit names <see cref="Behaviour"/>; a bind names <see cref="Noun"/> and
/// says which other corpora use the same name.
/// </summary>
public interface IWriteOutcome
{
    bool Ok { get; }

    string App { get; }

    string? Behaviour { get; }

    string? Noun { get; }

    /// <summary>Repository-relative, <c>/</c>-separated, as a project view's <c>corpus</c>.</summary>
    string File { get; }

    bool Committed { get; }

    string Note { get; }

    IReadOnlyList<string>? SharedWith { get; }

    /// <summary>Corpora skipped because they will not parse — so <see cref="SharedWith"/> is incomplete, and says so.</summary>
    IReadOnlyList<string>? UnreadableCorpora { get; }
}
