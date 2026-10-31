import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { leaveNote, setPaused, setPriority, setReview } from '../api/client'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'
import { loadBoard, type Entry } from '../boardLoad'
import { hueOf, isQuestion } from '../board'
import { keyOf, lowered, mine, ordered, readSeen, saveSeen, type InboxCard } from '../inbox'

/**
 * BEH-INBOX-1: the cards whose move is his, one at a time across every project. The order is fixed
 * when the page opens, so answering a card never reshuffles the ones still to come; acting moves on.
 */
export default function Inbox() {
  const entries = useResource(loadBoard, [])
  return (
    <>
      <h1>Inbox</h1>
      <p className="muted">
        The cards that are waiting on you, one at a time: the ones you have not opened first, then the rest, each by priority.
      </p>
      <ResourceView resource={entries}>{(all) => <Queue entries={all} />}</ResourceView>
    </>
  )
}

function Queue({ entries }: { entries: Entry[] }) {
  // Frozen at open: reading "seen" and ordering once is what keeps the queue from moving under his thumb.
  const [queue] = useState(() => ordered(mine(entries), readSeen()))
  const [at, setAt] = useState(0)
  const card = queue[at]

  // Opened is remembered in this browser, like the opening page (BEH-SETTINGS-2).
  useEffect(() => {
    if (!card) return
    const seen = readSeen()
    seen.add(keyOf(card))
    saveSeen(seen)
  }, [card])

  if (queue.length === 0) {
    return <p>Nothing is waiting on you.</p>
  }
  if (!card) {
    return <p>That is all of them: nothing else is waiting on you.</p>
  }
  return (
    <>
      <p className="muted" data-testid="inbox-position">
        Card {at + 1} of {queue.length}
      </p>
      <CardView key={keyOf(card)} card={card} onDone={() => setAt((n) => n + 1)} />
    </>
  )
}

/** One card and its buttons. Each calls the write the card's own page makes; a refusal stays on screen and the card stays. */
function CardView({ card, onDone }: { card: InboxCard; onDone: () => void }) {
  const { app, project, b } = card
  const [words, setWords] = useState('')
  const [denying, setDenying] = useState(false)
  const [reason, setReason] = useState('')
  const { write, run } = useWrite(onDone)
  const saving = write.state === 'saving'
  const next = lowered(b.priority)
  const denial = words.trim() || reason.trim()

  return (
    <Card className={`board-card hue-${hueOf(project)}`}>
      <h2>
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}>{b.title}</Link>{' '}
        <span className="muted id">{b.id}</span>
      </h2>
      <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
      <div className="badges">
        {isQuestion(b) && <Badge tone="primary">❓ question</Badge>}
        {b.paused && <Badge tone="neutral">⏸ paused{b.pausedUntil ? ` · asks again ${b.pausedUntil}` : ''}</Badge>}
        {b.priority !== undefined && <Badge tone={b.priority === 1 ? 'danger' : 'neutral'}>P{b.priority}</Badge>}
        <Badge tone="neutral">{b.layer}</Badge>
      </div>
      {b.review.note && b.review.state === 'requested' && <p data-testid="inbox-question">{b.review.note}</p>}
      {b.needsContext && <p data-testid="needs-context">👤 {b.needsContext}</p>}
      {!b.review.note && !b.needsContext && b.review.state === 'unreviewed' && (
        <p className="muted">Proposed, and not reviewed yet.</p>
      )}

      <label htmlFor="inbox-words">Your words</label>
      <Input id="inbox-words" value={words} onChange={(e) => setWords(e.target.value)} />

      <div className="badges">
        <Button type="button" onClick={() => run(() => setReview(app, b.id, 'approved'))} disabled={saving}>
          Approve
        </Button>
        <Button type="button" variant="danger" onClick={() => setDenying(true)} disabled={saving || denying}>
          Deny
        </Button>
        {(b.pending || b.paused) && (
          <Button type="button" variant="secondary" onClick={() => run(() => setPaused(app, b.id, !b.paused))} disabled={saving}>
            {b.paused ? 'Resume' : 'Pause'}
          </Button>
        )}
        {/* BEH-BOARD-26: pausing a paused card re-dates it, so this is how "ask me again later" is said. */}
        {b.paused && (
          <Button type="button" variant="secondary" onClick={() => run(() => setPaused(app, b.id, true))} disabled={saving}>
            Ask again in 2 weeks
          </Button>
        )}
        <Button type="button" onClick={() => run(() => leaveNote(app, words.trim(), b.id))} disabled={saving || words.trim() === ''}>
          Answer
        </Button>
        <Button
          type="button"
          variant="secondary"
          onClick={() => run(() => leaveNote(app, words.trim(), b.id))}
          disabled={saving || words.trim() === ''}
        >
          Comment
        </Button>
        <Button
          type="button"
          variant="ghost"
          onClick={() => run(() => setPriority(app, b.id, next))}
          disabled={saving || b.priority === 5}
        >
          Lower priority
        </Button>
        {/* His "dismiss": move on without writing anything; the card stays his and comes back next time. */}
        <Button type="button" variant="ghost" onClick={onDone} disabled={saving}>
          Skip
        </Button>
      </div>

      {denying && (
        // The server refuses a denial with no correction. BEH-INBOX-2: words he has already typed are that
        // correction, so the reason box only appears when "Your words" is empty; the confirm step stays either way.
        <div className="write">
          {words.trim() === '' ? (
            <>
              <label htmlFor="inbox-reason">Reason for denying (what is actually true instead)</label>
              <Input id="inbox-reason" value={reason} onChange={(e) => setReason(e.target.value)} />
            </>
          ) : (
            <p data-testid="inbox-deny-words">Your words above are the reason for denying.</p>
          )}
          <div className="badges">
            <Button
              type="button"
              variant="danger"
              onClick={() => run(() => setReview(app, b.id, 'denied', denial))}
              disabled={saving || denial === ''}
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
    </Card>
  )
}
