import { useId, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, askForBehaviour, fetchHistory, fetchNotes, fetchProject, fetchProjects, fetchSchedule } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Behaviour, ProjectSummary } from '../api/types'
import BoardViews from '../components/BoardViews'
import LayerChips from '../components/LayerChips'
import PublishedHint from '../components/PublishedHint'
import ResourceView from '../components/Resource'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'
import { byCompletion, byPriorityAcrossProjects, CAP, columnOf, hueOf, isIdea, isRung, isTrial, LAYERS, projectOf, RANKED, rungFirst, type Column, type Place } from '../board'

/** One project as the board needs it: its row, and either its behaviours or why it has none. */
interface Entry {
  summary: ProjectSummary
  behaviours: Behaviour[]
  /** Each scheduled id's place on this file's schedule (BEH-BOARD-3). */
  places: Map<string, Place>
  error: string | null
  /** The schedule alone failed: the behaviours still show, every pending one as backlog, and this says why. */
  scheduleError: string | null
  /** When each built behaviour was completed (BEH-BOARD-4): an ISO time, or null when built ahead of git. */
  completed: Map<string, string | null>
  /** When each behaviour was added (BEH-BOARD-12): an ISO time, or null when no commit holds it yet. */
  added: Map<string, string | null>
  /** Why there is no history: Done shows this project's cards after the dated ones, in corpus order. */
  historyMissing: string | null
  /** The items whose bell is rung and unanswered (BEH-BELL-3); empty when the notes could not be read. */
  bells: Set<string>
}

const messageOf = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : String(e))

/**
 * Every project's detail, schedule and history, fetched after the list. A project
 * that will not parse (the list says so) or whose detail fails keeps its place as
 * an error, so the board never shows fewer projects than Kit reads. A history that
 * fails costs Done its order, never a card.
 */
async function loadBoard(): Promise<Entry[]> {
  const { projects } = await fetchProjects()
  return Promise.all(
    projects.map(async (summary): Promise<Entry> => {
      const empty = {
        summary,
        behaviours: [],
        places: new Map<string, Place>(),
        scheduleError: null,
        completed: new Map<string, string | null>(),
        added: new Map<string, string | null>(),
        historyMissing: null,
        bells: new Set<string>(),
      }
      if (summary.error) return { ...empty, error: summary.error }
      const [detail, schedule, history, notes] = await Promise.allSettled([
        fetchProject(summary.app),
        fetchSchedule(summary.app),
        fetchHistory(summary.app),
        fetchNotes(summary.app),
      ])
      if (detail.status === 'rejected') return { ...empty, error: messageOf(detail.reason) }
      const places = new Map<string, Place>()
      if (schedule.status === 'fulfilled') {
        schedule.value.scheduled.forEach((s, order) => places.set(s.id, { order, next: s.next }))
      }
      const completed = new Map<string, string | null>()
      const added = new Map<string, string | null>()
      let historyMissing: string | null
      if (history.status === 'rejected') historyMissing = messageOf(history.reason)
      else if (history.value.history !== 'git') historyMissing = history.value.reason ?? 'no history'
      else {
        historyMissing = null
        history.value.completed.forEach((c) => completed.set(c.id, c.at))
        history.value.added.forEach((c) => added.set(c.id, c.at))
      }
      return {
        summary,
        behaviours: detail.value.behaviours,
        places,
        error: null,
        scheduleError: schedule.status === 'rejected' ? messageOf(schedule.reason) : null,
        completed,
        added,
        historyMissing,
        // Notes that fail cost the rung cards their lift, never a card.
        bells: new Set(
          notes.status === 'fulfilled' ? notes.value.notes.flatMap((n) => (n.kind === 'bell' && n.behaviour ? [n.behaviour] : [])) : [],
        ),
      }
    }),
  )
}

