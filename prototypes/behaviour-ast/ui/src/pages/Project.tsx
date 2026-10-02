import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Badge, Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { addBehaviour, fetchProject } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Behaviour, Conflict, ProjectDetail, Question } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import Count from '../components/Count'
import ResourceView from '../components/Resource'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'

/** Step 1 of his loop, one level down: the corpus, what conflicts, what nobody reviewed. */
export default function Project() {
  const { app = '' } = useParams()
  const { resource, reload } = useReloadableResource(() => fetchProject(app), [app])

  return (
    <>
      <p className="crumbs">
        <Link to="/">Projects</Link> / {app}
      </p>

      <ResourceView resource={resource}>
        {(value) => <Detail project={value} onWrote={reload} />}
      </ResourceView>
    </>
  )
}

function Detail({ project, onWrote }: { project: ProjectDetail; onWrote: () => void }) {
  const unreviewed = new Set(project.adjudication.unreviewed)

  return (
    <>
      <h1>{project.app}</h1>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        <Count n={project.behaviours.length} one="behaviour" tone="primary" />
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

      <QuestionSheet app={project.app} questions={project.questions} />

      <section>
        <h2>Behaviours</h2>
        <ul className="cards">
          {project.behaviours.map((b) => (
            <li key={b.id}>
              <BehaviourRow app={project.app} behaviour={b} unreviewed={unreviewed.has(b.id)} />
            </li>
          ))}
        </ul>
        <NewBehaviourForm app={project.app} onWrote={onWrote} />
      </section>
    </>
  )
}

/**
 * His step 4: "what the desired behaviour really is" — a behaviour that did not
 * exist before, written into the corpus itself rather than into a report about
 * it.
 *
 * ⚠️ **The new behaviour is marked `inferred`, and this form does not offer to
 * change that.** `writer.js` defaults `source` to `inferred`, which `parse()`
 * then marks `unreviewed` — James's call on claude-code-bot#68: *"I like this
 * default included but marked unreviewed"*. A field here that let the writer
 * claim `defined` would let a machine spend the silence that means a human wrote
 * it, which is the one thing a corpus cannot get back. Promoting an inference to
 * defined is an adjudication, and adjudication is a separate act from authoring.
 */
function NewBehaviourForm({ app, onWrote }: { app: string; onWrote: () => void }) {
  const [id, setId] = useState('')
  const [title, setTitle] = useState('')
  const [actor, setActor] = useState('')
  const { write, run } = useWrite(onWrote)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      // `steps` is deliberately omitted rather than sent empty: a new behaviour
      // starts with none, and the step form on its own page is where they are
      // added — one grammar, one place that types it.
      const result = await addBehaviour(app, {
        id: id.trim(),
        title: title.trim(),
        // An empty actor must not become `actor ""` in the corpus. Sent only
        // when it has a value; `writer.js` omits the line entirely then.
        ...(actor.trim() ? { actor: actor.trim() } : {}),
      })
      setId('')
      setTitle('')
      setActor('')
      return result
    })
  }

  return (
    <form onSubmit={submit} className="write">
      <h3>New behaviour</h3>
      <label htmlFor="new-id">Id</label>
      <Input
        id="new-id"
        placeholder="BEH-CUT-2"
        value={id}
        onChange={(e) => setId(e.target.value)}
        invalid={write.state === 'refused'}
      />
      <label htmlFor="new-title">Title</label>
      <Input
        id="new-title"
        placeholder="the cut is downloadable"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
      />
      <label htmlFor="new-actor">Actor</label>
      <Input
        id="new-actor"
        placeholder="editor"
        value={actor}
        onChange={(e) => setActor(e.target.value)}
      />
      <Button
        type="submit"
        disabled={write.state === 'saving' || id.trim() === '' || title.trim() === ''}
      >
        {write.state === 'saving' ? 'Writing…' : 'Add behaviour'}
      </Button>
      <WriteFeedback write={write} />
    </form>
  )
}

