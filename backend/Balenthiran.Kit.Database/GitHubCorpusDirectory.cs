using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ICorpusDirectory"/> whose READS come from GitHub (kit#88, BEH-PULL-1): the corpora
/// of every <c>KIT_PROJECTS</c> source, as of the last <see cref="RefreshAsync"/>. WRITES still go
/// to the pod's clone, which pushes them — so a write is only allowed for an app the clone holds.
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
public sealed class GitHubCorpusDirectory(ICorpusDirectory clone, IReadOnlyList<IProjectSource> sources, IGitHubCorpusReader reader) : ICorpusDirectory
{
    private const string Suffix = ".beh";
    private const string BindingsSuffix = ".bindings.json";
    private const string NotesSuffix = ".notes.md";

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
                    apps[app] = new Entry(snap.Source, file, bindings, notes);
                }
            }

            snapshots.CopyTo(last, 0);
            served = new Served(apps);

            // An override is spent once GitHub's sha has moved past the one it was written over.
            foreach (var (key, o) in overrides)
            {
                var now = Sha(key);
                if (now != o.Over)
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
    public void WriteText(string app, string text)
    {
        Writable(app);
        clone.WriteText(app, text);
        overrides[app + Suffix] = new Override(text, Sha(app + Suffix));
    }

    /// <inheritdoc />
    public void WriteBindingsText(string app, string text)
    {
        Writable(app);
        clone.WriteBindingsText(app, text);
        overrides[app + BindingsSuffix] = new Override(text, Sha(app + BindingsSuffix));
    }

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
    public void WriteNotesText(string app, string text)
    {
        Writable(app);
        clone.WriteNotesText(app, text);
        overrides[app + NotesSuffix] = new Override(text, Sha(app + NotesSuffix));
    }

    /// <inheritdoc />
    public string RelativeNotesPath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{app}{NotesSuffix}"
        : clone.RelativeNotesPath(app);

    /// <inheritdoc />
    public string FullNotesPath(string app) => clone.FullNotesPath(app);

    // An app read from a repository this pod has no clone of has nowhere to be written; writing
    // into the clone would create a corpus in the WRONG repository and push it there.
    private void Writable(string app)
    {
        if (!clone.Corpora().Contains(app, StringComparer.Ordinal))
        {
            var from = served?.Apps.TryGetValue(app, out var e) == true ? Key(e.Source) : "GitHub";
            throw new InvalidOperationException($"{app} is read from {from}, which this Kit has no clone of, so it cannot be edited here");
        }
    }

    /// <summary>The blob sha GitHub served for a file (by file name) in the current snapshot, or null.</summary>
    private string? Sha(string fileName)
    {
        if (served is not { } s)
        {
            return null;
        }

        var suffix = new[] { BindingsSuffix, NotesSuffix, Suffix }.First(x => fileName.EndsWith(x, StringComparison.Ordinal));
        if (!s.Apps.TryGetValue(fileName[..^suffix.Length], out var e))
        {
            return null;
        }

        return suffix switch
        {
            BindingsSuffix => e.Bindings?.Sha,
            NotesSuffix => e.Notes?.Sha,
            _ => e.Corpus.Sha,
        };
    }

    private static string Key(IProjectSource s) => $"{s.Owner}/{s.Repository}@{s.Branch}:{s.Path}";

    private sealed record Entry(IProjectSource Source, ISnapshotFile Corpus, ISnapshotFile? Bindings, ISnapshotFile? Notes);

    private sealed record Served(SortedDictionary<string, Entry> Apps);

    private sealed record Override(string Text, string? Over);
}