const COLUMNS: { id: Column; title: string }[] = [
  // First, beside Proposed: both are the agent waiting on him (kit#284).
  { id: 'review', title: 'Review requested' },
  { id: 'proposed', title: 'Proposed' },
  { id: 'backlog', title: 'Backlog' },
  { id: 'scheduled', title: 'Scheduled' },
  { id: 'in-progress', title: 'In progress' },
  { id: 'done', title: 'Done' },
]

/** The board (kit#237): what the corpora say about where every behaviour stands. */
export default function Board() {
  const entries = useResource(loadBoard, [])
  const [params, setParams] = useSearchParams()
  // How many cards each column shows (BEH-BOARD-7); a column not in here shows the first CAP.
  const [limits, setLimits] = useState<Partial<Record<Column, number>>>({})

  // The filter lives in the url so a view can be shared (BEH-BOARD-2).
  const selected = (params.get('project') ?? '').split(',').filter(Boolean)
  const showTrials = params.get('trials') === '1'
  // BEH-KIND-1 (his 2026-10-16 note): "filter the different types ui, ux and logic". The type is
  // the corpus's existing `layer` line, so the filter reads it rather than inventing a tag.
  const layers = (params.get('layer') ?? '').split(',').filter((l) => LAYERS.includes(l))

  function update(next: { project?: string[]; trials?: boolean; layer?: string[] }) {
    const project = next.project ?? selected
    const trials = next.trials ?? showTrials
    const layer = next.layer ?? layers
    const out = new URLSearchParams()
    if (project.length) out.set('project', project.join(','))
    if (layer.length) out.set('layer', layer.join(','))
    if (trials) out.set('trials', '1')
    setParams(out)
  }

  function toggleLayer(layer: string) {
    update({ layer: layers.includes(layer) ? layers.filter((l) => l !== layer) : [...layers, layer] })
  }

  function toggle(app: string) {
    update({ project: selected.includes(app) ? selected.filter((a) => a !== app) : [...selected, app] })
  }

  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every behaviour by where it stands: an inference nobody has adjudicated is proposed; a
        pending one is backlog until it is scheduled, and in progress at the top of its schedule;
        the rest is done, the most recently completed first. A card whose bell is rung sits at the
        top of its column until Kit answers it.
      </p>

      <ResourceView resource={entries}>
        {(all) => {
          // Ideas are never work, so they are off the board whatever the filter (kit#284 slice 4).
          const shown = all.filter(
            (e) =>
              !isIdea(e.summary) &&
              (showTrials || !isTrial(e.summary)) &&
              (selected.length === 0 || selected.includes(projectOf(e.summary))),
          )
          // One button per project, not per file: files naming one `# kit:project` share it (kit#237).
          const projects = [
            ...new Set(all.filter((e) => !isIdea(e.summary) && (showTrials || !isTrial(e.summary))).map((e) => projectOf(e.summary))),
          ]
          const failed = shown.filter((e) => e.error)
          return (
            <>
              <BoardAsk targets={targetsOf(all)} />

              <div className="board-controls">
                {projects.map((p) => (
                  <Button
                    key={p}
                    type="button"
                    size="sm"
                    variant={selected.includes(p) ? 'secondary' : 'ghost'}
                    aria-pressed={selected.includes(p)}
                    aria-label={`Filter to ${p}`}
                    onClick={() => toggle(p)}
                  >
                    <span className={`dot hue-${hueOf(p)}`} aria-hidden="true" />
                    {p}
                  </Button>
                ))}
                <Button
                  type="button"
                  size="sm"
                  variant={showTrials ? 'secondary' : 'ghost'}
                  aria-pressed={showTrials}
                  onClick={() => update({ trials: !showTrials })}
                >
                  Show trials
                </Button>
              </div>
              <LayerChips layers={layers} onToggle={toggleLayer} />

              {failed.map((e) => (
                <Card key={e.summary.app} elevation="flat">
                  <h2>{e.summary.app}</h2>
                  <Badge tone="danger">could not be read</Badge>
                  <p role="alert">{e.error}</p>
                </Card>
              ))}
              {shown
                .filter((e) => e.scheduleError)
                .map((e) => (
                  <Card key={`${e.summary.app}/schedule`} elevation="flat">
                    <h2>{e.summary.app}</h2>
                    <Badge tone="warning">schedule could not be read — its pending behaviours show as backlog</Badge>
                    <p role="alert">{e.scheduleError}</p>
                  </Card>
                ))}

              <div className="board">
                {COLUMNS.map((col) => {
                  // Scheduled reads in schedule order within each file; Done most recently completed
                  // first, across files (BEH-BOARD-4); Review requested, Proposed and Backlog by priority,
                  // across files (BEH-BOARD-5/6), newest first within a priority (BEH-BOARD-12); in
                  // progress in corpus order.
                  let cards = shown.flatMap((e) =>
                    e.behaviours
                      .filter((b) => columnOf(b, e.places.get(b.id)) === col.id && (layers.length === 0 || layers.includes(b.layer)))
                      .sort((a, b) => (col.id === 'scheduled' ? e.places.get(a.id)!.order - e.places.get(b.id)!.order : 0))
                      .map((b) => ({
                        app: e.summary.app,
                        project: projectOf(e.summary),
                        b,
                        at: e.historyMissing === null ? (col.id === 'done' ? e.completed : e.added).get(b.id) : undefined,
                        rung: isRung(b, e.bells),
                      })),
                  )
                  if (col.id === 'done') cards.sort((x, y) => byCompletion(x.at, y.at))
                  // Rung cards above every priority (BEH-BELL-3), until the agent answers the bell.
                  if (RANKED.includes(col.id)) cards = rungFirst(byPriorityAcrossProjects(cards))
                  const unordered = col.id === 'done' ? shown.filter((e) => !e.error && e.historyMissing !== null) : []
                  // Capped after sorting, so what is cut is always the bottom of the order (BEH-BOARD-7).
                  const limit = limits[col.id] ?? CAP
                  const hidden = Math.max(0, cards.length - limit)
                  return (
                    <section key={col.id} aria-labelledby={`board-${col.id}`}>
                      <h2>
                        <span id={`board-${col.id}`}>{col.title}</span> <span className="board-count">{cards.length}</span>
                      </h2>
                      {unordered.length > 0 && (
                        // Said, not silent: without a history these cards are in corpus order, not by date.
                        <p className="muted board-note">
                          Not ordered by date: {unordered.map((e) => `${e.summary.app} (${e.historyMissing})`).join('; ')}
                        </p>
                      )}
                      <ul className="cards">
                        {cards.slice(0, limit).map(({ app, project, b, rung }) => (
                          <li key={`${app}/${b.id}`}>
                            <Card className={`board-card hue-${hueOf(project)}`}>
                              <h3>
                                <Link
                                  to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}
                                >
                                  {b.title}
                                </Link>{' '}
                                <span className="muted id">{b.id}</span>
                              </h3>
                              {b.review.state === 'requested' && (
                                // What the agent wants looked at, so the card can be triaged unopened.
                                <p>{b.review.note}</p>
                              )}
                              <div className="badges">
                                {rung && <Badge tone="warning">🔔 rung</Badge>}
                                {b.priority !== undefined && (
                                  <Badge tone={b.priority === 1 ? 'danger' : 'neutral'}>P{b.priority}</Badge>
                                )}
                                <Badge tone="neutral">{b.layer}</Badge>
                              </div>
                              <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
                            </Card>
                          </li>
                        ))}
                      </ul>
                      {hidden > 0 && (
                        <Button
                          type="button"
                          variant="secondary"
                          onClick={() => setLimits({ ...limits, [col.id]: limit + CAP })}
                        >
                          Show {Math.min(CAP, hidden)} more ({hidden} hidden)
                        </Button>
                      )}
                    </section>
                  )
                })}
              </div>
            </>
          )
        }}
      </ResourceView>
    </>
  )
}

