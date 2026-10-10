namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>A configured GitHub credential could not be turned into a token. The message is shown — never the key or the JWT.</summary>
public sealed class GitHubTokenException : AppException
{
    public GitHubTokenException(string message)
        : base(message)
    {
    }

    public GitHubTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "github-token";
}
