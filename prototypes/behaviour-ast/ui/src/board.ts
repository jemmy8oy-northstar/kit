import type { Behaviour, ProjectSummary } from './api/types'

/**
 * The five columns kit#237 asked for, left to right, after kit#284's review requests; scheduled
 * and in progress read the schedule (BEH-BOARD-3).
 */
export type Column = 'review' | 'proposed' | 'backlog' | 'scheduled' | 'in-progress' | 'done'

/** Where a pending behaviour sits on its file's schedule (BEH-SCHED-1/3), if it is on it at all. */
export interface Place {
  /** Its position on the schedule, first first. */
  order: number
  /** The top still-pending entry: the one an agent picks up. */
  next: boolean
}

/**
 * Where a behaviour stands (BEH-BOARD-1), from what the corpus and the schedule say.
 * A denied behaviour is on no column: it is a correction waiting to be written,
 * not work. A review request (BEH-BOARD-6) is the agent asking him to look at
 * something, so it waits in Review requested whatever else is true of it until he
 * approves or denies it. Proposed is checked before anything pending, so an unreviewed inference
 * is proposed even when scheduled — someone has to adjudicate it before it is work.
 * A pending behaviour is in progress when it is its schedule's next (BEH-BOARD-3),
 * scheduled when it is anywhere else on it, and backlog otherwise. A built one is
 * done, whatever the schedule still says.
 */
export function columnOf(b: Behaviour, place?: Place): Column | null {
  if (b.review.state === 'denied') return null
  if (b.review.state === 'requested') return 'review'
  if (b.source.origin === 'inferred' && b.review.state === 'unreviewed') return 'proposed'
  if (!b.pending) return 'done'
  if (place?.next) return 'in-progress'
  if (place) return 'scheduled'
  return 'backlog'
}

/**
 * When a done card was completed (BEH-BOARD-4): an ISO time; null when the corpus is built ahead
 * of its newest commit; undefined when its project has no history on this server.
 */
export type Completed = string | null | undefined

/**
 * Done's order (BEH-BOARD-4): most recently completed first. Built ahead of the history is the
 * newest of all, and a card with no history goes after every dated one. Use it with a stable
 * sort, so ties keep corpus order.
 */
export function byCompletion(a: Completed, b: Completed): number {
  const rank = (c: Completed) => (c === null ? 0 : c === undefined ? 2 : 1)
  const r = rank(a) - rank(b)
  if (r !== 0 || a == null || b == null) return r
  // ISO 8601 in UTC compares as text.
  return a < b ? 1 : a > b ? -1 : 0
}

/**
 * Cards a column shows before "Show more" (BEH-BOARD-7, kit#284: "we are going to need limits on
 * the board… with a load more"). Ten fills a phone screen about three times over.
 */
export const CAP = 10

/** The columns ordered by priority (BEH-BOARD-5/6): what is waiting on a decision or a pick. */
export const RANKED: readonly Column[] = ['review', 'proposed', 'backlog']

/**
 * Priority order for Array.sort (BEH-BOARD-5, kit#284): 1 first, 5 last, and a card with no
 * priority after every card with one. Equal is 0, so the stable sort keeps corpus order among them.
 */
export function byPriority(a?: number, b?: number): number {
  if (a === undefined || b === undefined) return a === b ? 0 : a === undefined ? 1 : -1
  return a - b
}

/** The number of colours in `.hue-N` in index.css. */
export const HUES = 12

/** A project's colour (BEH-BOARD-2): a hash of its name, so it never changes between visits. */
export function hueOf(app: string): number {
  let h = 0
  for (const ch of app) h = (h * 31 + ch.charCodeAt(0)) >>> 0
  return h % HUES
}

/** The project a row is shown under (kit#237): its `# kit:project`, or its own name. */
export function projectOf(p: ProjectSummary): string {
  return p.project ?? p.app
}

/** The Ideas corpus's name (kit#284 slice 4): the nav links to it, so it is one well-known file. */
export const IDEAS = 'ideas'

/** An Ideas file (`# kit:idea`): apps that are not projects yet. Never on the board or the project list. */
export function isIdea(p: ProjectSummary): boolean {
  return Boolean(p.idea)
}

/** Trial corpora: an app that does not exist, or a second spec of one that does. */
export function isTrial(p: ProjectSummary): boolean {
  return Boolean(p.notReal || p.duplicateOf)
}
