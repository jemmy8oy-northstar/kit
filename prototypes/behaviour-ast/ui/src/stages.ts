import type { Behaviour, Feature, Stage } from './api/types'
import { behavioursUnder } from './features'

/**
 * The stages board's columns (BEH-STAGE-1, his 2026-10-19 08:23Z note), in his order. The ids
 * are the corpus's `stage` words, so the parser and this list must agree.
 */
export const STAGES: { id: Stage; title: string }[] = [
  { id: 'problem', title: 'Problem statement' },
  { id: 'ux', title: 'UX requirements' },
  { id: 'logic', title: 'Business logic' },
  { id: 'development', title: 'Development' },
  { id: 'ui', title: 'UI requirements' },
]

/** One feature's card on the stages board. */
export interface StageCard {
  app: string
  project: string
  feature: Feature
  /** Behaviours beneath it that are built (not `pending`), out of all beneath it (BEH-STAGE-3). */
  built: number
  total: number
}

/**
 * A file's cards: every feature with a `stage` line. A feature without one is not on the board,
 * so the structural features that group pages (kit#237) never crowd it.
 */
export function stageCards(app: string, project: string, features: Feature[], behaviours: Behaviour[]): StageCard[] {
  const pending = new Map(behaviours.map((b) => [b.id, b.pending]))
  return features
    .filter((f) => f.stage !== undefined)
    .map((feature) => {
      const under = [...behavioursUnder({ features, behaviours }, feature.id)]
      return { app, project, feature, built: under.filter((id) => !pending.get(id)).length, total: under.length }
    })
}
