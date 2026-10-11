import { useState } from 'react'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { fetchNotes, leaveNote } from '../api/client'
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
 */
export default function NotesPanel({ app, behaviour }: { app: string; behaviour?: string }) {
  const { resource, reload } = useReloadableResource(() => fetchNotes(app), [app])
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

  return (
    <section aria-labelledby="open-notes">
      <h2 id="open-notes">Notes</h2>
      <ResourceView resource={resource}>
        {(list) => <OpenNotes notes={list.notes.filter((n) => !behaviour || n.behaviour === behaviour)} file={list.file} />}
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
  )
}

function OpenNotes({ notes, file }: { notes: Note[]; file: string }) {
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
        {notes.map((n, i) => (
          <li key={`${n.at}:${i}`}>
            <Card elevation="flat">
              <div className="badges">
                <Badge tone="neutral">{n.at}</Badge>
                {n.behaviour && <Badge tone="primary">{n.behaviour}</Badge>}
              </div>
              <p className="note-body">{n.text}</p>
            </Card>
          </li>
        ))}
      </ul>
    </>
  )
}
