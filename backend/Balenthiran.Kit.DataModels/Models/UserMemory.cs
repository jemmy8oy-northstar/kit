using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IUserMemory"/>
public sealed class UserMemory : IUserMemory
{
    [JsonPropertyOrder(1)]
    public required string Body { get; init; }
}
