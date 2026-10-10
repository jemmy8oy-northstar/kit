namespace Balenthiran.Kit.Services;

/// <summary>What option C's mapping linked, what it could not, and whether it could not be read at all.</summary>
internal sealed class MappingResult
{
    public Dictionary<string, List<string>> Linked { get; } = new(StringComparer.Ordinal);

    public List<string> Errors { get; } = [];

    /// <summary>Set when the mapping's shape is not one Node could have read: the gate cannot look.</summary>
    public string? Refusal { get; set; }
}
