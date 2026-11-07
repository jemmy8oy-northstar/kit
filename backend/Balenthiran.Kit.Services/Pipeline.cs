using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>, wiring slice 1: a note left in Kit queues a triage run of its project's notes
/// (BEH-ORCH-1) on the runner pool, and the run's end lets the next one start. A triage run reports the bundles
/// it pushed with its end, and each becomes a work item queued for spec (or review, when flagged). Spec, build and
/// review runs start from that bundle and report it back changed, which moves the item on. A built item gets a pull
/// request from its branch, opened by Kit. Exists
/// only with <c>KIT_PIPELINE=on</c>, as every run it starts spends Kit's own Anthropic credit. Work items are kept
/// IN MEMORY ONLY, as the pool's queue is: a restart forgets them, and the next note queues a triage again. Which
/// notes a triage has sorted is kept in the store (BEH-ORCH-8), so that triage reads only the notes still unsorted.
/// </summary>
/// <param name="owner">Whose runs these are: the operator's, as only he leaves notes.</param>
/// <param name="projects">The only projects whose notes are triaged (<c>KIT_PIPELINE_PROJECTS</c>); null is every project.</param>
/// <param name="pulls">Opens a built item's pull request; null stops a built item, saying Kit cannot open one.</param>
/// <param name="scheduled"><c>KIT_PIPELINE_SCHEDULED</c> (BEH-ORCH-4): also start the agent's scheduled cards at spec, one per project at a time. Off by default: each starts runs on the owner's subscription.</param>
/// <param name="sorted">Where the notes a triage sorted are kept (BEH-ORCH-8), so a restart does not triage them again; null keeps them in memory.</param>
public sealed class Pipeline(ICorpusDirectory corpora, RunnerPool pool, IAgentRunStore runs, StageModelSettings models, string owner = ISessionStore.Operator, TimeProvider? clock = null, IReadOnlySet<string>? projects = null, IPullRequestOpener? pulls = null, StageTurnSettings? turns = null, bool scheduled = false, ITriagedNoteStore? sorted = null)
{
    private readonly object gate = new();

    // Work items Kit made itself from a scheduled card (BEH-ORCH-4): their bundle is not on a branch yet.
    private readonly HashSet<(string App, string Slug)> scheduledSlugs = [];

    // Only ever appended to, so an index names the same item for the life of the pipeline.
    private readonly List<WorkItem> items = [];

    // A started run's item, the note keys a triage's task carries, the bundle a later stage's run was given, and the
    // owner/repo@branch it ran on, which a built item's pull request goes into.
    private readonly Dictionary<string, (int Index, IReadOnlyList<string> Notes, ContextBundle? Given, string RepoAt)> started = new(StringComparer.Ordinal);

    // Notes a finished triage run has already sorted into bundles, per project: a later triage leaves them out.
    // Read from the store on a project's first use, and written through to it (BEH-ORCH-8).
    private readonly Dictionary<string, HashSet<string>> triaged = new(StringComparer.Ordinal);

    private readonly ITriagedNoteStore store = sorted ?? new MemoryTriagedNoteStore();

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
    /// one will read this note too), then start whatever may start. A project not in <c>projects</c> queues nothing.
    /// </summary>
    public void NoteLeft(string app)
    {
        if (projects is not null && !projects.Contains(app))
        {
            return;
        }

        lock (gate)
        {
            QueueTriage(app);
            StartReady();

            // After the triage, which may have ended at once: notes go first.
            if (QueueScheduled(app))
            {
                StartReady();
            }
        }
    }

