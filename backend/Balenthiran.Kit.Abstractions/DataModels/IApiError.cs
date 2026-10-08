namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A refusal: a code, a reason, and — for two of them — what would have been accepted.</summary>
public interface IApiError
{
    string Error { get; }

    string Reason { get; }

    string? Allow { get; }

    IReadOnlyList<string>? Known { get; }

    /// <summary>Set only on a throttled sign-in.</summary>
    int? RetryAfterSeconds { get; }

    /// <summary>What a noun is already bound to, on a refused rebind.</summary>
    System.Text.Json.Nodes.JsonNode? Current { get; }
}
