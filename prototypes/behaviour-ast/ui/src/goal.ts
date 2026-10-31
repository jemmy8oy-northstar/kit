import type { ProjectDetail } from './api/types'
import { epicCards, type EpicCard } from './epics'

/** BEH-NARR-1: what a project is working towards. `goal` is null when every epic is built. */
export interface Goal {
  goal: EpicCard | null
}

/**
 * The project's current goal, or null when it declares no epic (nothing to say yet).
 * An epic is open while any behaviour beneath its features is pending; the goal is the open
 * epic with the best (lowest) `priority`, then the first in the corpus. Epics do not carry a
 * priority in the grammar yet, so today file order decides.
 */
export function goalOf(files: ProjectDetail[]): Goal | null {
  const cards = files.flatMap((f) =>
    epicCards(f.app, f.project, f.epics ?? [], f.features ?? [], f.behaviours),
  )
  if (cards.length === 0) return null
  let best: EpicCard | null = null
  for (const card of cards) {
    if (card.built >= card.total) continue
    if (!best || (card.epic.priority ?? Infinity) < (best.epic.priority ?? Infinity)) best = card
  }
  return { goal: best }
}
