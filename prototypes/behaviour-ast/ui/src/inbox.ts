import type { Behaviour, ProjectSummary } from './api/types'
import { byPriority, columnOf, isIdea, isRung, isTrial, ownerOf, projectOf, type Place } from './board'

/** One card that is his move (BEH-INBOX-1). */
export interface InboxCard {
  app: string
  project: string
  b: Behaviour
}

/** What the inbox needs of a project: the board's own entry, so "whose move" is read the one way. */
interface Source {
  summary: ProjectSummary
  behaviours: Behaviour[]
  places: Map<string, Place>
  error: string | null
  bells: Set<string>
}

/** Today's local date as YYYY-MM-DD, the shape `pausedUntil` compares in. */
export function localDate(d: Date = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

export const keyOf = (c: InboxCard) => `${c.app}/${c.b.id}`

/**
 * Every card whose move is his, across every project (BEH-INBOX-1 on BEH-BOARD-17): `ownerOf` decides,
 * exactly as the board's 👤 badge does. Ideas and trial corpora are not work, as on the board.
 */
export function mine(entries: Source[], today: string = localDate()): InboxCard[] {
  return entries
    .filter((e) => !e.error && !isIdea(e.summary) && !isTrial(e.summary))
    .flatMap((e) =>
      e.behaviours
        // BEH-BOARD-26: a card paused until a later date is out of his Inbox till then; a bare `paused`, or a date reached, shows.
        .filter((b) => !(b.paused && b.pausedUntil && b.pausedUntil > today))
        .filter((b) => ownerOf(b, columnOf(b, e.places.get(b.id)), isRung(b, e.bells)) === 'james')
        .map((b) => ({ app: e.summary.app, project: projectOf(e.summary), b })),
    )
}

/** A review request is how the agent answers his question (BEH-INBOX-3), so it leads its group. */
const answerFirst = (c: InboxCard) => (c.b.review.state === 'requested' ? 0 : 1)

/** Cards not yet opened first, answers to him first, then highest priority; then the rest the same way. Ties keep their order. */
export function ordered(cards: InboxCard[], seen: ReadonlySet<string>): InboxCard[] {
  const by = (list: InboxCard[]) => [...list].sort((x, y) => answerFirst(x) - answerFirst(y) || byPriority(x.b.priority, y.b.priority))
  return [...by(cards.filter((c) => !seen.has(keyOf(c)))), ...by(cards.filter((c) => seen.has(keyOf(c))))]
}

/** Lower priority is a higher number, and 5 is as low as it goes. A card with no score already ranks after every scored one, so it goes to 5. */
export function lowered(priority: number | undefined): number {
  return priority === undefined ? 5 : Math.min(5, priority + 1)
}

const KEY = 'kit.inboxSeen'

/** The cards he has opened in this browser, like the opening page (BEH-SETTINGS-2): empty when storage is unavailable. */
export function readSeen(): Set<string> {
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(KEY) ?? '[]')
    return new Set(Array.isArray(parsed) ? parsed.filter((k): k is string => typeof k === 'string') : [])
  } catch {
    return new Set()
  }
}

export function saveSeen(seen: ReadonlySet<string>) {
  try {
    window.localStorage.setItem(KEY, JSON.stringify([...seen]))
  } catch {
    // A browser that refuses storage shows every card as unopened next time.
  }
}
