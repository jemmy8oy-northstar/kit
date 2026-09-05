import { Link, useParams } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { Generated, ProjectDetail, Step } from '../api/types'
import ResourceView from '../components/Resource'

/**
 * Step 2 of his loop: "iterating on the output" — the behaviour and the test
 * Kit generates from it, side by side, so the two can be compared without
 * running anything.
 */
export default function BehaviourPage() {
  const { app = '', id = '' } = useParams()
  const project = useResource(() => fetchProject(app), [app])

  return (
    <>
      <p className="crumbs">
        <Link to="/">Projects</Link> / <Link to={`/projects/${encodeURIComponent(app)}`}>{app}</Link> / {id}
      </p>

      <ResourceView resource={project}>
        {(value) => <Detail project={value} id={id} />}
      </ResourceView>
    </>
  )
}

function Detail({ project, id }: { project: ProjectDetail; id: string }) {
  const behaviour = project.behaviours.find((b) => b.id === id)

  // A behaviour id that is not in the corpus is a wrong URL, not an empty
  // behaviour. Say which, because the two are fixed differently.
  if (!behaviour) {
    return (
      <Card elevation="flat">
        <h1>No such behaviour</h1>
        <p role="alert">
          {project.app} has no behaviour called {id}.
        </p>
      </Card>
    )
  }

  const generated = project.generated.find((g) => g.id === id)

  return (
    <>
      <h1>{behaviour.title}</h1>
      <div className="badges">
        <Badge tone="primary">{behaviour.id}</Badge>
        <Badge tone="neutral">{behaviour.actor}</Badge>
        <Badge tone={behaviour.review.state === 'approved' ? 'success' : 'warning'}>
          {behaviour.review.state}
        </Badge>
        <Badge tone="neutral">{behaviour.source.origin}</Badge>
      </div>
      <p className="muted">{behaviour.at}</p>

      <div className="split">
        <section>
          <h2>Behaviour</h2>
          <ol className="steps">
            {behaviour.steps.map((s, i) => (
              <li key={`${s.kind}-${i}`}>
                <StepLine step={s} />
              </li>
            ))}
          </ol>
        </section>

        <section>
          <h2>Generated test</h2>
          <GeneratedPane generated={generated} />
        </section>
      </div>
    </>
  )
}

function StepLine({ step }: { step: Step }) {
  return (
    <>
      <span className="kind">{step.kind}</span> <code>{step.text}</code>
      {step.holes.length > 0 && (
        <span className="badges">
          <Badge tone="warning">{step.holes.length} unknown</Badge>
        </span>
      )}
    </>
  )
}

function GeneratedPane({ generated }: { generated?: Generated }) {
  // Nothing generated at all and "Kit generated an empty test" are different
  // facts; only the first is worth acting on and it is the one the read API
  // signals by omitting the entry.
  if (!generated) {
    return <p role="alert">Kit generated nothing for this behaviour.</p>
  }

  return (
    <>
      <div className="badges">
        <Badge tone="success">{generated.stats.generated} generated</Badge>
        {generated.stats.contract ? (
          <Badge tone="neutral">{generated.stats.contract} contract</Badge>
        ) : null}
        {generated.stats.ungenerated ? (
          <Badge tone="warning">{generated.stats.ungenerated} ungenerated</Badge>
        ) : null}
      </div>

      {generated.missing.length > 0 && (
        <p className="muted">
          Unbound nouns: {generated.missing.join(', ')} — these are why the steps
          above became comments.
        </p>
      )}

      <pre>
        <code>{generated.code}</code>
      </pre>
    </>
  )
}
