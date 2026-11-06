import { fetchRuns } from '../api/client'
import type { AgentRun } from '../api/types'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'

/** The run's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** How long it ran, or "running" while it has no end. */
function took(r: AgentRun): string {
  if (!r.ended) return 'running'
  const s = Math.max(0, Math.round((Date.parse(r.ended) - Date.parse(r.started)) / 1000))
  return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`
}

/** What each step of the runner's script failing means, in plain words (runner/entrypoint.sh names the step). */
const STEPS: Record<string, string> = {
  setup: 'could not start',
  clone: 'could not fetch the repo',
  checkout: 'could not check out the branch',
  process: "could not fetch Kit's process",
  claude: 'the agent stopped with an error',
  bundles: 'could not read its bundles',
  push: 'could not push its work',
  run: 'the runner failed',
}

/** `clone exit 128` as "could not fetch the repo (exit 128)"; a line this does not know is shown as it came. */
function failureInWords(failure: string): string {
  const m = /^([a-z]+) exit (\d+)$/.exec(failure)
  return m && STEPS[m[1]] ? `${STEPS[m[1]]} (exit ${m[2]})` : failure
}

/**
 * Every agent run Kit has started, newest first (BEH-USERS-9). The server sends them in that order and
 * this keeps it. A failed run says which step of the runner failed.
 */
export default function Runs() {
  const resource = useResource(fetchRuns, [])

  return (
    <>
      <h1>Runs</h1>

      <ResourceView resource={resource}>
        {({ capUsd, runs }) => (
          <>
            <p className="muted">Each run is capped at ${capUsd}.</p>
            {runs.length === 0 ? (
              <p>No runs yet.</p>
            ) : (
              <ol aria-label="Agent runs" className="activity">
                {runs.map((r) => (
                  <li key={r.id}>
                    <div className="muted">
                      <time dateTime={r.started}>{when(r.started)}</time> · {r.project} · {r.model}
                    </div>
                    {r.status} · {took(r)} · {r.inputTokens + r.outputTokens + r.cacheWriteTokens + r.cacheReadTokens} tokens · ${r.costUsd.toFixed(2)}
                    {r.status === 'failed' && r.failure ? <div>Failed: {failureInWords(r.failure)}</div> : null}
                  </li>
                ))}
              </ol>
            )}
          </>
        )}
      </ResourceView>
    </>
  )
}
