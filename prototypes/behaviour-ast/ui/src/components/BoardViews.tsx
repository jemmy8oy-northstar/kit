import { Link, NavLink, useMatch } from 'react-router-dom'
import { FEATURE_PAGES, useFlags } from './flags'

/**
 * BEH-TICKET-6: Kit has two boards, the epic board (its own page, BEH-EPIC-4) and the ticket board (/board). The
 * feature board and the spec board are decommissioned, so neither is a view here; their pages still open by URL.
 * A view whose feature is off for this group (BEH-FLAGS-2) is left out.
 */
export default function BoardViews() {
  const { isOn } = useFlags()
  return (
    <nav aria-label="Board view" className="tabs">
      {isOn('FEAT-EPICS') ? <NavLink to="/epics">Epics</NavLink> : null}
      {isOn('FEAT-BOARD') ? <NavLink to="/board">Tickets</NavLink> : null}
    </nav>
  )
}

/** The header's one Board link, current on either board, so the board has one entry not two. */
export function BoardNavLink() {
  // Every hook every render: a `||` would skip the later ones on the board (rules of hooks).
  const onBoard = useMatch('/board')
  const onStages = useMatch('/stages')
  const onEpics = useMatch('/epics')
  // BEH-FLAGS-2: it goes to the first board that is on for this group, and is gone when none is.
  const { isOn } = useFlags()
  const first = (['FEAT-BOARD', 'FEAT-EPICS'] as const).find(isOn)
  if (!first) return null
  // A plain Link: NavLink drops any aria-current of ours whenever its own path does not match.
  return (
    <Link to={FEATURE_PAGES[first].path} aria-current={onBoard || onStages || onEpics ? 'page' : undefined}>
      Board
    </Link>
  )
}
