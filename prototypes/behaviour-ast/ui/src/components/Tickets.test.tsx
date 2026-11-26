import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Tickets from './Tickets'
import type { Behaviour, Feature, Ticket } from '../api/types'

function ticket(over: Partial<Ticket> = {}): Ticket {
  return {
    slug: 'a-ticket',
    title: 'A ticket',
    state: 'ready',
    priority: 3,
    cards: [],
    pullRequest: null,
    blockedBecause: null,
    comments: [],
    ...over,
  }
}

function beh(id: string, inFeatures: string[]): Behaviour {
  return {
    id,
    title: `title of ${id}`,
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'defined', ref: null },
    review: { state: 'approved', note: null },
    asks: null,
    pending: false,
    layer: 'ux',
    at: 'x.beh:1',
    in: inFeatures,
  } as Behaviour
}

function feat(id: string): Feature {
  return { id, title: `title of ${id}`, in: [], at: 'x.beh:1' }
}

/** Answers the ticket list and the project's detail; anything else is refused, as the server would. */
function renderTickets(tickets: Ticket[], detail: { features: Feature[]; behaviours: Behaviour[] }) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      const body = path.endsWith('/tickets')
        ? { on: true, tickets }
        : path.endsWith('/api/projects/alpha')
          ? { app: 'alpha', project: 'alpha', ...detail }
          : null
      return body
        ? { ok: true, status: 200, statusText: 'OK', json: async () => body }
        : { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-route' }) }
    }),
  )
  render(
    <MemoryRouter>
      <Tickets app="alpha" />
    </MemoryRouter>,
  )
}

describe('the ticket board (BEH-TICKET-1)', () => {
  it('shows each ticket with its status and its open question (BEH-TICKET-1)', async () => {
    renderTickets(
      [
        ticket({ slug: 'asks', title: 'Asks him', state: 'ready', blockedBecause: 'Which colour is the header?' }),
        ticket({ slug: 'builds', title: 'Builds', state: 'built' }),
      ],
      { features: [], behaviours: [] },
    )
    const asks = within((await screen.findByText('Asks him')).closest('.board-card') as HTMLElement)
    expect(asks.getByText('ready')).toBeInTheDocument()
    expect(asks.getByTestId('blocked-because')).toHaveTextContent('Which colour is the header?')
    const builds = within(screen.getByText('Builds').closest('.board-card') as HTMLElement)
    expect(builds.getByText('built')).toBeInTheDocument()
    expect(builds.queryByTestId('blocked-because')).not.toBeInTheDocument()
  })

  it('shows the behaviours a ticket changes as its own list, and a ticket that names none stays in its column, under no behaviour (BEH-TICKET-1)', async () => {
    renderTickets(
      [
        ticket({ slug: 'two', title: 'Changes two', state: 'ready', cards: ['A-1', 'A-2'] }),
        ticket({ slug: 'none', title: 'Changes none', state: 'ready', cards: [] }),
      ],
      { features: [feat('FEAT-ONE')], behaviours: [beh('A-1', ['FEAT-ONE']), beh('A-2', ['FEAT-ONE'])] },
    )
    const two = within((await screen.findByText('Changes two')).closest('.board-card') as HTMLElement)
    const list = two.getByRole('list', { name: 'Specs touched' })
    expect(within(list).getAllByRole('listitem').map((li) => li.textContent)).toEqual(['A-1', 'A-2'])

    // Removing its list (it names no behaviour) leaves the ticket in its column, not under a behaviour.
    const none = screen.getByText('Changes none').closest('li') as HTMLElement
    expect(none.closest('[data-card]')).toBeNull()
    expect(none.closest('[data-state="ready"]')).not.toBeNull()
    expect(within(none).queryByRole('list', { name: 'Specs touched' })).not.toBeInTheDocument()
  })

  it('still shows a ticket that names no epic, on the ticket board (BEH-TICKET-7)', async () => {
    renderTickets([ticket({ slug: 'loose', title: 'Names no epic', cards: ['A-1'] })], {
      features: [feat('FEAT-ONE')],
      behaviours: [beh('A-1', ['FEAT-ONE'])],
    })
    expect(await screen.findByText('Names no epic')).toBeInTheDocument()
  })
})

describe('a ticket touches many features and specs (BEH-TICKET-7)', () => {
  it('lists every feature its specs sit in, and every spec it touches (BEH-TICKET-7)', async () => {
    renderTickets([ticket({ slug: 'wide', title: 'Wide change', cards: ['A-1', 'A-2', 'B-1'] })], {
      features: [feat('FEAT-ONE'), feat('FEAT-TWO')],
      behaviours: [beh('A-1', ['FEAT-ONE']), beh('A-2', ['FEAT-TWO']), beh('B-1', ['FEAT-TWO'])],
    })
    const card = within((await screen.findByText('Wide change')).closest('.board-card') as HTMLElement)
    const features = await card.findByLabelText('Features touched')
    expect(within(features).getByText('FEAT-ONE')).toBeInTheDocument()
    expect(within(features).getByText('FEAT-TWO')).toBeInTheDocument()
    for (const id of ['A-1', 'A-2', 'B-1']) expect(card.getByRole('link', { name: id })).toBeInTheDocument()
  })
})
