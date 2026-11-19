using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>, wiring slice 1: a note left in Kit queues a triage run of its project's notes
/// (BEH-ORCH-1) on the runner pool, and the run's end lets the next one start. A triage run reports the bundles
/// it pushed with its end, and each becomes a work item queued for spec (or review, when flagged). Spec, build and
/// review runs start from that bundle and report it back changed, which moves the item on. A built item gets a pull
/// request from its branch, opened by Kit. Exists
/// only with <c>KIT_PIPELINE=on</c>, as every run it starts spends Kit's own Anthropic credit. Each work item with a
/// bundle is also a ticket in the store (BEH-ORCH-11), which <see cref="Resume"/> reloads, so a restart keeps it; the
/// pool's queue and the runs in flight are in memory only, as before. Which
/// notes a triage has sorted is kept in the store (BEH-ORCH-8), so that triage reads only the notes still unsorted.
/// </summary>
/// <param name="owner">Whose runs these are: the operator's, as only he leaves notes.</param>
/// <param name="projects">The only projects whose notes are triaged (<c>KIT_PIPELINE_PROJECTS</c>); null is every project.</param>
/// <param name="pulls">Opens a built item's pull request; null stops a built item, saying Kit cannot open one.</param>
/// <param name="scheduled"><c>KIT_PIPELINE_SCHEDULED</c> (BEH-ORCH-4): also start the agent's scheduled cards at spec, one per project at a time. Off by default: each starts runs on the owner's subscription.</param>
/// <param name="merger">Merges a built item's pull request once its checks pass (BEH-ORCH-10); null stops such an item, saying Kit cannot merge.</param>
/// <param name="merge"><c>KIT_PIPELINE_MERGE</c> (BEH-ORCH-10): <see cref="MergeReadyAsync"/> merges built items' pull requests. Off by default: a merge puts code no person has read into the base branch.</param>
/// <param name="sorted">Where the notes a triage sorted are kept (BEH-ORCH-8), so a restart does not triage them again; null keeps them in memory.</param>
/// <param name="tickets">Where the tickets are kept (BEH-ORCH-11), so a restart does not lose them; null keeps them in memory.</param>
/// <param name="jobs">The stages a job owns (BEH-ORCH-11, slices 3 to 5: <see cref="PipelineStage.Context"/> with <c>KIT_PIPELINE_JOB_CONTEXT=on</c>, <see cref="PipelineStage.Spec"/> with <c>KIT_PIPELINE_JOB_PLAN=on</c>, <see cref="PipelineStage.Build"/> with <c>KIT_PIPELINE_JOB_BUILD=on</c>). An item queued at one of them waits for <see cref="JobTick"/> instead of <see cref="StartReady"/> starting it. Null or empty is none: each job's run spends the owner's subscription, so each is off by default.</param>
public sealed class Pipeline(ICorpusDirectory corpora, RunnerPool pool, IAgentRunStore runs, StageModelSettings models, string owner = ISessionStore.Operator, TimeProvider? clock = null, IReadOnlySet<string>? projects = null, IPullRequestOpener? pulls = null, StageTurnSettings? turns = null, bool scheduled = false, ITriagedNoteStore? sorted = null, IPullRequestMerger? merger = null, bool merge = false, ITicketStore? tickets = null, IReadOnlySet<PipelineStage>? jobs = null)
{
    private readonly object gate = new();

    // BEH-ORCH-11, slice 1: every work item that has a bundle is also a ticket row, written whenever the item changes
    // and reloaded by Resume. A work item's stage and status map onto a ticket's state like this. Each job's switch takes
    // over exactly one hop of the chain: with KIT_PIPELINE_JOB_CONTEXT off a triaged item goes straight to the spec stage
    // (today's "plan"), as before; with it on, it waits at Context, which the context job takes, and the chain resumes at
    // spec when the context run has ended. With KIT_PIPELINE_JOB_PLAN on, a queued Spec item waits too, however it got
    // there, for the plan job to start its spec run; with it off StartReady starts it as before. The same holds for a
    // queued Build item (a planned ticket) and KIT_PIPELINE_JOB_BUILD, the build job.
    //   Context (waiting, running, or under review) -> new
    //   Spec  (queued, running, or under review) -> ready
    //   Build (queued, running, or under review) -> planned
    //   Build, Done, no pull request merged      -> built      (its pull request, once opened, is on the ticket)
    //   Build, Done, Merged                      -> merged
    //   any stage, Stopped                       -> stopped    (with why)
    // A triage item is not a ticket: it has no bundle. Neither is the set-aside or backlog pseudo-item.
    // Resume reverses it: ready becomes a queued Spec item (which waits when the plan job is on), and so does new unless the
    // context job is on, when it is a waiting Context item; planned becomes a queued Build item (which waits when the build
    // job is on), built a Done Build item, which is not queued and so is never taken by a job.
    // A run in flight, or a review under way, is not kept: a resumed item starts its stage again.
    private readonly ITicketStore ticketStore = tickets ?? new MemoryTicketStore();

    // The priority a resumed ticket had (1 to 4, lowest first): a job takes the waiting ticket with the lowest,
    // and a save writes it back rather than resetting it. A ticket not here has DefaultPriority.
    private readonly Dictionary<(string App, string Slug), int> priorities = [];

