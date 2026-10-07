namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>A corpus could not be projected. The message is the reason a reader is shown — never a stack.</summary>
public sealed class ProjectionFailedException : AppException
{
    public ProjectionFailedException(string message)
        : base(message)
    {
    }

    public ProjectionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "projection-failed";
}
