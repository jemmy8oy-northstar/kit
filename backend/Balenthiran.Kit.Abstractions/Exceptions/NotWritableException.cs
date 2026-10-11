namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>
/// A write to a project this Kit can read but not save (kit#88): it comes from a repository the pod
/// has no clone of. A statement about the request, so it is answered 409 with this sentence — not
/// a 500 that reads as Kit being broken.
/// </summary>
public sealed class NotWritableException : AppException
{
    public NotWritableException(string message)
        : base(message)
    {
    }

    public NotWritableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "not-writable";
}
