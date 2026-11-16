namespace Balenthiran.Kit.Services;

/// <summary>The cadence choices as the Settings page names them, and how far apart each one's runs are.</summary>
public static class ReviewCadences
{
    /// <summary>The time between two review runs of a project on <paramref name="cadence"/>; null when it is off.</summary>
    public static TimeSpan? Interval(ReviewCadence cadence) => cadence switch
    {
        ReviewCadence.Daily => TimeSpan.FromDays(1),
        ReviewCadence.EveryTwoDays => TimeSpan.FromDays(2),
        ReviewCadence.Weekly => TimeSpan.FromDays(7),
        _ => null,
    };

    /// <summary>The cadence named by <paramref name="value"/>; blank or "off" is off. THROWS for any other name, so a typo is never read as off.</summary>
    public static ReviewCadence Parse(string? value) => value?.Trim() switch
    {
        null or "" or "off" => ReviewCadence.Off,
        "daily" => ReviewCadence.Daily,
        "every-two-days" => ReviewCadence.EveryTwoDays,
        "weekly" => ReviewCadence.Weekly,
        _ => throw new InvalidOperationException($"the review cadence is \"{value}\": choose off, daily, every-two-days or weekly"),
    };

    /// <summary>The name <see cref="Parse"/> reads back: what the Settings page and the route say.</summary>
    public static string Name(ReviewCadence cadence) => cadence switch
    {
        ReviewCadence.Daily => "daily",
        ReviewCadence.EveryTwoDays => "every-two-days",
        ReviewCadence.Weekly => "weekly",
        _ => "off",
    };
}

