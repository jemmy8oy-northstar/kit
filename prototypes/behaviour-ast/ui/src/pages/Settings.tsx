import { useEffect, useRef, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { addUser, changePassword, connectGitHub, signOut } from '../api/client'
import type { SessionState } from '../api/types'
import { githubNavigation } from '../components/githubNavigation'
import { OpeningPageSetting } from '../components/OpeningPage'
import { useSession } from '../components/session'

type Outcome = { ok: true; text: string } | { ok: false; text: string } | null

/** One refusal or success line, in the server's own words. */
function Say({ outcome }: { outcome: Outcome }) {
  if (!outcome) return null
  return outcome.ok
    ? <p role="status">{outcome.text}</p>
    : <p role="alert" className="error">{outcome.text}</p>
}

function ChangePassword() {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [busy, setBusy] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>(null)

  function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setOutcome(null)
    changePassword(current, next)
      .then(() => {
        setOutcome({ ok: true, text: 'Password changed.' })
        setCurrent('')
        setNext('')
      })
      // The server's `reason` is the message ("that is not your password", "at least 12 characters").
      .catch((err: unknown) => setOutcome({ ok: false, text: err instanceof Error ? err.message : String(err) }))
      .finally(() => setBusy(false))
  }

  return (
    <form onSubmit={submit}>
      <h3>Change password</h3>
      <label htmlFor="acct-current">Current password</label>
      <input id="acct-current" type="password" required autoComplete="current-password"
        value={current} onChange={(e) => setCurrent(e.target.value)} />
      <label htmlFor="acct-next">New password</label>
      <input id="acct-next" type="password" required autoComplete="new-password"
        value={next} onChange={(e) => setNext(e.target.value)} />
      <button type="submit" disabled={busy}>{busy ? 'Changing…' : 'Change password'}</button>
      <Say outcome={outcome} />
    </form>
  )
}

function AddUser() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>(null)

  function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setOutcome(null)
    addUser(username, password)
      .then((r) => {
        setOutcome({ ok: true, text: `Added ${r.user}.` })
        setUsername('')
        setPassword('')
      })
      .catch((err: unknown) => setOutcome({ ok: false, text: err instanceof Error ? err.message : String(err) }))
      .finally(() => setBusy(false))
  }

  return (
    <form onSubmit={submit}>
      <h3>Add a user</h3>
      <label htmlFor="acct-new-user">User name</label>
      <input id="acct-new-user" type="text" required autoComplete="off" autoCapitalize="none"
        value={username} onChange={(e) => setUsername(e.target.value)} />
      <label htmlFor="acct-new-pass">Password</label>
      <input id="acct-new-pass" type="password" required autoComplete="new-password"
        value={password} onChange={(e) => setPassword(e.target.value)} />
      <button type="submit" disabled={busy}>{busy ? 'Adding…' : 'Add user'}</button>
      <Say outcome={outcome} />
    </form>
  )
}

/**
 * BEH-USERS-1's second half: a user connects their GitHub by installing Kit's
 * GitHub App, then authorising it. GitHub sends them back to this page twice:
 * after the install with `installation_id` (sent straight on to authorise; the
 * id itself is never used, since anyone can type one), and after authorising
 * with `code` and `state` (handed to the server).
 * Returns whether the page is part-way through that, plus what to say.
 */
function useGitHubReturn(session: SessionState): { returning: boolean; outcome: Outcome } {
  const params = new URLSearchParams(window.location.search)
  const code = params.get('code')
  const state = params.get('state')
  const installation = params.get('installation_id')
  const [outcome, setOutcome] = useState<Outcome>(null)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    if (code && state) {
      started.current = true
      connectGitHub(code, state)
        // Reloaded without the query, so the account shows the connection and a refresh cannot resend a spent code.
        .then(() => githubNavigation.reload())
        .catch((err: unknown) => {
          window.history.replaceState(null, '', window.location.pathname)
          setOutcome({ ok: false, text: err instanceof Error ? err.message : String(err) })
        })
    } else if (installation && session.gitHubAuthorize) {
      started.current = true
      githubNavigation.to(session.gitHubAuthorize)
    }
  }, [code, state, installation, session.gitHubAuthorize])

  return { returning: !outcome && Boolean((code && state) || (installation && session.gitHubAuthorize)), outcome }
}

function ConnectGitHub({ session }: { session: SessionState }) {
  const { returning, outcome } = useGitHubReturn(session)
  if (!session.gitHubInstall) return null

  return (
    <div>
      <h3>GitHub</h3>
      {returning
        ? <p role="status">Connecting your GitHub…</p>
        : session.gitHub
          ? <p>Connected to <strong>{session.gitHub}</strong>. <a href={session.gitHubInstall}>Change</a></p>
          : <p><a href={session.gitHubInstall}>Connect your GitHub</a></p>}
      <Say outcome={outcome} />
    </div>
  )
}

/** The operator's GitHub is Kit's own App, set where Kit is deployed; connecting is for the users he adds. */
function OperatorGitHub() {
  return (
    <div>
      <h3>GitHub</h3>
      <p>Kit reads and writes your repos through its own GitHub App, set where Kit is deployed.
        The users you add connect their own GitHub here.</p>
    </div>
  )
}

/**
 * GitHub sends a user back to Kit's root (the App's registered callback URL), so
 * a return lands wherever that is and is sent on here, query intact, without the
 * callback URL having to change. Renders nothing otherwise.
 */
export function GitHubReturn() {
  const { pathname, search } = useLocation()
  const back = new URLSearchParams(search)
  const returning = (back.has('code') && back.has('state')) || back.has('installation_id')
  if (!returning || pathname === '/settings') return null
  return <Navigate to={`/settings${search}`} replace />
}

/**
 * BEH-SETTINGS-1, holding BEH-USERS-1's account area: who is signed in, their
 * GitHub, and what an account can do. A page of its own, reached from the
 * navbar, so there is one place for config as it grows.
 *
 * The operator adds users and cannot change his password here (it lives in the
 * server's config); everyone else can change their own. `operator` comes from
 * the server, so the client never guesses who may do what. A Kit without
 * accounts says so rather than showing an empty page.
 */
export default function Settings() {
  const session = useSession()
  const accounts = Boolean(session?.accounts && session.signedIn)

  async function leave() {
    await signOut().catch(() => {})
    window.location.reload()
  }

  return (
    <>
      <h1>Settings</h1>
      {!accounts || !session ? (
        <p className="muted">This Kit has no user accounts: it has one password, set where Kit is deployed.</p>
      ) : (
        <div className="account">
          <p>Signed in as {session.user}</p>
          <p><button type="button" onClick={leave}>Sign out</button></p>
          {/* BEH-USERS-10: once their GitHub is connected it is how they sign in, so there is no password to change. */}
          {session.operator ? <><OperatorGitHub /><AddUser /></> : <><ConnectGitHub session={session} />{session.gitHub ? null : <ChangePassword />}</>}
        </div>
      )}
      <OpeningPageSetting />
    </>
  )
}
