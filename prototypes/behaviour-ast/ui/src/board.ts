import type { Behaviour, ProjectSummary } from './api/types'

/** The three columns kit#237 asked for; scheduled and in progress wait on BEH-SCHED. */
export type Column = 'proposed' | 'backlog' | 'done'

/**
 * Where a behaviour stands (BEH-BOARD-1), from what the corpus already says.
 * A denied behaviour is on no column: it is a correction waiting to be written,
 * not work. Proposed is checked before backlog, so an unreviewed inference that
 * is also pending is proposed — someone has to adjudicate it before it is work.
 */
export function columnOf(b: Behaviour): Column | null {
  if (b.review.state === 'denied') return null
  if (b.source.origin === 'inferred' && b.review.state === 'unreviewed') return 'proposed'
  if (b.pending) return 'backlog'
  return 'done'
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
