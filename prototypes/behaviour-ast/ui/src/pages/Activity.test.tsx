import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import Activity from './Activity'

function mockApi(feed: unknown) {
  const fetch = vi.fn(async (path: string) => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    json: async () => (path.endsWith('/api/session') ? { required: false, signedIn: true } : feed),
  }))
  vi.stubGlobal('fetch', fetch)
  return fetch
}

afterEach(() => vi.unstubAllGlobals())

describe('Activity', () => {
  it('lists commits newest first and says which projects have no history here (BEH-ACTIVITY-1)', async () => {
    mockApi({
      entries: [
        { commit: 'aaaaaaa', at: '2026-10-03T09:00:00Z', app: 'kit', summary: 'approve the activity page' },
        { commit: 'bbbbbbb', at: '2026-10-02T09:00:00Z', app: 'vocab', summary: 'add a note' },
      ],
      noHistory: [{ app: 'remote', reason: 'the corpus is not on this server\'s disk, so it has no history here' }],
    })
    render(
      <MemoryRouter>
        <Activity />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Activity', level: 1 })).toBeInTheDocument()
    const items = within(await screen.findByRole('list', { name: 'Recent activity' })).getAllByRole('listitem')
    expect(items.map((i) => i.textContent)).toEqual([
      expect.stringContaining('approve the activity page'),
      expect.stringContaining('add a note'),
    ])
    expect(within(items[0]).getByRole('link', { name: 'kit' })).toHaveAttribute('href', '/projects/kit')

    const gaps = screen.getByRole('region', { name: 'No history here' })
    expect(within(gaps).getByRole('link', { name: 'remote' })).toBeInTheDocument()
    expect(within(gaps).getByText(/has no history here/)).toBeInTheDocument()
  })

  it('links an id the project has to its page and leaves an unknown id as text (BEH-ACTIVITY-2)', async () => {
    const fetch = vi.fn(async (path: string) => ({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () =>
        path.endsWith('/api/projects/kit')
          ? { app: 'kit', behaviours: [{ id: 'BEH-USERS-2' }], features: [{ id: 'FEAT-BOARD' }], epics: [] }
          : {
              entries: [
                { commit: 'aaaaaaa', at: '2026-10-03T09:00:00Z', app: 'kit', summary: 'approve BEH-USERS-2 under FEAT-BOARD, not BEH-GONE-9' },
                { commit: 'bbbbbbb', at: '2026-10-02T09:00:00Z', app: 'kit', summary: 'edit BEH-USERS-2 again' },
              ],
              noHistory: [],
            },
    }))
    vi.stubGlobal('fetch', fetch)
    render(
      <MemoryRouter>
        <Activity />
      </MemoryRouter>,
    )

    const items = within(await screen.findByRole('list', { name: 'Recent activity' })).getAllByRole('listitem')
    const first = await within(items[0]).findByRole('link', { name: 'BEH-USERS-2' })
    expect(first).toHaveAttribute('href', '/projects/kit/behaviours/BEH-USERS-2')
    expect(within(items[0]).getByRole('link', { name: 'FEAT-BOARD' })).toHaveAttribute('href', '/projects/kit/features/FEAT-BOARD')
    expect(within(items[0]).queryByRole('link', { name: 'BEH-GONE-9' })).not.toBeInTheDocument()
    expect(items[0]).toHaveTextContent('not BEH-GONE-9')
    expect(await within(items[1]).findByRole('link', { name: 'BEH-USERS-2' })).toBeInTheDocument()
    // One project fetch for the project, however many entries name it.
    expect(fetch.mock.calls.filter(([p]) => String(p).endsWith('/api/projects/kit'))).toHaveLength(1)
  })

  it('says so when nothing has changed', async () => {
    mockApi({ entries: [], noHistory: [] })
    render(
      <MemoryRouter>
        <Activity />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Nothing has changed yet.')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'No history here' })).not.toBeInTheDocument()
  })

  it('links Activity from every page', async () => {
    window.history.pushState({}, '', '/board')
    mockApi({ projects: [], entries: [], noHistory: [] })
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Activity' })).toHaveAttribute('href', '/activity')
  })
})
