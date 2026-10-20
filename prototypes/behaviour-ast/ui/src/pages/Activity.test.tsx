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
