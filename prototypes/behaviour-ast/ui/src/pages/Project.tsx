import { Link, useParams } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Behaviour, Conflict, ProjectDetail } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import ResourceView from '../components/Resource'

/** Step 1 of his loop, one level down: the corpus, what conflicts, what nobody reviewed. */
export default function Project() {
  const { app = '' } = useParams()
  const project = useResource(() => fetchProject(app), [app])

  return (
    <>
      <p className="crumbs">
        <Link to="/">Projects</Link> / {app}
      </p>

      <ResourceView resource={project}>{(value) => <Detail project={value} />}</ResourceView>
    </>
  )
}

function Detail({ project }: { project: ProjectDetail }) {
  const unreviewed = new Set(project.adjudication.unreviewed)

  return (
    <>
      <h1>{project.app}</h1>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        <Badge tone="primary">{project.behaviours.length} behaviours</Badge>
        <CoverageBadge coverage={project.coverage} />
        <Badge tone="neutral">{project.adjudication.defined} defined</Badge>
        {project.adjudication.inferred ? (
          <Badge tone="warning">{project.adjudication.inferred} inferred</Badge>
        ) : null}
      </div>

      {project.conflicts.length > 0 && (
        <section>
          <h2>Conflicts</h2>
          <ul className="cards">
            {project.conflicts.map((c) => (
              <li key={c.key}>
                <ConflictCard conflict={c} />
              </li>
            ))}
          </ul>
        </section>
      )}

      <section>
        <h2>Behaviours</h2>
        <ul className="cards">
          {project.behaviours.map((b) => (
            <li key={b.id}>
              <BehaviourRow app={project.app} behaviour={b} unreviewed={unreviewed.has(b.id)} />
            </li>
          ))}
        </ul>
      </section>
    </>
  )
}

function ConflictCard({ conflict }: { conflict: Conflict }) {
  return (
    <Card elevation="flat">
      <h3>{conflict.key}</h3>
      <p>
        held at <strong>{conflict.held.join(', ')}</strong> by {conflict.holders.join(', ')}
      </p>
      <ul>
        {conflict.challengers.map((ch) => (
          <li key={`${ch.from}:${ch.at}`}>
            {ch.from} says <strong>{ch.value.join(', ')}</strong> <span className="muted">({ch.at})</span>
          </li>
        ))}
      </ul>
    </Card>
  )
}

function BehaviourRow({
  app,
  behaviour,
  unreviewed,
}: {
  app: string
  behaviour: Behaviour
  unreviewed: boolean
}) {
  return (
    <Card interactive>
      <h3>
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(behaviour.id)}`}>
          {behaviour.id}
        </Link>{' '}
        {behaviour.title}
      </h3>
      <div className="badges">
        <Badge tone="neutral">{behaviour.actor}</Badge>
        <Badge tone="neutral">{behaviour.steps.length} steps</Badge>
        {behaviour.source.origin === 'inferred' && <Badge tone="warning">inferred</Badge>}
        {unreviewed && <Badge tone="warning">unreviewed</Badge>}
      </div>
      <p className="muted">{behaviour.at}</p>
    </Card>
  )
}
