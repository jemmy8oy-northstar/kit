using System.Text.Json;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The token counts of one Messages API call, read from a <c>usage</c> object. Each field is set only when
/// the object carries it and takes the LARGER of what it had and what it is told, because the API reports
/// cumulative counts: <c>message_start</c> brings the input and cache tokens, and <c>message_delta</c> brings
/// the output's running total, so a later object can never lower what an earlier one recorded.
/// </summary>
public sealed class UsageTally
{
    public long Input { get; private set; }

    public long Output { get; private set; }

    /// <summary>Tokens written to the 5 minute cache.</summary>
    public long Write5m { get; private set; }

    /// <summary>Tokens written to the 1 hour cache.</summary>
    public long Write1h { get; private set; }

    public long Read { get; private set; }

    /// <summary>True once a usage object has been read.</summary>
    public bool Any { get; private set; }

    /// <summary>
    /// Takes what <paramref name="usage"/> says. Cache writes come from <c>cache_creation</c>'s two
    /// lifetimes when it is there; with only the total, all of it is treated as the 1 hour kind, the dearer.
    /// </summary>
    public void Apply(JsonElement usage)
    {
        if (usage.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        Any = true;
        if (Count(usage, "input_tokens") is { } i)
        {
            Input = Math.Max(Input, i);
        }

        if (Count(usage, "output_tokens") is { } o)
        {
            Output = Math.Max(Output, o);
        }

        if (Count(usage, "cache_read_input_tokens") is { } r)
        {
            Read = Math.Max(Read, r);
        }

        if (usage.TryGetProperty("cache_creation", out var split) && split.ValueKind == JsonValueKind.Object
            && (Count(split, "ephemeral_5m_input_tokens") is not null || Count(split, "ephemeral_1h_input_tokens") is not null))
        {
            Write5m = Math.Max(Write5m, Count(split, "ephemeral_5m_input_tokens") ?? 0);
            Write1h = Math.Max(Write1h, Count(split, "ephemeral_1h_input_tokens") ?? 0);
        }
        else if (Count(usage, "cache_creation_input_tokens") is { } total && total > Write5m + Write1h)
        {
            // Only the part not already split is new, and it is priced as the dearer 1 hour kind.
            Write1h += total - Write5m - Write1h;
        }
    }

    private static long? Count(JsonElement o, string name)
    {
        if (o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) && n >= 0)
        {
            return n;
        }

        return null;
    }
}
