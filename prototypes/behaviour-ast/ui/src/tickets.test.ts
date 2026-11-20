import { describe, expect, it } from 'vitest'
import { ticketColumns } from './tickets'
import type { Ticket } from './api/types'

function ticket(over: Partial<Ticket> = {}): Ticket {
  return { slug: 'a', title: 'A', state: 'ready', priority: 3, cards: [], pullRequest: null, blockedBecause: null, comments: [], ...over }
}

describe('the ticket board’s state columns (BEH-UI-6)', () => {
  it('keeps each ticket in the column of its state, and leaves merged tickets off the board (BEH-UI-6)', () => {
    const cols = ticketColumns([
      ticket({ slug: 'n', state: 'new' }),
      ticket({ slug: 'r', state: 'ready' }),
      ticket({ slug: 'b', state: 'built' }),
      ticket({ slug: 'm', state: 'merged' }),
      ticket({ slug: 'r2', state: 'ready' }),
    ])
    expect(cols.map((c) => c.state)).toEqual(['new', 'ready', 'built'])
    for (const c of cols) for (const t of c.tickets) expect(t.state).toBe(c.state)
    expect(cols.find((c) => c.state === 'ready')!.tickets.map((t) => t.slug)).toEqual(['r', 'r2'])
  })

  it('orders each column by priority, 1 first and 5 last (BEH-UI-6)', () => {
    const [ready] = ticketColumns([
      ticket({ slug: 'p5', priority: 5 }),
      ticket({ slug: 'p1', priority: 1 }),
      ticket({ slug: 'p3', priority: 3 }),
    ])
    expect(ready.tickets.map((t) => t.slug)).toEqual(['p1', 'p3', 'p5'])
  })

  it('within one priority lists a blocked ticket above the unblocked ones, and never above a higher priority (BEH-UI-6)', () => {
    const [ready] = ticketColumns([
      ticket({ slug: 'p2', priority: 2 }),
      ticket({ slug: 'blk2', priority: 2, blockedBecause: 'which?' }),
      ticket({ slug: 'p1', priority: 1 }),
      ticket({ slug: 'blk3', priority: 3, blockedBecause: 'why?' }),
    ])
    expect(ready.tickets.map((t) => t.slug)).toEqual(['p1', 'blk2', 'p2', 'blk3'])
  })
})
