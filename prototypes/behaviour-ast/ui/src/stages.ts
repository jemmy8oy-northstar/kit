import type { Behaviour, Feature, Stage } from './api/types'
import { columnOf, isDenied } from './board'
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

/**
 * BEH-STAGE-7: a feature with no `stage` line sits in an Unstaged column, first, before problem,
 * since staging a feature moves it rightwards out of it. It is a column, not a `Stage`: the corpus
 * has no such word, so it is not in STAGES, which the parser must agree with.
 */
export const UNSTAGED = 'unstaged'
/**
 * BEH-STAGE-12: Done is the last column. Like Unstaged it is a column and not a `Stage`: a feature is
 * Done by what is built beneath it, never by a written word, so the parser needs no new stage value.
 */
export const DONE = 'done'
export const COLUMNS: { id: Stage | typeof UNSTAGED | typeof DONE; title: string }[] = [
  { id: UNSTAGED, title: 'Unstaged' },
  ...STAGES,
  { id: DONE, title: 'Done' },
]

/** One feature's card on the stages board. */
export interface StageCard {
  app: string
  project: string
  feature: Feature
  /** Behaviours beneath it that are built (not `pending`), out of all beneath it (BEH-STAGE-3). */
  built: number
  total: number
  /** BEH-STAGE-12: it has behaviours and every one is Done on the behaviour board. */
  done: boolean
}

/** The column a card sits in: Done when everything beneath it is built (BEH-STAGE-12), else its written stage. */
export function columnFor(card: StageCard): string {
  return card.done ? DONE : (card.feature.stage ?? UNSTAGED)
}

/**
 * A file's cards: every feature, the one with no `stage` line in the Unstaged column
 * (BEH-STAGE-7), so it can be found and staged.
 */
export function stageCards(app: string, project: string, features: Feature[], behaviours: Behaviour[]): StageCard[] {
  const byId = new Map(behaviours.map((b) => [b.id, b]))
  return features.map((feature) => {
    const under = [...behavioursUnder({ features, behaviours }, feature.id)]
    // BEH-STAGE-12: Done is the behaviour board's own Done (columnOf), so paused, blocked, proposed or
    // denied work beneath a feature keeps it out of Done. A denied card is not work (BEH-BOARD-21), so
    // it neither blocks Done nor counts towards it.
    const live = under.filter((id) => !(byId.has(id) && isDenied(byId.get(id)!)))
    const done = live.length > 0 && live.every((id) => byId.has(id) && columnOf(byId.get(id)!) === 'done')
    return { app, project, feature, built: under.filter((id) => !byId.get(id)?.pending).length, total: under.length, done }
  })
}
