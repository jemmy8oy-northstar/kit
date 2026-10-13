import { Card } from '@jemmy8oy-northstar/design-system'
import { fetchPending } from '../api/client'
import { useResource } from '../hooks/useResource'

/**
 * BEH-COMMIT-5 (kit#118): "view in kit the pending updates before they are
 * merged". Every edit is pushed to the edits branch as it is made (BEH-ACT-4),
 * so "pending" is that branch's commits the base does not have yet — for this
 * project's files only — with the Commit pull request they wait in, when one is
 * open.
 *
 * Nothing pending renders NOTHING: no heading, no empty card. Most visits find
 * nothing waiting, and a panel that is usually empty is how people learn to
 * scroll past it. A list that could not be read says so only if it has edits to
 * show beside the reason; otherwise the page loses nothing by staying quiet,
 * because Commit still names the failure when it is pressed.
 *
 * `version` re-reads it after a write elsewhere on the page (asking for a
 * behaviour, resolving a decision): each of those pushes a commit that is now
 * pending.
 */
export default function PendingPanel({ apps, version = 0 }: { apps: string[]; version?: number }) {
  // Settled, not all: one file of a project that cannot be read must not hide the others' edits.
  const resource = useResource(
    () => Promise.allSettled(apps.map(fetchPending)).then((rs) => rs.flatMap((r) => (r.status === 'fulfilled' ? [r.value] : []))),
    [apps.join('\n'), version],
  )
  if (resource.state !== 'ready' || resource.value.length === 0) return null

  const views = resource.value
  const edits = views.flatMap((v) => v.edits)
  if (edits.length === 0) return null

  const pull = views.find((v) => v.pullNumber !== null)
  const reasons = [...new Set(views.map((v) => v.reason).filter((r): r is string => !!r))]
  const { head, base } = views[0]

  return (
    <section aria-labelledby="pending-edits">
      <h2 id="pending-edits">Pending</h2>
      <Card>
        <p className="muted">
          {edits.length === 1 ? '1 edit' : `${edits.length} edits`} on <code>{head}</code> not yet on <code>{base}</code>
          {pull ? (
            <>
              {', waiting in '}
              {pull.pullUrl ? <a href={pull.pullUrl}>pull request #{pull.pullNumber}</a> : <>pull request #{pull.pullNumber}</>}
            </>
          ) : (
            ', not proposed yet'
          )}
          .
        </p>
        <ul>
          {edits.map((e) => (
            <li key={e.sha}>
              <code>{e.sha}</code> {e.summary}
              {e.when ? <span className="muted"> · {when(e.when)}</span> : null}
            </li>
          ))}
        </ul>
        {reasons.map((r) => (
          <p key={r} className="muted">
            {r}
          </p>
        ))}
      </Card>
    </section>
  )
}

function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}
