import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@jemmy8oy-northstar/design-system'
import { chatTurn, fetchTriage, leaveNote, setReview } from '../api/client'
import type { TriageStatus } from '../api/types'
import { MAX_TEXT, named, toMessages, type Line } from '../chat'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'
import { loadBoard, type Entry } from '../boardLoad'
import { isIdea, isTrial, projectOf } from '../board'
import { keyOf, mine, ordered, readSeen, type InboxCard } from '../inbox'


/**
 * BEH-CHAT-1 to BEH-CHAT-4 (his 2026-11-02 12:56Z and 14:12Z notes): a chat with the spec agent about one
 * project at a time. It opens on the project with the top item waiting on him, and that item is already
 * on the page, so he can just start typing: the agent infers whether he is answering it or talking about
 * other work. He changes project by tapping it, or by naming another project in a message. The Inbox and
 * its "Talk it through" stay as they are; this is the same agent, key and daily allowance on a page of
 * its own. The history stays across a project change, and the first message after one says which it is.
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

  return status === undefined ? null : !status?.configured ? (
    <>
      <h1>Chat</h1>
      <p data-testid="chat-off">The spec agent is not switched on here, so there is no one to chat with yet.</p>
    </>
  ) : (
    <ResourceView resource={entries}>{(all) => <Conversation entries={all} status={status} />}</ResourceView>
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
  const [lines, setLines] = useState<Line[]>([])
  // The first message after a project change carries it, so the agent knows which project it is about.
  const [announce, setAnnounce] = useState(true)
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [remaining, setRemaining] = useState(status.remaining)
  const end = useRef<HTMLLIElement>(null)

  useEffect(() => {
    end.current?.scrollIntoView?.({ block: 'end' })
  }, [lines.length, busy])

  if (apps.length === 0) return <p>There are no projects to talk about yet.</p>

  const label = (a: string) => {
    const p = apps.find((x) => x.app === a)?.project ?? a
    return p === a ? a : `${p} · ${a}`
  }
  const waiting = queue.filter((c) => !handled.has(keyOf(c)))
  // The item in hand: the top one waiting in this project, on the page with no click (his 14:12Z note).
  const focus = waiting.find((c) => c.app === app) ?? null
  const elsewhere = focus ? null : (waiting[0] ?? null)

  function pick(a: string, by: 'picked' | 'named' = 'picked') {
    if (a === app) return
    setApp(a)
    setLines((ls) => [...ls, by === 'named' ? { from: 'switch', app: a, named: true } : { from: 'switch', app: a }])
    setAnnounce(true)
  }

  function done(card: InboxCard) {
    setHandled((h) => new Set(h).add(keyOf(card)))
  }

  async function send() {
    const text = draft.trim()
    if (text === '' || busy) return
    // Naming another project moves the chat there before the message goes, so it goes with that project.
    const to = named(text, apps, app)
    const about = to ?? app
    const tell = announce || to !== null
    let before = lines
    if (to !== null) {
      before = [...lines, { from: 'switch', app: to, named: true }]
      setApp(to)
    }
    // A message that moves the chat by naming a project was not about the new project's item, which he has
    // not seen yet, so it cannot be posted as the answer to it.
    const item = to !== null ? null : (waiting.find((c) => c.app === about) ?? null)
    const after: Line[] = [...before, { from: 'user', text, sent: tell ? `[Now talking about ${about}] ${text}` : text, app: about, item: item?.b.id ?? null }]
    setLines(after)
    setDraft('')
    setBusy(true)
    setError(null)
    // The project is fixed while a reply is awaited: the project picker is disabled.
    try {
      const answer = await chatTurn(about, item?.b.id ?? null, toMessages(after))
      setLines([...after, { from: 'agent', text: answer.reply, app: about, item: item?.b.id ?? null }])
      setAnnounce(false)
      setRemaining(answer.remaining)
    } catch (e) {
      // His message goes back in the box, so a failed turn costs him nothing to retry. A switch he made by
      // naming a project stays: that was what he said, whether or not the agent answered.
      setLines(before)
      setDraft(text)
      if (to !== null) setAnnounce(true)
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-label="Chat" data-testid="chat" className="chat">
      <header className="chat-head">
        <h1>Chat</h1>
        <label className="chat-project">
          <span className="muted">Talking about</span>
          <select aria-label="Project" value={app} onChange={(e) => pick(e.target.value)} disabled={busy}>
            {apps.map((a) => (
              <option key={a.app} value={a.app}>
                {label(a.app)}
              </option>
            ))}
          </select>
        </label>
      </header>

      <ol className="chat-thread" aria-label="Messages">
        {lines.map((l, i) =>
          l.from === 'switch' ? (
            <li key={i} className="chat-switch muted" data-from="switch">
              Now talking about {label(l.app)}
              {l.named ? ', as you named it' : ''}.
            </li>
          ) : (
            <li key={i} className="chat-msg" data-from={l.from}>
              <span className="sr-only">{l.from === 'user' ? 'You: ' : 'Spec agent: '}</span>
              <div className="chat-bubble">{l.text}</div>
              {/* Not on the turn still awaiting a reply: a failed turn takes that line away again. */}
              {!(busy && i === lines.length - 1) && <MessageActions line={l} focus={focus} onAnswered={done} />}
            </li>
          ),
        )}
        {busy && (
          <li className="chat-msg muted" data-from="agent">
            <div className="chat-bubble">The spec agent is replying…</div>
          </li>
        )}
        <li ref={end} aria-hidden="true" className="chat-end" />
      </ol>

      <div className="chat-dock">
        {focus ? (
          <Focus key={keyOf(focus)} card={focus} onDone={() => done(focus)} chatting={lines.some((l) => l.from === 'user')} />
        ) : (
          <div className="chat-focus muted" data-testid="chat-nothing">
            Nothing is waiting on you in {label(app)}. Ask about it, or describe something new for it.
            {elsewhere && (
              <>
                {' '}
                <Button type="button" size="sm" variant="ghost" onClick={() => pick(elsewhere.app)} disabled={busy}>
                  Next waiting: {elsewhere.b.title} ({label(elsewhere.app)})
                </Button>
              </>
            )}
          </div>
        )}

        <div className="chat-composer">
          <textarea
            aria-label="Message the spec agent"
            rows={1}
            placeholder={focus ? `Reply to answer it, or ask about anything in ${label(app)}` : `Message about ${label(app)}`}
            // Room for the "[Now talking about x]" a project change adds, under the server's 4000.
            maxLength={MAX_TEXT - 100}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              // Enter that finishes a phone keyboard's word or an IME composition is not a send.
              if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault()
                void send()
              }
            }}
          />
          <Button type="button" aria-label="Send" onClick={() => void send()} disabled={busy || draft.trim() === ''}>
            ↑
          </Button>
        </div>
        <p className="chat-foot muted">
          Name another project in a message to talk about it instead.
          {remaining !== null && ` ${remaining} replies left today.`}
        </p>
        {error && <p role="alert">{error}</p>}
      </div>
    </section>
  )
}

