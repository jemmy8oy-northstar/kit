import type { Behaviour, ProjectSummary } from './api/types'

/** The five columns kit#237 asked for, left to right; scheduled and in progress read the schedule (BEH-BOARD-3). */
export type Column = 'proposed' | 'backlog' | 'scheduled' | 'in-progress' | 'done'

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
 * not work. Proposed is checked before anything pending, so an unreviewed inference
 * is proposed even when scheduled — someone has to adjudicate it before it is work.
 * A pending behaviour is in progress when it is its schedule's next (BEH-BOARD-3),
 * scheduled when it is anywhere else on it, and backlog otherwise. A built one is
 * done, whatever the schedule still says.
 */
export function columnOf(b: Behaviour, place?: Place): Column | null {
  if (b.review.state === 'denied') return null
  if (b.source.origin === 'inferred' && b.review.state === 'unreviewed') return 'proposed'
  if (!b.pending) return 'done'
  if (place?.next) return 'in-progress'
  if (place) return 'scheduled'
  return 'backlog'
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

/** Trial corpora: an app that does not exist, or a second spec of one that does. */
export function isTrial(p: ProjectSummary): boolean {
  return Boolean(p.notReal || p.duplicateOf)
}
