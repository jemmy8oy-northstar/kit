namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The parts of a WHATWG URL that Kit reads: what <c>new URL()</c> exposes as
/// <c>protocol</c>, <c>hostname</c>, <c>pathname</c> and <c>origin</c>.
/// </summary>
public interface IParsedUrl
{
    /// <summary>Lower-case, without the colon.</summary>
    string Scheme { get; }

    /// <summary>The serialised host (<c>hostname</c>): lower-case domain, dotted IPv4, or bracketed IPv6. Empty when there is none.</summary>
    string Host { get; }

    /// <summary>The path, dot segments resolved and percent-encoded as <c>new URL</c> leaves it.</summary>
    string Pathname { get; }

    /// <summary><c>scheme://host[:port]</c> with a default port dropped, or <c>"null"</c> for a scheme with no tuple origin.</summary>
    string Origin { get; }
}
