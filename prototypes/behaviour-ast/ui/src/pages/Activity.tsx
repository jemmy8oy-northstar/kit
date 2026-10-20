import { Link } from 'react-router-dom'
import { fetchActivity } from '../api/client'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'

/** The commit's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/**
 * What changed recently (BEH-ACTIVITY-1). An action is a commit that touched a corpus or its notes,
 * so nothing new is stored; the server sends them newest first and this keeps that order.
 */
export default function Activity() {
  const resource = useResource(fetchActivity, [])

  return (
    <>
      <h1>Activity</h1>
      <p className="muted">What changed recently, newest first: every commit that touched a corpus or its notes.</p>

      <ResourceView resource={resource}>
        {({ entries, noHistory }) => (
          <>
            {entries.length === 0 ? (
              <p>Nothing has changed yet.</p>
            ) : (
              <ol aria-label="Recent activity" className="activity">
                {entries.map((e) => (
                  <li key={`${e.commit}:${e.app}`}>
                    {/* When and where on a muted line of their own, so on a phone the summary reads first. */}
                    <div className="muted">
                      <time dateTime={e.at}>{when(e.at)}</time> · <Link to={`/projects/${encodeURIComponent(e.app)}`}>{e.app}</Link>
                    </div>
                    {e.summary}
                  </li>
                ))}
              </ol>
            )}

            {noHistory.length > 0 && (
              <section aria-label="No history here">
                <h2>No history here</h2>
                <p className="muted">This server cannot read these projects&apos; history, so they are not shown as quiet.</p>
                <ul>
                  {noHistory.map((g) => (
                    <li key={g.app}>
                      <Link to={`/projects/${encodeURIComponent(g.app)}`}>{g.app}</Link>: {g.reason}
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </>
        )}
      </ResourceView>
    </>
  )
}
