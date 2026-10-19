import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import BoardViews from '../components/BoardViews'
import { hueOf } from '../board'
import { epicCards } from '../epics'
import { featureHref } from '../features'
import { loadRealProjects } from '../realProjects'

/** Every real project's epics, with what is built beneath the features in each. */
async function loadEpics() {
  const { read, failed } = await loadRealProjects()
  const cards = read.flatMap(({ app, project, detail }) =>
    epicCards(app, project, detail.epics ?? [], detail.features ?? [], detail.behaviours),
  )
  return { cards, failed }
}

/**
 * BEH-EPIC-4 (his 2026-10-19 16:44Z note): "just another abstraction level in the boards for epic",
 * above the feature view. Each epic shows how much of what is tagged into it is built.
 */
export default function Epics() {
  const loaded = useResource(loadEpics, [])
  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every epic, with how many behaviours are built beneath the features tagged into it. A feature
        joins an epic with an <code>in EPIC-…</code> line.
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
            <div role="region" aria-label="Epics">
              {cards.length === 0 && <p className="muted">No project declares an epic yet.</p>}
              <ul className="cards">
                {cards.map(({ app, project, epic, features, built, total }) => (
                  <li key={`${app}/${epic.id}`}>
                    <Card className={`board-card hue-${hueOf(project)}`}>
                      <h3>
                        {epic.title} <span className="muted id">{epic.id}</span>
                      </h3>
                      <p>
                        {built} of {total} behaviours built
                      </p>
                      {features.length === 0 ? (
                        <p className="muted">No feature is in it yet.</p>
                      ) : (
                        <ul aria-label={`Features in ${epic.id}`}>
                          {features.map((f) => (
                            <li key={f.id}>
                              <Link to={featureHref(app, f.id, [])}>{f.title}</Link>
                            </li>
                          ))}
                        </ul>
                      )}
                      <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
                    </Card>
                  </li>
                ))}
              </ul>
            </div>
          </>
        )}
      </ResourceView>
    </>
  )
}
