using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The body of <c>GET /api/activity</c> (BEH-ACTIVITY-1): each project's recent commits, merged
/// newest first and capped. A project whose history cannot be read is listed apart, with the reason.
/// </summary>
/// <remarks>
/// Each project is asked for its own newest <see cref="Cap"/> commits, so the merged top
/// <see cref="Cap"/> is exact however the commits are spread across projects. A commit that touched
/// two projects is one entry for each.
/// </remarks>
public sealed class ActivityReader(ICorpusDirectory corpora, ICorpusHistory history) : IActivityReader
{
    /// <summary>The most entries the feed holds.</summary>
    public const int Cap = 50;

    /// <inheritdoc />
    public IActivityFeed Recent()
    {
        var entries = new List<(DateTimeOffset At, ActivityEntry Entry)>();
        var gaps = new List<ActivityGap>();
        foreach (var app in corpora.Corpora())
        {
            var found = history.Recent(corpora.FullPath(app), corpora.FullNotesPath(app), Cap);
            if (found.Reason is { } reason)
            {
                gaps.Add(new ActivityGap { App = app, Reason = reason });
                continue;
            }

            foreach (var c in found.Commits)
            {
                entries.Add((c.At, new ActivityEntry
                {
                    Commit = c.Commit,
                    At = c.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                    App = app,
                    Summary = c.Subject,
                }));
            }
        }

        return new ActivityFeed
        {
            // OrderByDescending is stable, so equal instants keep project order.
            Entries = entries.OrderByDescending(e => e.At).Take(Cap).Select(e => e.Entry).ToList(),
            NoHistory = gaps,
        };
    }
}
