using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ICorpusDirectory"/> whose READS come from GitHub (kit#88, BEH-PULL-1): the corpora
/// of every <c>KIT_PROJECTS</c> source, as of the last <see cref="RefreshAsync"/>. A WRITE to an
/// app the pod's clone holds goes to the clone, which pushes it. A write to any other app is held
/// until <see cref="CommitToOwnRepo"/> commits it into the app's own repository (BEH-USERS-12), and
/// without a <paramref name="writer"/> it is refused, as before.
/// </summary>
/// <remarks>
/// <para>
/// The interface stays synchronous and unchanged: a request reads the last snapshot, and a poller
/// refreshes it in the background. Until the first refresh succeeds, reads fall back to the clone,
/// so a GitHub outage at start-up serves the image's corpora rather than an empty list — the same
/// rule the entrypoint follows for a failed clone.
/// </para>
/// <para>
/// ⚠️ A refresh that STARTED before a write was pushed would bring back the old text and hide the
/// edit until the next one. So a write leaves an override holding its text and the blob sha
/// GitHub had when it was made; a refresh drops the override only once GitHub's sha has moved on.
/// </para>
/// </remarks>
public sealed class GitHubCorpusDirectory(ICorpusDirectory clone, IReadOnlyList<IProjectSource> sources, IGitHubCorpusReader reader, IGitHubCorpusWriter? writer = null) : ICorpusDirectory
{
    private const string Suffix = ".beh";
    private const string BindingsSuffix = ".bindings.json";
    private const string NotesSuffix = ".notes.md";
    private const string ScheduleSuffix = ".schedule";
    private const string TestsSuffix = ".tests.json";

    private readonly ICorpusSnapshot?[] last = new ICorpusSnapshot?[sources.Count];
    private readonly ConcurrentDictionary<string, Override> overrides = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim refreshing = new(1, 1);
    private volatile Served? served;

    /// <summary>True once a refresh has succeeded; until then every read is the clone's.</summary>
    public bool FromGitHub => served is not null;

