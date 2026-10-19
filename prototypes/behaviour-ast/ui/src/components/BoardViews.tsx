import { Link, NavLink, useMatch } from 'react-router-dom'

/**
 * BEH-STAGE-8 (his 2026-10-19 16:19Z note): the behaviour board and the stages board are two
 * levels of one board, "just another level of granularity", so each carries a switch to the
 * other. They are not merged: "They are different ways of viewing it."
 */
export default function BoardViews() {
  return (
    <nav aria-label="Board view" className="tabs">
      <NavLink to="/stages">Features</NavLink>
      <NavLink to="/board">Behaviours</NavLink>
    </nav>
  )
}

/** The header's one Board link, current on either view, so the board has one entry not two. */
export function BoardNavLink() {
  // Both hooks every render: a `||` would skip the second one on the board (rules of hooks).
  const onBoard = useMatch('/board')
  const onStages = useMatch('/stages')
  // A plain Link: NavLink drops any aria-current of ours whenever its own path does not match.
  return (
    <Link to="/board" aria-current={onBoard || onStages ? 'page' : undefined}>
      Board
    </Link>
  )
}
