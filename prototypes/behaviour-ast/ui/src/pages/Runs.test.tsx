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

  it('renders the runs in the order the API sends, with no client-side re-sort (BEH-UI-7)', async () => {
    // Oldest first on purpose: if the page re-sorted by start time, the first row would be the 9:00 run.
    mockApi({
      capUsd: 5,
      runs: [
        { ...run, id: 'early', project: 'vocab', started: '2026-11-04T08:00:00Z' },
        { ...run, id: 'late', project: 'kit', started: '2026-11-06T09:00:00Z' },
      ],
    })
    renderRuns()

    const items = within(await screen.findByRole('list', { name: 'Agent runs' })).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('vocab')
    expect(items[1]).toHaveTextContent('kit')
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

  it('BEH-ORCH-10 shows why a built pull request is not merged yet, under its link', async () => {
    mockBoth(
      { capUsd: 5, runs: [run] },
      {
        on: true,
        items: [
          { project: 'kit', slug: 'off-one', stage: 'build', status: 'done', pullRequest: 'https://github.com/o/r/pull/1', mergeNote: 'not merged: KIT_PIPELINE_MERGE is off, so Kit does not look at this pull request' },
          { project: 'kit', slug: 'waiting-one', stage: 'build', status: 'done', pullRequest: 'https://github.com/o/r/pull/2', mergeNote: 'waiting for its checks to pass, or for GitHub to say whether it can merge' },
        ],
      },
    )
    renderRuns()

    const items = within(await screen.findByRole('list', { name: 'Pipeline work items' })).getAllByRole('listitem')
    expect(items.find((li) => li.textContent?.includes('off-one'))).toHaveTextContent('not merged: KIT_PIPELINE_MERGE is off')
    expect(items.find((li) => li.textContent?.includes('waiting-one'))).toHaveTextContent('waiting for its checks to pass')
    expect(items.find((li) => li.textContent?.includes('waiting-one'))).not.toHaveTextContent('Merged')
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

  it('BEH-ORCH-17 shows each run still going: how long it has run and when it counts as lost, or how long it has waited', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-11-19T19:30:00Z'))
    mockBoth(
      {
        capUsd: 5,
        runs: [
          { ...run, id: 'q', stage: 'build', workItem: 'list-paging', status: 'queued', started: '2026-11-19T19:18:00Z', ended: null },
          { ...run, id: 't', stage: 'test', workItem: 'list-sorting', status: 'running', started: '2026-11-19T18:30:00Z', ended: null },
          { ...run, id: 'o', stage: 'triage', project: 'kit-ui', status: 'running', started: '2026-11-19T15:00:00Z', ended: null },
          { ...run, id: 'f' },
        ],
        claims: [
          { runId: 't', claimedAt: '2026-11-19T18:34:00Z', lostAt: '2026-11-19T21:04:00Z' },
          { runId: 'o', claimedAt: '2026-11-19T15:00:00Z', lostAt: '2026-11-19T17:30:00Z' },
        ],
      },
      { on: true, items: [] },
    )
    renderRuns()

    const now = await screen.findByRole('list', { name: 'Runs going now' })
    const rows = within(now).getAllByRole('listitem').map((li) => li.textContent)
    vi.useRealTimers()
    expect(rows).toHaveLength(3)
    expect(rows[0]).toContain('triage · kit-ui')
    expect(rows[0]).toContain('running 4h 30m · overdue')
    expect(rows[1]).toContain('test · kit · list-sorting')
    expect(rows[1]).toContain('running 56 min · counted lost at')
    expect(rows[2]).toContain('build · kit · list-paging')
    expect(rows[2]).toContain('waiting for a runner · 12 min')
  })

  it('BEH-ORCH-17 says when a run with its own time limit gives up', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-11-19T19:30:00Z'))
    mockBoth(
      {
        capUsd: 5,
        runs: [{ ...run, id: 't', stage: 'test', workItem: 'list-sorting', status: 'running', started: '2026-11-19T19:20:00Z', ended: null }],
        claims: [{ runId: 't', claimedAt: '2026-11-19T19:20:00Z', givesUpAt: '2026-11-19T19:50:00Z', lostAt: '2026-11-19T20:00:00Z' }],
      },
      { on: true, items: [] },
    )
    renderRuns()

    const now = await screen.findByRole('list', { name: 'Runs going now' })
    const row = within(now).getByRole('listitem').textContent
    vi.useRealTimers()
    expect(row).toContain('running 10 min · gives up at')
    expect(row).not.toContain('counted lost')
  })

  it('BEH-ORCH-17 shows no Now section when nothing is going', async () => {
    mockBoth({ capUsd: 5, runs: [run] }, { on: true, items: [] })
    renderRuns()

    await screen.findByRole('list', { name: 'Agent runs' })
    expect(screen.queryByRole('list', { name: 'Runs going now' })).toBeNull()
  })
})

describe('BEH-SPACE-5: each project has its own runs', () => {
  it("lists only that project's runs and pipeline items, and another project's are absent", async () => {
    // The runs and the pipeline are read whole and kept to one project here, in the page that shows them.
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) => {
        const body =
          path === '/api/pipeline'
            ? {
                on: true,
                items: [
                  { project: 'kit', slug: 'kit-item', stage: 'build', status: 'done' },
                  { project: 'snip-it', slug: 'snip-item', stage: 'build', status: 'done' },
                ],
              }
            : {
                capUsd: 5,
                runs: [
                  { ...run, id: 'k', project: 'kit', workItem: 'kit-item', costUsd: 0.1 },
                  { ...run, id: 's', project: 'snip-it', workItem: 'snip-item', costUsd: 0.2 },
                ],
              }
        return { ok: true, status: 200, statusText: 'OK', json: async () => body }
      }),
    )
    render(
      <MemoryRouter>
        <Runs only="kit" />
      </MemoryRouter>,
    )

    const runs = within(await screen.findByRole('list', { name: 'Agent runs' })).getAllByRole('listitem')
    expect(runs).toHaveLength(1)
    // Direct children only: each item's own runs are a list inside it.
    const items = Array.from((await screen.findByRole('list', { name: 'Pipeline work items' })).children)
    expect(items).toHaveLength(1)
    expect(items[0]).toHaveTextContent('kit · kit-item')
    expect(screen.queryByText(/snip-it/)).not.toBeInTheDocument()
    expect(screen.queryByText(/snip-item/)).not.toBeInTheDocument()
  })
})
