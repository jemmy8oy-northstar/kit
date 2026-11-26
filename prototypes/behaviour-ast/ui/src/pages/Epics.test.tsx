import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Epics from './Epics'
import { hueOf } from '../board'
import { epicCards } from '../epics'
import type { Behaviour, Epic, Feature, Ticket } from '../api/types'

function beh(id: string, over: Partial<Behaviour> = {}): Behaviour {
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
    in: [],
    ...over,
  }
}

function feat(id: string, over: Partial<Feature> = {}): Feature {
  return { id, title: `title of ${id}`, in: [], at: 'x.beh:1', ...over }
}

function epic(id: string): Epic {
  return { id, title: `title of ${id}`, at: 'x.beh:1' }
}

const details: Record<string, { epics?: Epic[]; features: Feature[]; behaviours: Behaviour[] }> = {
  alpha: {
    epics: [epic('EPIC-SHIP'), epic('EPIC-EMPTY')],
    features: [
      feat('FEAT-TOP', { epics: ['EPIC-SHIP'] }),
      feat('FEAT-PART', { in: ['FEAT-TOP'] }),
      feat('FEAT-OTHER'),
    ],
    behaviours: [
      beh('A-1', { in: ['FEAT-TOP'] }),
      beh('A-2', { in: ['FEAT-PART'], pending: true }),
      beh('A-3', { in: ['FEAT-OTHER'] }),
    ],
  },
  'kit-ui': { epics: [epic('EPIC-USERS')], features: [], behaviours: [] },
  plain: { features: [feat('FEAT-PLAIN')], behaviours: [] },
  ideas: { epics: [epic('EPIC-IDEA')], features: [], behaviours: [] },
}

const summaries = [
  { app: 'alpha' },
  { app: 'kit-ui', project: 'kit' },
  { app: 'plain' },
  { app: 'ideas', idea: true },
  { app: 'broken', error: 'broken.beh:3: expected a verb' },
]

function renderEpics(projects = summaries) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      const app = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      return { ok: true, status: 200, statusText: 'OK', json: async () => (app ? { app, ...details[app] } : { projects }) }
    }),
  )
  render(
    <MemoryRouter initialEntries={['/epics']}>
      <Epics />
    </MemoryRouter>,
  )
}

const card = (id: string) => within(screen.getByText(id).closest('.board-card') as HTMLElement)

describe('Epics', () => {
  it('shows a card per epic in every real project, coloured by project (BEH-EPIC-4)', async () => {
    renderEpics()
    const board = within(await screen.findByRole('region', { name: 'Epics' }))
    expect(board.getAllByRole('heading', { level: 3 }).map((h) => h.lastChild!.textContent)).toEqual([
      'EPIC-SHIP',
      'EPIC-EMPTY',
      'EPIC-USERS',
    ])
    expect(card('EPIC-SHIP').getByText('alpha').closest('.board-card')).toHaveClass(`hue-${hueOf('alpha')}`)
    expect(card('EPIC-USERS').getByText('kit · kit-ui')).toBeInTheDocument()
    expect(screen.queryByText('EPIC-IDEA')).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('broken.beh:3: expected a verb')
  })

  it('shows an epic its tagged features, linked, and their behaviours built out of total at any depth (BEH-EPIC-4)', async () => {
    renderEpics()
    await screen.findByRole('region', { name: 'Epics' })
    expect(card('EPIC-SHIP').getByText('1 of 2 behaviours built')).toBeInTheDocument()
    expect(card('EPIC-SHIP').getByRole('link', { name: 'title of FEAT-TOP' })).toHaveAttribute(
      'href',
      '/projects/alpha/features/FEAT-TOP',
    )
    expect(card('EPIC-EMPTY').getByText('0 of 0 behaviours built')).toBeInTheDocument()
    expect(card('EPIC-EMPTY').getByText('No feature is in it yet.')).toBeInTheDocument()
  })

  it('says so when no project declares an epic', async () => {
    renderEpics([{ app: 'plain' }])
    expect(await screen.findByText('No project declares an epic yet.')).toBeInTheDocument()
  })
})

