import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Epics from './Epics'
import { hueOf } from '../board'
import { epicCards } from '../epics'
import type { Behaviour, Epic, Feature } from '../api/types'

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
