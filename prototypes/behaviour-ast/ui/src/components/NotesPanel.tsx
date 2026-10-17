import { useEffect, useId, useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { dismissNote, fetchNotes, leaveNote, replyToNote } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Note } from '../api/types'
import { CAP } from '../board'
import { foldedBy, type Citation } from '../notes'
import Count from './Count'
import ResourceView from './Resource'
import { useWrite } from './useWrite'
import WriteResultNote from './WriteResultNote'

/**
 * BEH-NOTE-1..3 (kit#118): his "less structured input … where you can just
 * comment on something rather than defining a behaviour". A note is free text
 * in `<app>.notes.md`, committed like any edit, and folded into the spec by
 * whoever picks it up — who deletes it in the same pull request. So the list
 * below is only ever what is still OPEN.
 *
 * On a behaviour's page the note is tagged with that behaviour's id and the list
 * shows only its notes; on the project page it shows every note, tagged or not.
 *
 * BEH-NOTE-5..7 (kit#118, 2026-10-12): a note the folder could not decode comes
 * back with questions, and sits apart under "Needs your answer" — above the rest,
 * because it is the one kind of note waiting on HIM rather than on the folder.
 * Every note can be dismissed in one tap.
 *
 * `version` lets a form elsewhere on the page (asking for a behaviour, resolving
 * a decision) make this list re-read after it writes a note.
 *
 * BEH-NOTE-9/10 (his 2026-10-16 notes): newest first, ten at a time. And a note a
 * behaviour already cites in its source line (`citations`) is FOLDED: it leaves the
 * open notes for a list of its own, so "open" means nobody has picked it up yet. Only
 * the project page knows the corpus, so only it passes `citations`.
 */
export default function NotesPanel({
  app,
  behaviour,
  version = 0,
  citations,
  onOpenCount,
}: {
  app: string
  behaviour?: string
  version?: number
  citations?: Map<string, Citation[]>
  /** Told how many notes are still open, for a tab label (BEH-FEAT-7). */
  onOpenCount?: (n: number) => void
}) {
  const { resource, reload } = useReloadableResource(() => fetchNotes(app), [app, version])
  const [text, setText] = useState('')
  const { write, run } = useWrite(reload)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await leaveNote(app, text, behaviour)
      setText('')
      return result
    })
  }

  // Each note keeps its place in the FILE: that index is how a reply or a dismissal names it.
  // Newest first (BEH-NOTE-9): the file is oldest first, so the list is read backwards.
  const mine = (notes: Note[]) =>
    notes
      .map((note, index) => ({ note, index, folded: foldedBy(note, citations) }))
      .filter(({ note }) => !behaviour || note.behaviour === behaviour)
      .reverse()
  const open = resource.state === 'ready' ? mine(resource.value.notes).filter((p) => !p.folded.length).length : null
  useEffect(() => {
    if (open !== null) onOpenCount?.(open)
  }, [open, onOpenCount])

  return (
    <>
      <ResourceView resource={resource}>
        {(list) => (
          <NeedsAnswer app={app} notes={mine(list.notes).filter(({ note, folded }) => note.questions.length > 0 && !folded.length)} onChanged={reload} />
        )}
      </ResourceView>

      <section aria-labelledby="open-notes">
        <h2 id="open-notes">Notes</h2>
        <ResourceView resource={resource}>
          {(list) => (
            <OpenNotes
              app={app}
              notes={mine(list.notes).filter(({ note, folded }) => note.questions.length === 0 && !folded.length)}
              file={list.file}
              onChanged={reload}
            />
          )}
        </ResourceView>

        <form onSubmit={submit} className="write">
          <label htmlFor="note-text">
            {behaviour ? `Leave a note on ${behaviour}` : 'Leave a note'}
          </label>
          <textarea
            id="note-text"
            className="note-text"
            placeholder="Anything — it doesn't have to be a behaviour yet."
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <Button type="submit" disabled={write.state === 'saving' || text.trim() === ''}>
            {write.state === 'saving' ? 'Saving…' : 'Leave note'}
          </Button>
          {write.state === 'refused' && (
            <Card elevation="flat">
              <p role="alert">{write.message}</p>
            </Card>
          )}
          {write.state === 'wrote' && <WriteResultNote result={write.result} subject="a note" />}
        </form>
      </section>

      <ResourceView resource={resource}>
        {(list) => <Folded app={app} notes={mine(list.notes).filter(({ folded }) => folded.length > 0)} onChanged={reload} />}
      </ResourceView>
    </>
  )
}

type Placed = { note: Note; index: number; folded: Citation[] }

/** BEH-NOTE-9: the first ten of a list already in order, and a button for ten more. */
function Capped({ items, render }: { items: Placed[]; render: (p: Placed) => React.ReactNode }) {
  const [limit, setLimit] = useState(CAP)
  const hidden = items.length - limit
  return (
    <>
      <ul className="cards">
        {items.slice(0, limit).map((p) => (
          <li key={`${p.note.at}:${p.index}`}>{render(p)}</li>
        ))}
      </ul>
      {hidden > 0 && (
        <Button type="button" variant="ghost" size="sm" onClick={() => setLimit(limit + CAP)}>
          Show {Math.min(CAP, hidden)} more ({hidden} hidden)
        </Button>
      )}
    </>
  )
}

