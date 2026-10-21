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

/** A behaviour's `layer` values (kit#89), in the order he named them (BEH-KIND-1): how it looks, how it works, what is underneath. */
export const LAYERS = ['ui', 'ux', 'technical']

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

/**
 * BEH-BOARD-10 (his 2026-10-19 13:40Z note: "Currently it seems to be ordered by project"): a ranked
 * column by priority, and within one priority taking each project in turn, so equal scores never
 * read as a block of one project. BEH-BOARD-12 ("order by (score, most recent)"): a project's own
 * cards within one priority are most recently added first, by `at` read as {@link byCompletion}
 * reads it; ties, and a project without history, keep corpus order.
 */
export function byPriorityAcrossProjects<T extends { project: string; b: { priority?: number }; at?: Completed }>(cards: T[]): T[] {
  const tiers = new Map<number, T[]>()
  for (const c of cards) {
    const p = c.b.priority ?? 6
    tiers.set(p, [...(tiers.get(p) ?? []), c])
  }
  return [...tiers.keys()]
    .sort((a, b) => a - b)
    .flatMap((p) => {
      const queues = new Map<string, T[]>()
      const tier = [...tiers.get(p)!].sort((x, y) => byCompletion(x.at, y.at))
      for (const c of tier) queues.set(c.project, [...(queues.get(c.project) ?? []), c])
      const out: T[] = []
      for (let i = 0; out.length < tiers.get(p)!.length; i++) {
        for (const q of queues.values()) if (i < q.length) out.push(q[i])
      }
      return out
    })
}

/**
 * BEH-BELL-3 (his 2026-10-21 06:50Z note: "Bell ordered board items"): a rung card goes to the top of
 * its column, above every priority, until the agent answers the bell. Both halves keep the order they
 * came in, so rung cards are still by priority among themselves.
 */
export function rungFirst<T extends { rung: boolean }>(cards: T[]): T[] {
  return [...cards.filter((c) => c.rung), ...cards.filter((c) => !c.rung)]
}

/**
 * Whether a behaviour is rung (BEH-BELL-1): its own bell, or the bell of a feature it is in, so ringing
 * a feature lifts the cards that build it. `bells` holds the item ids of a file's unanswered bells.
 */
export function isRung(b: { id: string; in: string[] }, bells: ReadonlySet<string>): boolean {
  return bells.has(b.id) || b.in.some((f) => bells.has(f))
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
