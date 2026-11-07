import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Stages from './Stages'
import { hueOf } from '../board'
import { columnFor, stageCards } from '../stages'
import type { Behaviour, Feature } from '../api/types'

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

const details: Record<string, { features: Feature[]; behaviours: Behaviour[] }> = {
  alpha: {
    features: [
      feat('FEAT-PROBLEM', { stage: 'problem' }),
      feat('FEAT-NEWPART', { stage: 'architecture' }),
      feat('FEAT-BUILDING', { stage: 'development' }),
      feat('FEAT-PART', { in: ['FEAT-BUILDING'] }),
      feat('FEAT-PAGE'),
      feat('FEAT-SHIPPED', { stage: 'development' }),
      feat('FEAT-HELD', { stage: 'development' }),
    ],
    behaviours: [
      beh('A-1', { in: ['FEAT-BUILDING'] }),
      beh('A-2', { in: ['FEAT-PART'], pending: true }),
      beh('A-3', { in: ['FEAT-PART'] }),
      beh('A-UI', { in: ['FEAT-PROBLEM'], layer: 'ui', pending: true }),
      beh('A-4', { in: ['FEAT-SHIPPED'] }),
      beh('A-5', { in: ['FEAT-HELD'] }),
      beh('A-6', { in: ['FEAT-HELD'], pending: true, paused: true }),
    ],
  },
  'kit-ui': { features: [feat('FEAT-UX', { stage: 'ux' })], behaviours: [] },
  ideas: { features: [feat('FEAT-IDEA', { stage: 'problem' })], behaviours: [] },
}

const summaries = [
  { app: 'alpha' },
  { app: 'kit-ui', project: 'kit' },
  { app: 'ideas', idea: true },
  { app: 'broken', error: 'broken.beh:3: expected a verb' },
]

