namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Which projects may be read without signing in (kit-hosted.beh BEH-GATE-H1, kit#795), kept in
/// memory so a request never waits on GitHub. Absent altogether in local mode, where no project is.
/// </summary>
public interface IProjectVisibility
{
    /// <summary>
    /// True only when <paramref name="app"/>'s <c># kit:repo</c> line names a repository GitHub last
    /// said is public. No line, a private repo, a failed read, or no read yet: false.
    /// </summary>
    bool IsPublic(string app);
}
