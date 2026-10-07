using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>Stage 2 of the engine: fill each step's holes from what other behaviours provide.</summary>
public interface IBehaviourResolver
{
    /// <summary>
    /// Resolve a parsed corpus. ⚠️ Mutates its input, as <c>kit.js</c>'s <c>resolve</c>
    /// does: each behaviour gains <see cref="IBehaviour.Filled"/>/<see cref="IBehaviour.Open"/>
    /// and each filled step gains <see cref="IStep.Resolved"/>, which is what lets a
    /// hole filled by ANOTHER behaviour actually generate. Takes the behaviours
    /// <see cref="ICorpusParser"/> returned; any other implementation is refused.
    /// </summary>
    IResolution Resolve(IReadOnlyList<IBehaviour> behaviours);
}
