import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import BoardViews from '../components/BoardViews'
import { hueOf } from '../board'
import { featureHref } from '../features'
import { loadRealProjects } from '../realProjects'
import { COLUMNS, columnFor, stageCards } from '../stages'

/** Every real project's features, with what is built beneath each. */
async function loadStages() {
  const { read, failed } = await loadRealProjects()
  const cards = read.flatMap(({ app, project, detail }) => stageCards(app, project, detail.features ?? [], detail.behaviours))
  return { cards, failed }
}

/**
 * FEAT-STAGES (his 2026-10-19 08:23Z note): every feature by the stage it is at, separate from the
 * behaviour board for now (BEH-STAGE-1). A feature with no `stage` line is in the Unstaged column (BEH-STAGE-7).
 */
export default function Stages() {
  const loaded = useResource(loadStages, [])
  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every feature by the stage it is at: problem statement, UX requirements, business logic,
        development, then UI requirements; once everything under it is built it moves to Done. A feature is staged once its block in the corpus has a
        <code> stage</code> line; until then it is Unstaged.
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
              {COLUMNS.map((stage) => {
                const here = cards.filter((c) => columnFor(c) === stage.id)
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
