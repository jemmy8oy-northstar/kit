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

/** `details` maps an app to its `/api/projects/<app>` body; an app not in it answers with the list body. */
function mockApi(projects: unknown[], details: Record<string, unknown> = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      const app = /\/api\/projects\/([^/]+)$/.exec(path)?.[1]
      return {
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () =>
          path.endsWith('/api/session')
            ? { required: false, signedIn: true }
            : app && details[decodeURIComponent(app)]
              ? details[decodeURIComponent(app)]
              : { projects },
      }
    }),
  )
}

const detail = (uncovered: string[], titles: Record<string, string>) => ({
  coverage: { available: true, covered: [], uncovered },
  behaviours: Object.entries(titles).map(([id, title]) => ({ id, title })),
})

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

  it('never reads 100% until every behaviour is named (BEH-HEALTH-1)', async () => {
    mockApi([measured('a', 299, 1)])
    open()

    const total = (await screen.findByRole('heading', { name: 'In total' })).parentElement as HTMLElement
    expect(within(total).getByText(/99%/)).toBeInTheDocument()
    expect(screen.queryByText(/100%/)).not.toBeInTheDocument()
  })

  it('shows the last PR that improved a project, linked, and nothing when none is named (BEH-HEALTH-2)', async () => {
    const withPr = {
      ...measured('a', 1, 1),
      coverage: {
        available: true,
        covered: 1,
        uncovered: 1,
        lastImprovedBy: { pr: 'kit#538', url: 'https://github.com/o/r/pull/538', on: '2026-10-28' },
      },
    }
    mockApi([withPr, measured('b', 1, 1)])
    open()

    const link = await screen.findByRole('link', { name: 'kit#538' })
    expect(link).toHaveAttribute('href', 'https://github.com/o/r/pull/538')
    expect(link.parentElement).toHaveTextContent('Last improved by kit#538 on 2026-10-28')
    expect(screen.getAllByText(/Last improved by/)).toHaveLength(1)
  })

  it('lists under a project the behaviours no test names, each linking to its page (BEH-HEALTH-3)', async () => {
    mockApi([measured('a', 1, 2)], { a: detail(['BEH-A-2', 'BEH-A-3'], { 'BEH-A-1': 'One', 'BEH-A-2': 'Two', 'BEH-A-3': 'Three' }) })
    open()

    const list = await screen.findByRole('list', { name: 'Untested in a' })
    const links = within(list).getAllByRole('link')
    expect(links.map((l) => l.textContent)).toEqual(['BEH-A-2', 'BEH-A-3'])
    expect(links[0]).toHaveAttribute('href', '/projects/a/behaviours/BEH-A-2')
    expect(list).toHaveTextContent('BEH-A-2 Two')
    expect(list).not.toHaveTextContent('One')
  })

  it('says nothing extra for a project where every behaviour is named or none is measured (BEH-HEALTH-3)', async () => {
    mockApi([measured('a', 2, 0), unmeasured('c', 'no repo')])
    open()

    await screen.findByText(/Not measured: no repo/)
    expect(screen.queryByRole('list', { name: /Untested in/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/No test names these/)).not.toBeInTheDocument()
  })

  it('says why in words when the ids could not be read (BEH-HEALTH-3)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) =>
        /\/api\/projects\/a$/.test(path)
          ? { ok: false, status: 500, statusText: 'Boom', json: async () => ({ reason: 'corpus unreadable' }) }
          : { ok: true, status: 200, statusText: 'OK', json: async () => ({ projects: [measured('a', 1, 1)] }) },
      ),
    )
    open()

    expect(await screen.findByText(/Could not list the untested behaviours: .*corpus unreadable/)).toBeInTheDocument()
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
