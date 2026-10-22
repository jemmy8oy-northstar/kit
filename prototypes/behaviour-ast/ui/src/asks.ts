import type { Behaviour, ProjectDetail } from './api/types'
import { isQuestion } from './board'

/** One thing the agent is asking him, on the behaviour it is about. */
export interface Ask {
  app: string
  b: Behaviour
  /** The question, in the agent's words. */
  text: string
}

/**
 * BEH-SHEET-2 (his 2026-10-22 22:46Z and 22:48Z notes: "The questions tab doesn't really show
 * questions… kit starts to raise the questions which helps guide the user"): what the agent is
 * asking him, across a project's files. A needs-context line on a card still to build (BEH-BOARD-17)
 * is one, unless he paused the card; so is a review request whose note is a question (BEH-BOARD-11).
 * A denied card asks nothing. Corpus order, file by file.
 */
export function asksOf(files: ProjectDetail[]): Ask[] {
  return files.flatMap((f) =>
    f.behaviours.flatMap((b): Ask[] => {
      if (b.review.state === 'denied') return []
      if (isQuestion(b)) return [{ app: f.app, b, text: b.review.note!.trim() }]
      if (b.pending && !b.paused && b.needsContext) return [{ app: f.app, b, text: b.needsContext }]
      return []
    }),
  )
}
