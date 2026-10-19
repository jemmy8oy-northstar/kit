namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// An <c>epic</c> block (FEAT-EPICS, BEH-EPIC-2): a goal defined top down. Its parent is the
/// file's project, and a feature joins it with an <c>in EPIC-…</c> line.
/// </summary>
public interface IEpic
{
    string Id { get; }

    string Title { get; }

    string At { get; }
}
