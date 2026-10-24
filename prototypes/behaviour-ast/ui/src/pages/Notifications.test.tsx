import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import Notifications from './Notifications'
import { nextWake } from '../nextWake'

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

function page() {
  render(
    <MemoryRouter>
      <Notifications />
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('Notifications', () => {
  it("lists the agent's messages newest first, each with when and what kind (BEH-NOTIFY-1)", async () => {
    mockApi({
      entries: [
        { at: '2026-10-24T14:05:00Z', kind: 'pr', text: 'raised kit#438' },
        { at: '2026-10-24T14:00:00Z', kind: 'wake', text: 'waking' },
      ],
      skipped: 0,
      reason: null,
    })
    page()

    expect(await screen.findByRole('heading', { name: 'Notifications', level: 1 })).toBeInTheDocument()
    const items = within(await screen.findByRole('list', { name: 'Notifications' })).getAllByRole('listitem')
    expect(items.map((i) => i.textContent)).toEqual([
      expect.stringContaining('raised kit#438'),
      expect.stringContaining('waking'),
    ])
    expect(items[0].textContent).toContain('Pull request')
    expect(items[1].textContent).toContain('Woke')
    expect(within(items[0]).getByText(/raised/).closest('li')?.querySelector('time')).toHaveAttribute('dateTime', '2026-10-24T14:05:00Z')
  })

  it('shows a message as text, never as markup', async () => {
    mockApi({ entries: [{ at: '2026-10-24T14:00:00Z', kind: 'note', text: '<b>not bold</b>' }], skipped: 0, reason: null })
    page()

    expect(await screen.findByText('<b>not bold</b>')).toBeInTheDocument()
    expect(document.querySelector('ol b')).toBeNull()
  })

  it('says why when the feed could not be read, rather than showing it empty', async () => {
    mockApi({ entries: [], skipped: 0, reason: 'GitHub answered 404: Not Found' })
    page()

    expect(await screen.findByRole('alert')).toHaveTextContent('could not be read: GitHub answered 404')
    expect(screen.queryByText('The agent has not said anything yet.')).not.toBeInTheDocument()
  })

  it('says so when the agent has said nothing, and counts lines it could not read', async () => {
    mockApi({ entries: [], skipped: 2, reason: null })
    page()

    expect(await screen.findByText('The agent has not said anything yet.')).toBeInTheDocument()
    expect(screen.getByText(/2 lines of the feed could not be read/)).toBeInTheDocument()
  })

  it('says when the agent next wakes, and that his next action wakes it sooner (BEH-NOTIFY-5)', async () => {
    const until = '2099-01-01T07:00:00Z'
    mockApi({
      entries: [],
      skipped: 0,
      reason: null,
      schedule: { suspended: true, until, byAgent: true, why: 'idle overnight', reason: null },
    })
    page()

    const wake = await screen.findByRole('region', { name: 'Next wake' })
    expect(wake).toHaveTextContent(`Next wake: ${new Date(until).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}`)
    expect(wake).toHaveTextContent('about 5 minutes after your next action in Kit')
    expect(wake).toHaveTextContent('Why: idle overnight')
  })

  it("says what each pause means for when the agent wakes, and never reads an unknown one as awake", () => {
    const now = new Date('2026-10-24T23:00:00Z')
    const s = { suspended: true, until: '2026-10-25T07:00:00Z', byAgent: false, why: null, reason: null }

    expect(nextWake(s, now)).toMatch(/^Paused by you until .*Actions in Kit do not wake it\.$/)
    expect(nextWake({ ...s, until: '2026-10-24T22:00:00Z' }, now)).toMatch(/sleep ended .*: it wakes within 5 minutes/)
    expect(nextWake({ ...s, until: null }, now)).toContain('paused with no end time')
    expect(nextWake({ ...s, suspended: false, until: null }, now)).toContain('is not asleep')
    expect(nextWake({ ...s, suspended: false, reason: 'GitHub answered 404: Not Found' }, now)).toBe(
      'Kit cannot tell when the agent next wakes: GitHub answered 404: Not Found',
    )
  })

  it('shows no wake time on a feed that has no agent behind it', async () => {
    mockApi({ entries: [], skipped: 0, reason: null, schedule: null })
    page()

    expect(await screen.findByText('The agent has not said anything yet.')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Next wake' })).not.toBeInTheDocument()
  })

  it('links Notifications from every page', async () => {
    window.history.pushState({}, '', '/board')
    mockApi({ projects: [], entries: [], noHistory: [] })
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Notifications' })).toHaveAttribute('href', '/notifications')
  })
})
