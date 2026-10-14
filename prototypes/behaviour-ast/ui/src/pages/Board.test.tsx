import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import Board from './Board'
import App from '../App'
import { columnOf, hueOf, HUES } from '../board'
import type { Behaviour } from '../api/types'

// Hand-built, not the generated fixtures: the board's rules are about the shape
// of a behaviour, and a fixture regenerated from the real corpus would move them.
function beh(id: string, over: Partial<Behaviour> = {}): Behaviour {
  return {
    id,
    title: `title of ${id}`,
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'defined', ref: null },
    review: { state: 'approved', note: null },
    asks: null,
    pending: false,
    layer: 'ui',
    at: 'x.beh:1',
    in: [],
    ...over,
  }
}

const inferred = { origin: 'inferred', ref: null }

const details: Record<string, Behaviour[]> = {
  alpha: [
    beh('A-DONE'),
    beh('A-BACKLOG', { pending: true }),
    beh('A-PROPOSED', { source: inferred, review: { state: 'unreviewed', note: null } }),
    beh('A-DENIED', { source: inferred, review: { state: 'denied', note: 'no' } }),
    beh('A-NEXT', { pending: true }),
    beh('A-SECOND', { pending: true }),
    beh('A-THIRD', { pending: true }),
  ],
  beta: [beh('B-DONE')],
  trial: [beh('T-DONE')],
}

// alpha's schedule: a built entry first (it stays Done), then next, then two in an order
// that is NOT corpus order — so the Scheduled column can only get it right by reading this.
const schedules: Record<string, { id: string; next: boolean }[]> = {
  alpha: [
    { id: 'A-DONE', next: false },
    { id: 'A-NEXT', next: true },
    { id: 'A-THIRD', next: false },
    { id: 'A-SECOND', next: false },
  ],
}

const summaries = [
  { app: 'alpha' },
  { app: 'beta' },
  { app: 'trial', notReal: true },
  { app: 'broken', error: 'broken.beh:3 — expected a verb' },
]

/** The read API: the project list, a project's detail, or its schedule (BEH-SCHED-1). */
function answer(path: string, projects: object[] = summaries) {
  const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
  if (rest.endsWith('/schedule')) {
    const app = rest.slice(0, -'/schedule'.length)
    // The board reads only id, order and next; title and state are the panel's.
    const scheduled = (schedules[app] ?? []).map((s) => ({ ...s, title: null, state: 'pending' }))
    return { app, file: `${app}.schedule`, scheduled, backlog: [] }
  }
  return rest ? { app: rest, behaviours: details[rest] } : { projects }
}

function mockApi() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => ({ ok: true, status: 200, statusText: 'OK', json: async () => answer(path) })),
  )
}

function Search() {
  return <p data-testid="search">{useLocation().search}</p>
}

function renderBoard(url = '/board') {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Board />
      <Search />
    </MemoryRouter>,
  )
}

// The columns render once every project has loaded; before that there is only the loading line.
async function loaded() {
  await screen.findByRole('region', { name: 'Done' })
}

function column(name: string) {
  return within(screen.getByRole('region', { name }))
}

