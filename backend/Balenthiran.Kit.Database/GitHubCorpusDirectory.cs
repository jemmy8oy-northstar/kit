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
    private const string KitDir = ".kit";

    private readonly ICorpusSnapshot?[] last = new ICorpusSnapshot?[sources.Count];
    private readonly ConcurrentDictionary<string, Override> overrides = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim refreshing = new(1, 1);
    private readonly ThreadLocal<Dictionary<string, string?>> reads = new(() => new(StringComparer.Ordinal));
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
            var moved = new HashSet<string>(StringComparer.Ordinal);
            void Add(string app, Entry entry, string shown)
            {
                if (apps.TryGetValue(app, out var other))
                {
                    // BEH-USERS-12, mid-move: a project's .kit/ in its own repo wins over the copy it
                    // left elsewhere, so it is on the list throughout. Any other pair is still refused.
                    var here = MovedTo(entry.Source, other.Source);
                    if (here is null)
                    {
                        throw new GitHubReadException($"{shown} is in both {Key(other.Source)} and {Key(entry.Source)}; Kit will not choose one");
                    }

                    moved.Add(app);
                    if (!here.Value)
                    {
                        return;
                    }
                }

                apps[app] = entry;
            }

            foreach (var snap in snapshots)
            {
                // claude-code-bot#151: a features tree in the source is ONE app, which hides a flat file of its name.
                var tree = TreeEntry(snap);
                foreach (var (name, file) in snap.Files)
                {
                    if (!name.EndsWith(Suffix, StringComparison.Ordinal) || name.Contains('/', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var app = name[..^Suffix.Length];
                    if (app == tree?.App)
                    {
                        continue;
                    }

                    snap.Files.TryGetValue(app + BindingsSuffix, out var bindings);
                    snap.Files.TryGetValue(app + NotesSuffix, out var notes);
                    snap.Files.TryGetValue(app + ScheduleSuffix, out var schedule);
                    snap.Files.TryGetValue(app + TestsSuffix, out var tests);
                    Add(app, new Entry(snap.Source, file, bindings, notes, schedule, tests), app + Suffix);
                }

                if (tree is { } t)
                {
                    Add(t.App, t.Entry, $"the {t.App} feature tree");
                }
            }

            snapshots.CopyTo(last, 0);
            served = new Served(apps, moved);

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
        ? e.Tree is null ? $"{e.Source.Path}/{app}{Suffix}" : e.Source.Path
        : clone.RelativePath(app);

    /// <inheritdoc />
    public string RelativeBindingsPath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{PathOf(FileKey(app, BindingsSuffix))}"
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
            return Read(app + Suffix, o.Blob, o.Text)!;
        }

        if (served is not { } s)
        {
            return clone.ReadText(app);
        }

        if (!s.Apps.TryGetValue(app, out var e))
        {
            throw new KeyNotFoundException($"no corpus named {app}");
        }

        return e.Tree is null ? Read(app + Suffix, e.Corpus!.Sha, e.Corpus.Text)! : FeatureTree.Compose(TreeFiles(app, e));
    }

    /// <inheritdoc />
    public string? ReadBindingsText(string app) => ReadSidecar(app, BindingsSuffix, e => e.Bindings, () => clone.ReadBindingsText(app));

    /// <inheritdoc />
    /// <remarks>A tree app's edit is held, and committed, for each file it changed and no other (claude-code-bot#151).</remarks>
    public void WriteText(string app, string text)
    {
        if (served?.Apps.TryGetValue(app, out var e) == true && e.Tree is not null)
        {
            var changes = FeatureTree.Changes(TreeFiles(app, e), text);
            Write(app, changes.Select(c => (TreeKey(app, c.Path), c.Text)).ToList(), () => clone.WriteText(app, text));
            return;
        }

        Write(app, Suffix, text, () => clone.WriteText(app, text));
    }

    /// <inheritdoc />
    public void WriteBindingsText(string app, string text) => Write(app, BindingsSuffix, text, () => clone.WriteBindingsText(app, text));

    /// <inheritdoc />
    public string? ReadNotesText(string app) => ReadSidecar(app, NotesSuffix, e => e.Notes, () => clone.ReadNotesText(app));

    /// <inheritdoc />
    public void WriteNotesText(string app, string text) => Write(app, NotesSuffix, text, () => clone.WriteNotesText(app, text));

    /// <inheritdoc />
    public string RelativeNotesPath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{PathOf(FileKey(app, NotesSuffix))}"
        : clone.RelativeNotesPath(app);

    /// <inheritdoc />
    public string FullNotesPath(string app) => clone.FullNotesPath(app);

    /// <inheritdoc />
    public string? ReadScheduleText(string app) => ReadSidecar(app, ScheduleSuffix, e => e.Schedule, () => clone.ReadScheduleText(app));

    /// <inheritdoc />
    public string? ReadTestsText(string app) => served is { } s
        ? s.Apps.TryGetValue(app, out var e) ? e.Tree is null ? e.Tests?.Text : FeatureTree.MergeTests(e.Tree.Select(f => (f.Key, f.Value.Text))) : null
        : clone.ReadTestsText(app);

    /// <inheritdoc />
    public void WriteScheduleText(string app, string text) => Write(app, ScheduleSuffix, text, () => clone.WriteScheduleText(app, text));

    /// <inheritdoc />
    public string RelativeSchedulePath(string app) => served?.Apps.TryGetValue(app, out var e) == true
        ? $"{e.Source.Path}/{PathOf(FileKey(app, ScheduleSuffix))}"
        : clone.RelativeSchedulePath(app);

    /// <inheritdoc />
    public string FullSchedulePath(string app) => clone.FullSchedulePath(app);

    /// <inheritdoc />
    public (string Commit, string Branch)? CommitToOwnRepo(string app, string message)
    {
        if (writer is null || InClone(app))
        {
            return null;
        }

        // A tree app's files are every key held under its name: each .kit file an edit changed, and its sidecars.
        var files = new[] { Suffix, BindingsSuffix, NotesSuffix, ScheduleSuffix }.Select(x => app + x)
            .Concat(overrides.Keys.Where(k => k.StartsWith(app + "/", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            .ToList();
        if (served is not { } s || !s.Apps.TryGetValue(app, out var e))
        {
            // A re-read dropped the app between the write and its commit: the held edit has nowhere to go.
            var orphaned = files.Where(f => overrides.TryGetValue(f, out var p) && p.Pending).ToList();
            if (orphaned.Count == 0)
            {
                return null;
            }

            orphaned.ForEach(Undo);
            throw new NotWritableException($"{app} is no longer on GitHub, so the edit was not saved");
        }

        string? commit = null;
        foreach (var file in files)
        {
            if (!overrides.TryGetValue(file, out var o) || !o.Pending)
            {
                continue;
            }

            try
            {
                // Synchronous like the clone's git push: the caller holds the write lock until the edit is saved or refused.
                var (blob, made) = writer.PutAsync(e.Source, PathOf(file), o.Text, o.Blob, message).GetAwaiter().GetResult();
                overrides[file] = o with { Blob = blob, Pending = false, Replaced = null };
                commit = made;
            }
            catch (Exception ex)
            {
                // The page goes back to what it showed before this edit, whatever the failure was: a stale
                // refusal, a refusal, or an answer Kit could not read, which may hide a commit that landed.
                // So it re-reads GitHub after every one.
                // A tree edit is several files: the ones not yet committed are undone with it, not left to ride the next edit.
                files.Where(f => overrides.TryGetValue(f, out var p) && p.Pending).ToList().ForEach(Undo);
                ReReadSoon();

                if (ex is GitHubWriteException or GitHubReadException)
                {
                    throw new NotWritableException(ex.Message, ex);
                }

                throw;
            }
        }

        return commit is null ? null : (commit, e.Source.Branch);
    }

    /// <inheritdoc />
    public (string Repository, string Branch)? OwnRepo(string app) =>
        !InClone(app) && served is { } s && s.Apps.TryGetValue(app, out var e)
            ? ($"{e.Source.Owner}/{e.Source.Repository}", e.Source.Branch)
            : null;

    // An app the clone holds is written there, and its push is the caller's git write-back. Any other
    // app is held as a pending override for CommitToOwnRepo. Writing it into the clone instead would
    // create a corpus in the WRONG repository and push it there.
    private void Write(string app, string suffix, string text, Action toClone) => Write(app, [(FileKey(app, suffix), text)], toClone);

    private void Write(string app, IReadOnlyList<(string File, string Text)> changes, Action toClone)
    {
        if (InClone(app))
        {
            toClone();
            foreach (var (file, text) in changes)
            {
                overrides[file] = new Override(text, Sha(file));
            }

            return;
        }

        if (served is null)
        {
            throw new NotWritableException($"Kit has not read GitHub yet, so it cannot tell whether {app} has moved to its own repo; try again in a moment");
        }

        var source = served.Apps.TryGetValue(app, out var e) ? e.Source : null;
        if (source is null || writer is null)
        {
            throw new NotWritableException($"{app} is read from {(source is null ? "GitHub" : Key(source))}, which this Kit has no clone of, so it cannot be edited here");
        }

        // The commit names the blob this request's text was read from (a re-read since then would make
        // the commit refuse, not overwrite), or the one a previous commit made, or failing both the snapshot's.
        foreach (var (file, text) in changes)
        {
            var before = overrides.TryGetValue(file, out var o) ? o : null;
            var read = reads.Value!.TryGetValue(file, out var r) ? r : before?.Blob ?? Sha(file);
            overrides[file] = new Override(text, before?.Over ?? read, read, Pending: true, Replaced: before);
        }
    }

    // Put back what the file showed before a pending edit: the earlier committed override, or GitHub's text.
    private void Undo(string file)
    {
        if (overrides.TryGetValue(file, out var o) && o.Pending)
        {
            if (o.Replaced is { } back)
            {
                overrides[file] = back;
            }
            else
            {
                overrides.TryRemove(new KeyValuePair<string, Override>(file, o));
            }
        }
    }

    // A write route reads, splices, then writes on one thread under the router's lock, so the blob each
    // read served is kept per thread: the poller swapping the snapshot between the read and the write
    // cannot change which blob the commit names. A stale entry from an earlier request can only make a
    // commit refuse, never overwrite.
    private string? Read(string file, string? blob, string? text)
    {
        reads.Value![file] = blob;
        return text;
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

        // A tree app's key is `<app>/<path>`: its file is looked up by path.
        var slash = fileName.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            return s.Apps.TryGetValue(fileName[..slash], out var t) && t.Tree is not null && t.Tree.TryGetValue(fileName[(slash + 1)..], out var f) ? f.Sha : null;
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
            _ => e.Corpus?.Sha,
        };
    }

    // The key a file is held and read under: `<app><suffix>` for a flat app, as it is named in its source;
    // `<app>/<path>` for a tree app's sidecar (claude-code-bot#151), so two trees' `notes.md` never meet.
    private string FileKey(string app, string suffix) =>
        served?.Apps.TryGetValue(app, out var e) == true && e.Tree is not null
            ? TreeKey(app, suffix switch
            {
                BindingsSuffix => FeatureTree.BindingsFile,
                NotesSuffix => FeatureTree.NotesFile,
                ScheduleSuffix => FeatureTree.ScheduleFile,
                _ => throw new InvalidOperationException($"a tree app has no single {suffix} file"),
            })
            : app + suffix;

    private static string TreeKey(string app, string path) => $"{app}/{path}";

    // A key's path in its source: a tree key loses its app.
    private static string PathOf(string key) => key.Contains('/', StringComparison.Ordinal) ? key[(key.IndexOf('/', StringComparison.Ordinal) + 1)..] : key;

    // A sidecar read: a held edit, else the snapshot's file, else (before the first re-read) the clone's.
    private string? ReadSidecar(string app, string suffix, Func<Entry, ISnapshotFile?> pick, Func<string?> fromClone)
    {
        var key = FileKey(app, suffix);
        if (overrides.TryGetValue(key, out var o))
        {
            return Read(key, o.Blob, o.Text);
        }

        return served is { } s
            ? s.Apps.TryGetValue(app, out var e) ? Read(key, pick(e)?.Sha, pick(e)?.Text) : null
            : fromClone();
    }

    // A tree app's files as this request sees them: a held edit wins over the snapshot, and a file an edit
    // created is in it too. Each read is remembered, as Read does, so a commit names the blob it was read from.
    private List<(string Path, string Text)> TreeFiles(string app, Entry e)
    {
        var held = overrides.Keys.Where(k => k.StartsWith(app + "/", StringComparison.Ordinal)).Select(PathOf);
        var files = new List<(string Path, string Text)>();
        foreach (var path in e.Tree!.Keys.Concat(held).Distinct(StringComparer.Ordinal).Where(FeatureTree.IsTreeFile).Order(StringComparer.Ordinal))
        {
            var key = TreeKey(app, path);
            var text = overrides.TryGetValue(key, out var o) ? Read(key, o.Blob, o.Text)
                : e.Tree.TryGetValue(path, out var f) ? Read(key, f.Sha, f.Text)
                : null;
            if (text is not null)
            {
                files.Add((path, text));
            }
        }

        return files;
    }

    // claude-code-bot#151: a source holding a features tree serves it as ONE app, named by project.kit's
    // `# kit:project` line or else the repository. Null for a source with no tree.
    private static (string App, Entry Entry)? TreeEntry(ICorpusSnapshot snap)
    {
        if (!FeatureTree.IsTree(snap.Files.Keys))
        {
            return null;
        }

        var tree = new SortedDictionary<string, ISnapshotFile>(StringComparer.Ordinal);
        foreach (var (name, file) in snap.Files)
        {
            if (FeatureTree.IsTreeFile(name) || name is FeatureTree.NotesFile or FeatureTree.ScheduleFile or FeatureTree.BindingsFile)
            {
                tree[name] = file;
            }
        }

        tree.TryGetValue(FeatureTree.ProjectFile, out var project);
        var app = FeatureTree.ProjectName(project?.Text.TrimStart('\ufeff'), snap.Source.Repository);
        tree.TryGetValue(FeatureTree.BindingsFile, out var bindings);
        tree.TryGetValue(FeatureTree.NotesFile, out var notes);
        tree.TryGetValue(FeatureTree.ScheduleFile, out var schedule);
        return (app, new Entry(snap.Source, null, bindings, notes, schedule, null, tree));
    }

    // The clone's copy of a project that has moved to its own repo is left behind: the read and every
    // write are the own repo's, or an edit would land in a copy the page no longer shows.
    // Before the first re-read Kit cannot tell which projects have moved, so with any .kit/ source it writes nothing.
    private bool InClone(string app) =>
        clone.Corpora().Contains(app, StringComparer.Ordinal)
        && (served is { } s ? !s.Moved.Contains(app) : !sources.Any(x => x.Path == KitDir));

    /// <summary>True when <paramref name="incoming"/> is a project's .kit/ in its own repo and <paramref name="held"/> the copy it left in another; false the other way round; null for any other pair.</summary>
    private static bool? MovedTo(IProjectSource incoming, IProjectSource held)
    {
        var sameRepo = string.Equals(incoming.Owner, held.Owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(incoming.Repository, held.Repository, StringComparison.OrdinalIgnoreCase);
        var a = incoming.Path == KitDir;
        var b = held.Path == KitDir;
        return sameRepo || a == b ? null : a;
    }

    private static string Key(IProjectSource s) => $"{s.Owner}/{s.Repository}@{s.Branch}:{s.Path}";

    // Tree: a features-tree app's files by path relative to the source (claude-code-bot#151); null for a flat
    // app, whose text is Corpus. A tree app has no Corpus: its text is the tree composed.
    private sealed record Entry(IProjectSource Source, ISnapshotFile? Corpus, ISnapshotFile? Bindings, ISnapshotFile? Notes, ISnapshotFile? Schedule, ISnapshotFile? Tests, IReadOnlyDictionary<string, ISnapshotFile>? Tree = null);

    // Moved: apps whose .kit/ copy in their own repo won over one left in another source.
    private sealed record Served(SortedDictionary<string, Entry> Apps, IReadOnlySet<string> Moved);

    // Over: the sha GitHub served when the override began, so a re-read that has moved past it ends it.
    // Blob: the file's blob as Kit last knew it, which the next commit names. Pending: not yet committed.
    // Replaced: the override a pending one replaced, put back if its commit fails.
    private sealed record Override(string Text, string? Over, string? Blob = null, bool Pending = false, Override? Replaced = null);
}
