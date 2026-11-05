using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's price list: USD per million tokens at Anthropic's list price, as the card states them.
/// Cache writes cost 1.25x input for a 5 minute entry and 2x for a 1 hour one; a cache read costs 0.1x.
/// A model not in the list is priced at the dearest input and the dearest output in the list, so a model name cannot dodge a cap.
/// </summary>
public static class AgentPrices
{
    private const decimal PerMillion = 1_000_000m;

    /// <summary>Input and output USD per million tokens, by a substring of the model id; the first match wins.</summary>
    private static readonly (string Match, decimal Input, decimal Output)[] Table =
    [
        ("fable-5-1", 10m, 50m),
        ("opus-5-5", 4m, 20m),
        ("sonnet-5-5", 2m, 10m),
        ("haiku", 1m, 5m),
    ];

    /// <summary>The dearest input price and the dearest output price in the table, for a model it does not name.</summary>
    private static readonly (decimal Input, decimal Output) Dearest = (Table.Max(r => r.Input), Table.Max(r => r.Output));

    /// <summary>Input and output price per million tokens of <paramref name="model"/>.</summary>
    public static (decimal Input, decimal Output) Of(string? model)
    {
        foreach (var (match, input, output) in Table)
        {
            if (model?.Contains(match, StringComparison.OrdinalIgnoreCase) == true)
            {
                return (input, output);
            }
        }

        return Dearest;
    }

    /// <summary>What one call cost, USD.</summary>
    /// <param name="model">The model the call was made to.</param>
    /// <param name="input">Uncached input tokens.</param>
    /// <param name="output">Output tokens.</param>
    /// <param name="write5m">Tokens written to the 5 minute cache.</param>
    /// <param name="write1h">Tokens written to the 1 hour cache.</param>
    /// <param name="read">Tokens read from the cache.</param>
    public static decimal Cost(string? model, long input, long output, long write5m, long write1h, long read)
    {
        var (i, o) = Of(model);
        return (input * i + output * o + write5m * i * 1.25m + write1h * i * 2m + read * i * 0.1m) / PerMillion;
    }
}
