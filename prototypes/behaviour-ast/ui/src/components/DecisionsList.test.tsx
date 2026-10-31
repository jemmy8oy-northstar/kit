import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DecisionsList,{ type DecisionIn } from './DecisionsList'
import type { Decision } from '../api/types'

const decision = (id: string, part: Decision['part'], extra: Partial<DecisionIn> = {}): DecisionIn => ({
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

  it('offers no Approve or Deny on a decision that names no file to write to', () => {
    render(<DecisionsList decisions={[decision('ADR-DATA-1', 'data')]} />)

    expect(screen.queryByRole('button', { name: /Approve/ })).not.toBeInTheDocument()
  })
})

describe('behaviours resting on a decision (BEH-ARCH-1 slice B2)', () => {
  const rest = [
    { app: 'kit-ui', id: 'BEH-A', title: 'Opens the page', dependsOn: ['ADR-DATA-1'] },
    { app: 'kit-ui', id: 'BEH-B', title: 'Unrelated' },
  ]

  it('lists, linked, only the behaviours that name this decision (BEH-ARCH-1)', () => {
    render(
      <MemoryRouter>
        <DecisionsList decisions={[decision('ADR-DATA-1', 'data'), decision('ADR-DATA-2', 'data')]} behaviours={rest} />
      </MemoryRouter>,
    )

    const list = screen.getByRole('list', { name: 'Resting on ADR-DATA-1' })
    expect(within(list).getByRole('link', { name: 'Opens the page' })).toHaveAttribute('href', '/projects/kit-ui/behaviours/BEH-A')
    expect(screen.queryByText('Unrelated')).not.toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Resting on ADR-DATA-2' })).not.toBeInTheDocument()
  })

  it('shows nothing when no behaviour depends on any decision (BEH-ARCH-1)', () => {
    render(<DecisionsList decisions={[decision('ADR-DATA-1', 'data')]} />)

    expect(screen.queryByText('Behaviours resting on this:')).not.toBeInTheDocument()
  })
})

describe('approving or denying a decision (BEH-ARCH-1 slice B1)', () => {
  const wrote = {
    ok: true,
    status: 200,
    statusText: 'OK',
    json: async () => ({ ok: true, app: 'kit-ui', behaviour: 'ADR-DATA-1', file: 'behaviours/kit-ui.beh', committed: false, note: 'written to the working tree' }),
  }

  function stub(response: object = wrote) {
    const calls: { url: string; init?: RequestInit }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        calls.push({ url, init })
        return response
      }),
    )
    return calls
  }

  const owned = () => <DecisionsList decisions={[decision('ADR-DATA-1', 'data', { app: 'kit-ui' })]} />

  it('approves: posts to the decision route, drops the Unreviewed badge and shows Approved', async () => {
    const calls = stub()
    render(owned())
    expect(screen.getByText('Unreviewed suggestion')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Approve ADR-DATA-1' }))

    expect(await screen.findByText('Approved')).toBeInTheDocument()
    expect(screen.queryByText('Unreviewed suggestion')).not.toBeInTheDocument()
    expect(calls[0].url).toBe('/api/projects/kit-ui/decisions/ADR-DATA-1/review')
    expect(JSON.parse(String(calls[0].init?.body))).toEqual({ state: 'approved', note: null })
  })

  it('denies only with a correction, sends it, and shows it on the card', async () => {
    const calls = stub()
    render(owned())

    fireEvent.click(screen.getByRole('button', { name: 'Deny ADR-DATA-1' }))
    expect(screen.getByRole('button', { name: 'Confirm deny ADR-DATA-1' })).toBeDisabled()

    fireEvent.change(screen.getByLabelText(/Reason for denying/), { target: { value: 'it is a database, not files' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm deny ADR-DATA-1' }))

    expect(await screen.findByText('Correction: it is a database, not files')).toBeInTheDocument()
    expect(screen.getByText('Denied')).toBeInTheDocument()
    expect(JSON.parse(String(calls[0].init?.body))).toEqual({ state: 'denied', note: 'it is a database, not files' })
  })

  it('shows the server’s refusal and leaves the card unreviewed', async () => {
    stub({ ok: false, status: 409, statusText: 'Conflict', json: async () => ({ error: 'would-not-parse', reason: 'a denied decision must state the correction' }) })
    render(owned())

    fireEvent.click(screen.getByRole('button', { name: 'Approve ADR-DATA-1' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('a denied decision must state the correction')
    expect(screen.getByText('Unreviewed suggestion')).toBeInTheDocument()
  })
})
