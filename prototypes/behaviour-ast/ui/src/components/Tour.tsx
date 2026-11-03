import { useEffect, useRef, useState } from 'react'
import { Button, Card } from '@jemmy8oy-northstar/design-system'

import { markTourSeen } from './tourStore'

/** The same ideas as the help in Chat, in a few plain steps. */
const STEPS: { title: string; text: string }[] = [
  {
    title: 'Projects',
    text: "This is where you start. Each card is one of your projects, with its test health: how much of what the spec says is checked by a test.",
  },
  {
    title: 'The Board and the Queue',
    text: "A project's spec is shown as cards. Proposed ideas wait for your say, the Queue is what the coding agent builds next, Blocked needs something from you, and Done is built.",
  },
  {
    title: 'The Inbox',
    text: 'Everything that needs you, one item at a time. Approve it, deny it, or say what is true instead.',
  },
  {
    title: 'Chat',
    text: 'Talk the spec through with the spec agent, or design a new project with it. The chat changes no code; the coding agent builds what the spec says.',
  },
  {
    title: 'Notes',
    text: 'A quick brain dump. Write whatever is on your mind and the coding agent files it into the spec for you.',
  },
  {
    title: 'Settings',
    text: 'Connect your GitHub here, choose the page Kit opens on, and replay this tour whenever you like.',
  },
]

/**
 * BEH-TOUR-1: a short walk round Kit for a new user, one step at a time. Skipping, finishing and
 * Escape all count as seen. `onClose` is called after it is remembered.
 */
export default function Tour({ onClose }: { onClose: () => void }) {
  const [index, setIndex] = useState(0)
  const heading = useRef<HTMLHeadingElement>(null)
  const last = index === STEPS.length - 1
  const step = STEPS[index]

  function close() {
    markTourSeen()
    onClose()
  }

  // Focus moves into the dialog on open, and to the heading at each step so a screen reader reads it.
  useEffect(() => {
    heading.current?.focus()
  }, [index])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="tour-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="tour-title" className="tour">
        <Card>
          <p className="muted">Step {index + 1} of {STEPS.length}</p>
          <h2 id="tour-title" ref={heading} tabIndex={-1}>{step.title}</h2>
          <p>{step.text}</p>
          <div className="tour-actions">
            {!last && <Button type="button" variant="ghost" onClick={close}>Skip tour</Button>}
            {index > 0 && <Button type="button" variant="secondary" onClick={() => setIndex(index - 1)}>Back</Button>}
            <Button type="button" onClick={last ? close : () => setIndex(index + 1)}>{last ? 'Done' : 'Next'}</Button>
          </div>
        </Card>
      </div>
    </div>
  )
}
