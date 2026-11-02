import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@jemmy8oy-northstar/design-system'
import { chatTurn, dismissNote, fetchNotes, fetchTriage, leaveNote, setReview } from '../api/client'
import type { ReviewState, TriageAction, TriageStatus } from '../api/types'
import { MAX_TEXT, named, toMessages, untold, type Line } from '../chat'
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
  // The same set, current inside a write's callback, which may run after other items were marked off.
  const handledNow = useRef<ReadonlySet<string>>(handled)
  const undoing = useRef(new Set<Line>())
  const first = queue[0]?.app ?? apps[0]?.app ?? ''
  const [app, setApp] = useState(first)
  const [lines, setLines] = useState<Line[]>([])
  // The first message after a project change carries it, so the agent knows which project it is about.
  const [announce, setAnnounce] = useState(true)
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [remaining, setRemaining] = useState(status.remaining)
  const end = useRef<HTMLDivElement>(null)

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

  /**
   * His 17:54Z note: once he has acted on an item, Kit says so in the chat and offers what comes next,
   * the next item or other work, so the conversation carries on rather than going quiet.
   */
  function done(card: InboxCard, how: Acted, undo?: () => Promise<unknown>) {
    mark(card, true)
    const rest = queue.filter((c) => !handledNow.current.has(keyOf(c)))
    const next = rest.find((c) => c.app === card.app) ?? null
    const other = next ? null : (rest[0] ?? null)
    const here = label(card.app)
    const then = next
      ? `Next up in ${here}: “${next.b.title}”. Reply to answer it, or we can define new spec or discuss ${here}.`
      : other
        ? `Nothing else is waiting in ${here}. The next waiting item is in ${label(other.app)}, below. Or we can define new spec or discuss ${here}.`
        : `That was the last item waiting on you. We can define new spec or discuss ${here}.`
    // Every action can be taken back (BEH-CHAT-6): the write is reversed, and the item comes back to the chat.
    const back = async () => {
      await undo?.()
      mark(card, false)
    }
    setLines((ls) => [...ls, { from: 'kit', text: `${ACTED[how].said} “${card.b.title}”. ${then}`, tell: `[He ${ACTED[how].told} ${card.b.id}]`, undo: back }])
  }

  /** Marks an item off this chat, or brings it back after an undo. */
  function mark(card: InboxCard, off: boolean) {
    const now = new Set(handledNow.current)
    if (off) now.add(keyOf(card))
    else now.delete(keyOf(card))
    handledNow.current = now
    setHandled(now)
  }

  /** Runs a Kit line's undo, says so in the chat, and tells the agent. A refused undo says why. */
  async function undoLine(line: Line & { from: 'kit' }) {
    // A second tap while the first is still undoing would reverse it twice.
    if (undoing.current.has(line)) return
    undoing.current.add(line)
    try {
      await line.undo?.()
      setLines((ls) => [
        ...ls.map((l) => (l === line ? { ...line, undone: true } : l)),
        { from: 'kit', text: `Undone: ${line.text.split('. ')[0]}.`, tell: `[He undid this: ${line.tell}]` },
      ])
    } catch (e) {
      undoing.current.delete(line)
      setError(`Could not undo it: ${e instanceof Error ? e.message : String(e)}`)
    }
  }

  /**
   * BEH-CHAT-6 (his 17:53Z note): the agent asked for something to be done. It is done here with the same
   * writes as the page's buttons, so it is his session acting on his word, and each comes with an Undo.
   */
  async function act(action: TriageAction, card: InboxCard | null, now: Line[]) {
    if (action.kind === 'undo') {
      const last = [...now].reverse().find((l): l is Line & { from: 'kit' } => l.from === 'kit' && !!l.undo && !l.undone)
      if (last) await undoLine(last)
      else setLines((ls) => [...ls, { from: 'kit', text: 'There is nothing here to undo.', tell: '[There was nothing to undo]' }])
      return
    }
    if (!card) return
    const { app: on, b } = card
    const restore = () => setReview(on, b.id, b.review.state as ReviewState, b.review.note)
    try {
      if (action.kind === 'approve') {
        await setReview(on, b.id, 'approved')
        done(card, 'approved', restore)
      } else if (action.kind === 'deny') {
        await setReview(on, b.id, 'denied', action.text)
        done(card, 'denied', restore)
      } else {
        const text = action.text
        await leaveNote(on, text, b.id)
        done(card, 'answered', () => dismissAnswer(on, b.id, text))
      }
    } catch (e) {
      setError(`The agent could not do that: ${e instanceof Error ? e.message : String(e)}`)
    }
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
    // His 17:52Z note: the item moves into the chat where he first replies about it, so the thread reads in
    // order, and the agent is told of it at that point too.
    if (item && !before.some((l) => l.from === 'item' && l.app === item.app && l.id === item.b.id)) {
      const ask = askOf(item)
      const told = `[In view now: ${item.b.id} "${item.b.title}"${ask ? `, asking: ${ask.slice(0, MAX_TOLD)}` : ''}]`
      before = [...before, { from: 'item', app: item.app, id: item.b.id, title: item.b.title, ask, tell: told }]
    }
    const heads = [...(tell ? [`[Now talking about ${about}]`] : []), ...untold(before)]
    // His words always go whole; what goes ahead of them is cut to fit the server's limit.
    const sent = heads.length > 0 ? `${heads.join(' ').slice(0, MAX_TEXT - text.length - 1)} ${text}` : text
    const after: Line[] = [...before, { from: 'user', text, sent, app: about, item: item?.b.id ?? null }]
    setLines(after)
    setDraft('')
    setBusy(true)
    setError(null)
    // The project is fixed while a reply is awaited: the project picker is disabled.
    try {
      const answer = await chatTurn(about, item?.b.id ?? null, toMessages(after))
      // The chat takes turns, so a reply that only acted still has a line of its own.
      const replied: Line[] = [...after, { from: 'agent', text: answer.reply || 'Done.', app: about, item: item?.b.id ?? null }]
      setLines(replied)
      setAnnounce(false)
      setRemaining(answer.remaining)
      if (answer.action) await act(answer.action, item, replied)
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
          ) : l.from === 'item' ? (
            <li key={i} className="chat-item" data-from="item">
              <p className="muted chat-focus-lead">Top of your stack in {label(l.app)}</p>
              <p className="chat-focus-title">
                <Link to={`/projects/${encodeURIComponent(l.app)}/behaviours/${encodeURIComponent(l.id)}`}>{l.title}</Link>{' '}
                <span className="muted id">{l.id}</span>
              </p>
              {l.ask && <p className="chat-focus-ask">{l.ask}</p>}
            </li>
          ) : l.from === 'kit' ? (
            <li key={i} className="chat-msg" data-from="kit">
              <span className="sr-only">Kit: </span>
              <div className="chat-bubble">{l.text}</div>
              {l.undo && (
                <div className="chat-actions">
                  <Button type="button" size="sm" variant="ghost" onClick={() => void undoLine(l)} disabled={busy || l.undone}>
                    {l.undone ? 'Undone' : 'Undo'}
                  </Button>
                </div>
              )}
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
      </ol>

      <div className="chat-dock">
        {focus ? (
          <Focus
            key={keyOf(focus)}
            card={focus}
            onDone={(how, undo) => done(focus, how, undo)}
            inThread={lines.some((l) => l.from === 'item' && l.app === focus.app && l.id === focus.b.id)}
          />
        ) : (
          <div className="chat-focus muted" data-testid="chat-nothing">
            Nothing is waiting on you in {label(app)}. Ask about it, or describe something new for it.
            {elsewhere && (
              <>
                {' '}
                {/* His 17:50Z note: the project first, so the preview is not read as being about this one. */}
                <Button type="button" size="sm" variant="ghost" onClick={() => pick(elsewhere.app)} disabled={busy}>
                  <strong>{label(elsewhere.app)}</strong>: next waiting is “{elsewhere.b.title}”
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
            // Room for what goes ahead of his words (the project, the item in view, what he did), under the server's 4000.
            maxLength={MAX_TEXT - 1000}
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
      {/* Below the dock, not in the thread: scrolled to the foot of the page, the newest line sits just
          above the docked box rather than under it. */}
      <div ref={end} aria-hidden="true" className="chat-end" />
    </section>
  )
}

/**
 * BEH-CHAT-3: what a message can become. Either side's message about the item in hand can be posted as
 * his answer to it; an agent reply can also be saved as a note on the project it was about, which is how
 * new spec reaches the agent today.
 */
function MessageActions({
  line,
  focus,
  onAnswered,
}: {
  line: Line & { from: 'user' | 'agent' }
  focus: InboxCard | null
  onAnswered: (c: InboxCard, how: Acted, undo: () => Promise<unknown>) => void
}) {
  // Only for the item that was in hand when the message was sent: once it is answered, the next item's
  // question is a different one, and these words were not about it.
  const forItem = focus && focus.app === line.app && focus.b.id === line.item ? focus : null
  const answer = useWrite(() => forItem && onAnswered(forItem, 'answered', () => dismissAnswer(forItem.app, forItem.b.id, line.text)))
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

/** How he acted on an item, and how Kit says it in the chat and to the agent (his 17:54Z note). */
type Acted = 'approved' | 'denied' | 'skipped' | 'answered'
const ACTED: Record<Acted, { said: string; told: string }> = {
  approved: { said: 'Approved', told: 'approved' },
  denied: { said: 'Denied, with your correction:', told: 'denied, with his correction,' },
  skipped: { said: 'Skipped for now:', told: 'skipped, for now,' },
  answered: { said: 'Your answer is posted on', told: 'posted his answer to' },
}

/**
 * Undoes a posted answer (BEH-CHAT-6): dismisses the newest note on the item with exactly those words. Git
 * history keeps it, as for any dismissed note. Refused when it is no longer there to dismiss.
 */
async function dismissAnswer(app: string, id: string, text: string): Promise<unknown> {
  const { notes } = await fetchNotes(app)
  const index = notes.map((n) => n.behaviour === id && n.text.trim() === text.trim()).lastIndexOf(true)
  if (index < 0) throw new Error('the answer is no longer among the notes, so there is nothing to dismiss')
  return dismissNote(app, index, notes[index].at)
}

/** The most of an item's question the agent is told inline; the whole card is in its context anyway. */
const MAX_TOLD = 500

/** What an item is asking him, in words. */
function askOf({ b }: InboxCard): string | null {
  return b.review.note && b.review.state === 'requested'
    ? b.review.note
    : b.needsContext
      ? `👤 ${b.needsContext}`
      : b.review.state === 'unreviewed'
        ? 'Proposed, and not reviewed yet: approve it, or say what is true instead.'
        : null
}

/**
 * BEH-CHAT-2/3: the item in hand, shown from the start rather than behind a click. It says what it is
 * asking; Approve and "Skip to next item" are the Inbox's own writes, and either brings up the next item.
 * Once he has replied about it, the item itself is in the thread (his 17:52Z note), so here it shrinks to
 * its title and the two buttons.
 */
function Focus({ card, onDone, inThread }: { card: InboxCard; onDone: (how: Acted, undo?: () => Promise<unknown>) => void; inThread: boolean }) {
  const { app, b } = card
  // The card as loaded is its state before this chat touched it, which is what an undo puts back.
  const { write, run } = useWrite(() => onDone('approved', () => setReview(app, b.id, b.review.state as ReviewState, b.review.note)))
  const saving = write.state === 'saving'
  const ask = askOf(card)

  return (
    <div className="chat-focus" data-testid="chat-in-hand">
      {!inThread && <p className="muted chat-focus-lead">Top of your stack. Reply to answer it, or talk about other work.</p>}
      <p className="chat-focus-title">
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}>{b.title}</Link>{' '}
        <span className="muted id">{b.id}</span>
      </p>
      {ask && !inThread && <p className="chat-focus-ask">{ask}</p>}
      <div className="badges">
        <Button type="button" size="sm" onClick={() => run(() => setReview(app, b.id, 'approved'))} disabled={saving}>
          Approve
        </Button>
        {/* His 17:48Z note: "Skip" alone read as dismissing it. It only moves on; the item stays waiting. */}
        <Button type="button" size="sm" variant="ghost" onClick={() => onDone('skipped')} disabled={saving}>
          Skip to next item
        </Button>
      </div>
      <WriteFeedback write={write} />
    </div>
  )
}
