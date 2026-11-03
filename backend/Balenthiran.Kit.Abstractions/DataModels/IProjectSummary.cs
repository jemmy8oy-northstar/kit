namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One row of the project list: counts, not the projection.</summary>
public interface IProjectSummary
{
    string App { get; }

    string Corpus { get; }

    bool NotReal { get; }

    string? DuplicateOf { get; }

    int Behaviours { get; }

    int Conflicts { get; }

    ICoverageSummary Coverage { get; }

    int Unreviewed { get; }

    /// <summary>
    /// The project this corpus is shown under (kit#237): its <c># kit:project</c> directive, or
    /// its own name when it has none. Files sharing one are one project.
    /// </summary>
    string Project { get; }

    /// <summary>A <c># kit:idea</c> file (kit#284): app ideas that are not projects yet, kept off the board.</summary>
    bool Idea { get; }

    /// <summary><c>"self"</c> when the agent may promote this project's <c>dev</c> to <c>main</c> itself (BEH-SETTINGS-4); <c>null</c> otherwise.</summary>
    string? Promote { get; }

    /// <summary><c>"maintained"</c> when the project is marked as kept going, not worked towards (BEH-NARR-2); <c>null</c> otherwise.</summary>
    string? Goal { get; }

    /// <summary><c>"ask"</c> or <c>"wait"</c> when the project says what the agent does when specs conflict (BEH-CONFLICT-2); <c>null</c> for the default, build anyway.</summary>
    string? OnConflict { get; }

    /// <summary><c>"goals"</c> or <c>"architecture"</c> when the project says how involved its user is (BEH-ARCH-3); <c>null</c> for the default, behaviours.</summary>
    string? Depth { get; }
}