/**
 * Where a request for each project goes: the notes file named like the project, which is the one
 * its page shows (BEH-FEAT-5), else its first file. Ideas, trials and unreadable files are not
 * offered: a request is work, and none of those is.
 */
function targetsOf(all: Entry[]): { project: string; app: string }[] {
  const out = new Map<string, string>()
  for (const e of all) {
    if (e.error || isIdea(e.summary) || isTrial(e.summary)) continue
    const project = projectOf(e.summary)
    if (!out.has(project) || e.summary.app === project) out.set(project, e.summary.app)
  }
  return [...out].map(([project, app]) => ({ project, app })).sort((a, b) => a.project.localeCompare(b.project))
}

/**
 * BEH-BOARD-8 (his note 2026-10-16 07:30Z): "Boards could probably have a free form text input …
 * Maybe you can select a project before you submit or if you submit without selecting a project a
 * ui comes up and says which project is this for." It writes the same request note as a project
 * page's describe box (BEH-NOTE-4). With no project picked it asks, and writes nothing.
 */
/**
 * BEH-BOARD-9: the projects the text names, as whole words in any case, offered one tap each.
 * Only a listed project can be suggested, so a free-typed name is never written as one.
 */
function mentioned<T extends { project: string }>(text: string, targets: T[]): T[] {
  const words = new Set(text.toLowerCase().split(/[^a-z0-9-]+/).filter(Boolean))
  return targets.filter((t) => words.has(t.project.toLowerCase()))
}

