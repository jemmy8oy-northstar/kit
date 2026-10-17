import { fetchProject, fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Principle } from '../api/types'
import ResourceView from '../components/Resource'
import PrinciplesList from '../components/PrinciplesList'
import { isIdea } from '../board'

/** Every corpus's principles: `global` ones first, then each project's own (kit#284 slice 5). */
async function fetchAllPrinciples(): Promise<{ global: Principle[]; projects: { app: string; principles: Principle[] }[] }> {
  const { projects } = await fetchProjects()
  const views = await Promise.all(projects.filter((p) => !isIdea(p)).map((p) => fetchProject(p.app)))
  const all = views.map((v) => ({ app: v.app, principles: v.principles ?? [] }))

  return {
    global: all.flatMap((v) => v.principles.filter((p) => p.global)),
    projects: all
      .map((v) => ({ app: v.app, principles: v.principles.filter((p) => !p.global) }))
      .filter((v) => v.principles.length > 0),
  }
}

export default function Principles() {
  const resource = useResource(fetchAllPrinciples, [])

  return (
    <>
      <h1>Principles</h1>
      <p className="muted">
        How the agent decides when you are not there. Each lives as a <code># kit:principle</code> line in a
        corpus; leave a note on that project to change one.
      </p>

      <ResourceView resource={resource}>
        {({ global, projects }) =>
          global.length === 0 && projects.length === 0 ? (
            <p>No principles are written down yet.</p>
          ) : (
            <>
              <PrinciplesList principles={global} label="Global" badge={false} />
              {projects.map((p) => (
                <PrinciplesList key={p.app} principles={p.principles} label={p.app} />
              ))}
            </>
          )
        }
      </ResourceView>
    </>
  )
}