/**
 * BEH-CHAT-3: what a message can become. Either side's message about the item in hand can be posted as
 * his answer to it; an agent reply can also be saved as a note on the project it was about, which is how
 * new spec reaches the agent today.
 */
function MessageActions({ line, focus, onAnswered }: { line: Line & { from: 'user' | 'agent' }; focus: InboxCard | null; onAnswered: (c: InboxCard) => void }) {
  // Only for the item that was in hand when the message was sent: once it is answered, the next item's
  // question is a different one, and these words were not about it.
  const forItem = focus && focus.app === line.app && focus.b.id === line.item ? focus : null
  const answer = useWrite(() => forItem && onAnswered(forItem))
  const note = useWrite(() => {})
  if (!forItem && line.from === 'user') return null
  return (
    <div className="chat-actions">
      {forItem && (
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => answer.run(() => leaveNote(forItem.app, line.text, forItem.b.id))}
          disabled={answer.write.state === 'saving'}
        >
          Post as my answer
        </Button>
      )}
      {line.from === 'agent' && (
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => note.run(() => leaveNote(line.app, line.text))}
          disabled={note.write.state === 'saving' || note.write.state === 'wrote'}
        >
          {note.write.state === 'wrote' ? 'Saved as a note' : `Save as a note on ${line.app}`}
        </Button>
      )}
      {answer.write.state === 'refused' && <WriteFeedback write={answer.write} />}
      {note.write.state === 'refused' && <WriteFeedback write={note.write} />}
    </div>
  )
}

/**
 * BEH-CHAT-2/3: the item in hand, shown from the start rather than behind a click. It says what it is
 * asking; Approve and Skip are the Inbox's own writes, and either brings up the next item.
 */
function Focus({ card, onDone, chatting }: { card: InboxCard; onDone: () => void; chatting: boolean }) {
  const { app, b } = card
  const { write, run } = useWrite(onDone)
  const saving = write.state === 'saving'
  const ask =
    b.review.note && b.review.state === 'requested'
      ? b.review.note
      : b.needsContext
        ? `👤 ${b.needsContext}`
        : b.review.state === 'unreviewed'
          ? 'Proposed, and not reviewed yet: approve it, or say what is true instead.'
          : null

  return (
    <div className="chat-focus" data-testid="chat-in-hand">
      {!chatting && <p className="muted chat-focus-lead">Top of your stack. Reply to answer it, or talk about other work.</p>}
      <p className="chat-focus-title">
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}>{b.title}</Link>{' '}
        <span className="muted id">{b.id}</span>
      </p>
      {ask && <p className="chat-focus-ask">{ask}</p>}
      <div className="badges">
        <Button type="button" size="sm" onClick={() => run(() => setReview(app, b.id, 'approved'))} disabled={saving}>
          Approve
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onDone} disabled={saving}>
          Skip
        </Button>
      </div>
      <WriteFeedback write={write} />
    </div>
  )
}
