import { Link, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, fetchProject, fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Behaviour, ProjectSummary } from '../api/types'
import ResourceView from '../components/Resource'
import { columnOf, hueOf, isTrial, projectOf, type Column } from '../board'

/** One project as the board needs it: its row, and either its behaviours or why it has none. */
interface Entry {
  summary: ProjectSummary
  behaviours: Behaviour[]
  error: string | null
}

/**
 * Every project's detail, fetched after the list. A project that will not parse
 * (the list says so) or whose detail fails keeps its place as an error, so the
 * board never shows fewer projects than Kit reads.
 */
async function loadBoard(): Promise<Entry[]> {
  const { projects } = await fetchProjects()
  return Promise.all(
    projects.map(async (summary): Promise<Entry> => {
      if (summary.error) return { summary, behaviours: [], error: summary.error }
      try {
        const detail = await fetchProject(summary.app)
        return { summary, behaviours: detail.behaviours, error: null }
      } catch (e) {
        const message = e instanceof ApiError || e instanceof Error ? e.message : String(e)
        return { summary, behaviours: [], error: message }
      }
    }),
  )
}

const COLUMNS: { id: Column; title: string }[] = [
  { id: 'proposed', title: 'Proposed' },
  { id: 'backlog', title: 'Backlog' },
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
        Every behaviour by where it stands: an inference nobody has adjudicated is proposed, a
        pending one is backlog, the rest is done.
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

              <div className="board">
                {COLUMNS.map((col) => {
                  const cards = shown.flatMap((e) =>
                    e.behaviours
                      .filter((b) => columnOf(b) === col.id)
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
