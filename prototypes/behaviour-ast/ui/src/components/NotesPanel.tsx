import { useId, useState } from 'react'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { dismissNote, fetchNotes, leaveNote, replyToNote } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Note } from '../api/types'
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
 */
export default function NotesPanel({ app, behaviour, version = 0 }: { app: string; behaviour?: string; version?: number }) {
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
  const mine = (notes: Note[]) =>
    notes.map((note, index) => ({ note, index })).filter(({ note }) => !behaviour || note.behaviour === behaviour)

  return (
    <>
      <ResourceView resource={resource}>
        {(list) => <NeedsAnswer app={app} notes={mine(list.notes).filter(({ note }) => note.questions.length > 0)} onChanged={reload} />}
      </ResourceView>

      <section aria-labelledby="open-notes">
        <h2 id="open-notes">Notes</h2>
        <ResourceView resource={resource}>
          {(list) => (
            <OpenNotes
              app={app}
              notes={mine(list.notes).filter(({ note }) => note.questions.length === 0)}
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
    </>
  )
}

type Placed = { note: Note; index: number }

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
      <ul className="cards">
        {[...notes].reverse().map(({ note, index }) => (
          <li key={`${note.at}:${index}`}>
            <NoteCard app={app} note={note} index={index} onChanged={onChanged} />
          </li>
        ))}
      </ul>
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
        In <code>{file}</code>. Each one is deleted when it is folded into the spec.
      </p>
      <ul className="cards">
        {notes.map(({ note, index }) => (
          <li key={`${note.at}:${index}`}>
            <NoteCard app={app} note={note} index={index} onChanged={onChanged} />
          </li>
        ))}
      </ul>
    </>
  )
}

function NoteCard({ app, note, index, onChanged }: { app: string; note: Note; index: number; onChanged: () => void }) {
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
