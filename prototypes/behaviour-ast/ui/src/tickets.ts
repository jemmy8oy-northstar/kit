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
export const MAX_REPLY = 2000

/** Blocked first, as they are waiting on him; then top priority first; merged ones are done, so off the list. */
export function openTickets(tickets: Ticket[]): Ticket[] {
  return tickets
    .filter((t) => t.state !== 'merged')
    .map((t, i) => ({ t, i }))
    .sort((x, y) => Number(!!y.t.blockedBecause) - Number(!!x.t.blockedBecause) || x.t.priority - y.t.priority || x.i - y.i)
    .map(({ t }) => t)
}