describe('epic rules', () => {
  it('counts a behaviour under two of an epic\'s features once', () => {
    const features = [feat('FEAT-A', { epics: ['EPIC-X'] }), feat('FEAT-B', { epics: ['EPIC-X'] })]
    const behaviours = [beh('X-1', { in: ['FEAT-A', 'FEAT-B'] }), beh('X-2', { in: ['FEAT-B'], pending: true })]
    const [c] = epicCards('app', 'app', [epic('EPIC-X')], features, behaviours)
    expect([c.features.map((f) => f.id), c.built, c.total]).toEqual([['FEAT-A', 'FEAT-B'], 1, 2])
  })
})

/** A server for the ticket tests: each project's detail, and its tickets. Every path it is asked for is recorded. */
function stubTicketServer(
  projects: { app: string; project?: string }[],
  byApp: Record<string, { epics?: Epic[]; features: Feature[]; behaviours: Behaviour[] }>,
  tickets: Record<string, Ticket[]>,
) {
  const asked: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      asked.push(path)
      if (path.endsWith('/api/projects')) return { ok: true, status: 200, statusText: 'OK', json: async () => ({ projects }) }
      const m = /\/api\/projects\/([^/]+)(\/tickets)?$/.exec(path)
      if (!m) return { ok: false, status: 404, statusText: 'Not Found', json: async () => ({}) }
      const app = decodeURIComponent(m[1])
      const body = m[2] ? { on: true, tickets: tickets[app] ?? [] } : { app, ...byApp[app] }
      return { ok: true, status: 200, statusText: 'OK', json: async () => body }
    }),
  )
  return asked
}

function ticket(over: Partial<Ticket> = {}): Ticket {
  return { slug: 't', title: 'T', state: 'ready', priority: 3, cards: [], pullRequest: null, blockedBecause: null, comments: [], ...over }
}

/** Two epics in one project: EPIC-A is served by FEAT-A1 (A-1 built, A-2 pending) and EPIC-B by FEAT-B1 (B-1 pending). */
const twoEpics = {
  epics: [epic('EPIC-A'), epic('EPIC-B')],
  features: [feat('FEAT-A1', { epics: ['EPIC-A'] }), feat('FEAT-B1', { epics: ['EPIC-B'] })],
  behaviours: [beh('A-1', { in: ['FEAT-A1'] }), beh('A-2', { in: ['FEAT-A1'], pending: true }), beh('B-1', { in: ['FEAT-B1'], pending: true })],
}

const statusOf = (epicId: string) => within(screen.getByRole('list', { name: `Tickets for ${epicId}` }))

