using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>, wiring slice 1: a note left in Kit queues a triage run of its project's notes
/// (BEH-ORCH-1) on the runner pool, and the run's end lets the next one start. A triage run reports the bundles
/// it pushed with its end, and each becomes a work item queued for spec (or review, when flagged). Only triage is
/// wired: nothing starts spec, review or build yet, so those items wait. Exists
/// only with <c>KIT_PIPELINE=on</c>, as every run it starts spends Kit's own Anthropic credit. Work items are kept
/// IN MEMORY ONLY, as the pool's queue is: a restart forgets them, and the next note queues a triage again.
/// </summary>
/// <param name="owner">Whose runs these are: the operator's, as only he leaves notes.</param>
public sealed class Pipeline(ICorpusDirectory corpora, RunnerPool pool, IAgentRunStore runs, StageModelSettings models, string owner = ISessionStore.Operator, TimeProvider? clock = null)
{
    private readonly object gate = new();

    // Only ever appended to, so an index names the same item for the life of the pipeline.
    private readonly List<WorkItem> items = [];

    // A started run's item, and the note refs its task carries.
    private readonly Dictionary<string, (int Index, IReadOnlyList<string> Notes)> started = new(StringComparer.Ordinal);

    // Notes a finished triage run has already sorted into bundles: a later triage leaves them out.
    private readonly HashSet<string> triaged = new(StringComparer.Ordinal);

    // Each work item's bundle, as its triage reported it: what the stages after triage start from.
    private readonly Dictionary<(string App, string Slug), ContextBundle> bundles = [];

    private int count;

    /// <summary>Every work item, oldest first.</summary>
    public IReadOnlyList<WorkItem> Items()
    {
        lock (gate)
        {
            return items.ToList();
        }
    }

    /// <summary>
    /// A note was left on <paramref name="app"/>: queue a triage of its notes, unless one is already queued (that
    /// one will read this note too), then start whatever may start.
    /// </summary>
    public void NoteLeft(string app)
    {
        lock (gate)
        {
            QueueTriage(app);
            StartReady();
        }
    }

    /// <summary>The bundle work item <paramref name="slug"/> of <paramref name="app"/> was last given by a triage; null when none.</summary>
    public ContextBundle? Bundle(string app, string slug)
    {
        lock (gate)
        {
            return bundles.GetValueOrDefault((app, slug));
        }
    }

    /// <summary>
    /// Run <paramref name="runId"/> ended. A triage that worked marks its notes triaged and queues another, which
    /// reads any notes left for a later run; one that failed stops its item. A run the pipeline did not start is
    /// ignored. A run that never reports holds its project, as it holds the pool's, until Kit restarts.
    /// <paramref name="reported"/> are the bundles a triage run says it pushed: each becomes a work item queued for
    /// the stage after triage (review when it is flagged), or a stopped one saying why it could not be used.
    /// </summary>
    public void RunEnded(string runId, bool succeeded, IReadOnlyList<ReportedBundle>? reported = null)
    {
        lock (gate)
        {
            if (!started.Remove(runId, out var run))
            {
                return;
            }

            var item = items[run.Index];
            if (!succeeded)
            {
                items[run.Index] = Stop(item, "the triage run failed");
            }
            else
            {
                var usable = 0;
                foreach (var b in reported ?? [])
                {
                    usable += Took(item.Project, b) ? 1 : 0;
                }

                // Every note goes in a bundle, so a triage with none usable sorted nothing: its notes are read again by
                // the next note's triage, as a failed run's are, rather than being dropped.
                if (usable == 0)
                {
                    items[run.Index] = Stop(item, "the triage reported no usable bundle");
                }
                else
                {
                    items[run.Index] = item with { Status = WorkItemStatus.Done };
                    triaged.UnionWith(run.Notes);

                    // Always queued, so nothing is read here, outside StartReady's guard: a triage with no notes left is Done at once.
                    QueueTriage(item.Project);
                }
            }

            StartReady();
        }
    }

    private void QueueTriage(string app)
    {
        if (!items.Any(i => i.Project == app && i.Stage == PipelineStage.Triage && i.Status == WorkItemStatus.Queued))
        {
            // "#" is never in a bundle slug, so a triage item can never be mistaken for a bundle's.
            items.Add(new WorkItem(app, $"triage #{++count}", PipelineStage.Triage, WorkItemStatus.Queued));
        }
    }

