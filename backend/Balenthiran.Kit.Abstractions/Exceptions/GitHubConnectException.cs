namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>A user's GitHub could not be confirmed as theirs (BEH-USERS-1). The message is shown — never a code or a token.</summary>
public sealed class GitHubConnectException : AppException
{
    public GitHubConnectException(string message)
        : base(message)
    {
    }

    public GitHubConnectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "github-connect";
}
