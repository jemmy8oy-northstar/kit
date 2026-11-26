import { NavLink } from 'react-router-dom'
import { useFlags } from './flags'

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
