import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Agents from './Agents'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const BEHAVIOURS = path.join(HERE, '..', '..', '..', 'behaviours')

/** Answers each GET by its path: the project list, one project's detail, or the runs. Anything else is a test failure. */
function mockApi({
  projects,
  runs = [],
  details,
}: {
  projects: { app: string }[]
  runs?: unknown[]
  details: Record<string, unknown>
}) {
  const fetch = vi.fn(async (url: string) => {
    const p = decodeURIComponent(String(url))
    let body: unknown
    if (p.endsWith('/api/projects')) body = { projects }
    else if (p.endsWith('/api/runs')) body = { capUsd: 5, runs }
    else if (p.includes('/api/projects/')) body = details[p.slice(p.indexOf('/api/projects/') + '/api/projects/'.length)]
    else throw new Error(`unexpected request ${p}`)
    return { ok: true, status: 200, statusText: 'OK', json: async () => body }
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
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
  stage: 'build',
  workItem: 'list-sorting',
}

/** A behaviour as the project view reads it; the page only looks at the fields that decide the queue. */
function card(id: string, over: Record<string, unknown> = {}) {
  return {
    id,
    title: `${id} title`,
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'defined', ref: null },
    review: { state: 'approved', note: null },
    asks: null,
    pending: true,
    layer: 'technical',
    at: `kit.beh:${id}`,
    in: [],
    ...over,
  }
}

function renderAgents(path = '/agents') {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Agents />
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('Agents', () => {
  it('BEH-AGENTPAGE-1 lists the agents it knows, one per entry, and shows no work or queue until one is picked', async () => {
    mockApi({
      projects: [{ app: 'kit' }, { app: 'vocab' }],
      runs: [run],
      details: { kit: { app: 'kit', behaviours: [card('BEH-A-1')] }, vocab: { app: 'vocab', behaviours: [] } },
    })
    renderAgents()

    const agents = within(await screen.findByRole('list', { name: 'Agents' })).getAllByRole('listitem')
    expect(agents).toHaveLength(2)
    expect(agents[0]).toHaveTextContent('kit')
    expect(agents[1]).toHaveTextContent('vocab')
    expect(screen.queryByRole('list', { name: 'Recent runs' })).toBeNull()
    expect(screen.queryByText('No recent work')).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Queue' })).toBeNull()
    expect(screen.queryByText('Nothing in the queue')).toBeNull()
  })

  it('BEH-AGENTPAGE-1 picking an agent shows its runs from GET /api/runs, newest first, with status, stage and cost, for that project only', async () => {
    const fetch = mockApi({
      projects: [{ app: 'kit' }, { app: 'vocab' }],
      runs: [
        { ...run, id: 'old', started: '2026-11-05T09:00:00Z', status: 'failed', stage: 'review', costUsd: 0.1, workItem: null },
        { ...run, id: 'new', started: '2026-11-06T10:00:00Z', status: 'finished', stage: 'build', costUsd: 0.5 },
        { ...run, id: 'other', project: 'vocab', started: '2026-11-07T10:00:00Z', costUsd: 9 },
      ],
      details: { kit: { app: 'kit', behaviours: [] }, vocab: { app: 'vocab', behaviours: [] } },
    })
    renderAgents()

    fireEvent.click(await screen.findByRole('link', { name: 'kit' }))

    const items = within(await screen.findByRole('list', { name: 'Recent runs' })).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('finished · build · $0.50')
    expect(items[0]).toHaveTextContent('list-sorting')
    expect(items[1]).toHaveTextContent('failed · review · $0.10')
    expect(screen.queryByText(/\$9\.00/)).toBeNull()
    expect(fetch.mock.calls.map(([url]) => String(url))).toEqual(expect.arrayContaining([expect.stringMatching(/\/api\/runs$/)]))
  })

  it('BEH-AGENTPAGE-1 the queue marks approved, pending cards without a needs-context line as unblocked, in board order, and the rest as waiting on him', async () => {
    mockApi({
      projects: [{ app: 'kit' }],
      details: {
        kit: {
          app: 'kit',
          behaviours: [
            card('BEH-Q-PRIO-2', { priority: 2 }),
            card('BEH-Q-NONE'),
            card('BEH-Q-PRIO-1', { priority: 1 }),
            card('BEH-Q-WAIT', { needsContext: 'Add the API key first.' }),
            card('BEH-Q-UNREVIEWED', { review: { state: 'unreviewed', note: null } }),
            card('BEH-Q-BUILT', { pending: false }),
          ],
        },
      },
    })
    renderAgents('/agents?agent=kit')

    const unblocked = within(await screen.findByRole('list', { name: 'Unblocked' })).getAllByRole('listitem')
    expect(unblocked.map((li) => li.textContent)).toEqual([
      expect.stringContaining('BEH-Q-PRIO-1'),
      expect.stringContaining('BEH-Q-PRIO-2'),
      expect.stringContaining('BEH-Q-NONE'),
    ])
    const waiting = within(screen.getByRole('list', { name: 'Waiting on you' })).getAllByRole('listitem')
    expect(waiting).toHaveLength(1)
    expect(waiting[0]).toHaveTextContent('BEH-Q-WAIT')
    expect(waiting[0]).toHaveTextContent('Add the API key first.')
    expect(screen.queryByText(/BEH-Q-UNREVIEWED/)).toBeNull()
    expect(screen.queryByText(/BEH-Q-BUILT/)).toBeNull()
  })

  it('BEH-AGENTPAGE-1 an agent with no runs says "No recent work", and an empty queue says "Nothing in the queue"', async () => {
    mockApi({ projects: [{ app: 'vocab' }], runs: [run], details: { vocab: { app: 'vocab', behaviours: [] } } })
    renderAgents('/agents?agent=vocab')

    expect(await screen.findByText('No recent work')).toBeInTheDocument()
    expect(screen.getByText('Nothing in the queue')).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Recent runs' })).toBeNull()
    expect(screen.queryByRole('list', { name: 'Unblocked' })).toBeNull()
  })

  it('BEH-AGENTPAGE-1 browsing the page issues only GET requests and writes no corpus file', async () => {
    const before = fs.readdirSync(BEHAVIOURS).map((f) => [f, fs.readFileSync(path.join(BEHAVIOURS, f), 'utf8')] as const)
    const fetch = mockApi({
      projects: [{ app: 'kit' }],
      runs: [run],
      details: { kit: { app: 'kit', behaviours: [card('BEH-G-1')] } },
    })
    renderAgents()

    fireEvent.click(await screen.findByRole('link', { name: 'kit' }))
    await screen.findByRole('list', { name: 'Recent runs' })
    await screen.findByRole('list', { name: 'Unblocked' })

    expect(fetch).toHaveBeenCalled()
    for (const [, init] of fetch.mock.calls as unknown as [string, RequestInit | undefined][]) {
      expect(init?.method ?? 'GET').toBe('GET')
    }
    const after = fs.readdirSync(BEHAVIOURS).map((f) => [f, fs.readFileSync(path.join(BEHAVIOURS, f), 'utf8')] as const)
    expect(after).toEqual(before)
  })

  it('BEH-AGENTPAGE-1 the card sits in kit-ui.beh under FEAT-AGENTS with browser verbs, not in kit.beh FEAT-SERVER', () => {
    const kit = fs.readFileSync(path.join(BEHAVIOURS, 'kit.beh'), 'utf8')
    const ui = fs.readFileSync(path.join(BEHAVIOURS, 'kit-ui.beh'), 'utf8')

    expect(kit).not.toMatch(/BEH-UI-5/)
    expect(kit).not.toMatch(/agents page/i)

    const block = ui.slice(ui.indexOf('behaviour BEH-AGENTPAGE-1 '), ui.indexOf('feature FEAT-INBOX'))
    expect(block).toMatch(/^behaviour BEH-AGENTPAGE-1 /m)
    expect(block).toMatch(/^ {2}in FEAT-AGENTS$/m)
    expect(block).toMatch(/^ {2}when opens page:KitAgents$/m)
    expect(block).not.toMatch(/\bruns command:|\breports\b/)
    expect(ui).toMatch(/^feature FEAT-AGENTS "Agents"$/m)
  })
})
