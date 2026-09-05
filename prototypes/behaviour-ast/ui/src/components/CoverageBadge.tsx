import { Badge } from '@jemmy8oy-northstar/design-system'
import type { Coverage } from '../api/types'

/**
 * ui.js's rule 4, carried onto the screen: **unavailable is never zero.**
 *
 * A UI that renders "0 covered" when no test files were read is telling you an
 * app is untested when the truth is that nobody looked. The two states get
 * different words and different colours, and the reason is shown rather than
 * swallowed ([[empty-means-two-things]]).
 *
 * It takes either endpoint's shape — the list counts, the detail endpoint sends
 * the behaviour ids — because a component that took only one of them rendered
 * `BEH-HOME-1BEH-EDIT-1… covered` on the detail page for a day.
 */
export default function CoverageBadge({ coverage }: { coverage?: Coverage }) {
  if (!coverage) {
    return (
      <Badge tone="neutral" title="This project reported no coverage field at all">
        not measured
      </Badge>
    )
  }

  if (!coverage.available) {
    return (
      <Badge tone="neutral" title={coverage.reason}>
        not measured
      </Badge>
    )
  }

  const covered = count(coverage.covered)
  const total = covered + count(coverage.uncovered)
  return (
    <Badge tone={covered === total ? 'success' : 'warning'}>
      {covered}/{total} covered
    </Badge>
  )
}

function count(value: number | string[]): number {
  return Array.isArray(value) ? value.length : value
}
