import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Badge, Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { addBehaviour, fetchProject } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Behaviour, Conflict, ProjectDetail } from '../api/types'
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
