import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Badge, Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { addStep, fetchProject, setReview } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Behaviour, Generated, ProjectDetail, Step } from '../api/types'
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

      <AdjudicateForm app={project.app} behaviour={behaviour} onWrote={onWrote} />

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
 * His step 4, and the verb the UI has never had: **"what the desired behaviour
 * really is"**.
 *
 * ── Why this control is on THIS page and not on the queue ────────────────────
 * The project page lists what is unreviewed and links here. It would be two
 * clicks cheaper to put Approve on that list, and that is exactly the mechanism
 * his claude-code-bot#68 decision exists to prevent: *"I like this default
 * included but marked unreviewed"* is a guard against inferences passing
 * themselves off as requirements, and a queue with a row of Approve buttons is
 * how a guard becomes a formality. You cannot honestly approve a behaviour you
 * are not looking at — so the control sits where the steps and the generated
 * test are already on screen, which is the evidence the answer depends on.
 *
 * ── Deny needs the correction, and the button says so ────────────────────────
 * `parse()` refuses `review denied` with nothing after it — his #68 point that a
 * bare denial deletes a line where a denial with a correction compounds into the
 * corpus. The disabled button here is an AFFORDANCE, not the rule: the rule is
 * on the server, where it is one sentence in one place, and if these two ever
 * disagree the server wins and its refusal is what appears below.
 */
function AdjudicateForm({
  app,
  behaviour,
  onWrote,
}: {
  app: string
  behaviour: Behaviour
  onWrote: () => void
}) {
  const [note, setNote] = useState('')
  const { write, run } = useWrite(onWrote)

  async function adjudicate(state: 'approved' | 'denied') {
    await run(async () => {
      const result = await setReview(app, behaviour.id, state, state === 'denied' ? note.trim() : null)
      setNote('')
      return result
    })
  }

  const saving = write.state === 'saving'

  return (
    <section className="write">
      <h3>Adjudication</h3>
      <p className="muted">
        {behaviour.source.origin === 'inferred' ? (
          <>
            Kit inferred this from{' '}
            <code>{behaviour.source.ref ?? 'somewhere it could not name'}</code>. It is{' '}
            <strong>{behaviour.review.state}</strong>.
          </>
        ) : (
          <>
            A human wrote this — silence in a corpus means `defined`. It is{' '}
            <strong>{behaviour.review.state}</strong>.
          </>
        )}
      </p>
      {behaviour.review.note && (
        <p className="muted">Recorded correction: {behaviour.review.note}</p>
      )}

      <label htmlFor="deny-note">Correction (required to deny)</label>
      <Input
        id="deny-note"
        // The placeholder is the question a denial answers. "Reason" would
        // invite "wrong", which deletes a line; the corpus wants the thing that
        // is actually true, because that is what compounds.
        placeholder="what is actually true instead"
        value={note}
        onChange={(e) => setNote(e.target.value)}
        invalid={write.state === 'refused'}
      />
      <div className="badges">
        <Button type="button" onClick={() => adjudicate('approved')} disabled={saving}>
          {saving ? 'Writing…' : 'Approve'}
        </Button>
        <Button
          type="button"
          variant="danger"
          onClick={() => adjudicate('denied')}
          disabled={saving || note.trim() === ''}
        >
          {saving ? 'Writing…' : 'Deny'}
        </Button>
      </div>
      <WriteFeedback write={write} />
    </section>
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
