using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>The read model behind the project list and the behaviour page (<c>project.js</c>).</summary>
public interface IProjectViewer
{
    /// <summary>
    /// The full projection of one corpus. Throws <see cref="Exceptions.ProjectionFailedException"/>
    /// when it cannot be built — a corpus that will not parse, or parses to nothing.
    /// </summary>
    IProjectView View(string app);

    /// <summary>
    /// One list row: an <see cref="IProjectSummary"/>, or an <see cref="IProjectError"/> for a
    /// corpus that could not be projected — never a row with zero behaviours.
    /// </summary>
    object Summary(string app);
}
