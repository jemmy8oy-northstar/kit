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

    /// <summary>A refusal the spec names (BadFeatureTag, MissingParentTag), with its message.</summary>
    public CorpusParseException(string refusal, string message)
        : base(message)
    {
        Refusal = refusal;
    }

    public CorpusParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The refusal's name as the behaviour card gives it (BEH-PARSE-2 BadFeatureTag, BEH-PARSE-6
    /// MissingParentTag); null for a refusal no card names.
    /// </summary>
    public string? Refusal { get; }

    public override string ErrorCode => "invalid_corpus";
}
