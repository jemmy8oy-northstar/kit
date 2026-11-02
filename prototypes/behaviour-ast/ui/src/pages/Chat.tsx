import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button, Card, Input } from '@jemmy8oy-northstar/design-system'
import { chatTurn, fetchTriage, leaveNote, setReview } from '../api/client'
import type { TriageMessage, TriageStatus } from '../api/types'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'
import { loadBoard, type Entry } from '../boardLoad'
import { isIdea, isTrial, projectOf } from '../board'
import { keyOf, mine, ordered, readSeen, type InboxCard } from '../inbox'

/** A line in the chat: his, the agent's, or the note that he picked another project. */
type Line = { from: 'user'; text: string; sent: string } | { from: 'agent'; text: string } | { from: 'switch'; app: string }

/**
 * BEH-CHAT-1 to BEH-CHAT-4 (his 2026-11-02 12:56Z note): a chat with the spec agent about one project at
 * a time, picked in a select box. The Inbox and its "Talk it through" stay as they are; this is the same
 * agent, key and daily allowance on a page of its own. The chat lives in the page; the history stays
 * across a project change, and the first message after one tells the agent which project it is now about.
 */
export default function Chat() {
  const entries = useResource(loadBoard, [])
  // undefined while asking; null when the server could not say (signed out, or an older server).
  const [status, setStatus] = useState<TriageStatus | null | undefined>(undefined)

  useEffect(() => {
    let live = true
    fetchTriage().then((s) => live && setStatus(s), () => live && setStatus(null))
    return () => {
      live = false
    }
  }, [])

  return (
    <>
      <h1>Chat</h1>
      <p className="muted">Talk with the spec agent about one project at a time: answer what is waiting on you, define new spec, or discuss the project.</p>
      {status === undefined ? null : !status?.configured ? (
        <p data-testid="chat-off">The spec agent is not switched on here, so there is no one to chat with yet.</p>
      ) : (
        <ResourceView resource={entries}>{(all) => <Conversation entries={all} status={status} />}</ResourceView>
      )}
    </>
  )
}

function Conversation({ entries, status }: { entries: Entry[]; status: TriageStatus }) {
  // The projects he can talk about: what the board shows as work, so ideas and trials are left out.
  const apps = entries
    .filter((e) => !e.error && !isIdea(e.summary) && !isTrial(e.summary))
    .map((e) => ({ app: e.summary.app, project: projectOf(e.summary) }))
  // Frozen at open, in the Inbox's own order, so marking items off never reshuffles the rest.
  const [queue] = useState(() => ordered<InboxCard>(mine(entries), readSeen()))
  const [handled, setHandled] = useState<ReadonlySet<string>>(new Set())
  const first = queue[0]?.app ?? apps[0]?.app ?? ''
  const [app, setApp] = useState(first)
  const [lines, setLines] = useState<Line[]>(first ? [{ from: 'switch', app: first }] : [])
  // The first message after a project change carries it, so the agent knows what the select box says.
  const [announce, setAnnounce] = useState(true)
  const [inHand, setInHand] = useState<InboxCard | null>(null)
  // His answer to the item in hand; an agent reply goes in with "Use as my words".
  const [words, setWords] = useState('')
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [remaining, setRemaining] = useState(status.remaining)

  if (apps.length === 0) return <p>There are no projects to talk about yet.</p>

  const label = (a: string) => {
    const p = apps.find((x) => x.app === a)?.project ?? a
    return p === a ? a : `${p} · ${a}`
  }
  const waiting = queue.filter((c) => !handled.has(keyOf(c)))
  const next = waiting.find((c) => c.app === app) ?? waiting[0]

  function pick(a: string) {
    if (a === app) return
    setApp(a)
    setLines((ls) => [...ls, { from: 'switch', app: a }])
    setAnnounce(true)
    if (inHand && inHand.app !== a) setInHand(null)
  }

  function take(card: InboxCard) {
    pick(card.app)
    setInHand(card)
    setWords('')
  }

  function done() {
    if (inHand) setHandled((h) => new Set(h).add(keyOf(inHand)))
    setInHand(null)
    setWords('')
  }

  async function send() {
    const text = draft.trim()
    if (text === '' || busy) return
    const before = lines
    const after: Line[] = [...lines, { from: 'user', text, sent: announce ? `[Now talking about ${app}] ${text}` : text }]
    const messages: TriageMessage[] = after.flatMap((l): TriageMessage[] =>
      l.from === 'user' ? [{ from: 'user', text: l.sent }] : l.from === 'agent' ? [{ from: 'agent', text: l.text }] : [],
    )
    setLines(after)
    setDraft('')
    setBusy(true)
    setError(null)
    try {
      const answer = await chatTurn(app, inHand?.b.id ?? null, messages)
      setLines([...after, { from: 'agent', text: answer.reply }])
      setAnnounce(false)
      setRemaining(answer.remaining)
    } catch (e) {
      // His message goes back in the box, so a failed turn costs him nothing to retry.
      setLines(before)
      setDraft(text)
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-label="Chat" data-testid="chat">
      <label htmlFor="chat-project">Project</label>
      <select id="chat-project" className="chat-project" value={app} onChange={(e) => pick(e.target.value)}>
        {apps.map((a) => (
          <option key={a.app} value={a.app}>
            {label(a.app)}
          </option>
        ))}
      </select>

      <ol className="triage-messages">
        {lines.map((l, i) =>
          l.from === 'switch' ? (
            <li key={i} className="muted" data-from="switch">
              Now talking about {label(l.app)}.
            </li>
          ) : (
            <li key={i} data-from={l.from}>
              <strong>{l.from === 'user' ? 'You' : 'Spec agent'}:</strong> {l.text}
              {l.from === 'agent' && <ReplyAction text={l.text} app={app} inHand={inHand} onUse={setWords} />}
            </li>
          ),
        )}
        {busy && <li className="muted">The spec agent is replying…</li>}
      </ol>

      {inHand ? (
        <InHand key={keyOf(inHand)} card={inHand} words={words} setWords={setWords} onDone={done} />
      ) : (
        <div className="badges chat-suggestions" role="group" aria-label="Suggestions" data-testid="chat-suggestions">
          {next && (
            <Button type="button" variant="secondary" onClick={() => take(next)}>
              Answer: {next.b.title}
              {next.app !== app ? ` (${label(next.app)})` : ''}
            </Button>
          )}
          <Button type="button" variant="ghost" onClick={() => setDraft(`I want to add to ${label(app)}: `)}>
            Define new spec for {label(app)}
          </Button>
          <Button type="button" variant="ghost" onClick={() => setDraft(`Let's discuss ${label(app)}: `)}>
            Discuss {label(app)}
          </Button>
        </div>
      )}

      <label htmlFor="chat-draft">Message the spec agent</label>
      <Input
        id="chat-draft"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') void send()
        }}
      />
      <div className="badges">
        <Button type="button" onClick={() => void send()} disabled={busy || draft.trim() === ''}>
          Send
        </Button>
        {remaining !== null && <span className="muted">{remaining} replies left today</span>}
      </div>
      {error && <p role="alert">{error}</p>}
    </section>
  )
}

