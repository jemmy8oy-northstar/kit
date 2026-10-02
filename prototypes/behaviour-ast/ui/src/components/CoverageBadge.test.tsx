import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import CoverageBadge from './CoverageBadge'

/**
 * `Projects.test.tsx` already pins the load-bearing case — unavailable coverage
 * renders as "not measured" and never as a zero. These are the two rules
 * `mutate-ui.js` found nothing asserting, and both are component-level because
 * that is the level they are true at.
 */
describe('CoverageBadge', () => {
  it('renders "not measured" when there is no coverage field at all', () => {
    // ⚠️ Reachable through the TYPE rather than through today's server.
    // `ProjectSummary.coverage` is optional, and the only summary `ui.js` builds
    // without it is the `{ app, error }` shape for a corpus that will not parse
    // — which `Projects.tsx` intercepts before it ever reaches this component.
    //
    // It is still worth pinning here, and the distinction is worth stating
    // rather than blurring: this asserts the component's declared contract, not
    // a path a user can reach today. A component whose prop is optional and
    // whose undefined branch nothing exercises is one refactor away from
    // rendering "NaN/NaN covered".
    render(<CoverageBadge />)

    expect(screen.getByText('not measured')).toBeInTheDocument()
    expect(screen.queryByText(/covered/)).not.toBeInTheDocument()
  })

  it('gives a partly-covered project the warning tone, not the success tone', () => {
    // The badge's colour is the only part of it read at a glance, and "2/3" in
    // green says the opposite of what it means. Nothing asserted the tone, so
    // pinning success for every project survived every test in the suite.
    const { container } = render(
      <CoverageBadge coverage={{ available: true, covered: 2, uncovered: 1 }} />,
    )

    expect(screen.getByText(/2\/3 covered/)).toBeInTheDocument()
    expect(container.querySelector('.ds-badge--warning')).not.toBeNull()
    expect(container.querySelector('.ds-badge--success')).toBeNull()
  })

  it('gives a fully-covered project the success tone', () => {
    // The other half. Without it the rule above is satisfied by a badge that is
    // always amber, which is the same failure in the opposite direction.
    const { container } = render(
      <CoverageBadge coverage={{ available: true, covered: 3, uncovered: 0 }} />,
    )

    expect(screen.getByText(/3\/3 covered/)).toBeInTheDocument()
    expect(container.querySelector('.ds-badge--success')).not.toBeNull()
    expect(container.querySelector('.ds-badge--warning')).toBeNull()
  })

  it('counts the detail endpoint\'s id arrays instead of concatenating them', () => {
    // The two endpoints send different shapes for the same field: the list sends
    // counts, the detail sends the behaviour ids. Taking only one of them
    // rendered "BEH-HOME-1BEH-EDIT-1… covered" on the detail page for a day.
    render(
      <CoverageBadge
        coverage={{ available: true, covered: ['BEH-HOME-1', 'BEH-EDIT-1'], uncovered: ['BEH-CUT-1'] }}
      />,
    )

    expect(screen.getByText(/2\/3 covered/)).toBeInTheDocument()
    expect(screen.queryByText(/BEH-HOME-1/)).not.toBeInTheDocument()
  })
})
