import { describe, expect, it } from 'vitest'
import { openTickets, ticketColumns } from './tickets'
import type { Ticket } from './api/types'

function ticket(over: Partial<Ticket> = {}): Ticket {
  return { slug: 'a', title: 'A', state: 'ready', priority: 3, cards: [], pullRequest: null, blockedBecause: null, comments: [], ...over }
}

describe('the ticket board’s state columns (BEH-UI-6)', () => {
  it('keeps each ticket in the column of its state, and puts merged tickets in a closed column after the open ones (BEH-UI-6)', () => {
    const cols = ticketColumns([
      ticket({ slug: 'n', state: 'new' }),
      ticket({ slug: 'r', state: 'ready' }),
      ticket({ slug: 'b', state: 'built' }),
      ticket({ slug: 'm', state: 'merged' }),
      ticket({ slug: 'r2', state: 'ready' }),
    ])
    expect(cols.map((c) => c.state)).toEqual(['new', 'ready', 'built', 'closed'])
    for (const c of cols.slice(0, -1)) for (const t of c.tickets) expect(t.state).toBe(c.state)
    expect(cols.find((c) => c.state === 'ready')!.tickets.map((t) => t.slug)).toEqual(['r', 'r2'])
    expect(cols.at(-1)!.tickets.map((t) => t.slug)).toEqual(['m'])
  })

  it('still drops merged tickets from openTickets, so the open list and its count are unchanged by the closed column (BEH-UI-6)', () => {
    const tickets = [
      ticket({ slug: 'r', state: 'ready' }),
      ticket({ slug: 'm', state: 'merged' }),
      ticket({ slug: 'b', state: 'built', blockedBecause: 'which?' }),
    ]
    expect(openTickets(tickets).map((t) => t.slug)).toEqual(['b', 'r'])
    expect(openTickets(tickets)).toHaveLength(2)
  })

  it('returns no closed column when no ticket is merged (BEH-UI-6)', () => {
    const cols = ticketColumns([ticket({ slug: 'r', state: 'ready' }), ticket({ slug: 'n', state: 'new' })])
    expect(cols.map((c) => c.state)).toEqual(['new', 'ready'])
    expect(cols.some((c) => c.state === 'closed')).toBe(false)
  })

  it('orders the closed column by priority, and a blocked merged ticket sits above the unblocked ones (BEH-UI-6)', () => {
    const cols = ticketColumns([
      ticket({ slug: 'm5', state: 'merged', priority: 5 }),
      ticket({ slug: 'm1', state: 'merged', priority: 1 }),
      ticket({ slug: 'm1b', state: 'merged', priority: 1, blockedBecause: 'why?' }),
    ])
    expect(cols.at(-1)!.tickets.map((t) => t.slug)).toEqual(['m1b', 'm1', 'm5'])
  })

  it('places the closed column after an unlisted-state column, which goes after the listed ones (BEH-UI-6)', () => {
    const cols = ticketColumns([
      ticket({ slug: 'm', state: 'merged' }),
      ticket({ slug: 'x', state: 'archived' }),
      ticket({ slug: 's', state: 'stopped' }),
      ticket({ slug: 'r', state: 'ready' }),
    ])
    expect(cols.map((c) => c.state)).toEqual(['ready', 'stopped', 'archived', 'closed'])
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
