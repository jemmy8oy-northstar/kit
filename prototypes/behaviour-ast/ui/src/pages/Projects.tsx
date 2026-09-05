import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectSummary } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import Count from '../components/Count'
import ResourceView from '../components/Resource'

/** Step 1 of his loop: see the projects Kit knows about. */
export default function Projects() {
  const projects = useResource(fetchProjects, [])

  return (
    <>
      <h1>Projects</h1>
      <p className="muted">
        Every behaviour corpus Kit can read. Counts come from the corpus itself;
        coverage needs the app's repository, which the read API is not given by
        default.
      </p>

      <ResourceView resource={projects}>
        {({ projects: rows }) => (
          <ul className="cards">
            {rows.map((p) => (
              <li key={p.app}>
                <ProjectRow project={p} />
              </li>
            ))}
          </ul>
        )}
      </ResourceView>
    </>
  )
}

function ProjectRow({ project }: { project: ProjectSummary }) {
  // A corpus that will not parse is a state of the world, not a project with
  // nothing in it. ui.js reports it as an error and the row says so, rather
  // than showing a plausible-looking "0 behaviours".
  if (project.error) {
    return (
      <Card elevation="flat">
        <h2>{project.app}</h2>
        <Badge tone="danger">corpus will not parse</Badge>
        <p role="alert">{project.error}</p>
      </Card>
    )
  }

  return (
    <Card interactive>
      <h2>
        <Link to={`/projects/${encodeURIComponent(project.app)}`}>{project.app}</Link>
      </h2>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        <Count n={project.behaviours ?? 0} one="behaviour" tone="primary" />
        <CoverageBadge coverage={project.coverage} />
        {project.conflicts ? (
          <Count n={project.conflicts} one="conflict" tone="warning" />
        ) : null}
        {project.unreviewed ? (
          <Badge tone="warning">{project.unreviewed} unreviewed</Badge>
        ) : null}
      </div>
    </Card>
  )
}