/**
 * BEH-NOTE-10: notes a behaviour already cites. The fold is a pull request into dev and the
 * notes file lives on the branch Kit writes to, so the fold cannot delete the note; this list
 * is where it waits, with the behaviours it became one tap away.
 *
 * BEH-NOTE-11 (his 2026-10-16 11:53Z note): "auto archive them … and then have an option to
 * show archived". So the list is shut until he asks, and Dismiss still deletes one for good.
 */
function Folded({ app, notes, onChanged }: { app: string; notes: Placed[]; onChanged: () => void }) {
  const [shown, setShown] = useState(false)
  if (!notes.length) return null
  return (
    <section aria-labelledby="folded-notes">
      <h2 id="folded-notes">Archived</h2>
      <p className="muted">Notes a behaviour cites now, so they are folded into the spec.</p>
      <Button type="button" variant="ghost" size="sm" aria-expanded={shown} onClick={() => setShown(!shown)}>
        {shown ? 'Hide archived' : `Show archived (${notes.length})`}
      </Button>
      {shown && (
        <Capped
          items={notes}
          render={({ note, index, folded }) => (
            <NoteCard app={app} note={note} index={index} onChanged={onChanged} folded={folded} />
          )}
        />
      )}
    </section>
  )
}

/**
 * BEH-NOTE-5: absent when nothing is waiting. An empty "Needs your answer" on
 * every project page is exactly the scrolling he asked to be rid of, and a list
 * that failed to load still says so through `ResourceView`.
 */
function NeedsAnswer({ app, notes, onChanged }: { app: string; notes: Placed[]; onChanged: () => void }) {
  if (!notes.length) return null

  return (
    <section aria-labelledby="needs-answer">
      <h2 id="needs-answer">Needs your answer</h2>
      <Capped items={notes} render={({ note, index }) => <NoteCard app={app} note={note} index={index} onChanged={onChanged} />} />
    </section>
  )
}

function OpenNotes({ app, notes, file, onChanged }: { app: string; notes: Placed[]; file: string; onChanged: () => void }) {
  if (!notes.length) {
    // BEH-NOTE-3: says so rather than vanishing — an empty region and a broken one look the same.
    return <p className="muted">No open notes.</p>
  }

  return (
    <>
      <div className="badges">
        <Count n={notes.length} one="open note" />
      </div>
      <p className="muted">
        In <code>{file}</code>. Once a behaviour cites one, it is archived below.
      </p>
      <Capped items={notes} render={({ note, index }) => <NoteCard app={app} note={note} index={index} onChanged={onChanged} />} />
    </>
  )
}

function NoteCard({
  app,
  note,
  index,
  onChanged,
  folded = [],
}: {
  app: string
  note: Note
  index: number
  onChanged: () => void
  folded?: Citation[]
}) {
  const answer = useWrite(onChanged)
  const dismiss = useWrite(onChanged)
  const [text, setText] = useState('')
  const replyId = useId()

  async function reply(e: React.FormEvent) {
    e.preventDefault()
    await answer.run(async () => {
      const result = await replyToNote(app, index, note.at, text)
      setText('')
      return result
    })
  }

  const busy = answer.write.state === 'saving' || dismiss.write.state === 'saving'

  return (
    <Card elevation="flat">
      <div className="badges">
        <Badge tone="neutral">{note.at}</Badge>
        {note.kind === 'request' && <Badge tone="primary">asks for a behaviour</Badge>}
        {note.kind === 'resolution' && <Badge tone="warning">resolves {note.resolves}</Badge>}
        {note.behaviour && <Badge tone="primary">{note.behaviour}</Badge>}
      </div>
      <p className="note-body">{note.text}</p>
      {folded.length > 0 && (
        <p className="muted">
          Became{' '}
          {folded.map((c, i) => (
            <span key={`${c.app}/${c.id}`}>
              {i > 0 && ', '}
              <Link to={`/projects/${encodeURIComponent(c.app)}/behaviours/${encodeURIComponent(c.id)}`}>{c.title}</Link>
            </span>
          ))}
        </p>
      )}

      {/* Earlier rounds stay visible, so an answer is read beside what it answered. */}
      {note.thread.slice(0, note.questions.length ? -1 : undefined).map((entry, i) => (
        <p key={i} className="note-body muted">
          {entry.kind === 'answer' ? 'You answered: ' : 'Asked: '}
          {entry.text}
        </p>
      ))}

      {note.questions.length > 0 && (
        <form onSubmit={reply} className="write">
          <ol>
            {note.questions.map((q, i) => (
              <li key={i}>{q}</li>
            ))}
          </ol>
          <label htmlFor={replyId}>Your answer</label>
          <textarea id={replyId} className="note-text" value={text} onChange={(e) => setText(e.target.value)} />
          <Button type="submit" disabled={busy || text.trim() === ''}>
            {answer.write.state === 'saving' ? 'Sending…' : 'Reply'}
          </Button>
        </form>
      )}

      {/* BEH-NOTE-7: one tap and no confirm — git history is the undo. */}
      {/* Ghost, not filled: the quiet way out should never outshout the answer above it. */}
      <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={() => dismiss.run(() => dismissNote(app, index, note.at))}>
        {dismiss.write.state === 'saving' ? 'Dismissing…' : 'Dismiss'}
      </Button>
      {answer.write.state === 'refused' && <p role="alert">{answer.write.message}</p>}
      {dismiss.write.state === 'refused' && <p role="alert">{dismiss.write.message}</p>}
    </Card>
  )
}