/**
 * The question sheet, on the screen at last.
 *
 * `kit.js` has always built this — `questions()` + `renderSheet()` — and
 * `docs/design/ui.md` promised it as "the sheet, made clickable". `ui.js` has
 * been sending it in every project payload since the read model shipped; this UI
 * typed it `unknown[]` and rendered none of it. The material it was throwing
 * away is the most considered thing Kit produces: the question, both options
 * with their consequences, and a recommendation with its reasoning.
 *
 * ⚠️ **The two tiers do NOT get the same treatment, and the asymmetry is the
 * point.** A `review` entry is answered in a vocabulary that already exists, so
 * the queue links to where it can be answered. A `decision` entry — two
 * behaviours disagreeing — has no answer Kit can record: there is no `supersede`
 * keyword in the corpus grammar, no writer function for one, and adding either
 * is a change to the LANGUAGE rather than a feature on top of it. So a conflict
 * shows everything the sheet knows and offers no button. Rendering a resolution
 * control here would mean inventing the syntax it wrote into, which is his call
 * and not a gap to quietly fill.
 */
function QuestionSheet({ app, questions }: { app: string; questions: Question[] }) {
  if (!questions.length) return null

  const decisions = questions.filter((q) => q.tier === 'decision')
  const reviews = questions.filter((q) => q.tier === 'review')

  return (
    <section>
      <h2>Question sheet</h2>
      <div className="badges">
        <Count n={decisions.length} one="decision" tone="warning" />
        <Count n={reviews.length} one="review" />
      </div>

      {decisions.map((q) => (
        <DecisionCard key={q.key} question={q} />
      ))}

      {reviews.length > 0 && (
        <>
          <h3>Never adjudicated</h3>
          <p className="muted">
            Kit inferred each of these from code and marked it unreviewed. Open one to approve it,
            or to deny it with what is actually true — the corpus is what gets edited.
          </p>
          <ul className="cards">
            {reviews.map((q) => (
              <li key={q.key}>
                <Card interactive>
                  <h4>
                    <Link
                      to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(q.id ?? q.key)}`}
                    >
                      {q.id ?? q.key}
                    </Link>{' '}
                    {q.title}
                  </h4>
                  {/* In a <code>, which is not decoration: a source ref is one
                      unbroken token and as plain text it pushes the card wider
                      than a phone, clipping the title beside it. */}
                  {q.source?.ref && (
                    <p className="muted">
                      inferred from <code>{q.source.ref}</code>
                    </p>
                  )}
                </Card>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

function DecisionCard({ question }: { question: Question }) {
  return (
    <Card elevation="flat">
      <h3>{question.title}</h3>
      {question.sides && (
        <ul>
          {question.sides.map((s) => (
            <li key={s.id}>
              <strong>{s.id}</strong> — {s.title}: <code>{s.value.join(', ')}</code>{' '}
              {s.ref && <span className="muted">({s.ref})</span>}
            </li>
          ))}
        </ul>
      )}
      {question.asks && <p>{question.asks}</p>}
      {question.options.length > 0 && (
        <ol>
          {question.options.map((o) => (
            <li key={o.label}>
              <strong>{o.label}</strong> — {o.consequence}
            </li>
          ))}
        </ol>
      )}
      {question.recommend && (
        <p>
          <Badge tone="primary">recommended</Badge> {question.recommend.label} —{' '}
          {question.recommend.why}
        </p>
      )}
      {/* The argument AGAINST the recommendation, kept next to it rather than
          folded away: a recommendation shown without its own counter-case is how
          a suggestion becomes a decision nobody made. */}
      {question.against && <p className="muted">Against: {question.against}</p>}
      <p className="muted">
        Kit has no syntax for recording a resolution to this, so there is nothing to click. It is a
        change to the corpus language, and that is James's call.
      </p>
    </Card>
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
        <Count n={behaviour.steps.length} one="step" />
        {behaviour.source.origin === 'inferred' && <Badge tone="warning">inferred</Badge>}
        {unreviewed && <Badge tone="warning">unreviewed</Badge>}
      </div>
      <p className="muted">{behaviour.at}</p>
    </Card>
  )
}
