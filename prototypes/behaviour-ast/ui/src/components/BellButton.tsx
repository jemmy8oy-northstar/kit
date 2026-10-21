import { Button } from '@jemmy8oy-northstar/design-system'
import { fetchNotes, ringBell } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import { WriteFeedback } from '../pages/BehaviourPage'
import { useWrite } from './useWrite'

/**
 * BEH-BELL-1 (his 2026-10-19 19:33Z note): ring an epic's, feature's or behaviour's bell so the agent
 * looks at it again. A rung item says so and cannot be rung twice: the bell stays in the notes until
 * the agent answers it (BEH-BELL-2). Until the notes are read the button waits, so it never offers
 * a ring the server would refuse.
 */
export default function BellButton({ app, item }: { app: string; item: string }) {
  const { resource, reload } = useReloadableResource(() => fetchNotes(app), [app])
  const { write, run } = useWrite(reload)

  const rung = resource.state === 'ready' && resource.value.notes.some((n) => n.kind === 'bell' && n.behaviour === item)
  const saving = write.state === 'saving'

  return (
    <div className="write bell">
      <Button
        type="button"
        variant="ghost"
        size="sm"
        disabled={resource.state !== 'ready' || rung || saving}
        onClick={() => run(() => ringBell(app, item))}
      >
        {rung ? '🔔 Rung, waiting for Kit' : saving ? 'Ringing…' : '🔔 Ring the bell'}
      </Button>
      {!rung && resource.state === 'ready' && (
        <span className="muted"> Asks Kit to look at {item} again: it either raises its priority or says why not.</span>
      )}
      {resource.state === 'error' && <span className="muted"> Could not read the notes: {resource.message}</span>}
      <WriteFeedback write={write} />
    </div>
  )
}
