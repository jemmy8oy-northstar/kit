import { Link } from 'react-router-dom'
import { Card } from '@jemmy8oy-northstar/design-system'
import { fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectSummary } from '../api/types'
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
export default function Health() {
  const projects = useResource(fetchProjects, [])

  return (
    <>
      <h1>Health</h1>
      <p className="muted">
        The share of behaviours that a test names. Covered means a test names the behaviour, not that it passes.
      </p>
      <ResourceView resource={projects}>
        {({ projects: list }) => {
          const rows = orderRows(list.filter((p) => !isIdea(p)).map(toRow))
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
    </Card>
  )
}
