import type { Behaviour, Epic, Feature } from './api/types'
import { behavioursUnder } from './features'

/** One epic's card on the epic board (BEH-EPIC-4). */
export interface EpicCard {
  app: string
  project: string
  epic: Epic
  /** The features that name it in an `in EPIC-…` line (BEH-EPIC-2), in file order. */
  features: Feature[]
  /** Behaviours beneath those features that are built (not `pending`), out of all beneath them, each counted once. */
  built: number
  total: number
}

/**
 * A file's epic cards. Progress is the tagged features' behaviours, at any depth, as a feature
 * in development counts it (BEH-STAGE-3); a behaviour under two of its features counts once.
 */
export function epicCards(
  app: string,
  project: string,
  epics: Epic[],
  features: Feature[],
  behaviours: Behaviour[],
): EpicCard[] {
  const pending = new Map(behaviours.map((b) => [b.id, b.pending]))
  return epics.map((epic) => {
    const tagged = features.filter((f) => f.epics?.includes(epic.id))
    const under = new Set(tagged.flatMap((f) => [...behavioursUnder({ features, behaviours }, f.id)]))
    const ids = [...under]
    return { app, project, epic, features: tagged, built: ids.filter((id) => !pending.get(id)).length, total: ids.length }
  })
}
