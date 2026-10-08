using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// WHATWG URL parsing, as far as Kit reads it: <c>new URL(input)</c> and
/// <c>new URL(input, 'http://localhost')</c>. .NET's <c>Uri</c> is not this: it does not
/// resolve <c>%2e%2e</c>, treat <c>\</c> as <c>/</c> or throw where <c>new URL</c> throws.
/// </summary>
public interface IUrlParser
{
    /// <summary>
    /// Parse <paramref name="input"/>, relative to <c>http://localhost/</c> when
    /// <paramref name="againstLocalhost"/> is set. Null wherever <c>new URL</c> would throw.
    /// </summary>
    IParsedUrl? Parse(string input, bool againstLocalhost);
}
