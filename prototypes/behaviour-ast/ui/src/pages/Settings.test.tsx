import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import SignIn from '../components/SignIn'
import Settings, { GitHubReturn } from './Settings'
import { githubNavigation } from '../components/githubNavigation'

/**
 * BEH-USERS-1 / BEH-USERS-8: user accounts in the browser.
 *
 * What the server cannot see: with accounts OFF the page must be the Kit it
 * always was (no user-name field, a body of exactly `{ password }`, no account
 * area), and with them ON the right form must reach the right person.
 */

type Posted = { path: string; body: unknown }

/** A session that answers GET with `sessions` in turn (last one repeats) and records every POST. */
function server(sessions: unknown[], reply?: { status: number; body: unknown }, projects: unknown[] = [], idle = 'health') {
  const posted: Posted[] = []
  let gets = 0
  const fn = vi.fn((path: string, init?: RequestInit) => {
    if (init?.method === 'POST') {
      const sent = JSON.parse(init.body as string)
      posted.push({ path, body: sent })
      const r = reply ?? { status: 200, body: { ok: true, signedIn: true, user: 'sam' } }
      if (path === '/api/settings/idle' && r.status < 400) idle = sent.idle
      return Promise.resolve({
        ok: r.status < 400, status: r.status, json: () => Promise.resolve(r.body),
      } as Response)
    }
    if (path === '/api/session') {
      const s = sessions[Math.min(gets++, sessions.length - 1)]
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(s) } as Response)
    }
    if (path === '/api/projects') {
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ projects }) } as Response)
    }
    if (path === '/api/settings/idle') {
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ idle }) } as Response)
    }
    throw new Error(`unexpected fetch to ${path}`)
  })
  return { fn, posted }
}

afterEach(() => { vi.unstubAllGlobals() })

const app = (
  <>
    <MemoryRouter><Settings /></MemoryRouter>
    <p>the corpus</p>
  </>
)

