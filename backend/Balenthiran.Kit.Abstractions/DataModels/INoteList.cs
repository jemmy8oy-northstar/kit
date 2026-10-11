namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of <c>GET /api/projects/{app}/notes</c>: every note still open, oldest first.</summary>
public interface INoteList
{
    string App { get; }

    /// <summary>Where the notes live, relative to the repository root — whether or not the file exists yet.</summary>
    string File { get; }

    IReadOnlyList<INote> Notes { get; }
}
