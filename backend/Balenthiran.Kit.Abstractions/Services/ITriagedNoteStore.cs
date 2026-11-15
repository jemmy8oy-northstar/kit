namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-ORCH-8: the notes a finished triage has already sorted, per project, so a restart does not pay to triage
/// them again. A note is named by a key the pipeline makes from its date and text; the store only keeps keys.
/// </summary>
public interface ITriagedNoteStore
{
    /// <summary>The keys of <paramref name="project"/>'s sorted notes; empty when none are recorded.</summary>
    IReadOnlySet<string> Read(string project);

    /// <summary>Record <paramref name="keys"/> as sorted on <paramref name="project"/>. A key already recorded is left as it is.</summary>
    void Add(string project, IReadOnlyCollection<string> keys);

    /// <summary>Every project with at least one sorted note recorded.</summary>
    IReadOnlyList<string> Projects();
}
