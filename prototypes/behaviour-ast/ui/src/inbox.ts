import type { Behaviour, ProjectDetail, ProjectSummary } from './api/types'
import { byPriority, columnOf, isIdea, isRung, isTrial, ownerOf, projectOf, type Place } from './board'
import { goalOf } from './goal'

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
  detail?: ProjectDetail
}

/** Today's local date as YYYY-MM-DD, the shape `pausedUntil` compares in. */
export function localDate(d: Date = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/**
 * BEH-NARR-2: the question a project with no goal is asked, once. Not a behaviour, so it has no `b`;
 * `app` is the project's primary file, where "maintained" and the answer's note are written.
 */
export interface GoalCard {
  goalFor: string
  app: string
  project: string
}

/** Anything the inbox queues: a behaviour card, or a project's goal question. */
export type QueueCard = InboxCard | GoalCard

export const isGoalCard = (c: QueueCard): c is GoalCard => 'goalFor' in c

/** Stable and distinct from a behaviour's `app/id`: no app name carries a colon. */
export const keyOf = (c: QueueCard) => (isGoalCard(c) ? `goal:${c.goalFor}` : `${c.app}/${c.b.id}`)

/**
 * BEH-NARR-2 (his "if there aren't any big goals the ai can probe to find out the direction"): one
 * question for every project that has no goal epic (BEH-NARR-1's rule, `goalOf`) and is not marked
 * maintained. A project whose primary file is missing, or any of whose files failed to load, is not
 * asked: there is nowhere to write the answer, or no way to know it has no epic. Ideas and trials
 * are not projects, as in `mine`.
 */
export function goalQuestions(entries: Source[], answered: ReadonlySet<string> = new Set()): GoalCard[] {
  const byProject = new Map<string, Source[]>()
  for (const e of entries) {
    if (isIdea(e.summary) || isTrial(e.summary)) continue
    const p = projectOf(e.summary)
    byProject.set(p, [...(byProject.get(p) ?? []), e])
  }
  return [...byProject].flatMap(([project, files]) => {
    if (answered.has(project)) return []
    if (files.some((f) => f.error || !f.detail)) return []
    if (files.some((f) => f.summary.goal === 'maintained')) return []
    if (!files.some((f) => f.summary.app === project)) return []
    if (goalOf(files.map((f) => f.detail!))?.goal) return []
    return [{ goalFor: project, app: project, project }]
  })
}

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
const answerFirst = (c: QueueCard) => (!isGoalCard(c) && c.b.review.state === 'requested' ? 0 : 1)

/** A goal question has no score, so it ranks after every scored card in its group. */
const priorityOf = (c: QueueCard) => (isGoalCard(c) ? undefined : c.b.priority)

/** Cards not yet opened first, answers to him first, then highest priority; then the rest the same way. Ties keep their order. */
export function ordered<T extends QueueCard>(cards: T[], seen: ReadonlySet<string>): T[] {
  const by = (list: T[]) => [...list].sort((x, y) => answerFirst(x) - answerFirst(y) || byPriority(priorityOf(x), priorityOf(y)))
  return [...by(cards.filter((c) => !seen.has(keyOf(c)))), ...by(cards.filter((c) => seen.has(keyOf(c))))]
}

/** Lower priority is a higher number, and 5 is as low as it goes. A card with no score already ranks after every scored one, so it goes to 5. */
export function lowered(priority: number | undefined): number {
  return priority === undefined ? 5 : Math.min(5, priority + 1)
}

const KEY = 'kit.inboxSeen'
const ANSWERED = 'kit.goalAnswered'

/**
 * The projects whose goal question he answered with words (BEH-NARR-2). The note only becomes a goal
 * epic once the agent has turned it into spec, so without this the question would come straight back.
 * Kept in this browser, like "seen"; "Just maintaining it" needs none, the corpus line is the record.
 */
export function readAnswered(): Set<string> {
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(ANSWERED) ?? '[]')
    return new Set(Array.isArray(parsed) ? parsed.filter((k): k is string => typeof k === 'string') : [])
  } catch {
    return new Set()
  }
}

export function saveAnswered(project: string) {
  try {
    window.localStorage.setItem(ANSWERED, JSON.stringify([...readAnswered().add(project)]))
  } catch {
    // A browser that refuses storage asks again next visit.
  }
}

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