describe('the ticket counts on the epic board (BEH-TICKET-7, BEH-TICKET-8)', () => {
  it('counts a ticket toward each epic it names, through the features its specs sit in (BEH-TICKET-7)', async () => {
    stubTicketServer(
      [{ app: 'alpha' }],
      {
        alpha: {
          epics: [epic('EPIC-A'), epic('EPIC-B')],
          features: [feat('FEAT-A1', { epics: ['EPIC-A'] }), feat('FEAT-B1', { epics: ['EPIC-B'] })],
          behaviours: [beh('A-1', { in: ['FEAT-A1'], pending: true }), beh('B-1', { in: ['FEAT-B1'], pending: true })],
        },
      },
      { alpha: [ticket({ slug: 'both', cards: ['A-1', 'B-1'] })] },
    )
    render(
      <MemoryRouter initialEntries={['/epics']}>
        <Epics />
      </MemoryRouter>,
    )
    await screen.findByRole('region', { name: 'Epics' })
    expect(statusOf('EPIC-A').getByText('ready').nextElementSibling).toHaveTextContent('1')
    expect(statusOf('EPIC-B').getByText('ready').nextElementSibling).toHaveTextContent('1')
  })

  it('counts a ticket that names no epic toward no epic (BEH-TICKET-7)', async () => {
    stubTicketServer(
      [{ app: 'alpha' }],
      { alpha: { ...twoEpics, behaviours: [...twoEpics.behaviours, beh('LOOSE-1', { in: [] })] } },
      { alpha: [ticket({ slug: 'loose', cards: ['LOOSE-1'] })] },
    )
    render(
      <MemoryRouter initialEntries={['/epics']}>
        <Epics />
      </MemoryRouter>,
    )
    await screen.findByRole('region', { name: 'Epics' })
    expect(screen.getAllByText('No ticket names this epic yet.')).toHaveLength(2)
  })

  it('counts each open epic’s tickets by state, a blocked ticket once in the state it is in (BEH-TICKET-8)', async () => {
    stubTicketServer(
      [{ app: 'alpha' }],
      { alpha: twoEpics },
      {
        alpha: [
          ticket({ slug: 'n', state: 'new', cards: ['A-2'] }),
          ticket({ slug: 'r1', state: 'ready', cards: ['A-2'] }),
          ticket({ slug: 'r2', state: 'ready', cards: ['A-2'], blockedBecause: 'Which one?' }),
          ticket({ slug: 'p', state: 'planned', cards: ['A-2'] }),
          ticket({ slug: 'b', state: 'built', cards: ['A-2'] }),
          ticket({ slug: 'd', state: 'merged', cards: ['A-2'] }),
          ticket({ slug: 'x', state: 'stopped', cards: ['A-2'] }),
        ],
      },
    )
    render(
      <MemoryRouter initialEntries={['/epics']}>
        <Epics />
      </MemoryRouter>,
    )
    await screen.findByRole('region', { name: 'Epics' })
    const dl = within(screen.getByRole('list', { name: 'Tickets for EPIC-A' }))
    const count = (term: string) => dl.getByText(term).nextElementSibling
    expect([count('new'), count('ready'), count('planned'), count('built'), count('blocked'), count('done')].map((el) => el!.textContent)).toEqual([
      '1',
      '2',
      '1',
      '1',
      '1',
      '1',
    ])
    expect(count('ready')).toHaveTextContent('2')
  })

  it('computing the status opens no feature or spec page (BEH-TICKET-8)', async () => {
    const asked = stubTicketServer(
      [{ app: 'alpha' }],
      { alpha: twoEpics },
      { alpha: [ticket({ slug: 'x', state: 'ready', cards: ['A-2'] })] },
    )
    render(
      <MemoryRouter initialEntries={['/epics']}>
        <Epics />
      </MemoryRouter>,
    )
    await screen.findByRole('region', { name: 'Epics' })
    expect(statusOf('EPIC-A').getByText('ready').nextElementSibling).toHaveTextContent('1')
    expect(asked.filter((p) => /\/(features|behaviours)\//.test(p))).toEqual([])
  })

  it("shows a project's first open epic first, as its goal (BEH-TICKET-8)", async () => {
    stubTicketServer(
      [{ app: 'alpha' }],
      {
        alpha: {
          epics: [epic('EPIC-DONE'), epic('EPIC-OPEN'), epic('EPIC-LATER')],
          features: [
            feat('FEAT-D', { epics: ['EPIC-DONE'] }),
            feat('FEAT-O', { epics: ['EPIC-OPEN'] }),
            feat('FEAT-L', { epics: ['EPIC-LATER'] }),
          ],
          behaviours: [beh('D-1', { in: ['FEAT-D'] }), beh('O-1', { in: ['FEAT-O'], pending: true }), beh('L-1', { in: ['FEAT-L'], pending: true })],
        },
      },
      {},
    )
    render(
      <MemoryRouter initialEntries={['/epics']}>
        <Epics />
      </MemoryRouter>,
    )
    const board = within(await screen.findByRole('region', { name: 'Epics' }))
    expect(board.getAllByRole('heading', { level: 3 }).map((h) => h.lastChild!.textContent)).toEqual([
      'EPIC-OPEN',
      'EPIC-DONE',
      'EPIC-LATER',
    ])
  })
})