/**
 * BEH-CHAT-3: what an agent reply can become. With an item in hand it goes into his answer; with
 * none it can be saved as a note on the project, which is how new spec reaches the agent today.
 */
function ReplyAction({ text, app, inHand, onUse }: { text: string; app: string; inHand: InboxCard | null; onUse: (t: string) => void }) {
  const { write, run } = useWrite(() => {})
  if (inHand) {
    return (
      <>
        {' '}
        <Button type="button" size="sm" variant="ghost" onClick={() => onUse(text)}>
          Use as my words
        </Button>
      </>
    )
  }
  return (
    <>
      {' '}
      <Button
        type="button"
        size="sm"
        variant="ghost"
        onClick={() => run(() => leaveNote(app, text))}
        disabled={write.state === 'saving' || write.state === 'wrote'}
      >
        {write.state === 'wrote' ? 'Saved as a note' : `Save as a note on ${app}`}
      </Button>
      {write.state === 'refused' && <WriteFeedback write={write} />}
    </>
  )
}

/** BEH-CHAT-3: the item in hand, with the Inbox's own writes. Any of them marks it off and the next is suggested. */
function InHand({ card, words, setWords, onDone }: { card: InboxCard; words: string; setWords: (w: string) => void; onDone: () => void }) {
  const { app, b } = card
  const { write, run } = useWrite(onDone)
  const saving = write.state === 'saving'

  return (
    <Card className="board-card" data-testid="chat-in-hand">
      <h2>
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}>{b.title}</Link>{' '}
        <span className="muted id">{b.id}</span>
      </h2>
      {b.review.note && b.review.state === 'requested' && <p>{b.review.note}</p>}
      {b.needsContext && <p>👤 {b.needsContext}</p>}
      {!b.review.note && !b.needsContext && b.review.state === 'unreviewed' && <p className="muted">Proposed, and not reviewed yet.</p>}

      <label htmlFor="chat-words">Your answer</label>
      <Input id="chat-words" value={words} onChange={(e) => setWords(e.target.value)} />
      <div className="badges">
        <Button type="button" onClick={() => run(() => setReview(app, b.id, 'approved'))} disabled={saving}>
          Approve
        </Button>
        <Button type="button" onClick={() => run(() => leaveNote(app, words.trim(), b.id))} disabled={saving || words.trim() === ''}>
          Answer
        </Button>
        <Button type="button" variant="ghost" onClick={onDone} disabled={saving}>
          Skip
        </Button>
      </div>
      <WriteFeedback write={write} />
    </Card>
  )
}
