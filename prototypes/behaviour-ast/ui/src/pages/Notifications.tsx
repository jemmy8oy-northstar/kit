import { fetchNotifications } from '../api/client'
import type { AgentNotification } from '../api/types'
import { useResource } from '../hooks/useResource'
import PushControl from '../components/PushControl'
import ResourceView from '../components/Resource'
import { nextWake, when } from '../nextWake'

const KIND: Record<AgentNotification['kind'], string> = {
  wake: 'Woke',
  sleep: 'Slept',
  pr: 'Pull request',
  blocker: 'Blocked',
  done: 'Done',
  note: 'Note',
}

/**
 * What the agent has told him (BEH-NOTIFY-1), newest first: the messages that used to arrive on
 * Telegram (claude-code-bot#118). A feed the server could not read shows the reason, never an
 * empty list, because "nothing to say" and "could not look" must not look alike.
 */
export default function Notifications() {
  const resource = useResource(fetchNotifications, [])

  return (
    <>
      <h1>Notifications</h1>
      <p className="muted">What the agent has told you, newest first: wakes, sleeps, pull requests and blockers.</p>

      <PushControl />

      <ResourceView resource={resource}>
        {({ entries, skipped, reason, schedule }) => (
          <>
            {schedule && (
              <section aria-label="Next wake">
                <p>{nextWake(schedule)}</p>
                {schedule.suspended && schedule.why !== null && <p className="muted">Why: {schedule.why}</p>}
              </section>
            )}

            {reason !== null ? (
              <p role="alert">The agent&apos;s feed could not be read: {reason}</p>
            ) : entries.length === 0 ? (
              <p>The agent has not said anything yet.</p>
            ) : (
              <ol aria-label="Notifications" className="activity">
                {entries.map((n, i) => (
                  <li key={`${n.at}:${i}`}>
                    {/* When and what kind on a muted line of their own, so on a phone the message reads first. */}
                    <div className="muted">
                      <time dateTime={n.at}>{when(n.at)}</time> · {KIND[n.kind] ?? KIND.note}
                    </div>
                    <span className="notification-text">{n.text}</span>
                  </li>
                ))}
              </ol>
            )}

            {skipped > 0 && (
              <p className="muted">
                {skipped} {skipped === 1 ? 'line' : 'lines'} of the feed could not be read as a message and {skipped === 1 ? 'is' : 'are'} not shown.
              </p>
            )}
          </>
        )}
      </ResourceView>
    </>
  )
}
