namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A corpus file as one commit left it, reduced to what BEH-BOARD-4 reads.</summary>
public interface ICorpusVersion
{
    /// <summary>The committer date of the commit.</summary>
    DateTimeOffset At { get; }

    /// <summary>Every behaviour in this version, in file order, and whether it was <c>pending</c>.</summary>
    IReadOnlyList<KeyValuePair<string, bool>> Pending { get; }
}
