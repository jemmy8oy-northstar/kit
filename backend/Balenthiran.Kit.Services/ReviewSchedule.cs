namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-13</c>: which projects have a review cadence, and when each last started a review. Pure: it reads
/// no clock and starts nothing; the pipeline asks <see cref="Due"/> with the time it has and starts the runs. A project has
/// at most one review in flight, and a project's next review is one interval after its last start, so a look more often
/// than that starts nothing. Kept in memory: a restart puts every project back to off.
/// </summary>
public sealed class ReviewSchedule
{
    private readonly Dictionary<string, ReviewCadence> cadence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastStarted = new(StringComparer.Ordinal);
    private readonly HashSet<string> inFlight = new(StringComparer.Ordinal);

    /// <summary>The project's cadence; a project never set is off.</summary>
    public ReviewCadence CadenceOf(string project) => cadence.GetValueOrDefault(project, ReviewCadence.Off);

    /// <summary>Sets the project's cadence. Off stops its reviews; its last start is kept, so turning it back on does not start one at once.</summary>
    public void Set(string project, ReviewCadence value)
    {
        if (value == ReviewCadence.Off)
        {
            cadence.Remove(project);
        }
        else
        {
            cadence[project] = value;
        }
    }

    /// <summary>The projects whose cadence is not off, with it.</summary>
    public IReadOnlyDictionary<string, ReviewCadence> NotOff() =>new Dictionary<string, ReviewCadence>(cadence, StringComparer.Ordinal);

    /// <summary>The projects whose next review is due at <paramref name="now"/>, in name order: on a cadence, none in flight, and one interval since the last start (or never started).</summary>
    public IReadOnlyList<string> Due(DateTimeOffset now) => cadence
        .Where(kv => ReviewCadences.Interval(kv.Value) is { } every
            && !inFlight.Contains(kv.Key)
            && (!lastStarted.TryGetValue(kv.Key, out var last) || now - last >= every))
        .Select(kv => kv.Key)
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();

    /// <summary>A review of the project was started at <paramref name="now"/>.</summary>
    public void Started(string project, DateTimeOffset now)
    {
        lastStarted[project] = now;
        inFlight.Add(project);
    }

    /// <summary>The project's review run has ended, whether it passed or not.</summary>
    public void Ended(string project) => inFlight.Remove(project);
}
