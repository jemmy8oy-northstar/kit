import { fetchNotifications } from '../api/client'
import type { AgentNotification, AgentSchedule } from '../api/types'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'

/** The message's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

const KIND: Record<AgentNotification['kind'], string> = {
  wake: 'Woke',
  sleep: 'Slept',
  pr: 'Pull request',
  blocker: 'Blocked',
  done: 'Done',
  note: 'Note',
}

/**
 * When the agent next wakes (BEH-NOTIFY-5), from the pause file its gate reads before every
 * session. Whether a pause has ended is decided here, against the reader's clock, because the file
 * is read once a poll and the page is looked at for longer.
 */
export function nextWake(s: AgentSchedule, now: Date = new Date()): string {
  if (s.reason !== null) return `Kit cannot tell when the agent next wakes: ${s.reason}`
  if (!s.suspended) return 'The agent is not asleep: it starts a session within 5 minutes of its last one ending.'
  if (s.until === null) return 'The agent is paused with no end time: it wakes only when the pause is lifted.'
  if (new Date(s.until).getTime() <= now.getTime()) return `The agent's sleep ended ${when(s.until)}: it wakes within 5 minutes.`
  return s.byAgent
    ? `Next wake: ${when(s.until)}, or about 5 minutes after your next action in Kit.`
    : `Paused by you until ${when(s.until)}. Actions in Kit do not wake it.`
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
