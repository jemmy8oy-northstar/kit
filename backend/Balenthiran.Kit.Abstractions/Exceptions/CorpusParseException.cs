namespace Balenthiran.Kit.Abstractions.Exceptions;

/// <summary>
/// A corpus that is not a corpus. Carries the Node engine's message verbatim,
/// because these strings are what a human writing a tree by hand reads.
/// </summary>
/// <remarks>
/// A new <see cref="AppException"/> subclass rather than
/// <see cref="AppException"/>'s nearest shipped neighbour, per the template's
/// rule for a failure with no clean home: <c>invalid_input</c> means "well-formed
/// but rejected by a business rule", and a corpus that fails to parse is not
/// well-formed at all.
/// </remarks>
public sealed class CorpusParseException : AppException
{
    public CorpusParseException(string message)
        : base(message)
    {
    }

    public CorpusParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "invalid_corpus";
}
