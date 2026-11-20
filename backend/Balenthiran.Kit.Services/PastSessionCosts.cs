using System.Globalization;
using System.Text.Json;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-COST-1, past sessions on a dev machine: read from the Claude Code records under its Claude home
/// (<c>projects/&lt;project&gt;/&lt;session&gt;.jsonl</c>). Only a reported dollar cost (<c>costUSD</c> on a line) counts.
/// A session with token usage and no reported cost is skipped and counted, never priced from its tokens. Each counted
/// session is summed into the ISO week (Monday) it started in. Only a dev machine calls this: no runner and no server
/// route reads a Claude home, since a runner's is gone when its run ends.
/// </summary>
public static class PastSessionCosts
{
    public static PastCostReport Read(string claudeHome)
    {
        var weeks = new Dictionary<DateOnly, decimal>();
        var counted = 0;
        var skipped = 0;
        var projects = Path.Combine(claudeHome, "projects");
        if (!Directory.Exists(projects))
        {
            return new PastCostReport(weeks, 0, 0);
        }

        foreach (var file in Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            decimal total = 0;
            var reported = false;
            DateTimeOffset? first = null;
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                        && (first is null || at < first))
                    {
                        first = at;
                    }

                    if (root.TryGetProperty("costUSD", out var cost) && cost.ValueKind == JsonValueKind.Number
                        && cost.TryGetDecimal(out var dollars) && dollars >= 0)
                    {
                        total += dollars;
                        reported = true;
                    }
                }
            }

            if (!reported || first is null)
            {
                skipped++;
                continue;
            }

            counted++;
            var day = DateOnly.FromDateTime(first.Value.UtcDateTime);
            var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            weeks[monday] = weeks.GetValueOrDefault(monday) + total;
        }

        return new PastCostReport(weeks, counted, skipped);
    }
}
