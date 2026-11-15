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

  it('sums an item\'s runs into one row and leaves out a run with no item (BEH-ORCH-6)', async () => {
    mockApi({
      capUsd: 5,
      runs: [
        { ...run, id: 'b', workItem: 'list-sorting', started: '2026-11-06T10:00:00Z', costUsd: 0.1, inputTokens: 100, outputTokens: 0, cacheWriteTokens: 0, cacheReadTokens: 0 },
        { ...run, id: 'a', workItem: 'list-sorting', started: '2026-11-06T09:00:00Z', costUsd: 0.25, inputTokens: 200, outputTokens: 0, cacheWriteTokens: 0, cacheReadTokens: 0 },
        { ...run, id: 't', workItem: null, costUsd: 9, inputTokens: 5000 },
      ],
    })
    renderRuns()

    const rows = within(await screen.findByRole('list', { name: 'Cost by work item' })).getAllByRole('listitem')
    expect(rows).toHaveLength(1)
    expect(rows[0]).toHaveTextContent('kit · list-sorting')
    expect(rows[0]).toHaveTextContent('2 runs · 300 tokens · $0.35')
    expect(within(screen.getByRole('list', { name: 'Agent runs' })).getAllByRole('listitem')).toHaveLength(3)
  })

  it('shows no by-item section when no run has an item (BEH-ORCH-6)', async () => {
    mockApi({ capUsd: 5, runs: [run] })
    renderRuns()

    await screen.findByRole('list', { name: 'Agent runs' })
    expect(screen.queryByRole('list', { name: 'Cost by work item' })).toBeNull()
  })

  it('says so when there are no runs', async () => {
    mockApi({ capUsd: 5, runs: [] })
    renderRuns()

    expect(await screen.findByText('No runs yet.')).toBeInTheDocument()
  })

  function mockBoth(runs: unknown, pipeline: unknown) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => ({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () => (String(url).includes('/api/pipeline') ? pipeline : runs),
      })),
    )
  }

  it("BEH-ORCH-9 shows each work item's stage, flag, review and runs per stage, and cost by stage", async () => {
    mockBoth(
      {
        capUsd: 5,
        runs: [
          { ...run, id: 'b', stage: 'review', model: 'claude-opus-5-5', workItem: 'list-sorting', costUsd: 0.3 },
          { ...run, id: 'a', stage: 'spec', model: 'claude-haiku-5-5', workItem: 'list-sorting', costUsd: 0.1 },
          { ...run, id: 't', stage: 'triage', model: 'claude-haiku-5-5', costUsd: 0.05 },
          { ...run, id: 'h', costUsd: 1 },
        ],
      },
      {
        on: true,
        items: [
          { project: 'kit', slug: 'triage #1', stage: 'triage', status: 'done' },
          {
            project: 'kit', slug: 'list-sorting', stage: 'review', status: 'stopped', stoppedBecause: 'escalated: he must pick',
            flag: 'unsure which card (level: high)', review: 'escalated', reviewNote: 'he must pick',
          },
        ],
      },
    )
    renderRuns()

    const items = within(await screen.findByRole('list', { name: 'Pipeline work items' })).getAllByRole('listitem')
    expect(items[0]).toHaveTextContent('kit · triage #1')
    const item = items.find((li) => li.textContent?.includes('list-sorting'))!
    expect(item).toHaveTextContent('review · stopped')
    expect(item).toHaveTextContent('Stopped: escalated: he must pick')
    expect(item).toHaveTextContent('Flagged: unsure which card (level: high)')
    expect(item).toHaveTextContent('Review: escalated — he must pick')
    const own = within(within(item).getByRole('list', { name: 'Runs of list-sorting' })).getAllByRole('listitem')
    expect(own.map((li) => li.textContent)).toEqual(['spec · claude-haiku-5-5 · 1234 tokens · $0.10', 'review · claude-opus-5-5 · 1234 tokens · $0.30'])
    const stages = within(screen.getByRole('list', { name: 'Cost by stage' })).getAllByRole('listitem')
    expect(stages.map((li) => li.textContent)).toEqual(['triage: 1 run · $0.05', 'spec: 1 run · $0.10', 'review: 1 run · $0.30'])
  })

  it('BEH-ORCH-10 shows a Merged line under the pull request link once Kit merged it', async () => {
    mockBoth(
      { capUsd: 5, runs: [run] },
      {
        on: true,
        items: [
          { project: 'kit', slug: 'merged-one', stage: 'build', status: 'done', pullRequest: 'https://github.com/o/r/pull/1', merged: true },
          { project: 'kit', slug: 'open-one', stage: 'build', status: 'done', pullRequest: 'https://github.com/o/r/pull/2' },
        ],
      },
    )
    renderRuns()

    const items = within(await screen.findByRole('list', { name: 'Pipeline work items' })).getAllByRole('listitem')
    expect(items.find((li) => li.textContent?.includes('merged-one'))).toHaveTextContent('Merged')
    expect(items.find((li) => li.textContent?.includes('open-one'))).not.toHaveTextContent('Merged')
  })

  it('BEH-ORCH-9 shows no pipeline section when the pipeline is off', async () => {
    mockBoth({ capUsd: 5, runs: [run] }, { on: false, items: [] })
    renderRuns()

    await screen.findByRole('list', { name: 'Agent runs' })
    expect(screen.queryByRole('heading', { name: 'Pipeline' })).not.toBeInTheDocument()
  })

  it('BEH-ORCH-9 says when the pipeline is on with no items yet', async () => {
    mockBoth({ capUsd: 5, runs: [] }, { on: true, items: [] })
    renderRuns()

    expect(await screen.findByText('No work items since Kit last started.')).toBeInTheDocument()
  })
})
