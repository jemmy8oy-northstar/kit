import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, fetchSession, signIn, signInWithGitHub } from '../api/client'
import type { SessionState } from '../api/types'
import { githubNavigation } from './githubNavigation'
import Loading from './Loading'
import { SessionContext } from './session'

/**
 * BEH-USERS-10: GitHub sends someone who chose "Sign in with GitHub" back to
 * Kit's root with `code` and a `state` the server began with `in.`. A connect
 * coming back (Settings) carries a state without it, so it is left alone here.
 */
function gitHubSignInReturn(): { code: string; state: string } | null {
  const params = new URLSearchParams(window.location.search)
  const code = params.get('code')
  const state = params.get('state')
  return code && state?.startsWith('in.') ? { code, state } : null
}

/**
 * The gate in front of the whole app, for a Kit that is not on your laptop
 * (kit#46, executing James's choice on kit#44).
 *
 * ── why this wraps the app instead of guarding each form ────────────────────
 * The server refuses WRITES and serves reads to anyone, so a per-form guard
 * would be closer to the truth. It is still the wrong shape: on a phone the
 * first thing he does is open a behaviour and change it, and discovering the
 * lock at the moment he presses the button means losing what he typed. Asking
 * once, up front, costs a signed-in user nothing and is the difference between
 * a tool and a puzzle.
 *
 * ── the three states, and why none may be guessed ──────────────────────────
 *   unknown  — we have not asked yet. Render NOTHING that implies an answer.
 *   no lock  — a local Kit. There must be no password field at all; showing one
 *              would invent a step that does not exist.
 *   locked   — ask, and keep asking until the server accepts.
 *
 * The asking is deliberately a server round trip rather than an inference from
 * a failed write: "this Kit has no password" and "your session expired" are
 * different sentences and the user acts differently on each.
 */
export default function SignIn({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<SessionState | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [password, setPassword] = useState('')
  const [username, setUsername] = useState('')
  const [busy, setBusy] = useState(false)
  const [returning, setReturning] = useState(gitHubSignInReturn)
  const started = useRef(false)

  useEffect(() => {
    if (!returning || started.current) return
    started.current = true
    signInWithGitHub(returning.code, returning.state)
      // Reloaded without the query, so a refresh cannot resend a spent code.
      .then(() => githubNavigation.reload())
      .catch((err: unknown) => {
        window.history.replaceState(null, '', window.location.pathname)
        setError(err instanceof ApiError ? err.message : 'Could not sign in with GitHub')
        setReturning(null)
      })
  }, [returning])

  useEffect(() => {
    let live = true
    fetchSession()
      .then((s) => { if (live) setState(s) })
      .catch((e: unknown) => {
        if (!live) return
        // Could not ask. This must NOT fall through to rendering the app: the
        // app would then make read calls that also fail and present as an empty
        // corpus rather than as an unreachable server ([[empty-means-two-things]]).
        setError(e instanceof ApiError ? e.message : String(e))
      })
    return () => { live = false }
  }, [])

  const submit = useCallback((e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    signIn(password, state?.accounts ? username.trim() : undefined)
      .then(async () => {
        // With accounts on, who we are (and whether he is the operator) is only
        // known to the server, so ask again. Off, nothing more is known.
        const after = state?.accounts ? await fetchSession().catch(() => null) : null
        setState(after?.signedIn ? after : { ...state, required: true, signedIn: true })
        // Dropped as soon as it has been exchanged for a cookie. Keeping it in
        // component state would leave the password in a React tree for the rest
        // of the session for no benefit at all.
        setPassword('')
      })
      .catch((err: unknown) => {
        setError(err instanceof ApiError ? err.message : 'Could not sign in')
      })
      .finally(() => setBusy(false))
  }, [password, username, state])

  if (returning) {
    return <Loading splash label="Signing in with GitHub…" />
  }

  if (error && !state) {
    return <p role="alert" className="error">{error}</p>
  }

  // Nothing is rendered before the server has answered. A flash of the sign-in
  // form on a local Kit, or of the app on a locked one, would each be a lie for
  // one frame — and the second would fire read requests as a side effect.
  if (!state) {
    return <Loading splash label="Opening Kit…" />
  }

  if (state.required && !state.signedIn) {
    return (
      <form onSubmit={submit} className="signin">
        <h1>This Kit is locked</h1>
        <p className="muted">
          Reads are open to anyone; editing the corpus needs the password.
        </p>
        {/* BEH-USERS-10: a user who has connected their GitHub signs in here, and only here. */}
        {state.gitHubSignIn ? (
          <a className="github-sign-in" href={state.gitHubSignIn}>Sign in with GitHub</a>
        ) : null}
        {/* BEH-USERS-1: only when the server has accounts on. Optional — empty is the operator. */}
        {state.accounts ? (
          <>
            <label htmlFor="kit-username">User name</label>
            <input
              id="kit-username"
              type="text"
              value={username}
              autoComplete="username"
              autoCapitalize="none"
              onChange={(e) => setUsername(e.target.value)}
            />
          </>
        ) : null}
        <label htmlFor="kit-password">Password</label>
        <input
          id="kit-password"
          type="password"
          value={password}
          autoFocus
          // `current-password` so a phone keychain offers the saved one. The
          // whole point of this slice is a tool he opens on a phone, and typing
          // a long password on glass every week is how that stops happening.
          autoComplete="current-password"
          onChange={(e) => setPassword(e.target.value)}
        />
        {/* Empty is refused here rather than sent, so an accidental tap does
            not spend one of the throttle's five attempts. */}
        <button type="submit" disabled={busy || password.length === 0}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
        {error ? <p role="alert" className="error">{error}</p> : null}
      </form>
    )
  }

  return <SessionContext.Provider value={state}>{children}</SessionContext.Provider>
}
