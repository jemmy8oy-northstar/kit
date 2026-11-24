import { fetchPipeline, fetchRuns } from '../api/client'
import type { AgentRun, RunClaim } from '../api/types'
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

/** A span of minutes as "45 min" or "2h 05m". */
function minutes(ms: number): string {
  const m = Math.max(0, Math.floor(ms / 60000))
  return m < 60 ? `${m} min` : `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, '0')}m`
}

/** Just the clock time of an instant, in the reader's locale. */
function clock(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleTimeString(undefined, { timeStyle: 'short' })
}

/**
 * What each stage is doing right now (BEH-ORCH-17): every run not yet over, by stage. A run a runner has taken says
 * how long it has been running and when Kit counts its runner lost; one no runner has taken yet says how long it has
 * waited. A run that is still going but that Kit has no claim for was taken before Kit last started.
 */
function Now({ runs, claims, now }: { runs: AgentRun[]; claims: RunClaim[]; now: number }) {
  const going = runs.filter((r) => !r.ended).reverse()
  if (going.length === 0) return null
  const claimOf = new Map(claims.map((c) => [c.runId, c]))
  return (
    <>
      <h2>Now</h2>
      <ul aria-label="Runs going now" className="activity">
        {going.map((r) => {
          const c = claimOf.get(r.id)
          const over = c ? now > Date.parse(c.lostAt) : false
          return (
            <li key={r.id}>
              <div className="muted">
                {r.stage ?? 'by hand'} · {r.project}
                {r.workItem ? ` · ${r.workItem}` : ''}
              </div>
              {r.status === 'queued'
                ? `waiting for a runner · ${minutes(now - Date.parse(r.started))}`
                : c
                  ? `running ${minutes(now - Date.parse(c.claimedAt))} · ${over ? 'overdue: counted lost at the next claim' : c.givesUpAt ? `gives up at ${clock(c.givesUpAt)}` : `counted lost at ${clock(c.lostAt)}`}`
                  : 'running since before Kit last started'}
            </li>
          )
        })}
      </ul>
    </>
  )
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

const STAGES = ['triage', 'context', 'spec', 'review', 'build']

/** Runs summed per pipeline stage (BEH-ORCH-9), in pipeline order; a run started by hand has no stage and is left out. */
function byStage(runs: AgentRun[]): { stage: string; runs: number; costUsd: number }[] {
  return STAGES.map((stage) => {
    const of = runs.filter((r) => r.stage === stage)
    return { stage, runs: of.length, costUsd: of.reduce((sum, r) => sum + r.costUsd, 0) }
  }).filter((s) => s.runs > 0)
}

/**
 * The pipeline's work items (BEH-ORCH-9), oldest first: each one's stage and status, why it stopped, its flag and
 * review, and its runs, one per stage, so which stage spent the money reads off the item. Items are kept in memory,
 * so they are the ones since Kit last started; the cost by stage below them is every run on record.
 */
function Pipeline({ runs }: { runs: AgentRun[] }) {
  const resource = useResource(fetchPipeline, [])
  if (resource.state === 'error') return <p className="muted">The pipeline could not be read: {resource.message}</p>
  if (resource.state !== 'ready' || !resource.value.on || !Array.isArray(resource.value.items)) return null
  const items = resource.value.items
  const stages = byStage(runs)
  return (
    <>
      <h2>Pipeline</h2>
      {items.length === 0 ? (
        <p>No work items since Kit last started.</p>
      ) : (
        <ol aria-label="Pipeline work items" className="activity">
          {items.map((i) => {
            const own = i.slug.startsWith('triage #') ? [] : runs.filter((r) => r.project === i.project && r.workItem === i.slug)
            return (
              <li key={`${i.project}/${i.slug}`}>
                <div className="muted">{i.project} · {i.slug}</div>
                {i.stage} · {i.status}
                {i.stoppedBecause ? <div>Stopped: {i.stoppedBecause}</div> : null}
                {i.flag ? <div>Flagged: {i.flag}</div> : null}
                {i.review ? <div>Review: {i.review}{i.reviewNote ? ` — ${i.reviewNote}` : ''}</div> : null}
                {i.pullRequest ? <div><a href={i.pullRequest}>Pull request</a></div> : null}
                {i.pullRequest && i.merged ? <div>Merged</div> : null}
                {i.mergeNote && !i.merged ? <div>{i.mergeNote}</div> : null}
                {own.length > 0 ? (
                  <ul aria-label={`Runs of ${i.slug}`}>
                    {[...own].reverse().map((r) => (
                      <li key={r.id}>{r.stage ?? 'run'} · {r.model} · {tokensOf(r)} tokens · ${r.costUsd.toFixed(2)}</li>
                    ))}
                  </ul>
                ) : null}
              </li>
            )
          })}
        </ol>
      )}
      {stages.length > 0 ? (
        <ul aria-label="Cost by stage" className="activity">
          {stages.map((s) => (
            <li key={s.stage}>{s.stage}: {s.runs} {s.runs === 1 ? 'run' : 'runs'} · ${s.costUsd.toFixed(2)}</li>
          ))}
        </ul>
      ) : null}
    </>
  )
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
        {({ capUsd, runs, claims }) => (
          <>
            <p className="muted">Each run is capped at ${capUsd}.</p>
            <Now runs={runs} claims={claims ?? []} now={Date.now()} />
            <Pipeline runs={runs} />
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
