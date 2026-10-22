import type { Behaviour } from './api/types'

/**
 * More contracts than this and a behaviour is probably a feature (BEH-EPIC-9). Measured on
 * 2026-10-22 across every corpus: 2 of 282 behaviours are above six. "More than one page" was
 * the other rule I assumed, and it flagged only sign-in-then-dashboard flows, so it is not one.
 */
export const MOST_CONTRACTS = 6

/** Shorter than this, unpunctuated, and followed by a lowercase line: one sentence, hard-wrapped. */
const WRAP = 75

/**
 * Contracts counted as sentences, not lines. kit-hosted.beh wraps one sentence over several
 * `contract` lines, which counted as lines put eight on a behaviour that says three things.
 */
export function contractCount(b: Behaviour): number {
  let n = 0
  let prev: string | null = null
  for (const s of b.steps) {
    if (s.kind !== 'contract') {
      prev = null
      continue
    }
    const wrapped = prev !== null && prev.length <= WRAP && !/[.;:!?"')\]]$/.test(prev) && /^[a-z(]/.test(s.text)
    if (!wrapped) n++
    prev = s.text
  }
  return n
}

/**
 * Why a behaviour looks like a feature, or null when it does not (BEH-EPIC-9). Kit only spots
 * it; the agent proposes the split as a review request, and nothing splits on its own.
 */
export function broadness(b: Behaviour): string | null {
  if (b.review.state === 'denied' || b.review.state === 'requested') return null
  const contracts = contractCount(b)
  return contracts > MOST_CONTRACTS ? `${contracts} contracts, more than ${MOST_CONTRACTS}` : null
}
