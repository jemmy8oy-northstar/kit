import type { ProjectDetail } from './api/types'
import { epicCards, type EpicCard } from './epics'

/** BEH-NARR-1: what a project is working towards. `goal` is null when every epic is built. */
export interface Goal {
  goal: EpicCard | null
}

/**
 * The project's current goal, or null when it declares no epic (nothing to say yet).
 * An epic is open while any behaviour beneath its features is pending; the goal is the first
 * open epic in the corpus. Epics carry no priority in the grammar, so file order decides.
 */
export function goalOf(files: ProjectDetail[]): Goal | null {
  const cards = files.flatMap((f) =>
    epicCards(f.app, f.project, f.epics ?? [], f.features ?? [], f.behaviours),
  )
  if (cards.length === 0) return null
  return { goal: cards.find((card) => card.built < card.total) ?? null }
}