    /// <summary>
    /// Kit has started (BEH-ORCH-8): each project with sorted notes on record and notes not yet sorted gets a triage,
    /// so a note left while Kit was down, or whose triage failed, does not wait for his next note. A project with
    /// nothing on record starts nothing, so the first start with a store does not triage every note without a word
    /// from him. A store or notes file that cannot be read starts nothing for that project.
    /// </summary>
    public void Resume()
    {
        IReadOnlyList<string> recorded;
        try
        {
            recorded = store.Projects();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: listing the projects with triaged notes threw {e.GetType().Name}");
            return;
        }

        lock (gate)
        {
            foreach (var app in recorded.Where(a => projects is null || projects.Contains(a)))
            {
                bool waiting;
                try
                {
                    waiting = Untriaged(app).Count > 0;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"kit: reading {app}'s notes to resume threw {e.GetType().Name}");
                    continue;
                }

                if (waiting)
                {
                    QueueTriage(app);
                }
            }

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
        (int Index, WorkItem Item, ContextBundle Bundle, string RepoAt)? built = null;
        lock (gate)
        {
            if (!started.Remove(runId, out var run))
            {
                return;
            }

            var item = items[run.Index];
            if (item.Stage != PipelineStage.Triage)
            {
                items[run.Index] = Ended(item, run.Given!, succeeded, reported);

                // Built, whether straight from its build run or after a review of it: its branch becomes a pull request.
                if (items[run.Index] is { Stage: PipelineStage.Build, Status: WorkItemStatus.Done } done)
                {
                    built = (run.Index, done, bundles[(done.Project, done.Slug)], run.RepoAt);
                }
            }
            else if (!succeeded)
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
                    MarkSorted(item.Project, run.Notes);

                    // Always queued, so nothing is read here, outside StartReady's guard: a triage with no notes left is Done at once.
                    QueueTriage(item.Project);
                }
            }

