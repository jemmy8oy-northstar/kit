import { useEffect, useState } from 'react'
import { Badge, Button, Input } from '@jemmy8oy-northstar/design-system'
import { setDecisionReview } from '../api/client'
import type { Decision, DecisionPart } from '../api/types'
import { WriteFeedback } from '../pages/BehaviourPage'
import { useWrite } from './useWrite'

/** The order the page groups by (BEH-ARCH-1). */
const PARTS: { part: DecisionPart; label: string }[] = [
  { part: 'frontend', label: 'Frontend' },
  { part: 'backend', label: 'Backend' },
  { part: 'data', label: 'Data' },
  { part: 'deployment', label: 'Deployment' },
]

/** A decision and the corpus file it was read from; the file is where an approval is written. */
export type DecisionIn = Decision & { app?: string }

/**
 * A project's architecture decisions (BEH-ARCH-1), grouped by the part of the system they belong
 * to. A part with none gets no heading. Anything nobody has ruled on says so, because a decision is
 * my reading of the repo until he agrees; slice B1 lets him say so from the card. A decision with no
 * `app` has nowhere to write to, so it stays read-only.
 */
export default function DecisionsList({ decisions }: { decisions: DecisionIn[] }) {
  return (
    <>
      {PARTS.map(({ part, label }) => {
        const own = decisions.filter((d) => d.part === part)
        if (own.length === 0) return null

        return (
          <section key={part} aria-label={label}>
            <h2>{label}</h2>
            <ul className="cards">
              {own.map((d) => (
                <li key={d.id}>
                  <DecisionCard d={d} />
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </>
  )
}

function DecisionCard({ d }: { d: DecisionIn }) {
  // What the server just wrote, shown at once; the served value wins again whenever it changes.
  const [local, setLocal] = useState<Decision['review'] | null>(null)
  useEffect(() => setLocal(null), [d.review.state, d.review.note])
  const review = local ?? d.review

  return (
    <>
      <strong>{d.title}</strong>
      {review.state === 'unreviewed' && (
        <>
          {' '}
          <Badge tone="warning">Unreviewed suggestion</Badge>
        </>
      )}
      {review.state === 'approved' && (
        <>
          {' '}
          <Badge tone="success">Approved</Badge>
        </>
      )}
      {review.state === 'denied' && (
        <>
          {' '}
          <Badge tone="danger">Denied</Badge>
        </>
      )}
      {review.state === 'denied' && review.note && <p>Correction: {review.note}</p>}
      <p>Chosen: {d.chosen}</p>
      {d.considered.length > 0 && (
        <>
          <p>Considered:</p>
          <ul aria-label={`Considered for ${d.id}`}>
            {d.considered.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </>
      )}
      <p>Because:</p>
      <ul aria-label={`Because for ${d.id}`}>
        {d.because.map((b) => (
          <li key={b}>{b}</li>
        ))}
      </ul>
      <p className="muted">
        {d.id} · {d.at}
      </p>
      {d.app !== undefined && <Adjudicate app={d.app} id={d.id} state={review.state} onDone={setLocal} />}
    </>
  )
}

/** Approve, or deny with the correction (BEH-INBOX-2): the same two moves, and the same server rule, as a behaviour. */
function Adjudicate({
  app,
  id,
  state,
  onDone,
}: {
  app: string
  id: string
  state: Decision['review']['state']
  onDone: (review: Decision['review']) => void
}) {
  const [denying, setDenying] = useState(false)
  const [reason, setReason] = useState('')
  const { write, run } = useWrite(() => {})
  const saving = write.state === 'saving'
  const correction = reason.trim()

  function send(next: Decision['review']) {
    void run(async () => {
      const result = await setDecisionReview(app, id, next.state, next.note)
      onDone(next)
      setDenying(false)
      setReason('')
      return result
    })
  }

  return (
    <div className="write">
      <div className="badges">
        <Button
          type="button"
          onClick={() => send({ state: 'approved', note: null })}
          disabled={saving || state === 'approved'}
          aria-label={`Approve ${id}`}
        >
          Approve
        </Button>
        <Button type="button" variant="danger" onClick={() => setDenying(true)} disabled={saving || denying} aria-label={`Deny ${id}`}>
          Deny
        </Button>
      </div>
      {denying && (
        // The server refuses a denial with no correction, as for a behaviour; the disabled button is only the affordance.
        <div className="write">
          <label htmlFor={`deny-${id}`}>Reason for denying (what is actually true instead)</label>
          <Input id={`deny-${id}`} value={reason} onChange={(e) => setReason(e.target.value)} />
          <div className="badges">
            <Button
              type="button"
              variant="danger"
              onClick={() => send({ state: 'denied', note: correction })}
              disabled={saving || correction === ''}
              aria-label={`Confirm deny ${id}`}
            >
              Confirm deny
            </Button>
            <Button type="button" variant="ghost" onClick={() => setDenying(false)} disabled={saving}>
              Cancel
            </Button>
          </div>
        </div>
      )}
      <WriteFeedback write={write} />
    </div>
  )
}