    /// <summary>
    /// A bundle a triage of <paramref name="app"/> reported, as its work item. A later triage that updates a bundle
    /// replaces the item, unless that item is running (a stage after triage, once one is wired). True when the bundle was usable.
    /// </summary>
    private bool Took(string app, ReportedBundle reported)
    {
        var triage = new WorkItem(app, reported.Slug, PipelineStage.Triage, WorkItemStatus.Running);
        var parsed = reported.Text is null ? null : ContextBundle.Parse(reported.Text);
        ContextBundle? usable = null;
        WorkItem item;
        if (parsed is null)
        {
            item = Stop(triage, "its bundle was too large for the runner to report");
        }
        else if (parsed.Bundle is not { } bundle)
        {
            item = Stop(triage, $"its bundle is malformed: {parsed.Error}");
        }
        else if (bundle.Slug != reported.Slug)
        {
            item = Stop(triage, $"its bundle names work item \"{bundle.Slug}\"");
        }
        else
        {
            item = Orchestrator.Advance(triage, new StageResult(PipelineStage.Triage, true, bundle, null));
            usable = bundle;
        }

        var at = items.FindIndex(i => i.Project == app && i.Slug == reported.Slug);
        if (at >= 0 && items[at].Status == WorkItemStatus.Running)
        {
            return usable is not null;
        }

        if (at < 0)
        {
            items.Add(item);
        }
        else
        {
            items[at] = item;
        }

        if (usable is null)
        {
            bundles.Remove((app, reported.Slug));
        }
        else
        {
            bundles[(app, reported.Slug)] = usable;
        }

        return usable is not null;
    }

    private void StartReady()
    {
        // Only triage is wired: an item waiting for spec, review or build must not hold its project's next triage.
        foreach (var next in Orchestrator.Startable(items.Where(i => i.Stage == PipelineStage.Triage || i.Status == WorkItemStatus.Running)))
        {
            var index = items.IndexOf(next);
            try
            {
                items[index] = Begin(index, next);
            }
            catch (Exception e)
            {
                // A spec or notes file that cannot be read stops this item; it must not fail the note or the run's report.
                items[index] = Stop(next, $"the project could not be read ({e.GetType().Name})");
            }
        }
    }

    /// <summary>The item once its triage run is handed to the pool; Done when there is nothing to triage; Stopped when it cannot start.</summary>
    private WorkItem Begin(int index, WorkItem item)
    {
        var corpus = corpora.ReadText(item.Project);
        var (repoAt, problem) = RepoDirective.Read(corpus);
        if (repoAt is null)
        {
            return Stop(item, problem ?? $"{item.Project} names no repo: its spec wants a `# kit:repo owner/repo@branch` line");
        }

        var notes = Untriaged(item.Project);
        if (notes.Count == 0)
        {
            return item with { Status = WorkItemStatus.Done };
        }

        TriageTask task;
        try
        {
            task = TriageStage.BuildTask(item.Project, notes.Select(n => (n.Ref, n.Text)).ToList(), Cards(corpus));
        }
        catch (InvalidOperationException e)
        {
            return Stop(item, e.Message);
        }

        // Worked out before the run is queued, so nothing can throw between queuing it and recording it.
        var taken = notes.Take(task.NotesTaken).Select(n => n.Key).ToList();
        var at = repoAt.LastIndexOf('@');
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var token = AgentTokens.Mint();
        var run = runs.Create(owner, item.Project, Orchestrator.ModelFor(PipelineStage.Triage, models), AgentTokens.Hash(token), now);
        try
        {
            pool.Enqueue(run, token, repoAt[..at], task.Text, repoAt[(at + 1)..]);
        }
        catch (Exception e)
        {
            // The type only, as StartRun logs it: the exception may carry what the pool was told, and the token is in it.
            Console.Error.WriteLine($"kit: queuing a triage run threw {e.GetType().Name}");
            runs.SetStatus(run.Id, AgentRunStatus.Failed, now);
            return Stop(item, "the runner pool would not take the triage run");
        }

        started[run.Id] = (index, taken);
        return Orchestrator.Start(item);
    }

    /// <summary>
    /// The plain notes and behaviour requests on <paramref name="app"/> no finished triage has read, oldest first,
    /// as (ref, text, key). A note's date is to the minute, so two left in one minute share a ref: the triaged
    /// set is keyed on the date AND the text, or the second would be counted read with the first.
    /// </summary>
    private List<(string Ref, string Text, string Key)> Untriaged(string app)
    {
        var file = corpora.RelativeNotesPath(app);
        return Notes.Parse(corpora.ReadNotesText(app))
            .Where(n => n.Kind is Notes.NoteKind or Notes.RequestKind)
            .Select(n => (Ref: $"{file}#{n.At}", n.Text, Key: n.At + "\n" + n.Text))
            .Where(n => !triaged.Contains(n.Key))
            .ToList();
    }

    /// <summary>Each behaviour of <paramref name="corpus"/> as (id, title), in file order.</summary>
    public static List<(string Id, string Title)> Cards(string corpus)
    {
        var cards = new List<(string, string)>();
        foreach (var line in corpus.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!line.StartsWith("behaviour ", StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line["behaviour ".Length..];
            var space = rest.IndexOf(' ');
            var id = space < 0 ? rest : rest[..space];
            var title = space < 0 ? "" : rest[(space + 1)..].Trim().Trim('"');
            cards.Add((id, title));
        }

        return cards;
    }

    private static WorkItem Stop(WorkItem item, string because)
    {
        Console.Error.WriteLine($"kit: pipeline stopped {item.Project} {item.Slug}: {because}");
        return item with { Status = WorkItemStatus.Stopped, StoppedBecause = because };
    }
}
