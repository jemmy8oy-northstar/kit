import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import DecisionsList from './DecisionsList'
import type { Decision } from '../api/types'

const decision = (id: string, part: Decision['part'], extra: Partial<Decision> = {}): Decision => ({
  id,
  title: `Title of ${id}`,
  part,
  chosen: `chose ${id}`,
  considered: [],
  because: [`reason for ${id}`],
  review: { state: 'unreviewed', note: null },
  source: { origin: 'defined', ref: null },
  at: 'x.beh:1',
  ...extra,
})

describe('DecisionsList (BEH-ARCH-1)', () => {
  it('groups decisions by part in the fixed order, whatever order the file wrote them in', () => {
    render(
      <DecisionsList
        decisions={[decision('ADR-DEPLOYMENT-1', 'deployment'), decision('ADR-FRONTEND-1', 'frontend'), decision('ADR-DATA-1', 'data')]}
      />,
    )

    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual(['Frontend', 'Data', 'Deployment'])
    expect(screen.queryByRole('region', { name: 'Backend' })).not.toBeInTheDocument()
  })

  it('shows the title, what was chosen, what else was considered and why', () => {
    render(
      <DecisionsList
        decisions={[decision('ADR-BACKEND-1', 'backend', { considered: ['Node/Express', 'Go'], because: ['typed', 'ported'] })]}
      />,
    )

    const card = within(screen.getByRole('region', { name: 'Backend' }))
    expect(card.getByText('Title of ADR-BACKEND-1')).toBeInTheDocument()
    expect(card.getByText('Chosen: chose ADR-BACKEND-1')).toBeInTheDocument()
    expect(within(card.getByRole('list', { name: 'Considered for ADR-BACKEND-1' })).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Node/Express',
      'Go',
    ])
    expect(within(card.getByRole('list', { name: 'Because for ADR-BACKEND-1' })).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'typed',
      'ported',
    ])
  })

  it('badges an unreviewed decision, and not an approved one', () => {
    render(
      <DecisionsList
        decisions={[decision('ADR-DATA-1', 'data'), decision('ADR-DATA-2', 'data', { review: { state: 'approved', note: null } })]}
      />,
    )

    expect(screen.getAllByText('Unreviewed suggestion')).toHaveLength(1)
  })

  it('omits the Considered list when nothing was considered', () => {
    render(<DecisionsList decisions={[decision('ADR-DATA-1', 'data')]} />)

    expect(screen.queryByText('Considered:')).not.toBeInTheDocument()
  })
})
