import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectSummary } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import Count from '../components/Count'
import ResourceView from '../components/Resource'
import { byProject } from '../features'

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
          <ul className="cards" aria-label="Project list">
            {byProject(rows).map((g) =>
              // BEH-FEAT-4: one file that is its own project reads as it always did.
              g.files.length === 1 && g.files[0].app === g.project ? (
                <li key={g.project}>
                  <ProjectRow project={g.files[0]} />
                </li>
              ) : (
                <li key={g.project}>
                  <ProjectGroup project={g.project} files={g.files} />
                </li>
              ),
            )}
          </ul>
        )}
      </ResourceView>
    </>
  )
}

/**
 * Files that name one project with `# kit:project` (kit#237): one card, and each file is one of
 * its top-level features. The files stay separate underneath, so each keeps its own counts.
 */
function ProjectGroup({ project, files }: { project: string; files: ProjectSummary[] }) {
  const total = files.reduce((n, f) => n + (f.behaviours ?? 0), 0)
  return (
    <Card elevation="flat">
      <h2>{project}</h2>
      <div className="badges">
        <Count n={total} one="behaviour" tone="primary" />
        <Count n={files.length} one="file" />
      </div>
      <ul className="cards">
        {files.map((p) => (
          <li key={p.app}>
            <ProjectRow project={p} nested />
          </li>
        ))}
      </ul>
    </Card>
  )
}

function ProjectRow({ project, nested = false }: { project: ProjectSummary; nested?: boolean }) {
  // Inside a project group the group has the h2, so each file is a level below it.
  const H = nested ? 'h3' : 'h2'
  // A corpus that will not parse is a state of the world, not a project with
  // nothing in it. ui.js reports it as an error and the row says so, rather
  // than showing a plausible-looking "0 behaviours".
  if (project.error) {
    return (
      <Card elevation="flat">
        <H>{project.app}</H>
        <Badge tone="danger">corpus will not parse</Badge>
        <p role="alert">{project.error}</p>
      </Card>
    )
  }

  return (
    <Card interactive>
      <H>
        <Link to={`/projects/${encodeURIComponent(project.app)}`}>{project.app}</Link>
      </H>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        {/* A trial corpus describes an app that does not exist. It belongs in
            the list — hiding it would make the list lie about what corpora Kit
            reads — but a 0% score against a real app and a 0% score against
            nothing mean opposite things, so the row has to say which. `neutral`
            because this is a fact about the corpus, not an alarm. */}
        {project.notReal ? (
          <Badge tone="neutral" title="This corpus describes an app that does not exist — a trial, not a project">
            trial — no app
          </Badge>
        ) : null}
        {/* Same reasoning, different axis. This corpus describes an app that
            DOES exist and is already in the list under its own corpus, so
            `notReal` is false of it and would be a lie. Without a marker the
            trial and its subject read as two equal projects. Neutral for the
            same reason: a fact about the corpus, not an alarm. */}
        {project.duplicateOf ? (
          <Badge
            tone="neutral"
            title={`A second corpus for ${project.duplicateOf}, written forwards from a brief — not a separate project`}
          >
            trial — spec for {project.duplicateOf}
          </Badge>
        ) : null}
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
