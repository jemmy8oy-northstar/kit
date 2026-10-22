import { Link, useParams } from 'react-router-dom'
import { fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectDetail } from '../api/types'
import BellButton from '../components/BellButton'
import NotesPanel from '../components/NotesPanel'
import ResourceView from '../components/Resource'
import { epicCards } from '../epics'
import { featureHref } from '../features'

/** One epic (BEH-EPIC-8): its progress as the epic board's card counts it, and every feature tagged into it. */
export default function EpicPage() {
  const { app = '', id = '' } = useParams()
  const resource = useResource(() => fetchProject(app), [app])

  return (
    <ResourceView resource={resource}>
      {(project) => <Detail project={project} id={id} />}
    </ResourceView>
  )
}

function Detail({ project, id }: { project: ProjectDetail; id: string }) {
  const app = project.app
  // The same function the board card uses, so the page and the card can never count differently.
  const card = epicCards(app, project.project, project.epics ?? [], project.features, project.behaviours).find(
    (c) => c.epic.id === id,
  )
  const crumbs = (
    <nav aria-label="Breadcrumb" className="crumbs">
      <Link to="/">Projects</Link> / <Link to={`/projects/${encodeURIComponent(app)}`}>{app}</Link>
      {card && <> / {card.epic.title}</>}
    </nav>
  )

  if (!card) {
    return (
      <>
        {crumbs}
        <p role="alert">No epic {id} in {app}.</p>
      </>
    )
  }

  return (
    <>
      {crumbs}
      <h1>{card.epic.title}</h1>
      <p className="muted">
        {card.epic.id} · {card.project === app ? app : `${card.project} · ${app}`}
      </p>
      <BellButton app={app} item={card.epic.id} />
      <p>
        {card.built} of {card.total} behaviours built
      </p>
      <section aria-label="Features">
        {card.features.length === 0 ? (
          <p className="muted">No feature is in it yet.</p>
        ) : (
          <ul className="cards">
            {card.features.map((f) => (
              <li key={f.id}>
                <Link to={featureHref(app, f.id, [])}>{f.title}</Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* BEH-COMMENT-1: an epic carries its own comments, which are the notes left on it. */}
      <NotesPanel app={app} behaviour={card.epic.id} />
    </>
  )
}
