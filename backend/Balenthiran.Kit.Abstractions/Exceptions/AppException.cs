namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>
/// Base type for every <em>anticipated, gracefully handled</em> failure the app can raise.
/// Services throw a concrete subclass when they hit a recognised failure — a missing
/// resource, a conflict, invalid input, an upstream outage — and the global exception
/// handler in the WebApi turns it into an RFC 7807 <c>ProblemDetails</c> response.
/// Anything that is <em>not</em> an <see cref="AppException"/> is an unexpected fault and
/// becomes a generic 500 (with no internal detail leaked).
/// </summary>
/// <remarks>
/// This carries only a stable, machine-readable <see cref="ErrorCode"/> — never an HTTP
/// status code. The mapping from exception type to HTTP status lives at the WebApi boundary,
/// so no HTTP concerns leak into the service or abstraction layers. It sits in
/// <c>Abstractions</c> as shared domain vocabulary: both the throw-site (Services) and the
/// catch-site (WebApi) already reference this project.
/// <para>
/// <strong>Extending it:</strong> when a failure has no clean home among the subclasses
/// shipped here, add a <em>new</em> <see cref="AppException"/> subclass (its own file, a
/// distinct <see cref="ErrorCode"/>) and map it in <c>WebApi/ExceptionHandling/ExceptionResult</c>
/// — do not force it into an ill-fitting existing type.
/// </para>
/// </remarks>
public abstract class AppException : Exception
{
    protected AppException(string message)
        : base(message)
    {
    }

    protected AppException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// A stable, machine-readable code for this failure kind (e.g. <c>not_found</c>),
    /// surfaced in the response so clients can branch on it without parsing prose.
    /// </summary>
    public abstract string ErrorCode { get; }
}
