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

const tokensOf = (r: AgentRun) => r.inputTokens + r.outputTokens + r.cacheWriteTokens + r.cacheReadTokens

interface ItemCost {
  project: string
  slug: string
  runs: number
  tokens: number
  costUsd: number
  newest: string
}

/** Runs summed per work item (BEH-ORCH-6), newest item first; a run with no work item is left out. */
function byWorkItem(runs: AgentRun[]): ItemCost[] {
  const items = new Map<string, ItemCost>()
  for (const r of runs) {
    if (!r.workItem) continue
    const key = `${r.project}\n${r.workItem}`
    const item = items.get(key) ?? { project: r.project, slug: r.workItem, runs: 0, tokens: 0, costUsd: 0, newest: r.started }
    item.runs += 1
    item.tokens += tokensOf(r)
    item.costUsd += r.costUsd
    if (Date.parse(r.started) > Date.parse(item.newest)) item.newest = r.started
    items.set(key, item)
  }
  return [...items.values()].sort((a, b) => Date.parse(b.newest) - Date.parse(a.newest))
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
              <>
                {byWorkItem(runs).length > 0 ? (
                  <>
                    <h2>By work item</h2>
                    <ul aria-label="Cost by work item" className="activity">
                      {byWorkItem(runs).map((i) => (
                        <li key={`${i.project}/${i.slug}`}>
                          <div className="muted">{i.project} · {i.slug}</div>
                          {i.runs} {i.runs === 1 ? 'run' : 'runs'} · {i.tokens} tokens · ${i.costUsd.toFixed(2)}
                        </li>
                      ))}
                    </ul>
                  </>
                ) : null}
                <ol aria-label="Agent runs" className="activity">
                  {runs.map((r) => (
                    <li key={r.id}>
                      <div className="muted">
                        <time dateTime={r.started}>{when(r.started)}</time> · {r.project} · {r.model}
                      </div>
                      {r.status} · {took(r)} · {tokensOf(r)} tokens · ${r.costUsd.toFixed(2)}
                      {r.status === 'failed' && r.failure ? <div>Failed: {failureInWords(r.failure)}</div> : null}
                    </li>
                  ))}
                </ol>
              </>
            )}
          </>
        )}
      </ResourceView>
    </>
  )
}
