import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Health from './Health'

const measured = (app: string, covered: number, uncovered: number) => ({
  app,
  project: app,
  behaviours: covered + uncovered,
  coverage: { available: true, covered, uncovered },
})
const unmeasured = (app: string, reason: string) => ({
  app,
  project: app,
  behaviours: 4,
  coverage: { available: false, covered: null, uncovered: null, reason },
})

function mockApi(projects: unknown[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => ({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => (path.endsWith('/api/session') ? { required: false, signedIn: true } : { projects }),
    })),
  )
}

function open() {
  render(
    <MemoryRouter>
      <Health />
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('Health', () => {
  it('totals only the measured projects and says how many are left out (BEH-HEALTH-1)', async () => {
    mockApi([measured('a', 1, 3), measured('b', 3, 1), unmeasured('c', 'no test files were read')])
    open()

    const total = (await screen.findByRole('heading', { name: 'In total' })).parentElement as HTMLElement
    expect(within(total).getByText(/50%/)).toBeInTheDocument()
    expect(within(total).getByText(/4 of 8 behaviours/)).toBeInTheDocument()
    expect(within(total).getByText(/1 project is not measured/)).toBeInTheDocument()
  })

  it('shows the reason in the page text, not only a tooltip (BEH-HEALTH-1)', async () => {
    mockApi([unmeasured('c', 'no test files were read'), measured('a', 1, 1)])
    open()

    expect(await screen.findByText(/Not measured: no test files were read/)).toBeInTheDocument()
  })

  it('says so in words when nothing is measured, with no percentage (BEH-HEALTH-1)', async () => {
    mockApi([unmeasured('c', 'no repo'), unmeasured('d', 'no repo')])
    open()

    expect(await screen.findByText(/No project is measured yet/)).toBeInTheDocument()
    expect(screen.getByText(/2 projects are not measured/)).toBeInTheDocument()
    expect(screen.queryByText(/%/)).not.toBeInTheDocument()
  })

  it('orders lowest percentage first and unmeasured last (BEH-HEALTH-1)', async () => {
    mockApi([unmeasured('zz', 'none'), measured('high', 9, 1), measured('low', 1, 9), measured('mid', 5, 5)])
    open()

    const list = await screen.findByRole('list', { name: 'Project health' })
    const names = within(list)
      .getAllByRole('link')
      .map((l) => l.textContent)
    expect(names).toEqual(['low', 'mid', 'high', 'zz'])
    expect(within(list).getAllByRole('link')[0]).toHaveAttribute('href', '/projects/low')
  })
})
