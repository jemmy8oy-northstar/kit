namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>
/// GitHub could not be read: unreachable, too slow, refused, or answered something Kit cannot
/// read. Could-not-look, never an empty project list. The message names the layer that failed and
/// never holds the token.
/// </summary>
public sealed class GitHubReadException : AppException
{
    public GitHubReadException(string message)
        : base(message)
    {
    }

    public GitHubReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "github-read";
}
