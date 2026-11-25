using System.Globalization;
using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-ORCH-21: a run that ended on the subscription's usage limit. The runner (runner/entrypoint.sh, limit_reset) reads
/// the limit message and reports the failure as <c>limit exit &lt;code&gt; resets &lt;YYYY-MM-DDTHH:MM:SSZ&gt;</c>; only
/// the time it parsed goes out, never the message. The limit is the whole account's, so it says nothing about the work.
/// </summary>
public static class UsageLimit
{
    private static readonly Regex Failure = new(@"^limit exit [0-9]{1,3} resets ([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z)$", RegexOptions.Compiled);

    /// <summary>When the limit of a run that failed with <paramref name="failure"/> resets; null when it is no usage-limit failure, or its time is not a real one.</summary>
    public static DateTimeOffset? ResetOf(string? failure) =>
        failure is not null
        && Failure.Match(failure) is { Success: true } m
        && DateTimeOffset.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : null;
}
