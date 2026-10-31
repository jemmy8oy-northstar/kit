import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Stages from './Stages'
import { hueOf } from '../board'
import { stageCards } from '../stages'
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
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      const app = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      return { ok: true, status: 200, statusText: 'OK', json: async () => (app ? { app, ...details[app] } : { projects: summaries }) }
    }),
  )
}

async function stage(name: string) {
  return within(await screen.findByRole('region', { name }))
}

function renderStages() {
  mockApi()
  render(
    <MemoryRouter initialEntries={['/stages']}>
      <Stages />
    </MemoryRouter>,
  )
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
})
