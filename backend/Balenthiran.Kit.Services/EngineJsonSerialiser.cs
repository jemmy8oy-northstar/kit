using System.Text.Encodings.Web;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The one definition of how engine output is serialised.
///
/// These options are not a style preference: they are the settings under which
/// <c>System.Text.Json</c> reproduces <c>JSON.stringify(value, null, 2)</c>
/// byte-for-byte, which is the only reason the committed conformance goldens can
/// score this port at all. Measured over all eleven goldens before a line of the
/// port was written, and each setting has a red control in
/// <c>ConformanceTests</c> that turns the comparison red when it is changed.
/// </summary>
public sealed class EngineJsonSerialiser : IEngineJsonSerialiser
{
    /// <summary>
    /// ⚠️ <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> is
    /// LOAD-BEARING, not a loosening. The default encoder escapes every
    /// codepoint above U+007F and also <c>&amp;</c>, <c>+</c> and <c>'</c> inside
    /// Basic Latin, while <c>JSON.stringify</c> emits all of them raw. The
    /// goldens contain 36 apostrophes, 52 em-dashes, a CJK character and an
    /// ampersand, so the default encoder differs from Node on at least three
    /// corpora. "Unsafe" there means unsafe to interpolate into HTML, which is
    /// not what these bytes are for.
    /// </summary>
    public JsonSerializerOptions Options { get; } = Build();

    /// <inheritdoc />
    public string Serialise(object value) => JsonSerializer.Serialize(value, value.GetType(), Options) + "\n";

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions
        {
            // `JSON.stringify(x, null, 2)`. Two spaces is System.Text.Json's
            // default indent, and `IndentSize` is set anyway so that a change to
            // that default cannot silently move the goldens.
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // The engine's keys are camelCase; the C# properties are PascalCase.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        // ⚠️ `populateMissingResolver: true`, not the no-arg overload. The bare
        // `MakeReadOnly()` THROWS unless a TypeInfoResolver has already been set
        // ("JsonSerializerOptions instance must specify a TypeInfoResolver
        // setting before being marked as read-only"). When this was a static
        // initialiser that surfaced as a TypeInitializationException on first
        // use — every conformance case failed with a stack trace rather than a
        // diff, and none of them had reached the parser at all.
        //
        // The settings above were measured against all eleven goldens in a
        // throwaway probe project that never called MakeReadOnly. The VALUES were
        // right and the DELIVERY was untested: a measurement of the options is
        // not a measurement of the object the engine actually serialises with.
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
