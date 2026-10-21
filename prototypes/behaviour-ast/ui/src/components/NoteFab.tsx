import { useEffect, useId, useState } from 'react'
import { matchPath, useLocation } from 'react-router-dom'
import { Button, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProjects, leaveNote } from '../api/client'
import { useResource } from '../hooks/useResource'
import PublishedHint from './PublishedHint'
import { useWrite } from './useWrite'
import WriteResultNote from './WriteResultNote'

/**
 * Fired on `window` after a note is written from here, so a NotesPanel already on the
 * page re-reads and shows it. Without it the button says "saved" while the list
 * under it still lacks the note.
 */
export const NOTE_WRITTEN = 'kit:note-written'

/** The project, and the behaviour if there is one, that the current URL names. */
function pageItem(pathname: string): { app?: string; behaviour?: string } {
  const behaviour = matchPath('/projects/:app/behaviours/:id', pathname)
  if (behaviour) return { app: behaviour.params.app, behaviour: behaviour.params.id }
  const project = matchPath('/projects/:app/*', pathname)
  return { app: project?.params.app }
}

/**
 * BEH-NOTE-12 (his 2026-10-19 19:33Z note): "from anywhere there is a floating action
 * button which can add a note". It sits on every page. The note lands on the project
 * the page is about, and on its behaviour on a behaviour's page (as NotesPanel's form
 * does there); on a page with no project (the board, activity) it asks which.
 */
export default function NoteFab() {
  const { pathname } = useLocation()
  const here = pageItem(pathname)
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [chosen, setChosen] = useState('')
  const { write, run, reset } = useWrite(() => window.dispatchEvent(new Event(NOTE_WRITTEN)))
  const id = useId()

  // A new page is a new subject: what the last page's box said no longer applies.
  useEffect(() => {
    setOpen(false)
    reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname])

  const app = here.app ?? chosen

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await leaveNote(app, text, here.behaviour)
      setText('')
      return result
    })
  }

  return (
    <div className="note-fab">
      {open && (
        <Card elevation="raised" className="note-fab-panel">
          <form onSubmit={submit} className="write" aria-label="Add a note">
            {here.app ? (
              <label htmlFor={`${id}-text`}>
                {here.behaviour ? `Note on ${here.behaviour}` : `Note on ${here.app}`}
              </label>
            ) : (
              <ProjectPicker id={`${id}-project`} value={chosen} onChange={setChosen} />
            )}
            <textarea
              id={`${id}-text`}
              className="note-text"
              aria-label={here.app ? undefined : 'Note'}
              placeholder="Anything — it doesn't have to be a behaviour yet."
              aria-describedby={`${id}-published`}
              value={text}
              onChange={(e) => setText(e.target.value)}
              autoFocus
            />
            <PublishedHint id={`${id}-published`} />
            <Button type="submit" disabled={write.state === 'saving' || text.trim() === '' || app === ''}>
              {write.state === 'saving' ? 'Saving…' : 'Leave note'}
            </Button>
            {write.state === 'refused' && <p role="alert">{write.message}</p>}
            {write.state === 'wrote' && <WriteResultNote result={write.result} subject="a note" />}
          </form>
        </Card>
      )}
      <Button
        type="button"
        className="note-fab-button"
        aria-expanded={open}
        onClick={() => {
          setOpen(!open)
          reset()
        }}
      >
        {open ? 'Close' : '✎ Note'}
      </Button>
    </div>
  )
}

function ProjectPicker({ id, value, onChange }: { id: string; value: string; onChange: (app: string) => void }) {
  const projects = useResource(() => fetchProjects(), [])
  return (
    <>
      <label htmlFor={id}>Which project is this about?</label>
      <select id={id} className="note-select" value={value} onChange={(e) => onChange(e.target.value)} disabled={projects.state !== 'ready'}>
        <option value="">{projects.state === 'error' ? `Could not list projects: ${projects.message}` : 'Choose a project'}</option>
        {projects.state === 'ready' &&
          projects.value.projects
            .filter((p) => !p.error)
            .map((p) => (
              <option key={p.app} value={p.app}>
                {p.app}
              </option>
            ))}
      </select>
    </>
  )
}