    // Work items Kit made itself from a scheduled card (BEH-ORCH-4): their bundle is not on a branch yet.
    private readonly HashSet<(string App, string Slug)> scheduledSlugs = [];

    // BEH-ORCH-11, slice 7b: tickets that have had their one conflict-resolution run. In memory only: the ticket row has
    // no column for it, so a restart forgets it and a ticket may then get one more run.
    private readonly HashSet<(string App, string Slug)> resolved = [];

    // BEH-ORCH-12: the head commit a Kit test run passed at, per ticket, which the merge look hands the merger; and the commit
    // a test run in flight was started for. In memory only, like resolved: a restart forgets them and a ticket may get one more test run.
    private readonly Dictionary<(string App, string Slug), string> tested = [];

    private readonly Dictionary<(string App, string Slug), string> testing = [];

    // BEH-ORCH-11: the projects whose merge job, at its last look, needed a run (a test or a resolve) but found the
    // project busy. No job starts a ticket on them, so the next free slot goes to finishing a ticket, not starting one.
    // Replaced whole at the end of each merge look, so a want the merge job no longer has lasts at most one look.
    private HashSet<string> mergeWants = new(StringComparer.Ordinal);

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

    /// <summary>BEH-ORCH-8: how long after a failed triage the next try is made.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    /// <summary>BEH-ORCH-8: a failing triage is tried again at most this many times in a row, until one works.</summary>
    public const int MaxRetries = 12;

    // Per project, under gate: consecutive failed triages, and the one timer of the retry pending (null when none)
    // with its generation, so a timer that fires after it was replaced does not take its replacement's turn.
    private readonly Dictionary<string, (int Failures, ITimer? Pending, int Generation)> retries = new(StringComparer.Ordinal);

    private int retryGeneration;

    private int count;

    /// <summary>BEH-ORCH-16: every ticket of <paramref name="project"/>, oldest first, each with its comments, oldest first.</summary>
    public IReadOnlyList<(PipelineTicket Ticket, IReadOnlyList<TicketComment> Comments)> Tickets(string project) =>
        ticketStore.Read(project).Select(t => (t, ticketStore.Comments(project, t.Slug))).ToList();

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
        ResumeTickets();

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
                    waiting = Untriaged(app).Count > 0 || Replies(app).Count > 0;
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

    /// <summary>
    /// BEH-ORCH-11: reloads the open tickets as work items, so a restart does not lose them. A ticket whose project is
    /// not in <c>projects</c>, whose bundle cannot be read, or which the pipeline already holds is left out. A store that
    /// cannot be read resumes none. Starts nothing itself: <see cref="Resume"/> does that once everything is loaded.
    /// </summary>
    private void ResumeTickets()
    {
        IReadOnlyList<PipelineTicket> open;
        try
        {
            open = ticketStore.Open();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: reading the open tickets threw {e.GetType().Name}");
            return;
        }

        lock (gate)
        {
            foreach (var ticket in open)
            {
                if ((projects is not null && !projects.Contains(ticket.Project)) || items.Any(i => i.Project == ticket.Project && i.Slug == ticket.Slug))
                {
                    continue;
                }

                var bundle = ticket.Bundle is null ? null : ContextBundle.Parse(ticket.Bundle).Bundle;
                if (bundle is null)
                {
                    Console.Error.WriteLine($"kit: the bundle of ticket {ticket.Project} {ticket.Slug} could not be read; not resuming it");
                    continue;
                }

                var item = ticket.State switch
                {
                    TicketState.New when jobs?.Contains(PipelineStage.Context) == true => new WorkItem(ticket.Project, ticket.Slug, PipelineStage.Context, WorkItemStatus.Queued),
                    TicketState.New or TicketState.Ready => new WorkItem(ticket.Project, ticket.Slug, PipelineStage.Spec, WorkItemStatus.Queued),
                    TicketState.Planned => new WorkItem(ticket.Project, ticket.Slug, PipelineStage.Build, WorkItemStatus.Queued),
                    TicketState.Built => new WorkItem(ticket.Project, ticket.Slug, PipelineStage.Build, WorkItemStatus.Done, PullRequest: ticket.PullRequest),
                    _ => null,
                };
                if (item is null)
                {
                    continue;
                }

                // BEH-ORCH-16: a blocked ticket comes back still stopped, so no job takes it and he still sees why.
                if (ticket.BlockedBecause is not null)
                {
                    item = item with { Status = WorkItemStatus.Stopped, StoppedBecause = ticket.BlockedBecause, PullRequest = ticket.PullRequest };
                }

                bundles[(ticket.Project, ticket.Slug)] = bundle;
                priorities[(ticket.Project, ticket.Slug)] = ticket.Priority;
                if (ticket.Scheduled)
                {
                    scheduledSlugs.Add((ticket.Project, ticket.Slug));
                }

                items.Add(item);
            }
        }
    }

    /// <summary>Puts <paramref name="item"/> at <paramref name="index"/> and writes its ticket.</summary>
    private void Set(int index, WorkItem item)
    {
        var wasBlocked = items[index].Status == WorkItemStatus.Stopped;
        items[index] = item;
        Persist(item, wasBlocked);
    }

    /// <summary>Appends <paramref name="item"/> and writes its ticket.</summary>
    private void Add(WorkItem item)
    {
        items.Add(item);
        Persist(item);
    }

