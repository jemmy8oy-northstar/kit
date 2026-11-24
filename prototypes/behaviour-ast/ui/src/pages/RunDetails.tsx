import { useParams } from 'react-router-dom'
import { Badge } from '@jemmy8oy-northstar/design-system'
import { fetchRunVisibility } from '../api/client'
import type { RecentRun, RunVisibility } from '../api/types'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import { costText, took, when } from '../runs'

/**
 * One run's details page (BEH-UI-9): the run the card opened, the stages of its work item with each stage's recorded
 * cost and the reviews the item requested (BEH-VIS-2), and the agents running on that item with any review each
 * flagged (BEH-VIS-1). It does not repeat the cost by stage that the runs page shows (BEH-ORCH-9). A run id that
 * is not in the record says so, rather than showing an empty page.
 */
export default function RunDetails() {
  const { id = '' } = useParams()
  const resource = useResource(fetchRunVisibility, [id])

  return (
    <ResourceView resource={resource}>
      {(page) => <Details page={page} id={id} />}
    </ResourceView>
  )
}

function Details({ page, id }: { page: RunVisibility; id: string }) {
  const run = page.recent.find((r) => r.id === id)
  if (!run) {
    return (
      <>
        <h1>Run not found</h1>
        <p>No run has the id {id}. It may not be in Kit's record.</p>
      </>
    )
  }

  const sameItem = (r: { project: string; workItem: string | null }) =>
    run.workItem !== null && r.project === run.project && r.workItem === run.workItem
  const finished = page.finished.find(sameItem)
  // Oldest first, as the item's stages are read. A run with no item is its own only stage.
  const stages = finished
    ? finished.stages
    : page.recent.filter(sameItem).reverse().map((r: RecentRun) => ({ runId: r.id, stage: r.stage, model: r.model, costUsd: r.costUsd }))
  const reviews = finished?.reviews ?? []
  const agents = page.running.filter(sameItem)

  return (
    <>
      <h1>Run {run.id}</h1>
      <p className="muted">{run.project} · {run.model}</p>
      <p>
        <Badge pill>{run.status}</Badge> · {when(run.started)} · {took(run)} · {run.tokens} tokens · {costText(run.costUsd)}
      </p>
      {run.failure ? <p>Failed: {run.failure}</p> : null}

      {run.workItem === null ? (
        <p className="muted">This run is not for a work item.</p>
      ) : (
        <>
          <h2>Work item</h2>
          <p>{run.project} · {run.workItem}</p>

          <h2>Stages of this work item</h2>
          <ol aria-label="Stages of this work item" className="activity">
            {stages.map((s) => (
              <li key={s.runId}>{s.stage ?? 'by hand'} · {s.model} · {costText(s.costUsd)}</li>
            ))}
          </ol>

          <h2>Reviews requested</h2>
          {reviews.length === 0 ? (
            <p className="muted">No review was requested.</p>
          ) : (
            <ul aria-label="Reviews requested" className="activity">
              {reviews.map((rv) => (
                <li key={rv.runId}>Review requested by {rv.requestedBy ?? 'unknown'} at {rv.stage ?? 'unknown'}</li>
              ))}
            </ul>
          )}

          <h2>Agents running on this work item</h2>
          {agents.length === 0 ? (
            <p className="muted">No agent is running on this work item.</p>
          ) : (
            <ul aria-label="Agents running on this work item" className="activity">
              {agents.map((a) => (
                <li key={a.id}>
                  {a.stage ?? 'by hand'} · {a.model} · since {when(a.started)}
                  {a.flag ? <div>Flagged: {a.flag}{a.flaggedBy ? `, by ${a.flaggedBy}` : ''}</div> : null}
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </>
  )
}
