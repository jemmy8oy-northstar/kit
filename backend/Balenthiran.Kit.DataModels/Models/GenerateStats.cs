using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>How each step of one behaviour came out of <c>generate</c>. Key order is the goldens'.</summary>
public sealed class GenerateStats : IGenerateStats
{
    [JsonPropertyOrder(1)]
    public int Generated { get; set; }

    [JsonPropertyOrder(2)]
    public int Contract { get; set; }

    [JsonPropertyOrder(3)]
    public int Ungenerated { get; set; }
}
