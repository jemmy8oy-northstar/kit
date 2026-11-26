import { Link } from 'react-router-dom'
import { Card } from '@jemmy8oy-northstar/design-system'
import { fetchProject, fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { LinkHealth, ProjectDetail, ProjectSummary } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import ResourceView from '../components/Resource'
import { isIdea } from '../board'

interface Row {
  project: ProjectSummary
  /** Present only when a reading exists. Unavailable is never zero. */
  measured?: { covered: number; total: number; pct: number }
  reason?: string
}

// Floored, so 299 of 300 reads 99% and only a complete project reads 100%.
const pct = (covered: number, total: number) => (total === 0 ? 100 : Math.floor((covered / total) * 100))

function toRow(project: ProjectSummary): Row {
  const c = project.coverage
  if (c && c.available) {
    const total = c.covered + c.uncovered
    return { project, measured: { covered: c.covered, total, pct: pct(c.covered, total) } }
  }
  if (c) return { project, reason: c.reason }
  if (project.error) return { project, reason: project.error }
  return { project, reason: 'This project reported no coverage reading.' }
}

/** Lowest percentage first; projects with no reading last. */
function orderRows(rows: Row[]): Row[] {
  return [...rows].sort((a, b) => {
    if (!a.measured && !b.measured) return a.project.app.localeCompare(b.project.app)
    if (!a.measured) return 1
    if (!b.measured) return -1
    return a.measured.pct - b.measured.pct || a.project.app.localeCompare(b.project.app)
  })
}

/**
 * BEH-HEALTH-1: the share of behaviours a test names, per project and in total.
 * A project with no reading says "not measured" and why, and stays out of the total.
 */
export default function Health({ only }: { only?: string } = {}) {
  const projects = useResource(fetchProjects, [])

  return (
    <>
      <h1>Health</h1>
      <p className="muted">
        The share of behaviours that a test names. Covered means a test names the behaviour, not that it passes.
      </p>
      <ResourceView resource={projects}>
        {({ projects: list }) => {
          // BEH-NAV-4: a project's own health page shows that project's row alone.
          const rows = orderRows(list.filter((p) => !isIdea(p) && (!only || p.app === only)).map(toRow))
          return (
            <>
              <Total rows={rows} />
              <ul className="cards" aria-label="Project health">
                {rows.map((r) => (
                  <li key={r.project.app}>
                    <HealthRow row={r} />
                  </li>
                ))}
              </ul>
            </>
          )
        }}
      </ResourceView>
    </>
  )
}

function Total({ rows }: { rows: Row[] }) {
  const measured = rows.flatMap((r) => (r.measured ? [r.measured] : []))
  const unmeasured = rows.length - measured.length
  const covered = measured.reduce((t, m) => t + m.covered, 0)
  const total = measured.reduce((t, m) => t + m.total, 0)
  const notMeasured =
    unmeasured === 0 ? null : (
      <p className="muted">
        {unmeasured} {unmeasured === 1 ? 'project is' : 'projects are'} not measured and left out of the total.
      </p>
    )
  return (
    <Card>
      <h2>In total</h2>
      {measured.length === 0 ? (
        <p>No project is measured yet, so there is no total.</p>
      ) : (
        <p>
          <strong>{pct(covered, total)}%</strong> · {covered} of {total} behaviours named by a test, across{' '}
          {measured.length} {measured.length === 1 ? 'project' : 'projects'}
        </p>
      )}
      {notMeasured}
    </Card>
  )
}

function HealthRow({ row }: { row: Row }) {
  const { project, measured, reason } = row
  const c = project.coverage
  const last = c && c.available ? c.lastImprovedBy : undefined
  return (
    <Card>
      <h3>
        <Link to={`/projects/${encodeURIComponent(project.app)}`}>{project.app}</Link>
      </h3>
      <div className="badges">
        {measured ? (
          <span>
            <strong>{measured.pct}%</strong> · {measured.covered} of {measured.total}
          </span>
        ) : null}
        <CoverageBadge coverage={project.coverage} />
      </div>
      {reason ? <p className="muted">Not measured: {reason}</p> : null}
      {last ? (
        <p className="muted">
          Last improved by <a href={last.url}>{last.pr}</a> on {last.on}
        </p>
      ) : null}
      <Detail app={project.app} untested={!!measured && measured.covered < measured.total} />
    </Card>
  )
}

/**
 * The list endpoint only carries counts, so what sits under a project reads its own view once:
 * BEH-HEALTH-3's behaviours no test names (when the counts say there are some) and BEH-HEALTH-4's
 * possibly-broken test links (when the project has any recorded). A project with neither says nothing.
 */
function Detail({ app, untested }: { app: string; untested: boolean }) {
  const detail = useResource(() => fetchProject(app), [app])
  if (detail.state === 'loading') return untested ? <p className="muted">Reading which behaviours no test names…</p> : null
  if (detail.state === 'error') {
    return (
      <p className="muted">
        {untested ? 'Could not list the untested behaviours' : 'Could not check the test links'}: {detail.message}
      </p>
    )
  }
  const { coverage, behaviours, links } = detail.value
  return (
    <>
      {untested && coverage && coverage.available ? <Untested app={app} ids={coverage.uncovered} behaviours={behaviours} /> : null}
      {links ? <BrokenLinks app={app} links={links} /> : null}
    </>
  )
}

/** BEH-HEALTH-3: the behaviours no test names, each title taken from the same response as the ids. */
function Untested({ app, ids, behaviours }: { app: string; ids: string[]; behaviours: ProjectDetail['behaviours'] }) {
  const titles = new Map(behaviours.map((b) => [b.id, b.title]))
  if (ids.length === 0) return null
  return (
    <>
      <p className="muted">No test names these {ids.length}:</p>
      <ul aria-label={`Untested in ${app}`}>
        {ids.map((id) => (
          <li key={id}>
            <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}`}>{id}</Link>
            {titles.has(id) ? ` ${titles.get(id)}` : ''}
          </li>
        ))}
      </ul>
    </>
  )
}

/**
 * BEH-HEALTH-4: test links whose recorded hash no longer matches the code they exercise are "possibly
 * broken" — a prompt to re-check, not a verdict. Unavailable says why in words; it never reads as "all hold".
 */
function BrokenLinks({ app, links }: { app: string; links: LinkHealth }) {
  if (!links.available) return <p className="muted">Test links not checked: {links.reason}</p>
  if (links.broken.length === 0) {
    return <p className="muted">All {links.recorded} recorded test {links.recorded === 1 ? 'link holds' : 'links hold'}.</p>
  }
  return (
    <>
      <p className="muted">
        {links.broken.length} of {links.recorded} recorded test {links.recorded === 1 ? 'link is' : 'links are'} possibly broken:
      </p>
      <ul aria-label={`Possibly broken test links in ${app}`}>
        {links.broken.map((b) => (
          <li key={`${b.behaviourId}/${b.title}`}>
            <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.behaviourId)}`}>{b.behaviourId}</Link>{' '}
            {b.title} — {b.reason}
          </li>
        ))}
      </ul>
    </>
  )
}
