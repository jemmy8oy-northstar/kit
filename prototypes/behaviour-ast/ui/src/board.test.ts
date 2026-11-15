import { describe, expect, it } from 'vitest'
import { columnOf } from './board'
import type { Behaviour } from './api/types'

function beh(over: Partial<Behaviour> = {}): Behaviour {
  return {
    id: 'BEH-EPIC-KID-1',
    title: 'a child of an epic',
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'inferred', ref: null },
    review: { state: 'unreviewed', note: null },
    asks: null,
    pending: true,
    layer: 'ux',
    at: 'x.beh:1',
    in: ['FEAT-EPIC-CHILD'],
    ...over,
  }
}

describe('where an epic’s drafted children stand on the board (BEH-EPIC-5, BEH-EPIC-6)', () => {
  it('puts a child Kit drafted under an epic in Proposed, pending and not built (BEH-EPIC-6)', () => {
    const drafted = beh()
    expect(drafted.pending).toBe(true)
    expect(columnOf(drafted)).toBe('proposed')
  })

  it('moves the drafted child to the queue once he approves it, still pending (BEH-EPIC-5, BEH-EPIC-6)', () => {
    const approved = beh({ review: { state: 'approved', note: null } })
    expect(columnOf(approved)).toBe('queue')
  })

  it('leaves a drafted child he denied on no column, so it is not work (BEH-EPIC-6)', () => {
    const denied = beh({ review: { state: 'denied', note: 'wrong split' } })
    expect(columnOf(denied)).toBeNull()
  })
})
