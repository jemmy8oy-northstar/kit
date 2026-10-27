import { afterEach, describe, expect, it, vi } from 'vitest'
// `fireEvent` rather than `@testing-library/user-event`: the latter is not a
// dependency of this package, and adding one is a packaging change — his call
// under claude-code-bot#83, not something to smuggle in beside a feature.
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { githubNavigation } from './githubNavigation'
import SignIn from './SignIn'

/**
 * The gate in front of a deployed Kit (kit#46).
 *
 * The case these exist for is NOT "the password works" — that is pinned on the
 * server, over a real socket, in `kit.test.js`. It is the one the server cannot
 * see: which of the three states the page decides it is in. A local Kit that
 * renders a password field, or a locked Kit that renders the app for one frame
 * and fires read requests, are both failures no server-side test can reach.
 */

const fetchMock = (state: unknown, signInStatus = 200) =>
  vi.fn((path: string, init?: RequestInit) => {
    if (path === '/api/session' && init?.method === 'POST') {
      return Promise.resolve({
        ok: signInStatus === 200,
        status: signInStatus,
        json: () => Promise.resolve(
          signInStatus === 200
            ? { ok: true, signedIn: true }
            : { error: 'bad-password', reason: 'that is not the password' },
        ),
      } as Response)
    }
    if (path === '/api/session') {
      return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(state) } as Response)
    }
    throw new Error(`unexpected fetch to ${path}`)
  })

afterEach(() => { vi.unstubAllGlobals() })

const app = <p>the corpus</p>

describe('SignIn', () => {
  it('shows no password field at all when the Kit has no password', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: false, signedIn: true }))
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    // A local Kit inventing a sign-in step is the failure this asserts against.
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument()
  })

  it('renders nothing of the app until the server has answered', () => {
    // A never-settling fetch holds the component in its unknown state, which is
    // the frame where a wrong default would show.
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))
    render(<SignIn>{app}</SignIn>)
    expect(screen.queryByText('the corpus')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('asks for the password when the Kit is locked, and withholds the app', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: true, signedIn: false }))
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByLabelText('Password')).toBeInTheDocument()
    expect(screen.queryByText('the corpus')).not.toBeInTheDocument()
  })

  it('renders the app directly when the browser is already signed in', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: true, signedIn: true }))
    render(<SignIn>{app}</SignIn>)
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument()
  })

  it('signs in and reveals the app', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: true, signedIn: false }))
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Password'), { target: { value: 'hunter2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('the corpus')).toBeInTheDocument()
  })

  it('shows the server’s own sentence on a wrong password, and keeps asking', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: true, signedIn: false }, 401))
    render(<SignIn>{app}</SignIn>)
    fireEvent.change(await screen.findByLabelText('Password'), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('that is not the password')
    expect(screen.queryByText('the corpus')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  // An accidental tap must not spend one of the throttle's five attempts.
  it('refuses to submit an empty password rather than spending an attempt', async () => {
    vi.stubGlobal('fetch', fetchMock({ required: true, signedIn: false }))
    render(<SignIn>{app}</SignIn>)
    await screen.findByLabelText('Password')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled()
  })

  // 🔴 Not "show an error and carry on". A server we could not ask must not
  // fall through to the app, which would then make read calls that fail and
  // present as an empty corpus rather than an unreachable server.
  it('reports a server it could not reach, and does NOT render the app', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('down'))))
    render(<SignIn>{app}</SignIn>)
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())
    expect(screen.queryByText('the corpus')).not.toBeInTheDocument()
  })

  // BEH-USERS-10: a test user who has connected their GitHub signs in with it.
  describe('Sign in with GitHub', () => {
    afterEach(() => {
      vi.restoreAllMocks()
      window.history.replaceState(null, '', '/')
    })

    const withGitHub = (signInStatus: number) =>
      vi.fn((path: string, init?: RequestInit) => {
        if (path === '/api/session/github' && init?.method === 'POST') {
          return Promise.resolve({
            ok: signInStatus === 200,
            status: signInStatus,
            json: () => Promise.resolve(
              signInStatus === 200
                ? { ok: true, signedIn: true }
                : { error: 'github-not-connected', reason: 'no Kit user has connected this GitHub' },
            ),
          } as Response)
        }
        return fetchMock({ required: true, signedIn: false, accounts: true, gitHubSignIn: 'https://github.example/authorize?state=in.1.ab' })(path, init)
      })

    it('offers it beside the password when the server does', async () => {
      vi.stubGlobal('fetch', withGitHub(200))
      render(<SignIn>{app}</SignIn>)
      expect(await screen.findByRole('link', { name: 'Sign in with GitHub' })).toHaveAttribute('href', 'https://github.example/authorize?state=in.1.ab')
      expect(screen.getByLabelText('Password')).toBeInTheDocument()
    })

    it('hands GitHub’s code to the server on the way back, then reloads without it', async () => {
      const reload = vi.spyOn(githubNavigation, 'reload').mockImplementation(() => {})
      const fetch = withGitHub(200)
      vi.stubGlobal('fetch', fetch)
      window.history.replaceState(null, '', '/?code=c0de&state=in.1.ab')
      render(<SignIn>{app}</SignIn>)
      expect(screen.getByRole('status')).toHaveTextContent('Signing in with GitHub')
      await waitFor(() => expect(reload).toHaveBeenCalled())
      const sent = fetch.mock.calls.find(([path]) => path === '/api/session/github')!
      expect(JSON.parse(sent[1]!.body as string)).toEqual({ code: 'c0de', state: 'in.1.ab' })
    })

    it('shows the server’s sentence when GitHub signs no one in, and drops the spent code', async () => {
      vi.stubGlobal('fetch', withGitHub(401))
      window.history.replaceState(null, '', '/?code=c0de&state=in.1.ab')
      render(<SignIn>{app}</SignIn>)
      expect(await screen.findByRole('alert')).toHaveTextContent('no Kit user has connected this GitHub')
      expect(window.location.search).toBe('')
      expect(screen.getByLabelText('Password')).toBeInTheDocument()
    })

    it('leaves a connect coming back to Settings, whose state has no "in."', async () => {
      const fetch = withGitHub(200)
      vi.stubGlobal('fetch', fetch)
      window.history.replaceState(null, '', '/?code=c0de&state=1.ab')
      render(<SignIn>{app}</SignIn>)
      await screen.findByLabelText('Password')
      expect(fetch.mock.calls.some(([path]) => path === '/api/session/github')).toBe(false)
    })
  })
})
