using System.Text.Json;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The token counts of one Messages API call, read from a <c>usage</c> object. Each field is set only when
/// the object carries it and OVERWRITES, because the API reports cumulative counts: <c>message_start</c>
/// brings the input and cache tokens, and <c>message_delta</c> brings the output's running total.
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
            Input = i;
        }

        if (Count(usage, "output_tokens") is { } o)
        {
            Output = o;
        }

        if (Count(usage, "cache_read_input_tokens") is { } r)
        {
            Read = r;
        }

        if (usage.TryGetProperty("cache_creation", out var split) && split.ValueKind == JsonValueKind.Object
            && (Count(split, "ephemeral_5m_input_tokens") is not null || Count(split, "ephemeral_1h_input_tokens") is not null))
        {
            Write5m = Count(split, "ephemeral_5m_input_tokens") ?? 0;
            Write1h = Count(split, "ephemeral_1h_input_tokens") ?? 0;
        }
        else if (Count(usage, "cache_creation_input_tokens") is { } total)
        {
            Write5m = 0;
            Write1h = total;
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
