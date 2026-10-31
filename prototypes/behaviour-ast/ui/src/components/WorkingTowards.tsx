import { Link } from 'react-router-dom'
import type { ProjectDetail } from '../api/types'
import { epicHref } from '../features'
import { goalOf } from '../goal'

/**
 * BEH-NARR-1 (his 2026-10-31 note): the top of a project says what it is working towards, from
 * its epics. Nothing at all for a project with no epic: asking him what the goal is is BEH-NARR-2.
 */
export default function WorkingTowards({ files }: { files: ProjectDetail[] }) {
  const found = goalOf(files)
  if (!found) return null
  if (!found.goal) return <p className="muted">Every goal is built</p>
  const { app, epic, built, total } = found.goal
  return (
    <p>
      Working towards: <Link to={epicHref(app, epic.id)}>{epic.title}</Link>{' '}
      <span className="muted">
        {built} of {total} {total === 1 ? 'behaviour' : 'behaviours'} built
      </span>
    </p>
  )
}