            StartReady();
            if (QueueScheduled(items[run.Index].Project))
            {
                StartReady();
            }
        }

        // Outside the lock: opening it is a request to GitHub, and the pipeline must not wait on one.
        if (built is { } pr)
        {
            _ = OpenPullRequestAsync(pr.Index, pr.Item, pr.Bundle, pr.RepoAt);
        }
    }

    /// <summary>
    /// Opens the pull request of built <paramref name="item"/> from its work branch into the branch it ran on, and
    /// records its page on the item, or stops the item saying why there is none. An item a later triage has replaced
    /// meanwhile is left as it is.
    /// </summary>
    private async Task OpenPullRequestAsync(int index, WorkItem item, ContextBundle bundle, string repoAt)
    {
        var at = repoAt.LastIndexOf('@');
        string? url = null;
        string reason;
        if (pulls is null)
        {
            reason = "Kit has no way to open a pull request";
        }
        else
        {
            try
            {
                var result = await pulls.OpenAsync(TriageStage.Branch(item.Slug), repoAt[(at + 1)..], bundle.Title, BuildStage.PullRequestBody(item.Project, bundle), repoAt[..at]);
                url = result.Url;
                reason = result.Reason ?? "GitHub returned no pull request";
            }
            catch (Exception e)
            {
                // The type only, as Enqueue logs it: an exception from the opener may carry the token it sent.
                reason = $"opening it threw {e.GetType().Name}";
            }
        }

        lock (gate)
        {
            if (items[index] != item)
            {
                return;
            }

            items[index] = url is null ? Stop(item, $"its pull request could not be opened: {reason}") : item with { PullRequest = url };
        }
    }

    /// <summary>
    /// A spec or review run of <paramref name="item"/> ended: it must report its own bundle back, changed, and that
    /// bundle moves the item on. Bundles it reports for other work items are not its business.
    /// </summary>
    private WorkItem Ended(WorkItem item, ContextBundle given, bool succeeded, IReadOnlyList<ReportedBundle>? reported)
    {
        var stage = item.Stage.ToString().ToLowerInvariant();
        if (!succeeded)
        {
            return Stop(item, $"the {stage} run failed");
        }

        var own = reported?.FirstOrDefault(b => b.Slug == item.Slug);
        if (own is null)
        {
            return Stop(item, $"the {stage} run reported no bundle for its work item");
        }

        var parsed = own.Text is null ? null : ContextBundle.Parse(own.Text);
        if (parsed is null)
        {
            return Stop(item, "its bundle was too large for the runner to report");
        }

        if (parsed.Bundle is not { } bundle)
        {
            return Stop(item, $"its bundle is malformed: {parsed.Error}");
        }

        if (bundle.Slug != item.Slug)
        {
            return Stop(item, $"its bundle names work item \"{bundle.Slug}\"");
        }

        // A branch the run checked out and left alone is reported as it was: a stage that changed nothing did nothing.
        if (bundle.Render() == given.Render())
        {
            return Stop(item, $"the {stage} run left the bundle unchanged");
        }

        // A spec that only flagged itself would reach build after its review with nothing planned.
        if (item.Stage == PipelineStage.Spec && !bundle.Context.Contains(SpecStage.TestPlanHeading, StringComparison.Ordinal))
        {
            return Stop(item, "the spec run wrote no test plan");
        }

        // A build that does not say what it built cannot be told from one that only flagged itself. A review of a build
        // is held to it too, or a revision that dropped the list would still open a pull request.
        if ((item.Stage == PipelineStage.Build || item.ReviewedStage == PipelineStage.Build)
            && !bundle.Context.Contains(BuildStage.BuiltHeading, StringComparison.Ordinal))
        {
            return Stop(item, $"the {stage} run left no list of what was built");
        }

        bundles[(item.Project, item.Slug)] = bundle;
        return Orchestrator.Advance(item, new StageResult(item.Stage, true, bundle, null));
    }

    /// <summary>
    /// <c>BEH-ORCH-4</c>: <paramref name="app"/>'s schedule changed (a card was added to it): with
    /// <c>KIT_PIPELINE_SCHEDULED=on</c>, queue its first unstarted scheduled card at spec. Otherwise nothing happens.
    /// </summary>
    public void ScheduleChanged(string app)
    {
        if (!scheduled || (projects is not null && !projects.Contains(app)))
        {
            return;
        }

        lock (gate)
        {
            if (QueueScheduled(app))
            {
                StartReady();
            }
        }
    }

    /// <summary>
    /// <c>BEH-ORCH-4</c>: with the switch on, no triage queued for <paramref name="app"/> and no scheduled card of it
    /// unfinished, queue the first scheduled pending card that has no work item yet straight at spec, with a bundle
    /// written from the card itself. A card whose bundle cannot be made is queued stopped, saying why, so it is not
    /// tried again. True when an item was added.
    /// </summary>
    private bool QueueScheduled(string app)
    {
        if (!scheduled
            || (projects is not null && !projects.Contains(app))
            || items.Any(i => i.Project == app && i.Stage == PipelineStage.Triage && i.Status == WorkItemStatus.Queued)
            || items.Any(i => i.Project == app && scheduledSlugs.Contains((app, i.Slug)) && i.Status is WorkItemStatus.Queued or WorkItemStatus.Running))
        {
            return false;
        }

        try
        {
            var behaviours = new CorpusParser().Parse(corpora.Read(app), corpora.RelativePath(app));
            foreach (var id in Schedule.Parse(corpora.ReadScheduleText(app)))
            {
                var card = behaviours.FirstOrDefault(b => b.Id == id);
                // A paused card stays on the Backlog and is not built (BEH-BOARD-16), even when it is scheduled.
                if (card is not { Pending: true, Paused: false })
                {
                    continue;
                }

                var slug = SlugOf(id);
                if (items.Any(i => i.Project == app && i.Slug == slug))
                {
                    continue;
                }

                var contracts = card.Steps.Where(s => s.Kind == "contract").Select(s => s.Text.Trim()).Where(t => t.Length > 0).ToList();
                var context = contracts.Count == 0
                    ? $"Kit made this bundle from the scheduled card {id}, which has no contract lines yet."
                    : $"Kit made this bundle from the scheduled card {id}. Its contracts:\n" + string.Join('\n', contracts.Select(c => "- " + c));
                var notes = card.Source.Ref is { Length: > 0 } r && r == r.Trim() && !r.Contains('\n') ? [r] : new List<string>();
                var bundle = new ContextBundle(card.Title, slug, [id], notes, [], context);
                scheduledSlugs.Add((app, slug));
                if (bundle.Check() is { } why)
                {
                    items.Add(Stop(new WorkItem(app, slug, PipelineStage.Spec, WorkItemStatus.Queued), $"the scheduled card's bundle is not valid: {why}"));
                }
                else
                {
                    bundles[(app, slug)] = bundle;
                    items.Add(new WorkItem(app, slug, PipelineStage.Spec, WorkItemStatus.Queued));
                }

                return true;
            }
        }
        catch (Exception e)
        {
            // Not read means not queued: the next note, schedule change or run's end looks again.
            Console.Error.WriteLine($"kit: pipeline could not read the schedule of {app} ({e.GetType().Name})");
        }

        return false;
    }

    /// <summary>The work-item slug of card <paramref name="id"/>: lower-case words joined by hyphens (<c>BEH-ORCH-4</c> is <c>beh-orch-4</c>).</summary>
    private static string SlugOf(string id) =>
        System.Text.RegularExpressions.Regex.Replace(id.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

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
    /// replaces the item, unless that item is running a later stage. True when the bundle was usable.
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
        else if (bundle.Slug == TriageStage.SetAside)
        {
            // Its notes are sorted, so they are not triaged again, but nothing is built from them and no model reviews
            // them now: the item stops for a person, with the triage's reason.
            item = Stop(triage, $"set aside: {bundle.Flag ?? "the triage found nothing to build"}");
            usable = bundle;
        }
        else if (bundle.Slug == TriageStage.Backlog)
        {
            // An idea for later: its notes are sorted, nothing is specced or built, and the bot schedules it from its cards.
            item = Stop(triage, $"on the backlog: {bundle.Flag ?? "an idea for later"}");
            usable = bundle;
        }
        else
        {
            item = Orchestrator.Advance(triage, new StageResult(PipelineStage.Triage, true, bundle, null));
            usable = bundle;
        }

        return Record(app, reported.Slug, item, usable);
    }

    /// <summary>Puts <paramref name="item"/> in the list, replacing an earlier item of the same slug unless that is running. True when <paramref name="usable"/> is not null.</summary>
    private bool Record(string app, string slug, WorkItem item, ContextBundle? usable)
    {
        var at = items.FindIndex(i => i.Project == app && i.Slug == slug);
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
            bundles.Remove((app, slug));
        }
        else
        {
            bundles[(app, slug)] = usable;
        }

        return usable is not null;
    }

    private void StartReady()
    {
        // An item that ends at once (a triage with nothing to read, or one that cannot start) frees its project, so
        // look again. A queued triage goes first: its new notes may update a bundle that would otherwise be specced
        // or built twice. Then a build, so an item is finished before another is specced, even when a later triage
        // sent an item earlier in the list back to spec.
        bool again;
        do
        {
            again = false;
            var ready = items.OrderBy(i => i.Stage switch { PipelineStage.Triage => 0, PipelineStage.Build => 1, _ => 2 });
            foreach (var next in Orchestrator.Startable(ready))
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

                again |= items[index].Status != WorkItemStatus.Running;
            }
        }
        while (again);
    }

    /// <summary>The item once its run is handed to the pool; Done when a triage has nothing to read; Stopped when it cannot start.</summary>
    private WorkItem Begin(int index, WorkItem item)
    {
        var corpus = corpora.ReadText(item.Project);
        var (repoAt, problem) = RepoDirective.Read(corpus);
        if (repoAt is null)
        {
            return Stop(item, problem ?? $"{item.Project} names no repo: its spec wants a `# kit:repo owner/repo@branch` line");
        }

        if (item.Stage != PipelineStage.Triage)
        {
            if (bundles.GetValueOrDefault((item.Project, item.Slug)) is not { } bundle)
            {
                return Stop(item, "its bundle is gone");
            }

            string text;
            try
            {
                text = item.Stage switch
                {
                    PipelineStage.Spec => SpecStage.BuildTask(item.Project, corpora.RelativePath(item.Project), bundle, scheduledSlugs.Contains((item.Project, item.Slug))),
                    PipelineStage.Build => BuildStage.BuildTask(item.Project, bundle),
                    _ => ReviewStage.BuildTask(item.Project, bundle),
                };
            }
            catch (InvalidOperationException e)
            {
                return Stop(item, e.Message);
            }

            return Enqueue(index, item, repoAt, text, [], bundle);
        }

        var notes = Untriaged(item.Project);

        // The plain rule first (BEH-ORCH-1): a note that asks for nothing is set aside here, with no run, and counts as sorted.
        var noise = notes.Where(n => NoteNoise.IsNoise(n.Text)).ToList();
        if (noise.Count > 0)
        {
            MarkSorted(item.Project, noise.Select(n => n.Key).ToList());
            var aside = Stop(new WorkItem(item.Project, TriageStage.SetAside, PipelineStage.Triage, WorkItemStatus.Running), $"set aside: {NoteNoise.Reason}");
            Record(item.Project, TriageStage.SetAside, aside, null);
            notes = notes.Where(n => !NoteNoise.IsNoise(n.Text)).ToList();
        }

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
        return Enqueue(index, item, repoAt, task.Text, taken, null);
    }

    /// <summary>Hands a run of <paramref name="item"/>'s stage to the pool and records it; Stopped when the pool will not take it.</summary>
    private WorkItem Enqueue(int index, WorkItem item, string repoAt, string task, IReadOnlyList<string> notes, ContextBundle? given)
    {
        var stage = item.Stage.ToString().ToLowerInvariant();
        var at = repoAt.LastIndexOf('@');
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var token = AgentTokens.Mint();
        var run = runs.Create(owner, item.Project, Orchestrator.ModelFor(item.Stage, models, given?.Flag), AgentTokens.Hash(token), now,
            item.Stage == PipelineStage.Triage ? null : item.Slug);
        try
        {
            pool.Enqueue(run, token, repoAt[..at], task, repoAt[(at + 1)..], Orchestrator.TurnsFor(item.Stage, turns ?? StageTurnSettings.Defaults));
        }
        catch (Exception e)
        {
            // The type only, as StartRun logs it: the exception may carry what the pool was told, and the token is in it.
            Console.Error.WriteLine($"kit: queuing a {stage} run threw {e.GetType().Name}");
            runs.SetStatus(run.Id, AgentRunStatus.Failed, now);
            return Stop(item, $"the runner pool would not take the {stage} run");
        }

        started[run.Id] = (index, notes, given, repoAt);
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
            .Select(n => (Ref: $"{file}#{n.At}", n.Text, Key: KeyOf(n.At, n.Text)))
            .Where(n => !Sorted(app).Contains(n.Key))
            .ToList();
    }

    /// <summary>A note's key: the SHA-256 of its date and text, so the store keeps no copy of what he wrote.</summary>
    public static string KeyOf(string at, string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(at + "\n" + text)));

    /// <summary>
    /// <paramref name="app"/>'s sorted notes, read from the store on first use. A store that cannot be read is
    /// counted as empty until Kit restarts, so the cost of a database that is down is a triage, not Kit.
    /// </summary>
    private HashSet<string> Sorted(string app)
    {
        if (!triaged.TryGetValue(app, out var keys))
        {
            try
            {
                keys = store.Read(app).ToHashSet(StringComparer.Ordinal);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"kit: reading {app}'s triaged notes threw {e.GetType().Name}");
                keys = new HashSet<string>(StringComparer.Ordinal);
            }

            triaged[app] = keys;
        }

        return keys;
    }

    /// <summary>Marks <paramref name="keys"/> sorted on <paramref name="app"/>, here and in the store; a store that cannot be written costs a later triage, not this one's report.</summary>
    private void MarkSorted(string app, IReadOnlyList<string> keys)
    {
        Sorted(app).UnionWith(keys);
        try
        {
            store.Add(app, keys);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: recording {app}'s triaged notes threw {e.GetType().Name}");
        }
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
