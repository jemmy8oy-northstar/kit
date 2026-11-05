using System.Runtime.InteropServices;
using System.Text.Json;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Reads usage out of a server-sent-events stream as it passes, a chunk at a time: a line may be split
/// across chunks, so a partial one waits for its newline. Only <c>message_start</c> and
/// <c>message_delta</c> events are parsed; the rest are skipped without a JSON parse.
/// </summary>
public sealed class SseUsageReader(UsageTally tally)
{
    /// <summary>A line longer than this is not a usage event; it is skipped, not buffered.</summary>
    private const int MaxLine = 1 << 20;

    private readonly List<byte> line = [];
    private bool skipping;

    /// <summary>Takes the next chunk of the stream.</summary>
    public void Feed(ReadOnlySpan<byte> chunk)
    {
        foreach (var b in chunk)
        {
            if (b == (byte)'\n')
            {
                if (!skipping)
                {
                    Line();
                }

                line.Clear();
                skipping = false;
            }
            else if (!skipping)
            {
                if (line.Count >= MaxLine)
                {
                    skipping = true;
                    line.Clear();
                }
                else
                {
                    line.Add(b);
                }
            }
        }
    }

    private void Line()
    {
        var bytes = CollectionsMarshal.AsSpan(line);
        if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
        {
            bytes = bytes[..^1];
        }

        if (!bytes.StartsWith("data:"u8))
        {
            return;
        }

        var payload = bytes["data:"u8.Length..].Trim((byte)' ');
        if (payload.IndexOf("\"message_start\""u8) < 0 && payload.IndexOf("\"message_delta\""u8) < 0)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload.ToArray());
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            JsonElement usage = default;
            var found = type == "message_start"
                ? root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object && m.TryGetProperty("usage", out usage)
                : type == "message_delta" && root.TryGetProperty("usage", out usage);
            if (found)
            {
                tally.Apply(usage);
            }
        }
        catch (JsonException)
        {
            // Not a usage event we can read: the relay carries on, and the call is counted from what was seen.
        }
    }
}
