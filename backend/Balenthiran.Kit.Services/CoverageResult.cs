using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Services;

/// <summary>What option C's mapping linked, what it could not, and whether it could not be read at all.</summary>
internal sealed class CoverageResult : ICoverageResult
{
    public string? Refusal { get; set; }

    public List<string> Errors { get; } = [];

    public Dictionary<string, List<string>> LinkedFiles { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<IBehaviour> Covered { get; set; } = [];

    public IReadOnlyList<IBehaviour> Uncovered { get; set; } = [];

    public IReadOnlyList<IBehaviour> Pending { get; set; } = [];

    IReadOnlyList<string> ICoverageResult.Errors => Errors;

    IReadOnlyDictionary<string, IReadOnlyList<string>> ICoverageResult.Linked =>
        LinkedFiles.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
}
