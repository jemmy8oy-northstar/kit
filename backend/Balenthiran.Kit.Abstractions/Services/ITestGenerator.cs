using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>Stage 3 of the engine: turn one behaviour into a Playwright test through its corpus's bindings.</summary>
public interface ITestGenerator
{
    /// <summary>
    /// Generate one behaviour's test. <paramref name="bindings"/> is the corpus's
    /// <c>&lt;app&gt;.bindings.json</c> as read, verbatim — free-form JSON, because a
    /// binding's values reach the output through JavaScript's own coercions and a
    /// typed model would change what a malformed binding emits. Pass the behaviour
    /// AFTER <see cref="IBehaviourResolver"/> has run: a <c>fills</c> step only
    /// generates from the fields resolve wrote onto it.
    /// </summary>
    IGeneratedTest Generate(IBehaviour behaviour, JsonObject bindings, IReadOnlyDictionary<string, ISymbol> symbols);
}
