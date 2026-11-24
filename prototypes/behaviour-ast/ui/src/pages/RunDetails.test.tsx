import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import RunDetails from './RunDetails'
import type { RecentRun, RunVisibility } from '../api/types'

function mockVisibility(body: RunVisibility) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )
}

function recent(id: string, over: Partial<RecentRun>): RecentRun {
  return {
    id,
    project: 'kit',
    workItem: null,
    stage: null,
    model: 'claude-sonnet-5-5',
    status: 'finished',
    started: '2026-11-20T09:00:00Z',
    ended: '2026-11-20T09:01:00Z',
    tokens: 1234,
    failure: null,
    costUsd: null,
    ...over,
  }
}

/** Item slug-b has finished: a spec with a recorded cost, and a review with none, which the spec requested. slug-a is running a build. */
const page: RunVisibility = {
  running: [
    {
      id: 'a1',
      project: 'kit',
      workItem: 'slug-a',
      stage: 'build',
      model: 'claude-haiku-5-5',
      started: '2026-11-20T09:10:00Z',
      flag: 'unsure which card',
      flaggedBy: 'claude-sonnet-5-5',
    },
  ],
  finished: [
    {
      project: 'kit',
      workItem: 'slug-b',
      stages: [
        { runId: 'b1', stage: 'spec', model: 'claude-sonnet-5-5', costUsd: 0.1 },
        { runId: 'b2', stage: 'review', model: 'claude-opus-5-5', costUsd: null },
      ],
      reviews: [{ runId: 'b2', stage: 'spec', requestedBy: 'claude-sonnet-5-5' }],
    },
  ],
  recent: [
    recent('a1', { workItem: 'slug-a', stage: 'build', model: 'claude-haiku-5-5', status: 'running', ended: null, started: '2026-11-20T09:10:00Z' }),
    recent('b2', { workItem: 'slug-b', stage: 'review', model: 'claude-opus-5-5', started: '2026-11-20T08:20:00Z', ended: '2026-11-20T08:25:00Z' }),
    recent('b1', { workItem: 'slug-b', stage: 'spec', model: 'claude-sonnet-5-5', started: '2026-11-20T08:00:00Z', ended: '2026-11-20T08:10:00Z', costUsd: 0.1 }),
  ],
}

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/runs/:id" element={<RunDetails />} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('RunDetails', () => {
  it('BEH-UI-9 shows the stages and their recorded costs, with unknown for a stage with no cost, and the reviews it requested (BEH-VIS-2)', async () => {
    mockVisibility(page)
    renderAt('/runs/b2')

    expect(await screen.findByRole('heading', { name: 'Run b2', level: 1 })).toBeInTheDocument()
    const stages = within(await screen.findByRole('list', { name: 'Stages of this work item' })).getAllByRole('listitem')
    expect(stages.map((li) => li.textContent)).toEqual([
      'spec · claude-sonnet-5-5 · cost $0.10',
      'review · claude-opus-5-5 · cost unknown',
    ])
    const reviews = within(screen.getByRole('list', { name: 'Reviews requested' })).getAllByRole('listitem')
    expect(reviews[0]).toHaveTextContent('Review requested by claude-sonnet-5-5 at spec')
  })

  it('BEH-UI-9 shows the agents running on the run work item and any review each flagged (BEH-VIS-1)', async () => {
    mockVisibility(page)
    renderAt('/runs/a1')

    expect(await screen.findByRole('heading', { name: 'Run a1', level: 1 })).toBeInTheDocument()
    const agents = within(await screen.findByRole('list', { name: 'Agents running on this work item' })).getAllByRole('listitem')
    expect(agents[0]).toHaveTextContent('build · claude-haiku-5-5')
    expect(agents[0]).toHaveTextContent('Flagged: unsure which card, by claude-sonnet-5-5')
  })

  it('BEH-UI-9 does not repeat the cost by stage that BEH-ORCH-9 shows on the runs page', async () => {
    mockVisibility(page)
    renderAt('/runs/b2')

    await screen.findByRole('heading', { name: 'Run b2', level: 1 })
    expect(screen.queryByRole('list', { name: 'Cost by stage' })).toBeNull()
    expect(screen.queryByText('Cost by stage')).toBeNull()
  })

  it('BEH-UI-9 says run not found for a run id that no longer exists, not an empty page', async () => {
    mockVisibility(page)
    renderAt('/runs/gone')

    expect(await screen.findByText(/run not found/i)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Run gone', level: 1 })).toBeNull()
  })

  it('BEH-UI-9 reads the run from GET /api/runs/visibility, the route the runs page cards read', async () => {
    mockVisibility(page)
    renderAt('/runs/b2')

    await screen.findByRole('heading', { name: 'Run b2', level: 1 })
    expect(vi.mocked(fetch).mock.calls.map((c) => String(c[0]))).toContain('/api/runs/visibility')
  })
})