function mockApi() {
  const fetchMock = vi.fn(async (path: string) => {
    const app = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
    return { ok: true, status: 200, statusText: 'OK', json: async () => (app ? { app, ...details[app] } : { projects: summaries }) }
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function stage(name: string) {
  return within(await screen.findByRole('region', { name }))
}

function renderStages() {
  const fetchMock = mockApi()
  render(
    <MemoryRouter initialEntries={['/stages']}>
      <Stages />
    </MemoryRouter>,
  )
  return fetchMock
}

describe('Stages', () => {
  it('shows a column per stage, in order, and a card per staged feature in its column (BEH-STAGE-1)', async () => {
    renderStages()
    const board = await screen.findByRole('region', { name: 'Feature stages' })
    expect(within(board).getAllByRole('heading', { level: 2 }).map((h) => h.firstChild!.textContent)).toEqual([
      'Unstaged',
      'Problem statement',
      'UX requirements',
      'Business logic',
      'Architecture',
      'Development',
      'UI requirements',
      'Done',
    ])
    expect((await stage('Problem statement')).getByText('FEAT-PROBLEM')).toBeInTheDocument()
    expect((await stage('Development')).getByText('FEAT-BUILDING')).toBeInTheDocument()
    expect((await stage('UX requirements')).getByText('kit · kit-ui')).toBeInTheDocument()
  })

  it('keeps an idea and an unreadable file off the columns, and names the unreadable one (BEH-STAGE-1)', async () => {
    renderStages()
    await stage('Development')
    expect(screen.queryByText('FEAT-IDEA')).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('broken.beh:3: expected a verb')
  })

  it('puts the Unstaged column first, holding the features with no stage line and none that has one (BEH-STAGE-7)', async () => {
    renderStages()
    const board = await screen.findByRole('region', { name: 'Feature stages' })
    expect(within(board).getAllByRole('heading', { level: 2 })[0].firstChild!.textContent).toBe('Unstaged')
    const col = await stage('Unstaged')
    expect(col.getByText('FEAT-PAGE')).toBeInTheDocument()
    expect(col.getByText('FEAT-PART')).toBeInTheDocument()
    expect(col.getByText('FEAT-PAGE').closest('.board-card')).toHaveClass(`hue-${hueOf('alpha')}`)
    expect(col.queryByText('FEAT-PROBLEM')).not.toBeInTheDocument()
  })

  it('puts a feature whose behaviours are all built in a last Done column, and keeps one with a paused behaviour or none in its stage (BEH-STAGE-12)', async () => {
    renderStages()
    const board = await screen.findByRole('region', { name: 'Feature stages' })
    const heads = within(board).getAllByRole('heading', { level: 2 })
    expect(heads[heads.length - 1].firstChild!.textContent).toBe('Done')
    const done = await stage('Done')
    expect(done.getByText('FEAT-SHIPPED')).toBeInTheDocument()
    expect(done.queryByText('FEAT-HELD')).not.toBeInTheDocument()
    expect(done.queryByText('FEAT-PAGE')).not.toBeInTheDocument()
    expect((await stage('Development')).getByText('FEAT-HELD')).toBeInTheDocument()
    expect((await stage('Development')).queryByText('FEAT-SHIPPED')).not.toBeInTheDocument()
  })

  it('links a card to its feature page and colours it by project (BEH-STAGE-1)', async () => {
    renderStages()
    const col = await stage('Problem statement')
    expect(col.getByRole('link', { name: 'title of FEAT-PROBLEM' })).toHaveAttribute('href', '/projects/alpha/features/FEAT-PROBLEM')
    expect(col.getByRole('link', { name: 'title of FEAT-PROBLEM' }).closest('.board-card')).toHaveClass(`hue-${hueOf('alpha')}`)
  })

  it('shows a feature in development its behaviours built out of total, counting child features (BEH-STAGE-3)', async () => {
    renderStages()
    expect((await stage('Development')).getByText('2 of 3 behaviours built')).toBeInTheDocument()
    expect((await stage('Problem statement')).queryByText(/behaviours built/)).not.toBeInTheDocument()
  })

  it('offers Features and Behaviours on the board switch, with Features current and one tap to the behaviour board (BEH-STAGE-8)', async () => {
    renderStages()
    const views = await screen.findByRole('navigation', { name: 'Board view' })
    expect(within(views).getByRole('link', { name: 'Features' })).toHaveAttribute('aria-current', 'page')
    expect(within(views).getByRole('link', { name: 'Behaviours' })).toHaveAttribute('href', '/board')
  })

  it('only reads each stage line: no card can be dragged and the page sends no write (BEH-STAGE-9)', async () => {
    const fetchMock = renderStages()
    await stage('Development')
    expect(document.querySelectorAll('[draggable]')).toHaveLength(0)
    const calls = fetchMock.mock.calls as unknown as [string, RequestInit?][]
    const writes = calls.filter(([, init]) => (init?.method ?? 'GET').toUpperCase() !== 'GET')
    expect(writes).toEqual([])
  })

  it('leaves a feature at its written stage when UI requirements are written early (BEH-STAGE-4)', async () => {
    renderStages()
    // FEAT-PROBLEM already has a ui-layer behaviour: the stage line alone places it.
    expect((await stage('Problem statement')).getByText('FEAT-PROBLEM')).toBeInTheDocument()
    expect((await stage('UI requirements')).queryByText('FEAT-PROBLEM')).not.toBeInTheDocument()
    expect((await stage('Development')).queryByText('FEAT-PROBLEM')).not.toBeInTheDocument()
  })
})

describe('stage rules', () => {
  it('is Done only with behaviours beneath it, every one done on the behaviour board (BEH-STAGE-12)', () => {
    const features = [
      feat('FEAT-A', { stage: 'ui' }),
      feat('FEAT-B', { stage: 'ui' }),
      feat('FEAT-C', { stage: 'ui' }),
      feat('FEAT-D', { stage: 'ui' }),
      feat('FEAT-E', { stage: 'ui' }),
    ]
    const behaviours = [
      beh('A1', { in: ['FEAT-A'] }),
      beh('B1', { in: ['FEAT-B'] }),
      beh('B2', { in: ['FEAT-B'], needsContext: 'a secret' }),
      beh('C1', { in: ['FEAT-C'], review: { state: 'denied', note: null } }),
      // A denied card is not work (BEH-BOARD-21): it neither blocks Done nor makes it.
      beh('E1', { in: ['FEAT-E'] }),
      beh('E2', { in: ['FEAT-E'], pending: true, review: { state: 'denied', note: null } }),
    ]
    expect(stageCards('app', 'app', features, behaviours).map((c) => [c.feature.id, c.done])).toEqual([
      ['FEAT-A', true],
      ['FEAT-B', false],
      ['FEAT-C', false],
      ['FEAT-D', false],
      ['FEAT-E', true],
    ])
  })

  it('counts a behaviour under two staged features once in each, and a stage-less one gets a card too (BEH-STAGE-7)', () => {
    const features = [feat('FEAT-A', { stage: 'logic' }), feat('FEAT-B', { stage: 'development' }), feat('FEAT-C')]
    const behaviours = [beh('X', { in: ['FEAT-A', 'FEAT-B'], pending: true })]
    const cards = stageCards('app', 'app', features, behaviours)
    expect(cards.map((c) => [c.feature.id, c.built, c.total])).toEqual([
      ['FEAT-A', 0, 1],
      ['FEAT-B', 0, 1],
      ['FEAT-C', 0, 0],
    ])
  })

  it('BEH-ARCH-2: shows an Architecture column between Business logic and Development, holding a feature staged architecture', async () => {
    renderStages()
    const board = await screen.findByRole('region', { name: 'Feature stages' })
    const heads = within(board).getAllByRole('heading', { level: 2 }).map((h) => h.firstChild!.textContent)
    const at = heads.indexOf('Architecture')
    expect(heads[at - 1]).toBe('Business logic')
    expect(heads[at + 1]).toBe('Development')
    expect((await stage('Architecture')).getByText('FEAT-NEWPART')).toBeInTheDocument()
  })

  it('BEH-STAGE-10: a feature with some behaviour built sits in development, as its stage line says, not in Done', () => {
    const features = [feat('FEAT-A', { stage: 'development' })]
    const behaviours = [beh('A1', { in: ['FEAT-A'] }), beh('A2', { in: ['FEAT-A'], pending: true })]
    const [card] = stageCards('app', 'app', features, behaviours)
    expect([card.built, card.total]).toEqual([1, 2])
    expect(columnFor(card)).toBe('development')
  })

  it('BEH-STAGE-11: a feature with nothing built sits in the column its stage line names, whatever its spec says', () => {
    const features = [feat('FEAT-P', { stage: 'problem' }), feat('FEAT-U', { stage: 'ux' }), feat('FEAT-L', { stage: 'logic' })]
    // Each one's pending behaviour is in a layer that would suggest a later stage: the page must not read it.
    const behaviours = [
      beh('P1', { in: ['FEAT-P'], layer: 'ui', pending: true }),
      beh('U1', { in: ['FEAT-U'], layer: 'logic', pending: true }),
      beh('L1', { in: ['FEAT-L'], layer: 'ui', pending: true }),
    ]
    expect(stageCards('app', 'app', features, behaviours).map((c) => [c.feature.id, columnFor(c)])).toEqual([
      ['FEAT-P', 'problem'],
      ['FEAT-U', 'ux'],
      ['FEAT-L', 'logic'],
    ])
  })

  it('BEH-STAGE-12: a proposed or in-review behaviour keeps its feature in its stage, and Done leaves the stage line unchanged', () => {
    const features = [feat('FEAT-P', { stage: 'ui' }), feat('FEAT-R', { stage: 'ui' }), feat('FEAT-D', { stage: 'ui' })]
    const behaviours = [
      beh('P1', { in: ['FEAT-P'], source: { origin: 'inferred', ref: null }, review: { state: 'unreviewed', note: null } }),
      beh('R1', { in: ['FEAT-R'], review: { state: 'requested', note: null } }),
      beh('D1', { in: ['FEAT-D'] }),
    ]
    const cards = stageCards('app', 'app', features, behaviours)
    expect(cards.map((c) => [c.feature.id, c.done, columnFor(c)])).toEqual([
      ['FEAT-P', false, 'ui'],
      ['FEAT-R', false, 'ui'],
      ['FEAT-D', true, 'done'],
    ])
    expect(cards[2].feature.stage).toBe('ui')
  })

  it('BEH-ARCH-2: a feature may skip architecture, going from logic to development', () => {
    const features = [feat('FEAT-A', { stage: 'logic' }), feat('FEAT-B', { stage: 'development' })]
    const cards = stageCards('app', 'app', features, [])
    expect(cards.map((c) => columnFor(c))).toEqual(['logic', 'development'])
  })
})
