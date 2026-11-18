import { Link, useSearchParams } from 'react-router-dom'
import { fetchProject, fetchProjects, fetchRuns } from '../api/client'
import type { AgentRun, Behaviour } from '../api/types'
import { byPriority } from '../board'
import { useResource, type Resource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import Loading from '../components/Loading'

/** The run's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** Runs read as "load failed", "loading" or a value; a failed read says why and the other section still shows. */
function Unread({ message }: { message: string }) {
  return <p role="alert">Could not be read: {message}</p>
}

/**
 * BEH-AGENTPAGE-1: the picked agent's runs, newest first. Its runs are the ones for its project, which is the
 * only thing Kit records an agent by. A run started by hand has no stage and says so.
 */
function RecentWork({ app, runs }: { app: string; runs: Resource<{ runs: AgentRun[] }> }) {
  if (runs.state === 'loading') return <Loading label="Loading…" />
  if (runs.state === 'error') return <Unread message={runs.message} />
  const mine = runs.value.runs
    .filter((r) => r.project === app)
    .sort((a, b) => Date.parse(b.started) - Date.parse(a.started))
  if (mine.length === 0) return <p>No recent work</p>
  return (
    <ol aria-label="Recent runs" className="activity">
      {mine.map((r) => (
        <li key={r.id}>
          <div className="muted">
            <time dateTime={r.started}>{when(r.started)}</time>
            {r.workItem ? ` · ${r.workItem}` : null}
          </div>
          {r.status} · {r.stage ?? 'by hand'} · ${r.costUsd.toFixed(2)}
        </li>
      ))}
    </ol>
  )
}

/** Board order within a list: priority first, corpus order among equals (a stable sort). */
const boardOrder = (xs: Behaviour[]) => [...xs].sort((a, b) => byPriority(a.priority, b.priority))

/**
 * BEH-AGENTPAGE-1: the queue from the same project data the project view reads. Approved, pending cards with no
 * needs-context line are unblocked; pending cards with one are waiting on him. A denied card is neither.
 */
function Queue({ app, detail }: { app: string; detail: Resource<{ behaviours: Behaviour[] }> }) {
  if (detail.state === 'loading') return <Loading label="Loading…" />
  if (detail.state === 'error') return <Unread message={detail.message} />
  const pending = (detail.value.behaviours ?? []).filter((b) => b.pending && b.review.state !== 'denied')
  const unblocked = boardOrder(pending.filter((b) => b.review.state === 'approved' && !b.needsContext))
  const waiting = boardOrder(pending.filter((b) => b.needsContext))
  if (unblocked.length === 0 && waiting.length === 0) return <p>Nothing in the queue</p>
  const link = (b: Behaviour) => `/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`
  return (
    <>
      <h3>Unblocked</h3>
      {unblocked.length === 0 ? (
        <p className="muted">Nothing unblocked.</p>
      ) : (
        <ol aria-label="Unblocked" className="activity">
          {unblocked.map((b) => (
            <li key={b.id}>
              <Link to={link(b)}>{b.id}</Link> {b.title}
            </li>
          ))}
        </ol>
      )}
      <h3>Waiting on you</h3>
      {waiting.length === 0 ? (
        <p className="muted">Nothing waiting on you.</p>
      ) : (
        <ol aria-label="Waiting on you" className="activity">
          {waiting.map((b) => (
            <li key={b.id}>
              <Link to={link(b)}>{b.id}</Link> {b.title}
              <div>Needs you: {b.needsContext}</div>
            </li>
          ))}
        </ol>
      )}
    </>
  )
}

/** One picked agent: its recent work and its queue. Both are read here and nothing is written (every request is a GET). */
function AgentDetail({ app }: { app: string }) {
  const runs = useResource(fetchRuns, [])
  const detail = useResource(() => fetchProject(app), [app])
  return (
    <>
      <section aria-labelledby="recent-work">
        <h2 id="recent-work">Recent work</h2>
        <RecentWork app={app} runs={runs} />
      </section>
      <section aria-labelledby="queue">
        <h2 id="queue">Queue</h2>
        <Queue app={app} detail={detail} />
      </section>
    </>
  )
}

/**
 * BEH-AGENTPAGE-1: pick an agent from a list, then see its recent work and its queue. An agent is a project: Kit
 * records no agent of its own. Until one is picked (the `agent` in the url) only the list shows.
 */
export default function Agents() {
  const [params] = useSearchParams()
  const picked = params.get('agent')
  const projects = useResource(fetchProjects, [])

  return (
    <>
      <h1>Agents</h1>
      <p className="muted">Pick an agent to see its recent work and its queue.</p>

      <ResourceView resource={projects}>
        {(list) =>
          list.projects.length === 0 ? (
            <p>No agents yet.</p>
          ) : (
            <ul aria-label="Agents" className="activity">
              {list.projects.map((p) => (
                <li key={p.app}>
                  <Link to={`?agent=${encodeURIComponent(p.app)}`} aria-current={p.app === picked ? 'true' : undefined}>
                    {p.app}
                  </Link>
                </li>
              ))}
            </ul>
          )
        }
      </ResourceView>

      {picked ? <AgentDetail app={picked} /> : null}
    </>
  )
}