    /// <summary>
    /// Re-read every source, then swap the snapshot in whole. Throws <see cref="GitHubReadException"/>
    /// when a source cannot be read or two sources name the same app — and then the previous
    /// snapshot stays, because half of a refresh would serve some projects from the past.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshing.WaitAsync(cancellationToken);
        try
        {
            var snapshots = new ICorpusSnapshot[sources.Count];
            for (var i = 0; i < sources.Count; i++)
            {
                snapshots[i] = await reader.ReadAsync(sources[i], last[i], cancellationToken);
            }

            var apps = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var snap in snapshots)
            {
                foreach (var (name, file) in snap.Files)
                {
                    if (!name.EndsWith(Suffix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var app = name[..^Suffix.Length];
                    if (apps.TryGetValue(app, out var other))
                    {
                        throw new GitHubReadException($"{app}{Suffix} is in both {Key(other.Source)} and {Key(snap.Source)}; Kit will not choose one");
                    }

                    snap.Files.TryGetValue(app + BindingsSuffix, out var bindings);
                    snap.Files.TryGetValue(app + NotesSuffix, out var notes);
                    snap.Files.TryGetValue(app + ScheduleSuffix, out var schedule);
                    snap.Files.TryGetValue(app + TestsSuffix, out var tests);
                    apps[app] = new Entry(snap.Source, file, bindings, notes, schedule, tests);
                }
            }

            snapshots.CopyTo(last, 0);
            served = new Served(apps);

            // An override is spent once GitHub's sha has moved past the one it was written over. One
            // not yet committed is kept: dropping it would lose the edit, and its commit is refused anyway.
            foreach (var (key, o) in overrides)
            {
                var now = Sha(key);
                if (!o.Pending && now != o.Over)
                {
                    overrides.TryRemove(new KeyValuePair<string, Override>(key, o));
                }
            }
        }
        finally
        {
            refreshing.Release();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Corpora() => served is { } s ? s.Apps.Keys.ToList() : clone.Corpora();

    /// <inheritdoc />
    public string Read(string app) => ReadText(app).TrimStart('\ufeff');

    /// <inheritdoc />
    public JsonObject Bindings(string app)
    {
        var text = ReadBindingsText(app);

        // A file that exists and will not parse still throws: that is could-not-look.
        return text is null ? []
            : JsonNode.Parse(text.TrimStart('\ufeff')) as JsonObject
                ?? throw new InvalidOperationException("bindings.json is not a JSON object");
    }

    /// <inheritdoc />
    public string RelativePath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{app}{Suffix}"
        : clone.RelativePath(app);

    /// <inheritdoc />
    public string RelativeBindingsPath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{app}{BindingsSuffix}"
        : clone.RelativeBindingsPath(app);

    /// <inheritdoc />
    public string FullPath(string app) => clone.FullPath(app);

    /// <inheritdoc />
    public string FullBindingsPath(string app) => clone.FullBindingsPath(app);

    /// <inheritdoc />
    public string ReadText(string app)
    {
        if (overrides.TryGetValue(app + Suffix, out var o))
        {
            return o.Text;
        }

        if (served is not { } s)
        {
            return clone.ReadText(app);
        }

        return s.Apps.TryGetValue(app, out var e)
            ? e.Corpus.Text
            : throw new KeyNotFoundException($"no corpus named {app}");
    }

    /// <inheritdoc />
    public string? ReadBindingsText(string app)
    {
        if (overrides.TryGetValue(app + BindingsSuffix, out var o))
        {
            return o.Text;
        }

        return served is { } s
            ? s.Apps.TryGetValue(app, out var e) ? e.Bindings?.Text : null
            : clone.ReadBindingsText(app);
    }

    /// <inheritdoc />
    public void WriteText(string app, string text) => Write(app, Suffix, text, () => clone.WriteText(app, text));

    /// <inheritdoc />
    public void WriteBindingsText(string app, string text) => Write(app, BindingsSuffix, text, () => clone.WriteBindingsText(app, text));

    /// <inheritdoc />
    public string? ReadNotesText(string app)
    {
        if (overrides.TryGetValue(app + NotesSuffix, out var o))
        {
            return o.Text;
        }

        return served is { } s
            ? s.Apps.TryGetValue(app, out var e) ? e.Notes?.Text : null
            : clone.ReadNotesText(app);
    }

    /// <inheritdoc />
    public void WriteNotesText(string app, string text) => Write(app, NotesSuffix, text, () => clone.WriteNotesText(app, text));

    /// <inheritdoc />
    public string RelativeNotesPath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{app}{NotesSuffix}"
        : clone.RelativeNotesPath(app);

    /// <inheritdoc />
    public string FullNotesPath(string app) => clone.FullNotesPath(app);

    /// <inheritdoc />
    public string? ReadScheduleText(string app)
    {
        if (overrides.TryGetValue(app + ScheduleSuffix, out var o))
        {
            return o.Text;
        }

        return served is { } s
            ? s.Apps.TryGetValue(app, out var e) ? e.Schedule?.Text : null
            : clone.ReadScheduleText(app);
    }

    /// <inheritdoc />
    public string? ReadTestsText(string app) => served is { } s
        ? s.Apps.TryGetValue(app, out var e) ? e.Tests?.Text : null
        : clone.ReadTestsText(app);

    /// <inheritdoc />
    public void WriteScheduleText(string app, string text) => Write(app, ScheduleSuffix, text, () => clone.WriteScheduleText(app, text));

    /// <inheritdoc />
    public string RelativeSchedulePath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{app}{ScheduleSuffix}"
        : clone.RelativeSchedulePath(app);

    /// <inheritdoc />
    public string FullSchedulePath(string app) => clone.FullSchedulePath(app);

    /// <inheritdoc />
    public (string Commit, string Branch)? CommitToOwnRepo(string app, string message)
    {
        if (writer is null || clone.Corpora().Contains(app, StringComparer.Ordinal) || served is not { } s || !s.Apps.TryGetValue(app, out var e))
        {
            return null;
        }

        string? commit = null;
        foreach (var suffix in new[] { Suffix, BindingsSuffix, NotesSuffix, ScheduleSuffix })
        {
            var file = app + suffix;
            if (!overrides.TryGetValue(file, out var o) || !o.Pending)
            {
                continue;
            }

            try
            {
                // Synchronous like the clone's git push: the caller holds the write lock until the edit is saved or refused.
                var (blob, made) = writer.PutAsync(e.Source, file, o.Text, o.Blob, message).GetAwaiter().GetResult();
                overrides[file] = o with { Blob = blob, Pending = false };
                commit = made;
            }
            catch (Exception ex)
            {
                // Nothing was saved, so the page must stop showing the edit, whatever the failure was.
                overrides.TryRemove(new KeyValuePair<string, Override>(file, o));
                if (ex is GitHubWriteException { Stale: true })
                {
                    ReReadSoon();
                }

                if (ex is GitHubWriteException or GitHubReadException)
                {
                    throw new NotWritableException(ex.Message, ex);
                }

                throw;
            }
        }

        return commit is null ? null : (commit, e.Source.Branch);
    }

    // An app the clone holds is written there, and its push is the caller's git write-back. Any other
    // app is held as a pending override for CommitToOwnRepo. Writing it into the clone instead would
    // create a corpus in the WRONG repository and push it there.
    private void Write(string app, string suffix, string text, Action toClone)
    {
        var file = app + suffix;
        if (clone.Corpora().Contains(app, StringComparer.Ordinal))
        {
            toClone();
            overrides[file] = new Override(text, Sha(file));
            return;
        }

        var source = served is { } s && s.Apps.TryGetValue(app, out var e) ? e.Source : null;
        if (source is null || writer is null)
        {
            throw new NotWritableException($"{app} is read from {(source is null ? "GitHub" : Key(source))}, which this Kit has no clone of, so it cannot be edited here");
        }

        // A second write before the next re-read replaces the blob the first one committed, not the one first read.
        var before = overrides.TryGetValue(file, out var o) ? o : null;
        overrides[file] = new Override(text, before is null ? Sha(file) : before.Over, before is null ? Sha(file) : before.Blob, Pending: true);
    }

    // After a stale refusal the newer text is on GitHub; reading it now, not at the next poll, means trying again applies to it.
    // A failed read is the poller's to retry, so it is not reported from here.
    private void ReReadSoon() => _ = Task.Run(async () =>
    {
        try
        {
            await RefreshAsync();
        }
        catch (GitHubReadException)
        {
        }
    });

    /// <summary>The blob sha GitHub served for a file (by file name) in the current snapshot, or null.</summary>
    private string? Sha(string fileName)
    {
        if (served is not { } s)
        {
            return null;
        }

        var suffix = new[] { BindingsSuffix, NotesSuffix, ScheduleSuffix, Suffix }.First(x => fileName.EndsWith(x, StringComparison.Ordinal));
        if (!s.Apps.TryGetValue(fileName[..^suffix.Length], out var e))
        {
            return null;
        }

        return suffix switch
        {
            BindingsSuffix => e.Bindings?.Sha,
            NotesSuffix => e.Notes?.Sha,
            ScheduleSuffix => e.Schedule?.Sha,
            _ => e.Corpus.Sha,
        };
    }

    private static string Key(IProjectSource s) => $"{s.Owner}/{s.Repository}@{s.Branch}:{s.Path}";

    private sealed record Entry(IProjectSource Source, ISnapshotFile Corpus, ISnapshotFile? Bindings, ISnapshotFile? Notes, ISnapshotFile? Schedule, ISnapshotFile? Tests);

    private sealed record Served(SortedDictionary<string, Entry> Apps);

    // Over: the sha GitHub served when the override began, so a re-read that has moved past it ends it.
    // Blob: the file's blob as Kit last knew it, which the next commit names. Pending: not yet committed.
    private sealed record Override(string Text, string? Over, string? Blob = null, bool Pending = false);
}
