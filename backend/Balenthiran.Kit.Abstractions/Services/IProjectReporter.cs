using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The engine's verdicts on one resolved corpus — adjudication, displayed surface,
/// the question sheet and what each noun owes — in the shape the project view serves.
/// </summary>
public interface IProjectReporter
{
    /// <summary>
    /// Report on a corpus AFTER <see cref="IBehaviourResolver"/> has run: a <c>fills</c>
    /// owes a label on each field resolve wrote onto it. <paramref name="bindings"/> is
    /// the corpus's <c>&lt;app&gt;.bindings.json</c> verbatim, as for <see cref="ITestGenerator"/>.
    /// Takes the behaviours and conflicts the parser and resolver returned.
    /// </summary>
    IEngineReport Report(IReadOnlyList<IBehaviour> behaviours, IReadOnlyList<IConflict> conflicts, JsonObject bindings);
}
