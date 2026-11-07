namespace Balenthiran.Kit.Services;

/// <summary>How hard a review a flag needs (BEH-ORCH-2): low is reviewed on a cheaper model, high on the strongest.</summary>
public enum FlagLevel
{
    Low,
    High,
}

/// <summary>Reads the level off the end of a Flag line: <c>Flag: &lt;reason&gt; (level: low|high)</c>.</summary>
public static class FlagLevels
{
    /// <summary>
    /// The level <paramref name="flag"/> ends with, ignoring case and spacing. No level, an unreadable one, or one
    /// anywhere but the end is <see cref="FlagLevel.High"/>, so a doubt is never reviewed by a weaker model by accident.
    /// </summary>
    public static FlagLevel Of(string? flag)
    {
        if (flag is null) return FlagLevel.High;
        var text = flag.TrimEnd();
        if (!text.EndsWith(')')) return FlagLevel.High;
        var open = text.LastIndexOf('(');
        if (open < 0) return FlagLevel.High;
        var inner = text[(open + 1)..^1].Trim();
        var colon = inner.IndexOf(':');
        if (colon < 0 || !inner[..colon].Trim().Equals("level", StringComparison.OrdinalIgnoreCase)) return FlagLevel.High;
        return inner[(colon + 1)..].Trim().Equals("low", StringComparison.OrdinalIgnoreCase) ? FlagLevel.Low : FlagLevel.High;
    }

    /// <summary>The sentence every stage's flag instruction ends with, saying what each level means.</summary>
    public const string Guidance = "End the reason with \"(level: low)\" if you are unsure of a wording or which card a note belongs to, or \"(level: high)\" if you are unsure what the owner wants or the change touches how Kit stores or shows his work.";
}
