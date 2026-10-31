import { Link, NavLink, useMatch } from 'react-router-dom'
import { FEATURE_PAGES, useFlags } from './flags'

/**
 * BEH-STAGE-8 (his 2026-10-19 16:19Z note): the behaviour board and the stages board are two
 * levels of one board, "just another level of granularity", so each carries a switch to the
 * other. They are not merged: "They are different ways of viewing it." Epics are the level above
 * features (BEH-EPIC-4). A view whose feature is off for this group (BEH-FLAGS-2) is left out.
 */
export default function BoardViews() {
  const { isOn } = useFlags()
  return (
    <nav aria-label="Board view" className="tabs">
      {isOn('FEAT-EPICS') ? <NavLink to="/epics">Epics</NavLink> : null}
      {isOn('FEAT-STAGES') ? <NavLink to="/stages">Features</NavLink> : null}
      {isOn('FEAT-BOARD') ? <NavLink to="/board">Behaviours</NavLink> : null}
    </nav>
  )
}

/** The header's one Board link, current on either view, so the board has one entry not two. */
export function BoardNavLink() {
  // Both hooks every render: a `||` would skip the second one on the board (rules of hooks).
  const onBoard = useMatch('/board')
  const onStages = useMatch('/stages')
  const onEpics = useMatch('/epics')
  // BEH-FLAGS-2: it goes to the first view that is on for this group, and is gone when none is.
  const { isOn } = useFlags()
  const first = (['FEAT-BOARD', 'FEAT-STAGES', 'FEAT-EPICS'] as const).find(isOn)
  if (!first) return null
  // A plain Link: NavLink drops any aria-current of ours whenever its own path does not match.
  return (
    <Link to={FEATURE_PAGES[first].path} aria-current={onBoard || onStages || onEpics ? 'page' : undefined}>
      Board
    </Link>
  )
}
