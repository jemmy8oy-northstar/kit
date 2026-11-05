namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>
/// A commit into a project's own repository was not made (BEH-USERS-12): unreachable, refused, or
/// <see cref="Stale"/> because someone else changed the file first. The message names the layer that
/// failed and never holds the token.
/// </summary>
public sealed class GitHubWriteException : AppException
{
    public GitHubWriteException(string message, bool stale = false)
        : base(message)
    {
        Stale = stale;
    }

    public GitHubWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>GitHub refused because the file is no longer the blob Kit read.</summary>
    public bool Stale { get; }

    public override string ErrorCode => "github-write";
}