function BoardAsk({ targets }: { targets: { project: string; app: string }[] }) {
  const [text, setText] = useState('')
  const [project, setProject] = useState('')
  const [asking, setAsking] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)
  const picker = useRef<HTMLSelectElement>(null)
  const { write, run } = useWrite(() => undefined)
  const id = useId()

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    const target = targets.find((t) => t.project === project)
    if (!target) {
      setAsking(true)
      picker.current?.focus()
      return
    }
    setAsking(false)
    await run(async () => {
      const result = await askForBehaviour(target.app, text)
      setText('')
      setSentTo(target.project)
      return result
    })
  }

  return (
    <form onSubmit={submit} className="write">
      <label htmlFor={`${id}-text`}>Describe what should happen</label>
      <textarea
        id={`${id}-text`}
        className="note-text"
        placeholder="e.g. the board remembers the filter I last used"
        aria-describedby={`${id}-published`}
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <PublishedHint id={`${id}-published`} />
      <label htmlFor={`${id}-project`}>Project</label>
      <select
        id={`${id}-project`}
        className="note-select"
        ref={picker}
        value={project}
        aria-invalid={asking || undefined}
        onChange={(e) => {
          setProject(e.target.value)
          setAsking(false)
        }}
      >
        <option value="">Pick a project…</option>
        {targets.map((t) => (
          <option key={t.project} value={t.project}>
            {t.project}
          </option>
        ))}
      </select>
      {!project &&
        mentioned(text, targets).map((t) => (
          <Button
            key={t.project}
            type="button"
            size="sm"
            variant="ghost"
            onClick={() => {
              setProject(t.project)
              setAsking(false)
            }}
          >
            For {t.project}?
          </Button>
        ))}
      {asking && <p role="alert">Which project is this for? Pick one, then send it again.</p>}
      <Button type="submit" disabled={write.state === 'saving' || text.trim() === ''}>
        {write.state === 'saving' ? 'Sending…' : 'Ask for behaviour'}
      </Button>
      <WriteFeedback write={write} />
      {write.state === 'wrote' && sentTo && (
        <p>
          <Link to={`/projects/${encodeURIComponent(sentTo)}?tab=notes`}>See it in {sentTo}'s notes</Link>
        </p>
      )}
    </form>
  )
}
