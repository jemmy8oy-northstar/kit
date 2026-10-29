import { useEffect, useRef, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { addUser, changePassword, connectGitHub, fetchIdle, fetchProjects, setIdle, setPromote, signOut } from '../api/client'
import type { IdleMode, SessionState } from '../api/types'
import { isIdea, isTrial, projectOf } from '../board'
import { githubNavigation } from '../components/githubNavigation'
import { OpeningPageSetting } from '../components/OpeningPage'
import ResourceView from '../components/Resource'
import { useSession } from '../components/session'
import { useReloadableResource } from '../hooks/useResource'

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

/**
 * GitHub's install page dead-ends for an account that already has Kit's App: it offers
 * only "Configure", which need not send anyone back. Authorising straight away finds the
 * existing installation the same way the install's return does.
 */
function AlreadyInstalled({ session }: { session: SessionState }) {
  if (!session.gitHubAuthorize) return null
  return <> · <a href={session.gitHubAuthorize}>Already installed? Connect it</a></>
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
          : <p><a href={session.gitHubInstall}>Connect your GitHub</a><AlreadyInstalled session={session} /></p>}
      <Say outcome={outcome} />
    </div>
  )
}

/**
 * The operator's projects run on Kit's own App, set where Kit is deployed. BEH-SETTINGS-3:
 * he can also connect his personal GitHub, as a user does, and its repos are listed beside
 * the board's. Connecting it never becomes a way to sign in: his password stays.
 */
function OperatorGitHub({ session }: { session: SessionState }) {
  const { returning, outcome } = useGitHubReturn(session)

  return (
    <div>
      <h3>GitHub</h3>
      <p>Kit reads and writes the board's projects through its own GitHub App, set where Kit is deployed.</p>
      {!session.gitHubInstall ? null : returning
        ? <p role="status">Connecting your GitHub…</p>
        : session.gitHub
          ? <p>Your personal GitHub: <strong>{session.gitHub}</strong>. <a href={session.gitHubInstall}>Change</a></p>
          : <p><a href={session.gitHubInstall}>Connect your personal GitHub</a><AlreadyInstalled session={session} /></p>}
      <Say outcome={outcome} />
    </div>
  )
}

/**
 * BEH-SETTINGS-4: which projects the agent may promote to main itself. Each switch is the
 * `# kit:promote self` line in that project's own corpus, so a change is a commit he can see.
 * One row per project (its primary file), ideas and trials left out. New projects start off.
 */
function PromotionSetting() {
  const { resource, reload } = useReloadableResource(async () => {
    const { projects } = await fetchProjects()
    return projects
      .filter((p) => !p.error && !isIdea(p) && !isTrial(p) && projectOf(p) === p.app)
      .map((p) => ({ app: p.app, on: p.promote === 'self' }))
      .sort((a, b) => a.app.localeCompare(b.app))
  }, [])
  const [busy, setBusy] = useState<string | null>(null)
  const [outcome, setOutcome] = useState<Outcome>(null)

  function change(app: string, on: boolean) {
    setBusy(app)
    setOutcome(null)
    setPromote(app, on)
      .then(() => {
        setOutcome({ ok: true, text: on ? `The agent may now promote ${app} to main.` : `The agent no longer promotes ${app} to main.` })
        reload()
      })
      .catch((err: unknown) => setOutcome({ ok: false, text: err instanceof Error ? err.message : String(err) }))
      .finally(() => setBusy(null))
  }

  return (
    <section aria-labelledby="promotion-to-main">
      <h2 id="promotion-to-main">Promotion to main</h2>
      <p>
        When on, the agent may merge this project's dev into main itself once a feature is complete and
        dev's CI is green. Turning it off stops future promotions; nothing already on main changes.
      </p>
      <p className="muted">New projects start off.</p>
      <ResourceView resource={resource}>
        {(rows) => (
          <ul>
            {rows.map((r) => (
              <li key={r.app}>
                <label>
                  <input type="checkbox" checked={r.on} disabled={busy === r.app}
                    onChange={(e) => change(r.app, e.target.checked)} />
                  {' '}{r.app}
                </label>
              </li>
            ))}
          </ul>
        )}
      </ResourceView>
      <Say outcome={outcome} />
    </section>
  )
}

const IDLE_MODES: { mode: IdleMode; label: string }[] = [
  { mode: 'on-change', label: 'Wake on any change' },
  { mode: 'health', label: 'Work on test health when idle' },
  { mode: 'ideas', label: 'Suggest ideas when idle' },
  { mode: 'idle', label: 'Idle until asked' },
]

/**
 * BEH-SETTINGS-5: how busy the agent is when nothing is asked of it. One of four, stored as the
 * `# kit:idle` line in Kit's own corpus, so a change is a commit he can see. Default: test health.
 */
function IdleSettingSection() {
  const { resource, reload } = useReloadableResource(fetchIdle, [])
  const [busy, setBusy] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>(null)

  function change(mode: IdleMode, label: string) {
    setBusy(true)
    setOutcome(null)
    setIdle(mode)
      .then(() => {
        setOutcome({ ok: true, text: `When nothing is asked of it, the agent will now: ${label.toLowerCase()}.` })
        reload()
      })
      .catch((err: unknown) => setOutcome({ ok: false, text: err instanceof Error ? err.message : String(err) }))
      .finally(() => setBusy(false))
  }

  return (
    <section aria-labelledby="agent-idle">
      <h2 id="agent-idle">When nothing is asked of the agent</h2>
      <p>
        Work you have asked for is always built. This choice only decides what the agent does when the Queue is empty.
      </p>
      <ResourceView resource={resource}>
        {(current) => (
          <fieldset disabled={busy}>
            <legend>Idle behaviour</legend>
            {IDLE_MODES.map(({ mode, label }) => (
              <div key={mode}>
                <label>
                  <input type="radio" name="agent-idle" value={mode} checked={current.idle === mode}
                    onChange={() => change(mode, label)} />
                  {' '}{label}
                </label>
              </div>
            ))}
          </fieldset>
        )}
      </ResourceView>
      <Say outcome={outcome} />
    </section>
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
          {session.operator ? <><OperatorGitHub session={session} /><AddUser /></> : <><ConnectGitHub session={session} />{session.gitHub ? null : <ChangePassword />}</>}
        </div>
      )}
      {session?.operator && accounts ? <PromotionSetting /> : null}
      {session?.operator && accounts ? <IdleSettingSection /> : null}
      <OpeningPageSetting />
    </>
  )
}