describe('Board', () => {
  it('puts each behaviour in the column for where it stands', async () => {
    mockApi()
    renderBoard()
    await loaded()

    expect(await column('Proposed').findByText('A-PROPOSED')).toBeInTheDocument()
    expect(column('Backlog').getByText('A-BACKLOG')).toBeInTheDocument()
    expect(column('Done').getByText('A-DONE')).toBeInTheDocument()
    expect(column('Done').getByText('B-DONE')).toBeInTheDocument()
    // Each is in exactly one column.
    expect(column('Done').queryByText('A-BACKLOG')).not.toBeInTheDocument()
    expect(column('Backlog').queryByText('A-DONE')).not.toBeInTheDocument()
  })

  it('puts a denied inference on no column', async () => {
    mockApi()
    renderBoard()
    await loaded()

    await column('Done').findByText('A-DONE')
    expect(screen.queryByText('A-DENIED')).not.toBeInTheDocument()
  })

  it('links a card to its behaviour page and names its project', async () => {
    mockApi()
    renderBoard()
    await loaded()

    const link = await column('Backlog').findByRole('link', { name: 'title of A-BACKLOG' })
    expect(link).toHaveAttribute('href', '/projects/alpha/behaviours/A-BACKLOG')
    expect(column('Backlog').getByText('A-BACKLOG')).toHaveClass('muted')
    expect(column('Backlog').getByText('alpha')).toBeInTheDocument()
  })

  it('colours and filters by project, so files naming one # kit:project share a button and a colour', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) => ({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () => answer(path, [{ app: 'alpha', project: 'one' }, { app: 'beta', project: 'one' }]),
      })),
    )
    renderBoard()
    await loaded()

    expect(await screen.findByRole('button', { name: 'Filter to one' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Filter to alpha' })).not.toBeInTheDocument()
    const a = column('Done').getByText('one · alpha').closest('.board-card')!
    const b = column('Done').getByText('one · beta').closest('.board-card')!
    expect(a.className).toContain(`hue-${hueOf('one')}`)
    expect(b.className).toContain(`hue-${hueOf('one')}`)
  })

  it('names a corpus that will not parse, not drops it', async () => {
    mockApi()
    renderBoard()
    await loaded()

    expect(await screen.findByRole('alert')).toHaveTextContent('expected a verb')
    expect(screen.getByRole('heading', { name: 'broken' })).toBeInTheDocument()
  })

  it('hides trial corpora until asked, and records the ask in the url', async () => {
    mockApi()
    renderBoard()
    await loaded()
    await column('Done').findByText('A-DONE')
    expect(screen.queryByText('T-DONE')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Show trials' }))

    expect(await column('Done').findByText('T-DONE')).toBeInTheDocument()
    expect(screen.getByTestId('search')).toHaveTextContent('?trials=1')
  })

  it('filters to the selected projects, in the url', async () => {
    mockApi()
    renderBoard()
    await loaded()
    await column('Done').findByText('B-DONE')

    fireEvent.click(screen.getByRole('button', { name: 'Filter to alpha' }))

    expect(column('Done').queryByText('B-DONE')).not.toBeInTheDocument()
    expect(column('Done').getByText('A-DONE')).toBeInTheDocument()
    expect(screen.getByTestId('search')).toHaveTextContent('?project=alpha')
  })

  it('puts the top of a schedule in progress and the rest of it in Scheduled, in schedule order (BEH-BOARD-3)', async () => {
    mockApi()
    renderBoard()
    await loaded()

    const inProgress = screen.getByRole('region', { name: 'In progress' })
    expect(await within(inProgress).findByText('A-NEXT')).toBeInTheDocument()
    expect(within(inProgress).getAllByRole('listitem')).toHaveLength(1)
    const scheduled = within(screen.getByRole('region', { name: 'Scheduled' }))
      .getAllByRole('link')
      .map((a) => a.textContent)
    expect(scheduled).toEqual(['title of A-THIRD', 'title of A-SECOND'])
    // Built since it was scheduled: Done, not Scheduled. Unscheduled and pending: Backlog.
    expect(column('Done').getByText('A-DONE')).toBeInTheDocument()
    expect(column('Backlog').getByText('A-BACKLOG')).toBeInTheDocument()
    expect(column('Backlog').queryByText('A-NEXT')).not.toBeInTheDocument()
  })

  it('a schedule that cannot be read says so, and its pending behaviours stay on the board as backlog', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) =>
        path.endsWith('/alpha/schedule')
          ? { ok: false, status: 500, statusText: 'x', json: async () => ({ reason: 'alpha.schedule is unreadable' }) }
          : { ok: true, status: 200, statusText: 'OK', json: async () => answer(path) },
      ),
    )
    renderBoard()
    await loaded()

    expect(await screen.findByText(/schedule could not be read/)).toBeInTheDocument()
    expect(screen.getAllByRole('alert').some((a) => a.textContent?.includes('alpha.schedule is unreadable'))).toBe(true)
    expect(column('Backlog').getByText('A-NEXT')).toBeInTheDocument()
    expect(column('In progress').queryByText('A-NEXT')).not.toBeInTheDocument()
  })

  it('reads the filter from the url', async () => {
    mockApi()
    renderBoard('/board?project=beta')
    await loaded()

    expect(await column('Done').findByText('B-DONE')).toBeInTheDocument()
    expect(column('Done').queryByText('A-DONE')).not.toBeInTheDocument()
  })
})

describe('board rules', () => {
  it('an unreviewed inference is proposed even when pending', () => {
    expect(
      columnOf(beh('X', { pending: true, source: inferred, review: { state: 'unreviewed', note: null } })),
    ).toBe('proposed')
  })

  it('an approved inference is not proposed', () => {
    expect(columnOf(beh('X', { source: inferred }))).toBe('done')
  })

  it('the schedule moves only pending, adjudicated work (BEH-BOARD-3)', () => {
    const pending = beh('X', { pending: true })
    expect(columnOf(pending)).toBe('backlog')
    expect(columnOf(pending, { order: 1, next: false })).toBe('scheduled')
    expect(columnOf(pending, { order: 0, next: true })).toBe('in-progress')
    // A built behaviour is done whatever the schedule still says; an unadjudicated one is proposed.
    expect(columnOf(beh('X'), { order: 0, next: true })).toBe('done')
    expect(
      columnOf(beh('X', { pending: true, source: inferred, review: { state: 'unreviewed', note: null } }), { order: 0, next: true }),
    ).toBe('proposed')
  })

  it('a project keeps its colour', () => {
    expect(hueOf('snip-it')).toBe(hueOf('snip-it'))
    expect(hueOf('snip-it')).toBeGreaterThanOrEqual(0)
    expect(hueOf('snip-it')).toBeLessThan(HUES)
  })
})

describe('navbar', () => {
  it('links the board and the projects and marks the current view', async () => {
    window.history.pushState({}, '', '/board')
    // App wraps everything in SignIn, which asks /api/session first.
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) => ({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () =>
          path.endsWith('/api/session') ? { required: false, signedIn: true } : { projects: [] },
      })),
    )
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Board' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'Projects' })).not.toHaveAttribute('aria-current')
  })
})
