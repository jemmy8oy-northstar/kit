import { ApiError, fetchTickets } from './api/client'
import type { Ticket, TicketList } from './api/types'

/**
 * A project's tickets (BEH-ORCH-16 slice 4), or null for someone they are refused to, or a filter naming no project
 * Kit has: the board then shows no section rather than an error.
 */
export async function loadTickets(app: string): Promise<TicketList | null> {
  try {
    return await fetchTickets(app)
  } catch (e) {
    if (e instanceof ApiError && (e.status === 401 || e.status === 403 || e.status === 404)) return null
    throw e
  }
}

/** The longest reply on a ticket, as the server's MaxReply. */
export const MAX_REPLY = 1000

/** Blocked first, as they are waiting on him; then top priority first; merged ones are done, so off the list. */
export function openTickets(tickets: Ticket[]): Ticket[] {
  return tickets
    .filter((t) => t.state !== 'merged')
    .map((t, i) => ({ t, i }))
    .sort((x, y) => Number(!!y.t.blockedBecause) - Number(!!x.t.blockedBecause) || x.t.priority - y.t.priority || x.i - y.i)
    .map(({ t }) => t)
}

/** The pipeline's states, in the order a ticket moves through them (BEH-ORCH-16); a state not listed goes after them. */
export const TICKET_STATES = ['new', 'ready', 'planned', 'built', 'stopped']

/** One state column of the ticket board (BEH-UI-6): the open tickets in that state. */
export interface TicketColumn {
  state: string
  tickets: Ticket[]
}

/**
 * BEH-UI-6 (his 2026-11-18 note: each ticket keeps its state column): the open tickets grouped by state, columns in
 * {@link TICKET_STATES} order, and merged tickets off the board. Within a column a ticket sorts by priority, 1 first,
 * and within one priority a blocked ticket sits above the unblocked ones. A blocked ticket does not rise above a
 * higher priority. Equal keys keep the order the server sent, which is oldest first.
 */
export function ticketColumns(tickets: Ticket[]): TicketColumn[] {
  const open = tickets.filter((t) => t.state !== 'merged')
  const rank = (state: string) => {
    const i = TICKET_STATES.indexOf(state)
    return i === -1 ? TICKET_STATES.length : i
  }
  const states = [...new Set(open.map((t) => t.state))].sort((a, b) => rank(a) - rank(b))
  return states.map((state) => ({
    state,
    tickets: open
      .map((t, i) => ({ t, i }))
      .filter((x) => x.t.state === state)
      .sort((x, y) => x.t.priority - y.t.priority || Number(!!y.t.blockedBecause) - Number(!!x.t.blockedBecause) || x.i - y.i)
      .map(({ t }) => t),
  }))
}