    /// <summary>
    /// Writes <paramref name="item"/>'s ticket (the mapping is at the top of the class). A store that cannot be written
    /// costs the ticket its restart, not the pipeline its run. Called with the gate held.
    /// </summary>
    private void Persist(WorkItem item, bool wasBlocked = false)
    {
        // "#" is never in a bundle slug: a triage's own item is no ticket, nor are the pseudo-items of notes set aside.
        if (item.Slug.Contains('#') || item.Slug is TriageStage.SetAside or TriageStage.Backlog)
        {
            return;
        }

        bundles.TryGetValue((item.Project, item.Slug), out var bundle);
        string? text = null;
        try
        {
            text = bundle?.Render();
        }
        catch (InvalidOperationException)
        {
            // A bundle that will not render is kept out of the row; the ticket is still written.
        }

        // BEH-ORCH-16: a stopped item keeps the state it reached and is tagged blocked with its reason, so the ticket is
        // "ready but blocked" rather than a state of its own.
        var stage = item.Stage == PipelineStage.Review ? item.ReviewedStage ?? PipelineStage.Spec : item.Stage;
        var state = item switch
        {
            { Merged: true } => TicketState.Merged,
            _ => stage switch
            {
                PipelineStage.Spec => TicketState.Ready,
                // A stopped item is no longer Done, so its pull request is what says it was built (a refused merge).
                PipelineStage.Build => item.Status == WorkItemStatus.Done || item.PullRequest is not null ? TicketState.Built : TicketState.Planned,
                PipelineStage.Merge => TicketState.Built, // its conflict-resolution run is in flight (slice 7b)
                PipelineStage.Test => TicketState.Built, // its test run is in flight (BEH-ORCH-12)
                _ => TicketState.New, // Context, or a triage under review
            },
        };
        var blocked = item.Status == WorkItemStatus.Stopped && !item.Merged ? item.StoppedBecause ?? "stopped" : null;
        try
        {
            ticketStore.Save(new PipelineTicket(item.Project, item.Slug, bundle?.Title ?? item.Slug, state, priorities.GetValueOrDefault((item.Project, item.Slug), DefaultPriority), bundle?.Cards ?? [], text, item.PullRequest, item.StoppedBecause, scheduledSlugs.Contains((item.Project, item.Slug)), blocked));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: writing the ticket {item.Project} {item.Slug} threw {e.GetType().Name}");
        }

        // Apart from the row, so a row that could not be written does not also lose the only comment saying why.
        if (blocked is not null && !wasBlocked)
        {
            try
            {
                ticketStore.Comment(new TicketComment(item.Project, item.Slug, KitAuthor, $"Blocked: {blocked}", (clock ?? TimeProvider.System).GetUtcNow()));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"kit: commenting on the ticket {item.Project} {item.Slug} threw {e.GetType().Name}");
            }
        }
    }

    // BEH-ORCH-11, slice 6: a ticket's priority is the lowest `priority` (1 to 4) among the cards its bundle lists, and
    // 3 when none of them has one. The jobs read it again on every tick, so a card re-prioritised in Kit reorders the waiting tickets.
    private const int DefaultPriority = 3;

    /// <summary><paramref name="app"/>'s cards, parsed; null when its corpus cannot be read, so the priorities it had stand.</summary>
    private IReadOnlyList<IBehaviour>? CorpusCards(string app)
    {
        try
        {
            return new CorpusParser().Parse(corpora.Read(app), corpora.RelativePath(app));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: pipeline could not read the priorities of {app}'s cards ({e.GetType().Name})");
            return null;
        }
    }

    /// <summary>
    /// Sets the priority of ticket (<paramref name="app"/>, <paramref name="slug"/>) from the cards <paramref name="bundle"/> lists.
    /// A card that is not in <paramref name="cards"/>, or has no priority, is ignored; when none counts the ticket keeps the
    /// priority it has (3, or the one a restart read back). A null <paramref name="cards"/> changes nothing. Called with the gate held.
    /// </summary>
    private void SetPriority(string app, string slug, ContextBundle bundle, IReadOnlyList<IBehaviour>? cards)
    {
        if (cards is null)
        {
            return;
        }

        if (CardPriority(bundle.Cards, cards) is { } priority)
        {
            priorities[(app, slug)] = priority;
        }
    }

    /// <summary>
    /// The lowest priority (1 to 4; the card scale's 5 counts as 4) of the cards <paramref name="ids"/> names, and
    /// <see cref="DefaultPriority"/> when they are in <paramref name="cards"/> but none has one (so a priority line he
    /// removes takes effect). Null only when none of them is in <paramref name="cards"/>: there is nothing to read it from.
    /// </summary>
    private static int? CardPriority(IEnumerable<string> ids, IReadOnlyList<IBehaviour> cards)
    {
        var found = ids.SelectMany(id => cards.Where(c => c.Id == id)).ToList();
        if (found.Count == 0)
        {
            return null;
        }

        var set = found.Where(c => c.Priority is not null).Select(c => Math.Min(c.Priority!.Value, 4)).ToList();
        return set.Count == 0 ? DefaultPriority : set.Min();
    }

    /// <summary>Reads <paramref name="app"/>'s cards once and sets the priority of each of its tickets that has a bundle. Called with the gate held.</summary>
    private void RefreshPriorities(string app)
    {
        var cards = CorpusCards(app);
        if (cards is null)
        {
            return;
        }

        foreach (var ((project, slug), bundle) in bundles.Where(b => b.Key.App == app).ToList())
        {
            SetPriority(project, slug, bundle, cards);
        }
    }

    /// <summary>
    /// BEH-ORCH-8, slice 2: a triage of <paramref name="app"/> failed (a failed run, or one cut short by the subscription's
    /// limit), so another is tried <see cref="RetryAfter"/> later. A project has at most one retry pending, and none is
    /// scheduled once <see cref="MaxRetries"/> retries in a row have failed too. Called with the gate held.
    /// </summary>
    private void TriageFailed(string app)
    {
        retries.TryGetValue(app, out var state);
        var failures = state.Failures + 1;
        if (failures > MaxRetries)
        {
            state.Pending?.Dispose();
            retries[app] = (failures, null, 0);
            Console.Error.WriteLine($"kit: {app}'s triage failed {failures} times in a row; not retrying until one works");
            return;
        }

        if (state.Pending is not null)
        {
            retries[app] = (failures, state.Pending, state.Generation);
            return;
        }

        var generation = ++retryGeneration;
        var pending = (clock ?? TimeProvider.System).CreateTimer(_ => RetryDue(app, generation), null, RetryAfter, Timeout.InfiniteTimeSpan);
        retries[app] = (failures, pending, generation);
    }

    /// <summary>A triage of <paramref name="app"/> worked: its failures are forgotten and a retry still pending is cancelled. Called with the gate held.</summary>
    private void ClearRetries(string app)
    {
        if (retries.Remove(app, out var state))
        {
            state.Pending?.Dispose();
        }
    }

    /// <summary>The hour is up: queue the triage again, which reads only the notes still unsorted.</summary>
    private void RetryDue(string app, int generation)
    {
        try
        {
            lock (gate)
            {
                if (!retries.TryGetValue(app, out var state) || state.Pending is null || state.Generation != generation)
                {
                    return;
                }

                state.Pending.Dispose();
                retries[app] = (state.Failures, null, 0);
                QueueTriage(app);
                StartReady();
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: retrying {app}'s triage threw {e.GetType().Name}");
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
    /// ignored. A run that never reports holds its project, as it holds the pool's, until the pool counts its runner
    /// lost (<see cref="RunnerPool.Lease"/>, BEH-ORCH-14) and ends it here as failed.
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
            if (item.Stage == PipelineStage.Merge)
            {
                // BEH-ORCH-11, slice 7b: a conflict-resolution run. It reports no bundle; its success is the run's. A
                // built item again, so the next merge look reads its pull request, waits for the checks on the new head
                // and merges. The pull request is already open, so none is opened here.
                Set(run.Index, succeeded
                    ? item with { Stage = PipelineStage.Build, Status = WorkItemStatus.Done }
                    : Stop(item, "the conflict-resolution run failed"));
            }
            else if (item.Stage == PipelineStage.Test)
            {
                // BEH-ORCH-12: a test run of the pull request's head commit. It reports no bundle; its success is the run's. A
                // pass is remembered for that commit and the item is built again, so the next merge look merges exactly it.
                var key = (item.Project, item.Slug);
                testing.Remove(key, out var sha);
                if (succeeded && sha is not null)
                {
                    tested[key] = sha;
                    Set(run.Index, item with { Stage = PipelineStage.Build, Status = WorkItemStatus.Done });
                }
                else
                {
                    Set(run.Index, Stop(item, $"the project's tests failed at {(sha is null ? "its head commit" : sha.Length > 7 ? sha[..7] : sha)}"));
                }
            }
            else if (item.Stage != PipelineStage.Triage)
            {
                Set(run.Index, Ended(item, run.Given!, succeeded, reported));

                // Built, whether straight from its build run or after a review of it: its branch becomes a pull request.
                if (items[run.Index] is { Stage: PipelineStage.Build, Status: WorkItemStatus.Done } done)
                {
                    built = (run.Index, done, bundles[(done.Project, done.Slug)], run.RepoAt);
                }
            }
            else if (!succeeded)
            {
                Set(run.Index, Stop(item, "the triage run failed"));
                TriageFailed(item.Project);
            }
            else
            {
                var usable = 0;
                var took = new HashSet<string>(StringComparer.Ordinal);
                foreach (var b in reported ?? [])
                {
                    if (Took(item.Project, b))
                    {
                        usable++;
                        took.Add(b.Slug);
                    }
                }

                // Every note goes in a bundle, so a triage with none usable sorted nothing: its notes are read again by
                // the next note's triage, as a failed run's are, rather than being dropped.
                if (usable == 0)
                {
                    Set(run.Index, Stop(item, "the triage reported no usable bundle"));
                }
                else
                {
                    Set(run.Index, item with { Status = WorkItemStatus.Done });
                    SayUnchanged(item.Project, run.Notes, took);
                    MarkSorted(item.Project, run.Notes);
                    ClearRetries(item.Project);

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

            Set(index, url is null ? Stop(item, $"its pull request could not be opened: {reason}") : item with { PullRequest = url });
        }
    }

    /// <summary>
    /// <c>BEH-ORCH-10</c>: with the switch on, asks to merge the pull request of each built item not yet merged. A merge
    /// is merged, still waiting (left for the next look), or refused (the item stops saying why, and the pull request
    /// stays open). An item changed meanwhile is left as it is. Does nothing with the switch off.
    /// </summary>
    public async Task MergeReadyAsync(CancellationToken cancellationToken)
    {
        // With the merge job on, the job owns this hop (BEH-ORCH-11, slice 7), whatever KIT_PIPELINE_MERGE says.
        if (!merge || jobs?.Contains(PipelineStage.Merge) == true)
        {
            return;
        }

        List<WorkItem> ready;
        lock (gate)
        {
            ready = items.Where(IsBuilt).ToList();
        }

        foreach (var item in ready)
        {
            await MergeOneAsync(item, false, cancellationToken);
        }
    }

    private static bool IsBuilt(WorkItem i) => i is { Stage: PipelineStage.Build, Status: WorkItemStatus.Done, PullRequest: not null, Merged: false };

    /// <summary>
    /// BEH-ORCH-11, slice 7: one look of the merge job (<c>KIT_PIPELINE_JOB_MERGE=on</c>; nothing happens with it off). Per
    /// project with a built, unmerged item, takes only the one with the lowest priority number (oldest first among equals)
    /// and asks to merge it after bringing its branch up to date with its base, so merges into the base branch happen one
    /// at a time in priority order. Merged, waiting and refused are as for <see cref="MergeReadyAsync"/>: a refused item
    /// stops, and the next look takes the next one.
    /// </summary>
    public async Task MergeTickAsync(CancellationToken cancellationToken)
    {
        if (jobs?.Contains(PipelineStage.Merge) != true)
        {
            return;
        }

        List<WorkItem> top;
        lock (gate)
        {
            // A project whose ticket is having a conflict resolved (slice 7b) or tested (BEH-ORCH-12) merges nothing else until that run ends.
            var apps = items.Where(IsBuilt).Select(i => i.Project).Distinct(StringComparer.Ordinal)
                .Where(app => !items.Any(i => i.Project == app && i.Stage is PipelineStage.Merge or PipelineStage.Test && i.Status == WorkItemStatus.Running))
                .ToList();
            top = [];
            foreach (var app in apps)
            {
                RefreshPriorities(app);
                var next = items
                    .Select((item, index) => (Item: item, Index: index))
                    .Where(x => x.Item.Project == app && IsBuilt(x.Item))
                    .OrderBy(x => priorities.GetValueOrDefault((x.Item.Project, x.Item.Slug), DefaultPriority))
                    .ThenBy(x => x.Index)
                    .First();
                top.Add(next.Item);
            }
        }

        var wants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in top)
        {
            await MergeOneAsync(item, true, cancellationToken, wants);
        }

        lock (gate)
        {
            mergeWants = wants;
        }
    }

    private async Task MergeOneAsync(WorkItem item, bool updateFirst, CancellationToken cancellationToken, HashSet<string>? wants = null)
    {
        IPullRequestMerge? result = null;
        string? reason = null;
        if (merger is null)
        {
            reason = "Kit has no way to merge a pull request";
        }
        else
        {
            string? testedSha = null;
            if (updateFirst)
            {
                lock (gate)
                {
                    tested.TryGetValue((item.Project, item.Slug), out testedSha);
                }
            }

            try
            {
                result = await merger.MergeIfGreenAsync(item.PullRequest!, null, cancellationToken, updateFirst, testedSha);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The type only, as OpenPullRequestAsync does: an exception from the merger may carry the token it sent.
                Console.Error.WriteLine($"kit: merging {item.Project} {item.Slug} threw {e.GetType().Name}");
                return;
            }
        }

        if (result is { Merged: false, Waiting: true })
        {
            return;
        }

        lock (gate)
        {
            var index = items.IndexOf(item);
            if (index < 0 || items[index] != item)
            {
                return;
            }

            // BEH-ORCH-11, slice 7b: with the merge job on, a conflict goes to one resolve run instead of stopping the item.
            if (updateFirst && result is { Merged: false, Conflict: true })
            {
                if (resolved.Contains((item.Project, item.Slug)))
                {
                    Set(index, Stop(item, "its pull request was not merged: it still conflicts with its base after an agent tried to resolve it"));
                    return;
                }

                // A project with a run in flight is busy: the item stays built, the project is kept for it, and the next look asks again.
                if (items.Any(i => i.Project == item.Project && i.Status == WorkItemStatus.Running))
                {
                    wants?.Add(item.Project);
                    return;
                }

                resolved.Add((item.Project, item.Slug));
                try
                {
                    Set(index, Begin(index, item with { Stage = PipelineStage.Merge, Status = WorkItemStatus.Queued }));
                }
                catch (Exception e)
                {
                    Set(index, Stop(item, $"the project could not be read ({e.GetType().Name})"));
                }

                return;
            }

            // BEH-ORCH-12: a private repo's pull request has no Actions run, so Kit tests the head commit itself, once per
            // commit: a head that moves is asked about again, and a pass is remembered for the commit it passed at.
            if (updateFirst && result is { Merged: false, NeedsTest: true, HeadSha: { Length: > 0 } sha })
            {
                // A project with a run in flight is busy: the item stays built, the project is kept for it, and the next look asks again.
                if (items.Any(i => i.Project == item.Project && i.Status == WorkItemStatus.Running))
                {
                    wants?.Add(item.Project);
                    return;
                }

                testing[(item.Project, item.Slug)] = sha;
                try
                {
                    Set(index, Begin(index, item with { Stage = PipelineStage.Test, Status = WorkItemStatus.Queued }));
                }
                catch (Exception e)
                {
                    Set(index, Stop(item, $"the project could not be read ({e.GetType().Name})"));
                }

                return;
            }

            Set(index, result is { Merged: true }
                ? item with { Merged = true }
                : Stop(item, $"its pull request was not merged: {reason ?? result?.Reason ?? "GitHub gave no reason"}"));
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
        return AfterTriage(item, Orchestrator.Advance(item, new StageResult(item.Stage, true, bundle, null)));
    }

    /// <summary>
    /// BEH-ORCH-11, slice 3: the one hop the context job's switch takes over. With <c>KIT_PIPELINE_JOB_CONTEXT=on</c> an
    /// item that has finished triage (or the review of it) and would be queued for spec waits at Context instead, for
    /// <see cref="JobTick"/>. With it off, or for any other stage's result, <paramref name="after"/> is returned as it is.
    /// </summary>
    private WorkItem AfterTriage(WorkItem before, WorkItem after) =>
        jobs?.Contains(PipelineStage.Context) == true
        && (before.Stage == PipelineStage.Triage || before is { Stage: PipelineStage.Review, ReviewedStage: PipelineStage.Triage })
        && after is { Stage: PipelineStage.Spec, Status: WorkItemStatus.Queued }
            ? after with { Stage = PipelineStage.Context }
            : after;

    /// <summary>An item a job owns the stage of and has not taken yet: queued, but not for <see cref="StartReady"/> to start.</summary>
    private bool IsWaiting(WorkItem item) => item.Status == WorkItemStatus.Queued && jobs?.Contains(item.Stage) == true;

    /// <summary>
    /// BEH-ORCH-11, slices 3 to 5: one look of the job for <paramref name="stage"/> (Context: <c>new</c> tickets; Spec: <c>ready</c>
    /// tickets, the plan job; Build: <c>planned</c> tickets, the build job). With that stage's switch off, nothing happens. Per project with a ticket waiting at that
    /// stage, and no run in flight, takes the one with the lowest priority number (oldest first among equals) and starts
    /// its run. One ticket at a time per project, and a project with nothing waiting makes no model call. A ticket that
    /// cannot start (no repo, no bundle) is stopped with why, and the next one is taken.
    /// </summary>
    public void JobTick(PipelineStage stage)
    {
        if (jobs?.Contains(stage) != true)
        {
            return;
        }

        lock (gate)
        {
            // A project the merge job is waiting on for a test or resolve run starts nothing new (mergeWants).
            foreach (var app in items.Where(i => i.Stage == stage && IsWaiting(i)).Select(i => i.Project).Distinct(StringComparer.Ordinal).Where(app => !mergeWants.Contains(app)).ToList())
            {
                RefreshPriorities(app);
                // Begin returns Running or Stopped, never Queued, so each pass takes a ticket off the waiting list.
                while (!items.Any(i => i.Project == app && i.Status == WorkItemStatus.Running))
                {
                    var next = items
                        .Select((item, index) => (Item: item, Index: index))
                        .Where(x => x.Item.Project == app && x.Item.Stage == stage && IsWaiting(x.Item))
                        .OrderBy(x => priorities.GetValueOrDefault((x.Item.Project, x.Item.Slug), DefaultPriority))
                        .ThenBy(x => x.Index)
                        .Cast<(WorkItem Item, int Index)?>()
                        .FirstOrDefault();
                    if (next is not { } taken)
                    {
                        break;
                    }

                    try
                    {
                        Set(taken.Index, Begin(taken.Index, taken.Item));
                    }
                    catch (Exception e)
                    {
                        Set(taken.Index, Stop(taken.Item, $"the project could not be read ({e.GetType().Name})"));
                    }
                }
            }

            StartReady();
        }
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
                    Add(Stop(new WorkItem(app, slug, PipelineStage.Spec, WorkItemStatus.Queued), $"the scheduled card's bundle is not valid: {why}"));
                }
                else
                {
                    bundles[(app, slug)] = bundle;
                    SetPriority(app, slug, bundle, behaviours);
                    Add(new WorkItem(app, slug, PipelineStage.Spec, WorkItemStatus.Queued));
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
    /// BEH-ORCH-11, slice 2: the open tickets are in the triage's task, so a bundle reported under an open ticket's
    /// slug is that ticket's update: it replaces the ticket's item and bundle (one item, one row). Today a project runs
    /// one thing at a time, and the reporting triage is it, so no other item can be running when the report lands; when
    /// the jobs run beside triage that can happen, and the update is then dropped here (its notes still count as sorted),
    /// which the job slices must handle.
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
            item = AfterTriage(triage, Orchestrator.Advance(triage, new StageResult(PipelineStage.Triage, true, bundle, null)));
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

        var wasBlocked = at >= 0 && items[at].Status == WorkItemStatus.Stopped;
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
            SetPriority(app, slug, usable, CorpusCards(app));
        }

        // After the bundle is recorded, as the ticket carries it (and its priority).
        Persist(item, wasBlocked);
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
            var ready = items.Where(i => !IsWaiting(i)).OrderBy(i => i.Stage switch { PipelineStage.Triage => 0, PipelineStage.Build => 1, _ => 2 });
            foreach (var next in Orchestrator.Startable(ready))
            {
                var index = items.IndexOf(next);
                try
                {
                    Set(index, Begin(index, next));
                }
                catch (Exception e)
                {
                    // A spec or notes file that cannot be read stops this item; it must not fail the note or the run's report.
                    Set(index, Stop(next, $"the project could not be read ({e.GetType().Name})"));
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
                    PipelineStage.Context => ContextStage.BuildTask(item.Project, corpora.RelativePath(item.Project), bundle),
                    PipelineStage.Spec => SpecStage.BuildTask(item.Project, corpora.RelativePath(item.Project), bundle, scheduledSlugs.Contains((item.Project, item.Slug))),
                    PipelineStage.Build => BuildStage.BuildTask(item.Project, bundle),
                    PipelineStage.Merge => ResolveStage.BuildTask(item.Project, bundle, repoAt[(repoAt.LastIndexOf('@') + 1)..]),
                    PipelineStage.Test => TestStage.BuildTask(item.Project, bundle, repoAt[(repoAt.LastIndexOf('@') + 1)..], testing[(item.Project, item.Slug)]),
                    _ => ReviewStage.BuildTask(item.Project, bundle),
                };
            }
            catch (InvalidOperationException e)
            {
                return Stop(item, e.Message);
            }

            return Enqueue(index, item, repoAt, text, [], bundle);
        }

        // BEH-ORCH-16 slice 3: his replies on blocked tickets first, so a long run of notes cannot crowd them out.
        var replies = Replies(item.Project);
        var notes = replies.Select(r => (r.Ref, r.Text, r.Key, Card: (string?)null)).Concat(Untriaged(item.Project)).ToList();

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
            // A blocked ticket is left out of the list, so no note updates it, except one he has replied on: that reply is its
            // update. Listed first (priority 0), so the list's cap can never drop the ticket the reply names.
            var replied = replies.Select(r => r.Slug).ToHashSet(StringComparer.Ordinal);
            var tickets = OpenTickets(item.Project).Select(t => replied.Contains(t.Slug) ? t with { BlockedBecause = null, Priority = 0 } : t).ToList();
            task = TriageStage.BuildTask(item.Project, notes.Select(n => (n.Ref, TriageStage.OnCard(n.Card, n.Text))).ToList(), Cards(corpus), tickets, corpora.RelativePath(item.Project));
        }
        catch (InvalidOperationException e)
        {
            return Stop(item, e.Message);
        }

        // Worked out before the run is queued, so nothing can throw between queuing it and recording it.
        var taken = notes.Take(task.NotesTaken).Select(n => n.Key).ToList();
        return Enqueue(index, item, repoAt, task.Text, taken, null);
    }

    /// <summary>BEH-ORCH-11: <paramref name="app"/>'s tickets, for the triage task to list the open ones. A store that cannot be read gives none: the triage then runs as before.</summary>
    private IReadOnlyList<PipelineTicket> OpenTickets(string app)
    {
        try
        {
            // The priorities are the live ones, not the rows': a card may have been re-prioritised since the row was written.
            var cards = CorpusCards(app);
            return ticketStore.Read(app)
                .Select(t => t with { Priority = (cards is null ? null : CardPriority(t.Cards, cards)) ?? priorities.GetValueOrDefault((t.Project, t.Slug), t.Priority) })
                .ToList();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: reading {app}'s tickets for triage threw {e.GetType().Name}");
            return [];
        }
    }

    /// <summary>Hands a run of <paramref name="item"/>'s stage to the pool and records it; Stopped when the pool will not take it.</summary>
    private WorkItem Enqueue(int index, WorkItem item, string repoAt, string task, IReadOnlyList<string> notes, ContextBundle? given)
    {
        // The merge job's run is the one that resolves a conflict, and Runs shows it so.
        var stage = item.Stage == PipelineStage.Merge ? "resolve" : item.Stage.ToString().ToLowerInvariant();
        var at = repoAt.LastIndexOf('@');
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var token = AgentTokens.Mint();
        var run = runs.Create(owner, item.Project, Orchestrator.ModelFor(item.Stage, models, given?.Flag), AgentTokens.Hash(token), now,
            item.Stage == PipelineStage.Triage ? null : item.Slug, stage);
        try
        {
            pool.Enqueue(run, token, repoAt[..at], task, repoAt[(at + 1)..], Orchestrator.TurnsFor(item.Stage, turns ?? StageTurnSettings.Defaults), Orchestrator.SecondsFor(item.Stage));
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
    /// as (ref, text, key, card). A note's date is to the minute, so two left in one minute share a ref: the triaged
    /// set is keyed on the date AND the text, or the second would be counted read with the first. The card is the
    /// behaviour a note was left on, null for a note on the project: its text alone may not name it ("I think this is
    /// ready now", 2026-11-16 22:56Z on BEH-USERS-12, was set aside as naming no card).
    /// </summary>
    private List<(string Ref, string Text, string Key, string? Card)> Untriaged(string app)
    {
        var file = corpora.RelativeNotesPath(app);
        return Notes.Parse(corpora.ReadNotesText(app))
            .Where(n => n.Kind is Notes.NoteKind or Notes.RequestKind)
            .Select(n => (Ref: $"{file}#{n.At}", n.Text, Key: KeyOf(n.At, n.Text), Card: n.Behaviour))
            .Where(n => !Sorted(app).Contains(n.Key))
            .ToList();
    }

    /// <summary>
    /// BEH-ORCH-16 slice 3: his replies on <paramref name="app"/>'s blocked tickets that no finished triage has read, oldest
    /// first, each as a note for the triage task: (ticket slug, ref, text, key). Only a ticket the first agent can still
    /// change counts (new, ready or planned): a built one's block is a pull request's, which a spec change does not clear.
    /// A reply is keyed like a note, in the same store, so a failed triage reads it again and a finished one never does.
    /// A store that cannot be read gives none.
    /// </summary>
    private List<(string Slug, string Ref, string Text, string Key)> Replies(string app)
    {
        try
        {
            return ticketStore.Read(app)
                .Where(t => t.BlockedBecause is not null && t.State is TicketState.New or TicketState.Ready or TicketState.Planned)
                .SelectMany(t => ticketStore.Comments(app, t.Slug)
                    .Where(c => c.Author != KitAuthor)
                    .Select(c => (Ticket: t, Comment: c, Key: KeyOf($"ticket {t.Slug} {c.At:O}", c.Body))))
                .Where(r => !Sorted(app).Contains(r.Key))
                .OrderBy(r => r.Comment.At)
                .Select(r => (
                    r.Ticket.Slug,
                    $"ticket {r.Ticket.Slug}, reply of {r.Comment.At.UtcDateTime:yyyy-MM-dd HH:mm}Z",
                    // His words first, and the rest short: a note is cut at TriageStage.MaxNoteChars, and a reply's
                    // ref is no file the agent could read the cut part from (MaxReply keeps the whole within it).
                    $"The reply: {r.Comment.Body}\n"
                        + $"It is {r.Comment.Author}'s reply on ticket {r.Ticket.Slug} (\"{Short(r.Ticket.Title, 80)}\"), blocked because: {Short(r.Ticket.BlockedBecause!, 200)}\n"
                        + $"Report this note under THAT ticket's slug, {r.Ticket.Slug}, on its branch {TriageStage.Branch(r.Ticket.Slug)}: change the spec as the reply says and update the bundle. Add a Flag line only if a question is still open.",
                    r.Key))
                .ToList();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"kit: reading {app}'s ticket replies threw {e.GetType().Name}");
            return [];
        }
    }

    private static string Short(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    /// <summary>
    /// BEH-ORCH-16 slice 3: after a finished triage of <paramref name="app"/>, a reply it read whose ticket it reported no
    /// bundle for is still marked read, as every note in a finished triage is, so Kit says on the thread that nothing
    /// changed: otherwise his reply would sit, read and unanswered, under a ticket still blocked. Called before the run's
    /// notes are marked sorted, while the replies are still pending.
    /// </summary>
    private void SayUnchanged(string app, IReadOnlyList<string> read, IReadOnlySet<string> reported)
    {
        foreach (var slug in Replies(app).Where(r => read.Contains(r.Key) && !reported.Contains(r.Slug)).Select(r => r.Slug).Distinct())
        {
            try
            {
                ticketStore.Comment(new TicketComment(app, slug, KitAuthor, "Read your reply. The triage did not update this ticket, so it is still blocked: leave another reply, or a note, to try again.", (clock ?? TimeProvider.System).GetUtcNow()));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"kit: commenting on the ticket {app} {slug} threw {e.GetType().Name}");
            }
        }
    }

    /// <summary>The author of every comment Kit posts itself.</summary>
    public const string KitAuthor = "kit";

    /// <summary>What <see cref="Replied"/> did.</summary>
    public enum Reply
    {
        /// <summary>No ticket of that project has that slug.</summary>
        NoTicket,

        /// <summary>The comment is on the thread; the ticket is not blocked where the first agent can act, so nothing runs.</summary>
        Commented,

        /// <summary>The comment is on the thread, and a triage is queued to read it.</summary>
        Woke,

        /// <summary>The ticket store could not be read or written, so the reply is not saved.</summary>
        NotSaved,
    }

    /// <summary>
    /// BEH-ORCH-16 slice 3: <paramref name="author"/> replied <paramref name="body"/> on <paramref name="app"/>'s ticket
    /// <paramref name="slug"/>. The reply goes on its thread; on a blocked ticket the first agent can still change, it
    /// wakes a triage, as a note does, which reads it and updates the ticket. A project not in <c>projects</c> wakes nothing.
    /// </summary>
    public Reply Replied(string app, string slug, string author, string body)
    {
        lock (gate)
        {
            PipelineTicket? ticket;
            try
            {
                ticket = ticketStore.Read(app).FirstOrDefault(t => t.Slug == slug);
                if (ticket is null)
                {
                    return Reply.NoTicket;
                }

                ticketStore.Comment(new TicketComment(app, slug, author, body, (clock ?? TimeProvider.System).GetUtcNow()));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"kit: replying on the ticket {app} {slug} threw {e.GetType().Name}");
                return Reply.NotSaved;
            }
            if (ticket.BlockedBecause is null
                || ticket.State is not (TicketState.New or TicketState.Ready or TicketState.Planned)
                || (projects is not null && !projects.Contains(app)))
            {
                return Reply.Commented;
            }

            QueueTriage(app);
            StartReady();
            return Reply.Woke;
        }
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
