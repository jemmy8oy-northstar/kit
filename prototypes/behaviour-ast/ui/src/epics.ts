import type { Behaviour, Epic, Feature, Ticket } from './api/types'
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

/** An epic is open while any behaviour beneath it is pending, and is never open when its title parks it out of scope. */
export function isOpenEpic(card: EpicCard): boolean {
  return card.built < card.total && !/out of scope/i.test(card.epic.title)
}

/**
 * BEH-TICKET-7: the epics a feature is in, its own and those of the features it sits in, at any depth (BEH-EPIC-2).
 * `seen` stops a feature that names itself as its parent from looping.
 */
export function epicsOfFeature(id: string, features: Feature[], seen = new Set<string>()): string[] {
  if (seen.has(id)) return []
  seen.add(id)
  const feature = features.find((f) => f.id === id)
  if (!feature) return []
  return [...new Set([...(feature.epics ?? []), ...feature.in.flatMap((parent) => epicsOfFeature(parent, features, seen))])]
}

/**
 * BEH-TICKET-7: the epics a ticket progresses toward, which are those of the features its specs sit in. A ticket
 * that touches no epic-tagged feature names none, and so counts toward none.
 */
export function epicsOfTicket(ticket: Ticket, features: Feature[], behaviours: Behaviour[]): string[] {
  const named = new Set(ticket.cards)
  const featureIds = behaviours.filter((b) => named.has(b.id)).flatMap((b) => b.in)
  return [...new Set(featureIds.flatMap((id) => epicsOfFeature(id, features)))]
}

/** BEH-TICKET-7: one project's tickets, grouped by each epic they name, so a ticket naming two epics is in both. */
export function ticketsByEpic(tickets: Ticket[], features: Feature[], behaviours: Behaviour[]): Map<string, Ticket[]> {
  const byEpic = new Map<string, Ticket[]>()
  for (const ticket of tickets) {
    for (const epic of epicsOfTicket(ticket, features, behaviours)) byEpic.set(epic, [...(byEpic.get(epic) ?? []), ticket])
  }
  return byEpic
}

/** The states an epic's tickets are counted in on the epic board (BEH-TICKET-8), in the order a ticket moves through them. */
export const STATUS_STATES = ['new', 'ready', 'planned', 'built', 'done'] as const

/**
 * BEH-TICKET-8: an epic's tickets counted by state. Merged is done; a stopped ticket is in no state, so it counts
 * nowhere. Blocked is a tag, so a blocked ticket is counted once, in the state it is in, and `blocked` counts the
 * blocked ones beside the states without adding to them.
 */
export interface TicketStatus {
  states: Record<(typeof STATUS_STATES)[number], number>
  blocked: number
}

export function ticketStatus(tickets: Ticket[]): TicketStatus {
  const states = Object.fromEntries(STATUS_STATES.map((s) => [s, 0])) as TicketStatus['states']
  let blocked = 0
  for (const ticket of tickets) {
    const state = ticket.state === 'merged' ? 'done' : ticket.state
    if (!(STATUS_STATES as readonly string[]).includes(state)) continue
    states[state as keyof TicketStatus['states']] += 1
    if (ticket.blockedBecause) blocked += 1
  }
  return { states, blocked }
}
