import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import EpicPage from './EpicPage'
import Epics from './Epics'
import FeaturePage from './FeaturePage'
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

const epic = (id: string): Epic => ({ id, title: `title of ${id}`, at: 'x.beh:1' })

const alpha = {
  app: 'alpha',
  project: 'kit',
  epics: [epic('EPIC-SHIP'), epic('EPIC-EMPTY')],
  features: [feat('FEAT-TOP', { epics: ['EPIC-SHIP'] }), feat('FEAT-PART', { in: ['FEAT-TOP'] })],
  behaviours: [beh('A-1', { in: ['FEAT-TOP'] }), beh('A-2', { in: ['FEAT-PART'], pending: true })],
  adjudication: { unreviewed: [] },
}

function renderAt(url: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      const app = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      const json = app ? alpha : { projects: [{ app: 'alpha', project: 'kit' }] }
      return { ok: true, status: 200, statusText: 'OK', json: async () => json }
    }),
  )
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/epics" element={<Epics />} />
        <Route path="/projects/:app/epics/:id" element={<EpicPage />} />
        <Route path="/projects/:app/features/:id" element={<FeaturePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EpicPage (BEH-EPIC-8)', () => {
  it('shows the epic its title, id and project, its behaviours built out of total, and its features linked (BEH-EPIC-8)', async () => {
    renderAt('/projects/alpha/epics/EPIC-SHIP')

    expect(await screen.findByRole('heading', { name: 'title of EPIC-SHIP', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('EPIC-SHIP · kit · alpha')).toBeInTheDocument()
    // The board card's count: a child feature's behaviours are the tagged feature's too.
    expect(screen.getByText('1 of 2 behaviours built')).toBeInTheDocument()
    const features = within(screen.getByRole('region', { name: 'Features' }))
    expect(features.getByRole('link', { name: 'title of FEAT-TOP' })).toHaveAttribute('href', '/projects/alpha/features/FEAT-TOP')
  })

  it('says so when no feature is in the epic (BEH-EPIC-8)', async () => {
    renderAt('/projects/alpha/epics/EPIC-EMPTY')

    expect(await screen.findByText('0 of 0 behaviours built')).toBeInTheDocument()
    expect(screen.getByText('No feature is in it yet.')).toBeInTheDocument()
  })

  it('names an epic the corpus does not have, instead of an empty page (BEH-EPIC-8)', async () => {
    renderAt('/projects/alpha/epics/EPIC-GONE')

    expect(await screen.findByRole('alert')).toHaveTextContent('No epic EPIC-GONE in alpha.')
  })

  it('is what the epic board card title opens (BEH-EPIC-8)', async () => {
    renderAt('/epics')
    expect(await screen.findByRole('link', { name: 'title of EPIC-SHIP' })).toHaveAttribute('href', '/projects/alpha/epics/EPIC-SHIP')
  })

  it('is what a feature page epic tag opens (BEH-EPIC-8)', async () => {
    renderAt('/projects/alpha/features/FEAT-TOP')
    expect(await screen.findByRole('link', { name: 'title of EPIC-SHIP' })).toHaveAttribute('href', '/projects/alpha/epics/EPIC-SHIP')
  })
})
