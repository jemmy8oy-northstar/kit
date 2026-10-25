import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { fetchActivity, fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'

/** The commit's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** The corpus's own id grammar: BEH-USERS-2, FEAT-BOARD, EPIC-KIT-USERS. */
const ID = /\b(?:BEH|FEAT|EPIC)-[A-Z0-9]+(?:-[A-Z0-9]+)*\b/g

/** Where an id's page is, by its prefix (the routes in App.tsx). */
function pathOf(app: string, id: string): string {
  const kind = id.startsWith('BEH-') ? 'behaviours' : id.startsWith('FEAT-') ? 'features' : 'epics'
  return `/projects/${encodeURIComponent(app)}/${kind}/${encodeURIComponent(id)}`
}

/**
 * BEH-ACTIVITY-2: each id in a summary links to its page in the entry's project, but only when the
 * project still has it. `known` holds the ids per project; a project not loaded (or that failed)
 * has none, so its ids stay plain text rather than linking to a page that may not exist.
 */
function Linked({ app, text, known }: { app: string; text: string; known?: Set<string> }) {
  const parts: React.ReactNode[] = []
  let last = 0
  for (const m of text.matchAll(ID)) {
    parts.push(text.slice(last, m.index))
    parts.push(known?.has(m[0]) ? <Link key={m.index} to={pathOf(app, m[0])}>{m[0]}</Link> : m[0])
    last = m.index + m[0].length
  }
  parts.push(text.slice(last))
  return <>{parts}</>
}

/** The ids each project holds, fetched once per distinct project whose entries name an id. */
function useKnownIds(entries: { app: string; summary: string }[]): Map<string, Set<string>> {
  const [known, setKnown] = useState<Map<string, Set<string>>>(new Map())
  const apps = [...new Set(entries.filter((e) => e.summary.match(ID)).map((e) => e.app))].sort()
  const key = apps.join('\n')
  useEffect(() => {
    let live = true
    for (const app of apps) {
      fetchProject(app)
        .then((p) => {
          const ids = new Set([...p.behaviours.map((b) => b.id), ...p.features.map((f) => f.id), ...(p.epics ?? []).map((x) => x.id)])
          if (live) setKnown((k) => new Map(k).set(app, ids))
        })
        .catch(() => undefined)
    }
    return () => {
      live = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key])
  return known
}

function Entries({ entries }: { entries: { commit: string; at: string; app: string; summary: string }[] }) {
  const known = useKnownIds(entries)
  return (
    <ol aria-label="Recent activity" className="activity">
      {entries.map((e) => (
        <li key={`${e.commit}:${e.app}`}>
          {/* When and where on a muted line of their own, so on a phone the summary reads first. */}
          <div className="muted">
            <time dateTime={e.at}>{when(e.at)}</time> · <Link to={`/projects/${encodeURIComponent(e.app)}`}>{e.app}</Link>
          </div>
          <Linked app={e.app} text={e.summary} known={known.get(e.app)} />
        </li>
      ))}
    </ol>
  )
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
              <Entries entries={entries} />
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
