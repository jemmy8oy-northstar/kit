using System.Text.Json;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>The one definition of how engine output is serialised.</summary>
public interface IEngineJsonSerialiser
{
    /// <summary>
    /// The settings under which <c>System.Text.Json</c> reproduces
    /// <c>JSON.stringify(value, null, 2)</c> byte-for-byte.
    /// </summary>
    JsonSerializerOptions Options { get; }

    /// <summary>
    /// The serialised form, including the trailing newline <c>conformance.js</c>
    /// writes. Serialises the value's RUNTIME type, so a result handed back
    /// through an interface is written in its concrete shape.
    /// </summary>
    string Serialise(object value);
}
