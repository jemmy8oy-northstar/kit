import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Button } from '@jemmy8oy-northstar/design-system'
import { chatTurn, dismissNote, fetchNotes, fetchTriage, leaveNote, setReview } from '../api/client'
import type { ReviewState, TriageAction, TriageStatus } from '../api/types'
import { MAX_TEXT, named, splitIds, toMessages, untold, type Line } from '../chat'
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
  // BEH-CHAT-11 (his 2026-11-03 13:36Z note): one chat per project, so apps sharing a `# kit:project` line
  // (kit, kit-ui, kit-hosted) are talked about in the one chat. Each turn still goes to one app's spec agent:
  // the app of the item in hand, else the project's home app.
  const projectOfApp = (a: string) => apps.find((x) => x.app === a)?.project ?? a
  const projects = [...new Set(apps.map((a) => a.project))]
  const home = (p: string) => (apps.some((a) => a.app === p) ? p : (apps.find((a) => a.project === p)?.app ?? p))
  // BEH-CHAT-15: the ids Kit knows (an epic, feature or behaviour in any project), each to its own page.
  // An id in more than one project goes to the first; one Kit does not know stays plain text.
  const pages = new Map<string, string>()
  for (const e of entries) {
    const at = `/projects/${encodeURIComponent(e.summary.app)}`
    for (const [kind, ids] of [['epics', e.detail?.epics ?? []], ['features', e.detail?.features ?? []], ['behaviours', e.behaviours]] as const) {
      for (const { id } of ids) if (!pages.has(id)) pages.set(id, `${at}/${kind}/${encodeURIComponent(id)}`)
    }
  }
  const linked = (text: string) =>
    splitIds(text, (id) => pages.has(id)).map((p, i) => (typeof p === 'string' ? p : <Link key={i} to={pages.get(p.id)!}>{p.id}</Link>))
  // ?project=x opens on that project (an app's name opens its project), and with &design=1 starts designing it (BEH-SPECAGENT-1).
  const [params] = useSearchParams()
  const askedApp = apps.find((a) => a.app === params.get('project') || a.project === params.get('project'))
  const asked = askedApp?.project
  const first = asked ?? (queue[0] ? projectOfApp(queue[0].app) : undefined) ?? apps[0]?.project ?? ''
  const [project, setProject] = useState(first)
  // The app the chat speaks to when no item is in hand, and where its notes go.
  const app = home(project)
  // The project being designed: while the chat is about it, every turn asks the agent to lead its design.
  const [designing, setDesigning] = useState<string | null>(null)
  const started = useRef(false)
  // The top item is in the thread from the first render rather than an effect later, so it never flashes in.
  const [lines, setLines] = useState<Line[]>(() => {
    const top = queue.find((c) => projectOfApp(c.app) === first)
    return top ? [itemLine(top)] : []
  })
  // The same lines, current inside an awaited reply, which may land after he has acted with a button.
  const linesNow = useRef<Line[]>(lines)
  useEffect(() => {
    linesNow.current = lines
  }, [lines])
  // The first message after a project change carries it, so the agent knows which project it is about.
  const [announce, setAnnounce] = useState(true)
  // BEH-CHAT-5: the first line the agent is sent; what came before it stays on the page but costs nothing.
  const [forgotten, setForgotten] = useState(0)
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [remaining, setRemaining] = useState(status.remaining)
  const end = useRef<HTMLDivElement>(null)

  useEffect(() => {
    end.current?.scrollIntoView?.({ block: 'end' })
  }, [lines.length, busy])

  const waiting = queue.filter((c) => !handled.has(keyOf(c)))
  // The item in hand: the top one waiting in any app of this project, on the page with no click (his 14:12Z note).
  const focus = waiting.find((c) => projectOfApp(c.app) === project) ?? null
  const elsewhere = focus ? null : (waiting[0] ?? null)

  // His 19:04Z note: the top of his stack is in the thread as soon as it is in hand (on open, after a project
  // switch, after Kit's "Next up", after an undo), unless it is already the latest item there. Not while a
  // reply is on its way, so a message that switched project by naming it is never followed mid-turn by the
  // new project's item; it comes in when the reply lands.
  const focusKey = focus ? keyOf(focus) : null
  useEffect(() => {
    if (busy || !focus) return
    setLines((ls) => {
      const last = [...ls].reverse().find((l) => l.from === 'item')
      return last?.from === 'item' && last.app === focus.app && last.id === focus.b.id ? ls : [...ls, itemLine(focus)]
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusKey, busy])

  // A link with &design=1 is a deliberate ask, so the first turn goes once, with no further tap.
  useEffect(() => {
    if (started.current || !asked || params.get('design') !== '1') return
    started.current = true
    design(asked)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (apps.length === 0) return <p>There are no projects to talk about yet.</p>

  // BEH-CHAT-5: after a project change, what was said before it can be left out of every later turn.
  const lastSwitch = lines.map((l) => l.from).lastIndexOf('switch')
  const canForget = lastSwitch > forgotten && lines.slice(forgotten, lastSwitch).some((l) => l.from === 'user' && !l.local)
  function forget() {
    setForgotten(lastSwitch)
    // The next message says the project again, as the agent no longer sees where it was named.
    setAnnounce(true)
    setLines((ls) => [...ls, { from: 'kit', text: 'The agent no longer sees the chat before this project, so each reply costs less.', tell: '' }])
  }

  // BEH-CHAT-10: the agent's options are live only on its latest line, until he sends anything after it.
  // Kit's own lines and the item coming into the thread do not retire them.
  const lastAgent = lines.map((l) => l.from).lastIndexOf('agent')
  const optionsAt = lines.slice(lastAgent + 1).some((l) => l.from === 'user' || l.from === 'switch') ? -1 : lastAgent

  const label = (a: string) => {
    const p = apps.find((x) => x.app === a)?.project ?? a
    return p === a ? a : `${p} · ${a}`
  }

  /** Moves the chat to project `p`; a switch line carries the project's name. */
  function pick(p: string) {
    if (p === project) return
    setProject(p)
    setLines((ls) => [...ls, { from: 'switch', app: p }])
    setAnnounce(true)
  }

  /**
   * His 17:54Z note: once he has acted on an item, Kit says so in the chat and offers what comes next,
   * the next item or other work, so the conversation carries on rather than going quiet.
   */
  function done(card: InboxCard, how: Acted, undo?: () => Promise<unknown>, words?: string) {
    mark(card, true)
    const rest = queue.filter((c) => !handledNow.current.has(keyOf(c)))
    const next = rest.find((c) => projectOfApp(c.app) === projectOfApp(card.app)) ?? null
    const other = next ? null : (rest[0] ?? null)
    const here = label(projectOfApp(card.app))
    const then = next
      ? `Reply to answer the next item, or we can define new spec or discuss ${here}.`
      : other
        ? `Nothing else is waiting in ${here}. The next waiting item is in ${label(projectOfApp(other.app))}, below. Or we can define new spec or discuss ${here}.`
        : `That was the last item waiting on you. We can define new spec or discuss ${here}.`
    // Every action can be taken back (BEH-CHAT-6): the write is reversed, and the item comes back to the chat.
    const back = async () => {
      await undo?.()
      mark(card, false)
    }
    setLines((ls) => [...ls, { from: 'kit', text: `${ACTED[how].said} “${card.b.title}”${words ? `: “${words}”` : ''}. ${then}`, tell: `[He ${ACTED[how].told} ${card.b.id}]`, undo: back }])
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
  async function act(action: TriageAction, card: InboxCard | null, about: string) {
    // A write that was refused is said in the thread and told to the agent, so its words claiming it never
    // stand alone; the error line alone would be cleared by his next message.
    const failed = (e: unknown) => {
      const why = e instanceof Error ? e.message : String(e)
      setError(`The agent could not do that: ${why}`)
      setLines((ls) => [...ls, { from: 'kit', text: `That did not go through, so nothing was done: ${why}`, tell: '[Nothing was done: the write was refused]' }])
    }
    if (action.kind === 'note') {
      // On the project the turn was about, not tied to the item, so it works with none in hand (his 19:04Z note).
      const text = action.text
      try {
        await leaveNote(about, text, undefined, 'chat')
        const shown = text.length > 200 ? `${text.slice(0, 200)}…` : text
        setLines((ls) => [
          ...ls,
          { from: 'kit', text: `Saved as a note on ${label(about)}: “${text}”`, tell: `[Kit saved a note: ${shown}]`, undo: () => dismissAnswer(about, null, text) },
        ])
      } catch (e) {
        failed(e)
      }
      return
    }
    if (action.kind === 'question') {
      // BEH-SPECAGENT-3: on the project's notes, marked for the coding agent, which answers it on the card
      // the question is about when it next works. Undone the way a note is.
      const text = `Question for the coding agent: ${action.text}`
      try {
        await leaveNote(about, text, undefined, 'chat')
        setLines((ls) => [
          ...ls,
          {
            from: 'kit',
            text: `Asked the coding agent on ${label(about)}: “${action.text}” It answers when it next works, on the card the question is about.`,
            tell: `[Kit asked the coding agent: ${action.text.slice(0, 200)}]`,
            undo: () => dismissAnswer(about, null, text),
          },
        ])
      } catch (e) {
        failed(e)
      }
      return
    }
    if (action.kind === 'show') {
      // Again, even when it is already the latest item line: he asked to see it. None in hand, nothing to show.
      if (card && !handledNow.current.has(keyOf(card))) setLines((ls) => [...ls, itemLine(card)])
      else setLines((ls) => [...ls, { from: 'kit', text: 'Nothing is in hand to show.', tell: '[Nothing was in hand to show]' }])
      return
    }
    if (action.kind === 'shownote') {
      // BEH-CHAT-12: read-only, so no Undo. Found by the item id it is on or its time, among the notes of the
      // project's apps (the newest three when an item has more).
      const want = action.text.trim()
      const sibling = apps.filter((a) => a.project === projectOfApp(about)).map((a) => a.app)
      const found = (
        await Promise.all((sibling.includes(about) ? sibling : [about]).map((a) => fetchNotes(a).then(({ notes }) => notes.filter((n) => n.behaviour === want || n.at === want).map((note) => ({ app: a, note })), () => [])))
      ).flat()
      if (found.length === 0) {
        setLines((ls) => [...ls, { from: 'kit', text: `There is no note on ${want}, and none at that time.`, tell: `[No note was found for ${want}]` }])
        return
      }
      setLines((ls) => [
        ...ls,
        ...found.slice(-3).map(({ app: a, note }): Line => ({ from: 'note', app: a, note, tell: `[Kit showed the note from ${note.at}${note.behaviour ? ` on ${note.behaviour}` : ''}]` })),
      ])
      return
    }
    if (action.kind === 'undo') {
      // The live lines, not the turn's: he may have acted with a button while the reply was on its way.
      const last = [...linesNow.current].reverse().find((l): l is Line & { from: 'kit' } => l.from === 'kit' && !!l.undo && !l.undone)
      if (last) await undoLine(last)
      else setLines((ls) => [...ls, { from: 'kit', text: 'There is nothing here to undo.', tell: '[There was nothing to undo]' }])
      return
    }
    // Nothing in hand, or he already dealt with it while the reply was on its way: doing it again would write
    // twice. Either way he is told, so the agent's words never claim something that did not happen.
    if (!card || handledNow.current.has(keyOf(card))) {
      setLines((ls) => [
        ...ls,
        { from: 'kit', text: 'The agent meant to act on an item, but none is in hand any more, so nothing was done.', tell: '[Nothing was done: no item was in hand]' },
      ])
      return
    }
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
        await leaveNote(on, text, b.id, 'chat')
        // BEH-CHAT-13: the line quotes what was posted, in full.
        done(card, 'answered', () => dismissAnswer(on, b.id, text), text)
      }
    } catch (e) {
      failed(e)
    }
  }

  /**
   * BEH-SPECAGENT-1 (kit.notes.md#2026-10-31-1643): the agent takes him through the project's first design,
   * topic by topic, and saves each agreed topic as a note for the coding agent to file as unreviewed spec.
   */
  function design(p: string) {
    setDesigning(p)
    void send(`Let's design ${label(p)} from the start. Please lead, one topic at a time.`, p)
  }

  /** `said` is a message Kit's own buttons send for him; his draft stays in the box. `designed` starts design mode. */
  async function send(said?: string, designed?: string) {
    const text = (said ?? draft).trim()
    if (text === '' || busy) return
    // His 19:28Z note: help is answered here, with no model call and no reply spent.
    if (asksHelp(text)) {
      setLines((ls) => [...ls, { from: 'user', text, sent: text, app, item: null, local: true }, helpLine()])
      if (said === undefined) setDraft('')
      return
    }
    // Naming another project moves the chat there before the message goes, so it goes with that project.
    // Naming another app of this project is not a move (BEH-CHAT-11).
    const hit = named(text, apps, app)
    const to = hit !== null && projectOfApp(hit) !== project ? projectOfApp(hit) : null
    const tell = announce || to !== null
    let before = lines
    if (to !== null) {
      before = [...lines, { from: 'switch', app: to, named: true }]
      setProject(to)
    }
    // A message that moves the chat by naming a project was not about the new project's item, which he has
    // not seen yet, so it cannot be posted as the answer to it.
    const item = to !== null ? null : focus
    // The turn goes to the app of the item in hand, else the project's home app (BEH-CHAT-11).
    const about = item?.app ?? home(to ?? project)
    const heads = [...(tell ? [`[Now talking about ${to ?? project}]`] : []), ...untold(before)]
    // His words always go whole; what goes ahead of them is cut to fit the server's limit.
    const sent = heads.length > 0 ? `${heads.join(' ').slice(0, MAX_TEXT - text.length - 1)} ${text}` : text
    const after: Line[] = [...before, { from: 'user', text, sent, app: about, item: item?.b.id ?? null }]
    setLines(after)
    if (said === undefined) setDraft('')
    setBusy(true)
    setError(null)
    // The project is fixed while a reply is awaited: the project picker is disabled.
    try {
      const answer = await chatTurn(about, item?.b.id ?? null, toMessages(after.slice(forgotten)), (to ?? project) === (designed ?? designing))
      // The chat takes turns, so a reply that only acted still has a line of its own. Appended, not set:
      // anything Kit said while the reply was on its way stays.
      setLines((ls) => [
        ...ls,
        { from: 'agent', text: answer.reply || 'Done.', app: about, item: item?.b.id ?? null, options: answer.options },
        // BEH-CHAT-16: a reply the model stopped for length is never shown as if it were whole.
        ...(answer.truncated ? [{ from: 'kit', text: CUT_SHORT, tell: '' } satisfies Line] : []),
      ])
      setAnnounce(false)
      setRemaining(answer.remaining)
      if (answer.action) await act(answer.action, item, about)
    } catch (e) {
      // His message goes back in the box, so a failed turn costs him nothing to retry. A switch he made by
      // naming a project stays: that was what he said, whether or not the agent answered.
      setLines(before)
      if (said === undefined) setDraft(text)
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
          <select aria-label="Project" value={project} onChange={(e) => pick(e.target.value)} disabled={busy}>
            {projects.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </label>
        {/* His 19:28Z note: what Kit is, and that the chat changes nothing itself. No model call. */}
        <Button type="button" size="sm" variant="ghost" onClick={() => setLines((ls) => [...ls, helpLine()])} disabled={busy}>
          What can Kit do?
        </Button>
        {designing === project ? (
          <p className="muted" data-testid="chat-designing">
            Designing {label(project)}: the agent leads, and saves each topic you agree as a note.{' '}
            <Button type="button" size="sm" variant="ghost" onClick={() => setDesigning(null)} disabled={busy}>
              Stop designing
            </Button>
          </p>
        ) : (
          <Button type="button" size="sm" variant="ghost" onClick={() => design(project)} disabled={busy}>
            Design this project
          </Button>
        )}
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
              {l.ask && <p className="chat-focus-ask">{linked(l.ask)}</p>}
            </li>
          ) : l.from === 'note' ? (
            // BEH-CHAT-12: a note on request, read-only: its text, its answer and where it came from.
            <li key={i} className="chat-item" data-from="note">
              <p className="muted chat-focus-lead">
                Note in {label(l.app)}, {l.note.at}
                {l.note.behaviour && <> on <span className="id">{linked(l.note.behaviour)}</span></>}
                {l.note.from && `, from ${l.note.from.replace('-', ' ')}`}
              </p>
              {l.note.text && <p className="chat-focus-ask">{linked(l.note.text)}</p>}
              {l.note.thread.filter((t) => t.kind === 'answer').map((t, j) => (
                <p key={j} className="chat-focus-ask">
                  Answer: {linked(t.text)}
                </p>
              ))}
            </li>
          ) : l.from === 'kit' ? (
            <li key={i} className="chat-msg" data-from="kit">
              <span className="sr-only">Kit: </span>
              <div className="chat-bubble">{linked(l.text)}</div>
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
              <div className="chat-bubble">{linked(l.text)}</div>
              {/* His 13:50Z note: the answers it proposes, one tap each. Only under the newest line, and gone once
                  anything follows it; he can always type his own. */}
              {l.from === 'agent' && l.options && i === optionsAt && (
                <div className="chat-actions chat-options" role="group" aria-label="Suggested answers">
                  {l.options.map((o) => (
                    <Button key={o} type="button" size="sm" variant="ghost" onClick={() => void send(o)} disabled={busy}>
                      {o}
                    </Button>
                  ))}
                </div>
              )}
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
            // His 13:49Z note: the item again at the foot of the chat, with no model call and no reply spent.
            onShow={() => setLines((ls) => [...ls, itemLine(focus)])}
            busy={busy}
          />
        ) : (
          <div className="chat-focus muted" data-testid="chat-nothing">
            Nothing is waiting on you in {label(project)}. Ask about it, or describe something new for it.
            {elsewhere && (
              <>
                {' '}
                {/* His 17:50Z note: the project first, so the preview is not read as being about this one. */}
                <Button type="button" size="sm" variant="ghost" onClick={() => pick(projectOfApp(elsewhere.app))} disabled={busy}>
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
            placeholder={focus ? `Reply to answer it, or ask about anything in ${label(project)}` : `Message about ${label(project)}`}
            // Room for what goes ahead of his words (the project, the item in view, what he did), under the server's 4000.
            maxLength={MAX_TEXT - 1000}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              // Enter that finishes a phone keyboard's word or an IME composition is not a send. BEH-CHAT-17: on a
              // touch screen (a coarse main pointer) Enter is a new line and only Send sends.
              if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing && !window.matchMedia?.('(pointer: coarse)')?.matches) {
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
          {remaining !== null && ` ${remaining} replies left today.`}{' '}
          {/* His 19:04Z note: Kit decides what is worth noting; this only asks it to. */}
          <Button
            type="button"
            size="sm"
            variant="ghost"
            onClick={() => void send(`Please add what we have agreed in this chat as a note on ${label(project)}.`)}
            disabled={busy || !lines.some((l) => l.from === 'user' && !l.local)}
          >
            Ask Kit to note this chat
          </Button>{' '}
          {canForget && (
            <Button type="button" size="sm" variant="ghost" onClick={forget} disabled={busy}>
              Forget the chat before {label(project)}
            </Button>
          )}{' '}
          {/* BEH-NOTE-14 (his 2026-11-02 19:05Z note): notes stay, as the quick brain dump, one tap from here. */}
          <Link to={`/projects/${encodeURIComponent(app)}?tab=notes`}>Notes on {label(app)}</Link>
        </p>
        {error && <p role="alert">{error}</p>}
      </div>
      {/* Below the dock, not in the thread: scrolled to the foot of the page, the newest line sits just
          above the docked box rather than under it. */}
      <div ref={end} aria-hidden="true" className="chat-end" />
    </section>
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
 * Undoes a posted answer, or with no `id` a note on the project (BEH-CHAT-6): dismisses the newest note on
 * the item (or the newest with no behaviour) with exactly those words. Git history keeps it, as for any
 * dismissed note. Refused when it is no longer there to dismiss.
 */
async function dismissAnswer(app: string, id: string | null, text: string): Promise<unknown> {
  const { notes } = await fetchNotes(app)
  // Compared with spacing evened out, as the notes file may rewrap what was posted.
  const flat = (s: string) => s.replace(/\s+/g, ' ').trim()
  const on = (n: { behaviour?: string | null }) => (id === null ? !n.behaviour : n.behaviour === id)
  const index = notes.map((n) => on(n) && flat(n.text) === flat(text)).lastIndexOf(true)
  if (index < 0) throw new Error('the note is no longer among the notes, so there is nothing to dismiss')
  return dismissNote(app, index, notes[index].at)
}

/** The most of an item's question the agent is told inline; the whole card is in its context anyway. */
const MAX_TOLD = 500

/** The item in the thread, with what the agent is told of it (his 19:04Z note). */
function itemLine(card: InboxCard): Line {
  const ask = askOf(card)
  const tell = `[In view now: ${card.b.id} "${card.b.title}"${ask ? `, asking: ${ask.slice(0, MAX_TOLD)}` : ''}]`
  return { from: 'item', app: card.app, id: card.b.id, title: card.b.title, ask, tell }
}

/** His 19:28Z note: what Kit is, and that the chat changes nothing itself. */
const HELP =
  "Kit keeps your project's spec. Here you discuss and define it with the spec agent; this chat changes no code. A coding agent works in the background to build what the spec says and keep your app up to date. When you ask, the agent can approve, deny or answer the top item (each with Undo), show it again, save what you agree as a note, or ask the coding agent a technical question, which it answers when it next works. Notes are also a quick brain-dump."
const CUT_SHORT = 'That reply was cut short. Ask me to go on.'
const helpLine = (): Line => ({ from: 'kit', text: HELP, tell: '' })
const asksHelp = (text: string) => /^\/?help$/i.test(text.trim())

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
 * BEH-CHAT-2/3: the item in hand. The item itself, with what it asks, is in the thread from the moment it is
 * in hand (his 19:04Z note), so here it is only its title and the two buttons: Approve and "Skip to next
 * item" are the Inbox's own writes, and either brings up the next item.
 */
function Focus({
  card,
  onDone,
  onShow,
  busy,
}: {
  card: InboxCard
  onDone: (how: Acted, undo?: () => Promise<unknown>) => void
  onShow: () => void
  // While a reply is on its way the agent may act on this item itself, so its buttons wait (BEH-CHAT-6).
  busy: boolean
}) {
  const { app, b } = card
  // The card as loaded is its state before this chat touched it, which is what an undo puts back.
  const { write, run } = useWrite(() => onDone('approved', () => setReview(app, b.id, b.review.state as ReviewState, b.review.note)))
  const saving = write.state === 'saving'

  return (
    <div className="chat-focus" data-testid="chat-in-hand">
      <div className="badges">
        {/* The title and question are in the thread just above; here only which item the buttons act on. */}
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`} className="muted id" title={b.title}>
          {b.id}
        </Link>
        <Button type="button" size="sm" onClick={() => run(() => setReview(app, b.id, 'approved'))} disabled={saving || busy}>
          Approve
        </Button>
        {/* His 17:48Z note: "Skip" alone read as dismissing it. It only moves on; the item stays waiting. */}
        <Button type="button" size="sm" variant="ghost" onClick={() => onDone('skipped')} disabled={saving || busy}>
          Skip to next item
        </Button>
        {/* His 13:49Z note: when the item is far up the chat, post it again at the bottom. */}
        <Button type="button" size="sm" variant="ghost" onClick={onShow} disabled={saving || busy}>
          Show again
        </Button>
      </div>
      <WriteFeedback write={write} />
    </div>
  )
}
