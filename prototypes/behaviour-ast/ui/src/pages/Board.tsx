import { Link, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, fetchProject, fetchProjects, fetchSchedule } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Behaviour, ProjectSummary } from '../api/types'
import ResourceView from '../components/Resource'
import { columnOf, hueOf, isTrial, projectOf, type Column, type Place } from '../board'

/** One project as the board needs it: its row, and either its behaviours or why it has none. */
interface Entry {
  summary: ProjectSummary
  behaviours: Behaviour[]
  /** Each scheduled id's place on this file's schedule (BEH-BOARD-3). */
  places: Map<string, Place>
  error: string | null
  /** The schedule alone failed: the behaviours still show, every pending one as backlog, and this says why. */
  scheduleError: string | null
}

const messageOf = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : String(e))

/**
 * Every project's detail and schedule, fetched after the list. A project that will
 * not parse (the list says so) or whose detail fails keeps its place as an error,
 * so the board never shows fewer projects than Kit reads.
 */
async function loadBoard(): Promise<Entry[]> {
  const { projects } = await fetchProjects()
  return Promise.all(
    projects.map(async (summary): Promise<Entry> => {
      const empty = { summary, behaviours: [], places: new Map<string, Place>(), scheduleError: null }
      if (summary.error) return { ...empty, error: summary.error }
      const [detail, schedule] = await Promise.allSettled([fetchProject(summary.app), fetchSchedule(summary.app)])
      if (detail.status === 'rejected') return { ...empty, error: messageOf(detail.reason) }
      const places = new Map<string, Place>()
      if (schedule.status === 'fulfilled') {
        schedule.value.scheduled.forEach((s, order) => places.set(s.id, { order, next: s.next }))
      }
      return {
        summary,
        behaviours: detail.value.behaviours,
        places,
        error: null,
        scheduleError: schedule.status === 'rejected' ? messageOf(schedule.reason) : null,
      }
    }),
  )
}

const COLUMNS: { id: Column; title: string }[] = [
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

  // The filter lives in the url so a view can be shared (BEH-BOARD-2).
  const selected = (params.get('project') ?? '').split(',').filter(Boolean)
  const showTrials = params.get('trials') === '1'

  function update(next: { project?: string[]; trials?: boolean }) {
    const project = next.project ?? selected
    const trials = next.trials ?? showTrials
    const out = new URLSearchParams()
    if (project.length) out.set('project', project.join(','))
    if (trials) out.set('trials', '1')
    setParams(out)
  }

  function toggle(app: string) {
    update({ project: selected.includes(app) ? selected.filter((a) => a !== app) : [...selected, app] })
  }

  return (
    <>
      <h1>Board</h1>
      <p className="muted">
        Every behaviour by where it stands: an inference nobody has adjudicated is proposed; a
        pending one is backlog until it is scheduled, and in progress at the top of its schedule;
        the rest is done.
      </p>

      <ResourceView resource={entries}>
        {(all) => {
          const shown = all.filter(
            (e) =>
              (showTrials || !isTrial(e.summary)) &&
              (selected.length === 0 || selected.includes(projectOf(e.summary))),
          )
          // One button per project, not per file: files naming one `# kit:project` share it (kit#237).
          const projects = [...new Set(all.filter((e) => showTrials || !isTrial(e.summary)).map((e) => projectOf(e.summary)))]
          const failed = shown.filter((e) => e.error)
          return (
            <>
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
                  // Scheduled reads in schedule order within each file; every other column in corpus order.
                  const cards = shown.flatMap((e) =>
                    e.behaviours
                      .filter((b) => columnOf(b, e.places.get(b.id)) === col.id)
                      .sort((a, b) => (col.id === 'scheduled' ? e.places.get(a.id)!.order - e.places.get(b.id)!.order : 0))
                      .map((b) => ({ app: e.summary.app, project: projectOf(e.summary), b })),
                  )
                  return (
                    <section key={col.id} aria-labelledby={`board-${col.id}`}>
                      <h2>
                        <span id={`board-${col.id}`}>{col.title}</span> <span className="board-count">{cards.length}</span>
                      </h2>
                      <ul className="cards">
                        {cards.map(({ app, project, b }) => (
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
                              <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
                            </Card>
                          </li>
                        ))}
                      </ul>
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
