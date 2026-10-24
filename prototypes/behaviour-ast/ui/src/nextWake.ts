import type { AgentSchedule } from './api/types'

/** An instant in the reader's locale; an unparseable stamp is shown as it came. */
export function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
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
