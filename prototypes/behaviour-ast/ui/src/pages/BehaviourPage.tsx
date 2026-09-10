import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Badge, Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { addStep, fetchProject } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Generated, ProjectDetail, Step } from '../api/types'
import ResourceView from '../components/Resource'
import Count from '../components/Count'
import WriteResultNote from '../components/WriteResultNote'
import { useWrite } from '../components/useWrite'

/**
 * Steps 2 and 3 of his loop, on one page, which is the point: "iterating on the
 * output" is the behaviour and its generated test side by side, and "creating
 * new assertions based on what I see" is being able to add a step *while looking
 * at them* and watch the test change.
 *
 * Separating those two into different screens would break the loop he described
 * — the whole value is that the output is in view when you decide what to assert.
 */
export default function BehaviourPage() {
  const { app = '', id = '' } = useParams()
  const { resource, reload } = useReloadableResource(() => fetchProject(app), [app])

  return (
    <>
      <p className="crumbs">
        <Link to="/">Projects</Link> / <Link to={`/projects/${encodeURIComponent(app)}`}>{app}</Link> / {id}
      </p>

      <ResourceView resource={resource}>
        {(value) => <Detail project={value} id={id} onWrote={reload} />}
      </ResourceView>
    </>
  )
}

function Detail({
  project,
  id,
  onWrote,
}: {
  project: ProjectDetail
  id: string
  onWrote: () => void
}) {
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
          <AddStepForm app={project.app} id={behaviour.id} onWrote={onWrote} />
        </section>

        <section>
          <h2>Generated test</h2>
          <GeneratedPane generated={generated} />
        </section>
      </div>
    </>
  )
}

/**
 * His step 3, at the smallest useful size: one line of corpus, appended to the
 * behaviour already on screen.
 *
 * The input takes the step **as it is typed into the corpus** (`then sees
 * button:Save`) rather than offering a verb dropdown and a noun picker. That is
 * deliberate and it is the cheaper thing to be wrong about: a form that knew the
 * grammar would be a second definition of what a step is, and `writer.js`
 * already refuses to hold one — it validates by re-parsing the whole file with
 * `kit.js`, so there is exactly one grammar in the system. A picker here would
 * drift from it silently. If typing turns out to be the friction, the picker can
 * be added over a working loop; a second grammar cannot be removed from one.
 */
function AddStepForm({ app, id, onWrote }: { app: string; id: string; onWrote: () => void }) {
  const [line, setLine] = useState('')
  const { write, run } = useWrite(onWrote)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await addStep(app, id, line)
      setLine('')
      return result
    })
  }

  return (
    <form onSubmit={submit} className="write">
      <h3>Add a step</h3>
      <label htmlFor="new-step">Step</label>
      <Input
        id="new-step"
        // The placeholder is a real step from a real corpus rather than
        // `<verb> <noun>`: the grammar is learnable from one example and not
        // from a schema.
        placeholder="then sees button:Save"
        value={line}
        onChange={(e) => setLine(e.target.value)}
        invalid={write.state === 'refused'}
      />
      <Button type="submit" disabled={write.state === 'saving' || line.trim() === ''}>
        {write.state === 'saving' ? 'Writing…' : 'Add step'}
      </Button>
      <WriteFeedback write={write} />
    </form>
  )
}

/**
 * Shared by both forms so a refusal cannot be reported one way here and another
 * way there — the message from the corpus is the same message wherever the edit
 * came from.
 */
export function WriteFeedback({ write }: { write: ReturnType<typeof useWrite>['write'] }) {
  if (write.state === 'refused') {
    return (
      <Card elevation="flat">
        <p role="alert">{write.message}</p>
      </Card>
    )
  }
  if (write.state === 'wrote') {
    return <WriteResultNote result={write.result} />
  }
  return null
}

function StepLine({ step }: { step: Step }) {
  return (
    <>
      <span className="kind">{step.kind}</span> <code>{step.text}</code>
      {step.holes.length > 0 && (
        <span className="badges">
          <Count n={step.holes.length} one="unknown" many="unknowns" tone="warning" />
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
