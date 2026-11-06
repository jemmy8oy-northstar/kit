import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Runs from './Runs'

function mockApi(body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )
}

const run = {
  id: 'r1',
  project: 'kit',
  model: 'claude-sonnet-5-5',
  status: 'finished',
  started: '2026-11-06T09:00:00Z',
  ended: '2026-11-06T09:02:05Z',
  inputTokens: 1000,
  outputTokens: 200,
  cacheWriteTokens: 30,
  cacheReadTokens: 4,
  costUsd: 0.456,
}

function renderRuns() {
  render(
    <MemoryRouter>
      <Runs />
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('Runs', () => {
  it('lists runs in the order sent, with the cap, duration, tokens and cost (BEH-USERS-9)', async () => {
    mockApi({ capUsd: 5, runs: [run, { ...run, id: 'r0', project: 'vocab', started: '2026-11-05T09:00:00Z', ended: null, status: 'running' }] })
    renderRuns()

    expect(await screen.findByRole('heading', { name: 'Runs', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('Each run is capped at $5.')).toBeInTheDocument()
    const items = within(await screen.findByRole('list', { name: 'Agent runs' })).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('kit')
    expect(items[0]).toHaveTextContent('finished · 2m 5s · 1234 tokens · $0.46')
    expect(items[1]).toHaveTextContent('vocab')
    expect(items[1]).toHaveTextContent('running')
  })

  it('says why a failed run failed in plain words', async () => {
    mockApi({
      capUsd: 5,
      runs: [
        { ...run, id: 'a', status: 'failed', failure: 'clone exit 128' },
        { ...run, id: 'b', status: 'failed', failure: 'run exit 3' },
        { ...run, id: 'c', status: 'failed' },
      ],
    })
    renderRuns()

    const items = within(await screen.findByRole('list', { name: 'Agent runs' })).getAllByRole('listitem')
    expect(items[0]).toHaveTextContent('Failed: could not fetch the repo (exit 128)')
    expect(items[1]).toHaveTextContent('Failed: the runner failed (exit 3)')
    expect(items[2]).not.toHaveTextContent('Failed:')
  })

  it('says so when there are no runs', async () => {
    mockApi({ capUsd: 5, runs: [] })
    renderRuns()

    expect(await screen.findByText('No runs yet.')).toBeInTheDocument()
  })
})
