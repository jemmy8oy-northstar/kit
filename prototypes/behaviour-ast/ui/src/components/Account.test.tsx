import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import SignIn from './SignIn'
import Account from './Account'
import { githubNavigation } from './githubNavigation'

/**
 * BEH-USERS-1 / BEH-USERS-8: user accounts in the browser.
 *
 * What the server cannot see: with accounts OFF the page must be the Kit it
 * always was (no user-name field, a body of exactly `{ password }`, no account
 * area), and with them ON the right form must reach the right person.
 */

type Posted = { path: string; body: unknown }

/** A session that answers GET with `sessions` in turn (last one repeats) and records every POST. */
function server(sessions: unknown[], reply?: { status: number; body: unknown }) {
  const posted: Posted[] = []
  let gets = 0
  const fn = vi.fn((path: string, init?: RequestInit) => {
    if (init?.method === 'POST') {
      posted.push({ path, body: JSON.parse(init.body as string) })
      const r = reply ?? { status: 200, body: { ok: true, signedIn: true, user: 'sam' } }
      return Promise.resolve({
        ok: r.status < 400, status: r.status, json: () => Promise.resolve(r.body),
      } as Response)
    }
    if (path === '/api/session') {
      const s = sessions[Math.min(gets++, sessions.length - 1)]
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(s) } as Response)
    }
    throw new Error(`unexpected fetch to ${path}`)
  })
  return { fn, posted }
}

afterEach(() => { vi.unstubAllGlobals() })

const app = (
  <>
    <Account />
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

describe('Account area', () => {
  it('is absent when accounts are off, even when signed in', async () => {
    vi.stubGlobal('fetch', server([{ required: false, signedIn: true }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    expect(screen.queryByText(/Signed in as/)).not.toBeInTheDocument()
  })

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

describe('Connect your GitHub (BEH-USERS-1)', () => {
  const sam = {
    required: true, signedIn: true, accounts: true, user: 'sam',
    gitHubInstall: 'https://github.com/apps/kit/installations/new',
    gitHubAuthorize: 'https://github.com/login/oauth/authorize?client_id=c&state=1.ab',
  }

  afterEach(() => {
    vi.restoreAllMocks()
    sessionStorage.clear()
    window.history.replaceState(null, '', '/')
  })

  it('offers a user the install link, and the operator nothing', async () => {
    vi.stubGlobal('fetch', server([sam]).fn)
    const { unmount } = render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('link', { name: 'Connect your GitHub' })).toHaveAttribute('href', sam.gitHubInstall)
    unmount()

    vi.stubGlobal('fetch', server([{ required: true, signedIn: true, accounts: true, user: 'james', operator: true }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Signed in as james')).toBeInTheDocument()
    expect(screen.queryByText('Connect your GitHub')).not.toBeInTheDocument()
  })

  it('names the account once connected', async () => {
    vi.stubGlobal('fetch', server([{ ...sam, gitHub: 'sam-gh' }]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('sam-gh')).toBeInTheDocument()
    expect(screen.queryByText('Connect your GitHub')).not.toBeInTheDocument()
  })

  it('sends the install redirect straight on to authorise, keeping the installation as a hint', async () => {
    const to = vi.spyOn(githubNavigation, 'to').mockImplementation(() => {})
    window.history.replaceState(null, '', '/?installation_id=42&setup_action=install')
    vi.stubGlobal('fetch', server([sam]).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('Connecting your GitHub…')).toBeInTheDocument()
    expect(to).toHaveBeenCalledWith(sam.gitHubAuthorize)
    expect(sessionStorage.getItem('kit.github.installation')).toBe('42')
  })

  it('hands the code, state and hint to the server, then reloads without them', async () => {
    const reload = vi.spyOn(githubNavigation, 'reload').mockImplementation(() => {})
    sessionStorage.setItem('kit.github.installation', '42')
    window.history.replaceState(null, '', '/?code=c0de&state=1.ab')
    const s = server([sam], { status: 200, body: { ...sam, gitHub: 'sam-gh' } })
    vi.stubGlobal('fetch', s.fn)
    render(<SignIn>{app}</SignIn>)
    await vi.waitFor(() => expect(reload).toHaveBeenCalled())
    expect(s.posted).toEqual([{ path: '/api/github/connected', body: { code: 'c0de', state: '1.ab', installation: '42' } }])
    expect(sessionStorage.getItem('kit.github.installation')).toBeNull()
  })

  it('shows the server reason when the connection is refused, and drops the spent code from the URL', async () => {
    window.history.replaceState(null, '', '/?code=c0de&state=1.ab')
    vi.stubGlobal('fetch', server([sam], { status: 403, body: { error: 'bad-state', reason: 'connect again' } }).fn)
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByRole('alert')).toHaveTextContent('connect again')
    expect(window.location.search).toBe('')
  })
})
