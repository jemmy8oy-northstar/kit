import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import Settings from '../pages/Settings'
import SignIn from './SignIn'
import { FlagsProvider } from './FeatureGate'

/**
 * BEH-FLAGS-1 / BEH-FLAGS-2: Kit's features switched on and off for him and for testers.
 * The server decides the group and what is off for it; the browser hides the navigation entry and
 * answers a direct visit with a short message. It hides screens, it guards nothing.
 */

const james = { required: true, signedIn: true, accounts: true, user: 'james', operator: true }
const ann = { required: true, signedIn: true, accounts: true, user: 'ann' }

const table = [
  { id: 'FEAT-INBOX', title: 'His items, one at a time', operator: true, testers: false },
  { id: 'FEAT-HEALTH', title: 'Kit health', operator: true, testers: true },
  { id: 'FEAT-PARSER', title: 'Parser', operator: true, testers: false },
]

type Posted = { path: string; body: unknown }

function server(session: unknown, switches: unknown) {
  const posted: Posted[] = []
  const fn = vi.fn((path: string, init?: RequestInit) => {
    if (init?.method === 'POST') {
      posted.push({ path, body: JSON.parse(init.body as string) })
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ ok: true, app: 'kit' }) } as Response)
    }
    const body = path === '/api/session' ? session
      : path === '/api/settings/features' ? switches
        : path === '/api/settings/idle' ? { idle: 'health', order: ['health'] }
          : { projects: [] }
    return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) } as Response)
  })
  return { fn, posted }
}

afterEach(() => {
  vi.unstubAllGlobals()
  window.history.pushState({}, '', '/')
})

describe('Settings lists the features (BEH-FLAGS-1)', () => {
  it('BEH-FLAGS-1 lists every feature with a You and a Testers switch, defaulting as the server says', async () => {
    const s = server(james, { group: 'operator', off: [], features: table })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn><FlagsProvider><MemoryRouter><Settings /></MemoryRouter></FlagsProvider></SignIn>)

    expect(await screen.findByRole('heading', { name: 'Features' })).toBeInTheDocument()
    expect(await screen.findByRole('checkbox', { name: 'You: His items, one at a time' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Testers: His items, one at a time' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Testers: Kit health' })).toBeChecked()
  })

  it('BEH-FLAGS-1 says a feature with no screen of its own has no visible effect yet', async () => {
    vi.stubGlobal('fetch', server(james, { group: 'operator', off: [], features: table }).fn)
    render(<SignIn><FlagsProvider><MemoryRouter><Settings /></MemoryRouter></FlagsProvider></SignIn>)

    const parser = (await screen.findByText('FEAT-PARSER')).closest('li')!
    expect(within(parser).getByText(/no visible effect/)).toBeInTheDocument()
    expect(within(screen.getByText('FEAT-INBOX').closest('li')!).queryByText(/no visible effect/)).not.toBeInTheDocument()
  })

  it('BEH-FLAGS-1 posts the named switch when Testers is ticked', async () => {
    const s = server(james, { group: 'operator', off: [], features: table })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn><FlagsProvider><MemoryRouter><Settings /></MemoryRouter></FlagsProvider></SignIn>)

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Testers: His items, one at a time' }))

    expect(await screen.findByText('FEAT-INBOX is now on for testers.')).toBeInTheDocument()
    expect(s.posted).toEqual([{ path: '/api/settings/features', body: { feature: 'FEAT-INBOX', group: 'testers', on: true } }])
  })

  it('BEH-FLAGS-1 shows a tester no Features section', async () => {
    vi.stubGlobal('fetch', server(ann, { group: 'testers', off: ['FEAT-INBOX'] }).fn)
    render(<SignIn><FlagsProvider><MemoryRouter><Settings /></MemoryRouter></FlagsProvider></SignIn>)

    expect(await screen.findByText(/Signed in as ann/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Features' })).not.toBeInTheDocument()
  })
})

describe('A switched-off feature is gone for that group (BEH-FLAGS-2)', () => {
  it('BEH-FLAGS-2 leaves the navigation entry of a feature that is off for testers out of their bar', async () => {
    vi.stubGlobal('fetch', server(ann, { group: 'testers', off: ['FEAT-INBOX', 'FEAT-PARSER'] }).fn)
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(await within(nav).findByRole('link', { name: 'Health' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Inbox' })).not.toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Activity' })).toBeInTheDocument()
  })

  it('BEH-FLAGS-2 shows the same feature in his bar when it is on for him', async () => {
    vi.stubGlobal('fetch', server(james, { group: 'operator', off: [], features: table }).fn)
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(await within(nav).findByRole('link', { name: 'Inbox' })).toBeInTheDocument()
  })

  it('BEH-FLAGS-2 answers a direct visit to a switched-off page with a short message, not the page', async () => {
    window.history.pushState({}, '', '/inbox')
    vi.stubGlobal('fetch', server(ann, { group: 'testers', off: ['FEAT-INBOX'] }).fn)
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Not available' })).toBeInTheDocument()
    expect(screen.getByText(/isn't switched on for you/)).toBeInTheDocument()
  })

  it('BEH-FLAGS-2 hides a board view that is off and points the Board link at one that is on', async () => {
    window.history.pushState({}, '', '/epics')
    vi.stubGlobal('fetch', server(ann, { group: 'testers', off: ['FEAT-BOARD', 'FEAT-EPICS'] }).fn)
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Not available' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Board' })).toHaveAttribute('href', '/stages')
  })

  it('BEH-FLAGS-2 shows everything when the switches cannot be read: it hides screens, it is not a guard', async () => {
    const s = server(ann, null)
    vi.stubGlobal('fetch', vi.fn((path: string, init?: RequestInit) =>
      path === '/api/settings/features'
        ? Promise.resolve({ ok: false, status: 500, json: () => Promise.resolve({ error: 'x', reason: 'down' }) } as Response)
        : s.fn(path, init)))
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(await within(nav).findByRole('link', { name: 'Inbox' })).toBeInTheDocument()
  })
})
