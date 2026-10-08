using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The answer to signing in or out. The token is never here — it travels only as an HttpOnly cookie.</summary>
public sealed class SignInState : ISignInState
{
    [JsonPropertyOrder(1)]
    public required bool Ok { get; init; }

    [JsonPropertyOrder(2)]
    public required bool SignedIn { get; init; }
}