describe('sign-in with accounts', () => {
  it('has no user name field and sends only { password } when accounts are off', async () => {
    const s = server([{ required: true, signedIn: false }])
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Password'), { target: { value: 'pw' } })
    expect(screen.queryByLabelText('User name')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    expect(s.posted).toEqual([{ path: '/api/session', body: { password: 'pw' } }])
    expect(screen.queryByText(/Signed in as/)).not.toBeInTheDocument()
  })

  it('shows a user name field and sends { username, password } when accounts are on', async () => {
    const s = server([
      { required: true, signedIn: false, accounts: true },
      { required: true, signedIn: true, accounts: true, user: 'sam' },
    ])
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    const name = await screen.findByLabelText('User name')
    expect(name).toHaveAttribute('autocomplete', 'username')
    fireEvent.change(name, { target: { value: 'sam' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'a-long-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    expect(s.posted).toEqual([{ path: '/api/session', body: { username: 'sam', password: 'a-long-password' } }])
    expect(screen.getByText('Signed in as sam')).toBeInTheDocument()
  })

  it('leaves the user name optional: empty sends { password } (the operator)', async () => {
    const s = server([
      { required: true, signedIn: false, accounts: true },
      { required: true, signedIn: true, accounts: true, user: 'james', operator: true },
    ])
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Password'), { target: { value: 'pw' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    expect(s.posted[0].body).toEqual({ password: 'pw' })
  })
})

describe('Settings page (BEH-SETTINGS-1)', () => {
  it('says this Kit has no accounts when they are off, even when signed in', async () => {
    vi.stubGlobal('fetch', server([{ required: false, signedIn: true }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('heading', { name: 'Settings' })).toBeInTheDocument()
    expect(screen.getByText(/has no user accounts/)).toBeInTheDocument()
    expect(screen.queryByText(/Signed in as/)).not.toBeInTheDocument()
  })

  it('tells the operator Kit runs on its own GitHub App', async () => {
    vi.stubGlobal('fetch', server([
      { required: true, signedIn: true, accounts: true, user: 'james', operator: true },
    ]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('heading', { name: 'GitHub' })).toBeInTheDocument()
    expect(screen.getByText(/through its own GitHub App/)).toBeInTheDocument()
  })

  it('sends a GitHub return that lands elsewhere on to Settings, query intact', async () => {
    vi.stubGlobal('fetch', server([{ required: false, signedIn: true }]).fn)
    render(
      <MemoryRouter initialEntries={['/?installation_id=42&setup_action=install']}>
        <GitHubReturn />
        <Routes>
          <Route path="/" element={<p>the projects</p>} />
          <Route path="/settings" element={<Probe />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(await screen.findByText('/settings?installation_id=42&setup_action=install')).toBeInTheDocument()
  })

  it('leaves every other page where it is', () => {
    render(
      <MemoryRouter initialEntries={['/board?app=kit']}>
        <GitHubReturn />
        <Routes><Route path="/board" element={<p>the board</p>} /></Routes>
      </MemoryRouter>,
    )
    expect(screen.getByText('the board')).toBeInTheDocument()
  })
})

function Probe() {
  const { pathname, search } = useLocation()
  return <p>{pathname + search}</p>
}

describe('Account area', () => {

  it('offers only "Add a user" to the operator', async () => {
    vi.stubGlobal('fetch', server([
      { required: true, signedIn: true, accounts: true, user: 'james', operator: true },
    ]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as james')).toBeInTheDocument()
    expect(screen.getByLabelText('User name')).toBeInTheDocument()
    expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
  })

  it('offers only "Change password" to anyone else', async () => {
    vi.stubGlobal('fetch', server([
      { required: true, signedIn: true, accounts: true, user: 'sam' },
    ]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as sam')).toBeInTheDocument()
    expect(screen.getByLabelText('Current password')).toBeInTheDocument()
    expect(screen.queryByText('Add a user')).not.toBeInTheDocument()
  })

  it('posts the change-password body and says so', async () => {
    const s = server([{ required: true, signedIn: true, accounts: true, user: 'sam' }],
      { status: 200, body: { ok: true, user: 'sam' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Current password'), { target: { value: 'old-old-old-old' } })
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'new-new-new-new' } })
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }))
    expect(await screen.findByText('Password changed.')).toBeInTheDocument()
    expect(s.posted).toEqual([{
      path: '/api/session/password', body: { current: 'old-old-old-old', next: 'new-new-new-new' },
    }])
  })

  it('shows the server reason when the password change is refused', async () => {
    const s = server([{ required: true, signedIn: true, accounts: true, user: 'sam' }],
      { status: 400, body: { error: 'bad-new-password', reason: 'a password needs at least 12 characters' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Current password'), { target: { value: 'x' } })
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'short' } })
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('a password needs at least 12 characters')
  })

  it('posts the add-user body and says who was added', async () => {
    const s = server([{ required: true, signedIn: true, accounts: true, user: 'james', operator: true }],
      { status: 201, body: { ok: true, user: 'ana' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('User name'), { target: { value: 'ana' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'twelve-chars-ok' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    expect(await screen.findByText('Added ana.')).toBeInTheDocument()
    expect(s.posted).toEqual([{
      path: '/api/users', body: { username: 'ana', password: 'twelve-chars-ok' },
    }])
  })

  it('shows the server reason when adding a user is refused', async () => {
    const s = server([{ required: true, signedIn: true, accounts: true, user: 'james', operator: true }],
      { status: 409, body: { error: 'user-exists', reason: 'there is already a user called ana' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('User name'), { target: { value: 'ana' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'twelve-chars-ok' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('there is already a user called ana')
  })
})

describe('Promotion to main (BEH-SETTINGS-4)', () => {
  const james = { required: true, signedIn: true, accounts: true, user: 'james', operator: true }
  const projects = [
    { app: 'kit', project: 'kit', promote: 'self' },
    { app: 'kit-ui', project: 'kit' },
    { app: 'snip-it', project: 'snip-it' },
    { app: 'ideas', project: 'ideas', idea: true },
    { app: 'longlist', project: 'longlist', notReal: true },
  ]

  it('shows the operator one switch per project, on only where the grant is', async () => {
    vi.stubGlobal('fetch', server([james], undefined, projects).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('heading', { name: 'Promotion to main' })).toBeInTheDocument()
    expect(await screen.findByRole('checkbox', { name: 'kit' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'snip-it' })).not.toBeChecked()
    // A second file of a project, an idea and a trial are not projects to grant.
    expect(screen.queryByRole('checkbox', { name: 'kit-ui' })).not.toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'ideas' })).not.toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'longlist' })).not.toBeInTheDocument()
    expect(screen.getByText(/nothing already on main changes/)).toBeInTheDocument()
    expect(screen.getByText(/New projects start off/)).toBeInTheDocument()
  })

  it('posts the named state when a switch is flipped, and says so', async () => {
    const s = server([james], { status: 200, body: { app: 'snip-it' } }, projects)
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.click(await screen.findByRole('checkbox', { name: 'snip-it' }))
    expect(await screen.findByText('The agent may now promote snip-it to main.')).toBeInTheDocument()
    expect(s.posted).toEqual([{ path: '/api/projects/snip-it/promote', body: { promote: true } }])

    fireEvent.click(screen.getByRole('checkbox', { name: 'kit' }))
    expect(await screen.findByText('The agent no longer promotes kit to main.')).toBeInTheDocument()
    expect(s.posted[1]).toEqual({ path: '/api/projects/kit/promote', body: { promote: false } })
  })

  it('shows the server reason when the change is refused', async () => {
    vi.stubGlobal('fetch', server([james], { status: 403, body: { error: 'not-yours', reason: 'not yours to change' } }, projects).fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.click(await screen.findByRole('checkbox', { name: 'snip-it' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('not yours to change')
  })

  it('does not show the section to anyone but the operator', async () => {
    vi.stubGlobal('fetch', server([{ required: true, signedIn: true, accounts: true, user: 'sam' }], undefined, projects).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as sam')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Promotion to main' })).not.toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })
})

describe('Idle behaviour (BEH-SETTINGS-5)', () => {
  const james = { required: true, signedIn: true, accounts: true, user: 'james', operator: true }

  it('shows the operator four choices, the default ticked, and says asked-for work is always built', async () => {
    vi.stubGlobal('fetch', server([james]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('heading', { name: 'When nothing is asked of the agent' })).toBeInTheDocument()
    expect(await screen.findByRole('radio', { name: 'Work on test health when idle' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Wake on any change' })).not.toBeChecked()
    expect(screen.getByRole('radio', { name: 'Suggest ideas when idle' })).not.toBeChecked()
    expect(screen.getByRole('radio', { name: 'Idle until asked' })).not.toBeChecked()
    expect(screen.getByText(/Work you have asked for is always built/)).toBeInTheDocument()
  })

  it('shows the mode the server says is chosen', async () => {
    vi.stubGlobal('fetch', server([james], undefined, [], 'ideas').fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('radio', { name: 'Suggest ideas when idle' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Work on test health when idle' })).not.toBeChecked()
  })

  it('posts the named mode when one is picked, says so, and shows it ticked', async () => {
    const s = server([james], { status: 200, body: { app: 'kit' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.click(await screen.findByRole('radio', { name: 'Idle until asked' }))
    expect(await screen.findByText(/the agent will now: idle until asked/)).toBeInTheDocument()
    expect(s.posted).toEqual([{ path: '/api/settings/idle', body: { idle: 'idle' } }])
    expect(await screen.findByRole('radio', { name: 'Idle until asked' })).toBeChecked()
  })

  it('shows the server reason when the change is refused', async () => {
    vi.stubGlobal('fetch', server([james], { status: 403, body: { error: 'not-yours', reason: 'not yours to change' } }).fn)
    render(<SignIn>{app}</SignIn>)
    fireEvent.click(await screen.findByRole('radio', { name: 'Wake on any change' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('not yours to change')
  })

  it('does not show the choice to anyone but the operator', async () => {
    vi.stubGlobal('fetch', server([{ required: true, signedIn: true, accounts: true, user: 'sam' }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as sam')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'When nothing is asked of the agent' })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
  })
})

describe('Connect your GitHub (BEH-USERS-1)', () => {
  const sam = {
    required: true, signedIn: true, accounts: true, user: 'sam',
    gitHubInstall: 'https://github.com/apps/kit/installations/new',
    gitHubAuthorize: 'https://github.com/login/oauth/authorize?client_id=c&state=1.ab',
  }

  afterEach(() => {
    vi.restoreAllMocks()
    window.history.replaceState(null, '', '/')
  })

  it('offers a user the install link, and the operator nothing', async () => {
    vi.stubGlobal('fetch', server([sam]).fn)
    const { unmount } = render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('link', { name: 'Connect your GitHub' })).toHaveAttribute('href', sam.gitHubInstall)
    // An account that already has the App cannot install it again, so it authorises directly.
    expect(screen.getByRole('link', { name: 'Already installed? Connect it' })).toHaveAttribute('href', sam.gitHubAuthorize)
    unmount()

    vi.stubGlobal('fetch', server([{ required: true, signedIn: true, accounts: true, user: 'james', operator: true }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as james')).toBeInTheDocument()
    expect(screen.queryByText('Connect your GitHub')).not.toBeInTheDocument()
  })

  // BEH-SETTINGS-3: once the server offers him the install link, he connects his personal GitHub.
  it('offers the operator his personal GitHub, and names it once connected', async () => {
    const james = { required: true, signedIn: true, accounts: true, user: 'james', operator: true, gitHubInstall: sam.gitHubInstall, gitHubAuthorize: sam.gitHubAuthorize }
    vi.stubGlobal('fetch', server([james]).fn)
    const { unmount } = render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('link', { name: 'Connect your personal GitHub' })).toHaveAttribute('href', sam.gitHubInstall)
    expect(screen.getByRole('link', { name: 'Already installed? Connect it' })).toHaveAttribute('href', sam.gitHubAuthorize)
    expect(screen.getByLabelText('User name')).toBeInTheDocument()
    unmount()

    vi.stubGlobal('fetch', server([{ ...james, gitHub: 'james-gh' }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('james-gh')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Connect your personal GitHub' })).not.toBeInTheDocument()
  })

  it('names the account once connected', async () => {
    vi.stubGlobal('fetch', server([{ ...sam, gitHub: 'sam-gh' }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('sam-gh')).toBeInTheDocument()
    expect(screen.queryByText('Connect your GitHub')).not.toBeInTheDocument()
    // BEH-USERS-10: their GitHub is how they sign in now, so there is no password to change.
    expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
  })

  it('sends the install redirect straight on to authorise, posting nothing', async () => {
    const to = vi.spyOn(githubNavigation, 'to').mockImplementation(() => {})
    window.history.replaceState(null, '', '/?installation_id=42&setup_action=install')
    const s = server([sam])
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Connecting your GitHub…')).toBeInTheDocument()
    // The text renders a commit before the effect that navigates: wait for it, or a
    // slow runner asserts first and the late effect calls the restored, real `to`.
    await vi.waitFor(() => expect(to).toHaveBeenCalledWith(sam.gitHubAuthorize))
    expect(s.posted).toEqual([])
  })

  it('hands the code and state to the server, then reloads without them', async () => {
    const reload = vi.spyOn(githubNavigation, 'reload').mockImplementation(() => {})
    window.history.replaceState(null, '', '/?code=c0de&state=1.ab&installation_id=42')
    const s = server([sam], { status: 200, body: { ...sam, gitHub: 'sam-gh' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    await vi.waitFor(() => expect(reload).toHaveBeenCalled())
    // The redirect's installation_id is never sent: anyone can type one.
    expect(s.posted).toEqual([{ path: '/api/github/connected', body: { code: 'c0de', state: '1.ab' } }])
  })

  it('shows the server reason when the connection is refused, and drops the spent code from the URL', async () => {
    window.history.replaceState(null, '', '/?code=c0de&state=1.ab')
    vi.stubGlobal('fetch', server([sam], { status: 403, body: { error: 'bad-state', reason: 'connect again' } }).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('alert')).toHaveTextContent('connect again')
    expect(window.location.search).toBe('')
  })
})
