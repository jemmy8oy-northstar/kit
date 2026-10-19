import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, fetchProject, fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import BoardViews from '../components/BoardViews'
import { hueOf, isIdea, isTrial, projectOf } from '../board'
import { featureHref } from '../features'
import { STAGES, stageCards, type StageCard } from '../stages'

interface Loaded {
  cards: StageCard[]
  /** Files that could not be read, by name, so the board never shows less than Kit reads without saying so. */
  failed: { app: string; error: string }[]
}

const messageOf = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : String(e))

/** Every real project's features, with what is built beneath each. Ideas and trials are not work, so not here. */
async function loadStages(): Promise<Loaded> {
  const { projects } = await fetchProjects()
  const wanted = projects.filter((p) => !isIdea(p) && !isTrial(p))
  const failed = wanted.filter((p) => p.error).map((p) => ({ app: p.app, error: p.error! }))
  const readable = wanted.filter((p) => !p.error)
  const details = await Promise.allSettled(readable.map((p) => fetchProject(p.app)))
  const cards: StageCard[] = []
  details.forEach((r, i) => {
    const p = readable[i]
    if (r.status === 'fulfilled') cards.push(...stageCards(p.app, projectOf(p), r.value.features ?? [], r.value.behaviours))
    else failed.push({ app: p.app, error: messageOf(r.reason) })
  })
  return { cards, failed }
}

/**
 * FEAT-STAGES (his 2026-10-19 08:23Z note): every feature by the stage it is at, separate from the
 * behaviour board for now (BEH-STAGE-1). A feature is on it when its corpus block has a `stage` line.
 */
export default function Stages() {
  const loaded = useResource(loadStages, [])
  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every feature by the stage it is at: problem statement, UX requirements, business logic,
        development, then UI requirements. A feature is here once its block in the corpus has a
        <code> stage</code> line.
      </p>
      <ResourceView resource={loaded}>
        {({ cards, failed }) => (
          <>
            {failed.map((f) => (
              <Card key={f.app} elevation="flat">
                <h2>{f.app}</h2>
                <Badge tone="danger">could not be read</Badge>
                <p role="alert">{f.error}</p>
              </Card>
            ))}
            <div className="board" role="region" aria-label="Feature stages">
              {STAGES.map((stage) => {
                const here = cards.filter((c) => c.feature.stage === stage.id)
                return (
                  <section key={stage.id} aria-labelledby={`stage-${stage.id}`}>
                    <h2>
                      <span id={`stage-${stage.id}`}>{stage.title}</span> <span className="board-count">{here.length}</span>
                    </h2>
                    <ul className="cards">
                      {here.map(({ app, project, feature, built, total }) => (
                        <li key={`${app}/${feature.id}`}>
                          <Card className={`board-card hue-${hueOf(project)}`}>
                            <h3>
                              <Link to={featureHref(app, feature.id, [])}>{feature.title}</Link>{' '}
                              <span className="muted id">{feature.id}</span>
                            </h3>
                            {stage.id === 'development' && (
                              // BEH-STAGE-3: development runs through the spec, so its progress is the spec's.
                              <p>
                                {built} of {total} behaviours built
                              </p>
                            )}
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
        )}
      </ResourceView>
    </>
  )
}
