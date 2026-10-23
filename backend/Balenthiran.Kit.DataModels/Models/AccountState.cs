using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The answer to creating an account or changing its password (BEH-USERS-1). Never carries a password or hash.</summary>
public sealed class AccountState : IAccountState
{
    [JsonPropertyOrder(1)]
    public required bool Ok { get; init; }

    [JsonPropertyOrder(2)]
    public required string User { get; init; }
}
