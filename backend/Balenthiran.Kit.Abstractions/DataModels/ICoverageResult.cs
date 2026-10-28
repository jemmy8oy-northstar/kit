namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What option C's mapping says about a corpus's behaviours: which a test names, which none does, which are not built yet.</summary>
public interface ICoverageResult
{
    /// <summary>Set when the mapping's shape is not one Node could have read: the count cannot look.</summary>
    string? Refusal { get; }

    /// <summary>Mapping entries that no longer name a behaviour or a test — the mapping rotting loudly.</summary>
    IReadOnlyList<string> Errors { get; }

    /// <summary>Behaviour id to the files whose tests name it.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Linked { get; }

    /// <summary>Behaviours a test names, pending or not, in corpus order.</summary>
    IReadOnlyList<IBehaviour> Covered { get; }

    /// <summary>Built behaviours no test names, in corpus order — the denominator's other half.</summary>
    IReadOnlyList<IBehaviour> Uncovered { get; }

    /// <summary>Pending behaviours no test names: spec'd, not built, not counted.</summary>
    IReadOnlyList<IBehaviour> Pending { get; }
}
