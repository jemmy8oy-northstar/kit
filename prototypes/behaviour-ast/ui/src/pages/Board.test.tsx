import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import Board from './Board'
import App from '../App'
import { byCompletion, byPriority, columnOf, hueOf, HUES, isQuestion } from '../board'
import type { Behaviour, CompletionList } from '../api/types'

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

/** Each app's history (BEH-BOARD-4); one not named answers git with nothing dated. */
let histories: Record<string, CompletionList> = {}

/** Each app's unanswered bells (BEH-BELL-3): the item ids rung. One not named has none. */
let bells: Record<string, string[]> = {}

/** The read API: the project list, a project's detail, its schedule (BEH-SCHED-1) or its history. */
function answer(path: string, projects: object[] = summaries) {
  const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
  if (rest.endsWith('/history')) {
    const app = rest.slice(0, -'/history'.length)
    return histories[app] ?? { app, file: `${app}.beh`, history: 'git', reason: null, completed: [], added: [] }
  }
  if (rest.endsWith('/notes')) {
    const app = rest.slice(0, -'/notes'.length)
    const notes = (bells[app] ?? []).map((id) => ({ at: '2026-10-21 06:50Z', kind: 'bell', behaviour: id, resolves: null, text: '', thread: [], questions: [] }))
    return { app, file: `${app}.notes.md`, notes }
  }
  if (rest.endsWith('/schedule')) {
    const app = rest.slice(0, -'/schedule'.length)
    // The board reads only id, order and next; title and state are the panel's.
    const scheduled = (schedules[app] ?? []).map((s) => ({ ...s, title: null, state: 'pending' }))
    return { app, file: `${app}.schedule`, scheduled, backlog: [] }
  }
  return rest ? { app: rest, behaviours: details[rest] } : { projects }
}

function mockApi(h: Record<string, CompletionList> = {}, b: Record<string, string[]> = {}) {
  histories = h
  bells = b
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => ({ ok: true, status: 200, statusText: 'OK', json: async () => answer(path) })),
  )
}

function done(app: string, completed: [string, string | null][]): CompletionList {
  return { app, file: `${app}.beh`, history: 'git', reason: null, completed: completed.map(([id, at]) => ({ id, at })), added: [] }
}

/** The ids on Done's cards, top to bottom. */
function doneOrder() {
  return column('Done')
    .getAllByRole('listitem')
    .map((li) => li.querySelector('.id')!.textContent)
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
  // A test that stubs fetch itself still answers histories through `answer`; none may inherit another's.
  beforeEach(() => {
    histories = {}
    bells = {}
  })

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

  it('shows each card its layer and filters by layer, in the url beside the project filter (BEH-KIND-1)', async () => {
    const saved = details.beta
    details.beta = [beh('B-DONE', { layer: 'technical' }), beh('B-UX', { layer: 'ux' })]
    try {
      mockApi()
      renderBoard('/board?project=alpha,beta')
      await loaded()
      const card = (await column('Done').findByText('B-DONE')).closest('li')!
      expect(within(card).getByText('technical')).toBeInTheDocument()

      fireEvent.click(screen.getByRole('button', { name: 'Filter to the technical layer' }))
      expect(column('Done').getByText('B-DONE')).toBeInTheDocument()
      expect(column('Done').queryByText('B-UX')).not.toBeInTheDocument()
      expect(column('Done').queryByText('A-DONE')).not.toBeInTheDocument() // alpha's are all ui
      expect(screen.getByTestId('search')).toHaveTextContent('?project=alpha%2Cbeta&layer=technical')

      // Two layers is either, not both.
      fireEvent.click(screen.getByRole('button', { name: 'Filter to the ux layer' }))
      expect(column('Done').getByText('B-UX')).toBeInTheDocument()
      expect(column('Done').getByText('B-DONE')).toBeInTheDocument()
    } finally {
      details.beta = saved
    }
  })

  it('CONTROL: a layer in the url that the corpus has no word for filters nothing out', async () => {
    mockApi()
    renderBoard('/board?layer=logic')
    await loaded()
    expect(await column('Done').findByText('B-DONE')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Filter to the ui layer' })).toHaveAttribute('aria-pressed', 'false')
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

  it('orders Done most recently completed first, across projects (BEH-BOARD-4)', async () => {
    details.alpha.push(beh('A-OLD'), beh('A-AHEAD'))
    try {
      mockApi({
        alpha: done('alpha', [['A-DONE', '2026-10-02T09:00:00Z'], ['A-OLD', '2026-09-01T09:00:00Z'], ['A-AHEAD', null]]),
        beta: done('beta', [['B-DONE', '2026-10-05T09:00:00Z']]),
      })
      renderBoard()
      await loaded()
      await column('Done').findByText('A-AHEAD')

      // Built ahead of the history is newest of all; the rest by date, whichever project.
      expect(doneOrder()).toEqual(['A-AHEAD', 'B-DONE', 'A-DONE', 'A-OLD'])
      expect(column('Done').queryByText(/Not ordered by date/)).not.toBeInTheDocument()
    } finally {
      details.alpha.splice(-2)
    }
  })

  it('keeps the Ideas file off the board, even when trials are shown (BEH-IDEA-1)', async () => {
    details.ideas = [beh('IDEA-1', { pending: true })]
    try {
      vi.stubGlobal(
        'fetch',
        vi.fn(async (path: string) => ({
          ok: true,
          status: 200,
          statusText: 'OK',
          json: async () => answer(path, [...summaries, { app: 'ideas', idea: true }]),
        })),
      )
      renderBoard('/board?trials=1')
      await loaded()
      await column('Backlog').findByText('A-BACKLOG')

      expect(screen.queryByText('IDEA-1')).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'ideas' })).not.toBeInTheDocument()
    } finally {
      delete details.ideas
    }
  })

  // ── BEH-BOARD-8 (his note 2026-10-16 07:30Z): free text on the board, for a project ──
  function mockApiWithPosts(projects: object[] = summaries) {
    const posts: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string, init?: RequestInit) => {
        if (init?.method === 'POST') {
          posts.push({ url: path, body: JSON.parse(String(init.body)) })
          return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app: 'x', behaviour: null, file: 'x.notes.md', note: 'x' }) }
        }
        return { ok: true, status: 200, statusText: 'OK', json: async () => answer(path, projects) }
      }),
    )
    return posts
  }

  it('asks which project when none is picked, and writes nothing until one is (BEH-BOARD-8)', async () => {
    const posts = mockApiWithPosts()
    renderBoard()
    await loaded()

    fireEvent.change(screen.getByLabelText('Describe what should happen'), { target: { value: 'remember my filter' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ask for behaviour' }))
    expect(await screen.findByText('Which project is this for? Pick one, then send it again.')).toHaveAttribute('role', 'alert')
    expect(posts).toEqual([])

    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    expect(screen.queryByText(/Which project is this for/)).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Ask for behaviour' }))
    expect(await screen.findByRole('link', { name: "See it in beta's notes" })).toHaveAttribute('href', '/projects/beta?tab=notes')
    expect(posts).toEqual([{ url: '/api/projects/beta/notes', body: { text: 'remember my filter', kind: 'request' } }])
    expect(screen.getByLabelText('Describe what should happen')).toHaveValue('')
  })

  it('suggests a project the text names, and one tap picks it (BEH-BOARD-9)', async () => {
    const posts = mockApiWithPosts()
    renderBoard()
    await loaded()

    const box = screen.getByLabelText('Describe what should happen')
    fireEvent.change(box, { target: { value: 'alphabet soup' } })
    // A whole word only: "alphabet" is not alpha.
    expect(screen.queryByRole('button', { name: /^For / })).not.toBeInTheDocument()

    fireEvent.change(box, { target: { value: 'In Beta, remember my filter' } })
    fireEvent.click(screen.getByRole('button', { name: 'For beta?' }))
    expect(screen.getByLabelText('Project')).toHaveValue('beta')
    expect(screen.queryByRole('button', { name: 'For beta?' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Ask for behaviour' }))
    await screen.findByRole('link', { name: "See it in beta's notes" })
    expect(posts).toEqual([{ url: '/api/projects/beta/notes', body: { text: 'In Beta, remember my filter', kind: 'request' } }])
  })

  it('CONTROL: a name nobody lists is never suggested (BEH-BOARD-9)', async () => {
    mockApiWithPosts()
    renderBoard()
    await loaded()
    fireEvent.change(screen.getByLabelText('Describe what should happen'), { target: { value: 'for gamma please' } })
    expect(screen.queryByRole('button', { name: /^For / })).not.toBeInTheDocument()
  })

  it('sends a grouped project to the file named like it, and offers no idea, trial or unreadable file (BEH-BOARD-8)', async () => {
    const saved = { ...details }
    details['kit-ui'] = [beh('K-UI')]
    details.kit = [beh('K-CLI')]
    details.ideas = [beh('I-1')]
    try {
      const posts = mockApiWithPosts([
        { app: 'kit-ui', project: 'kit' },
        { app: 'kit', project: 'kit' },
        { app: 'ideas', idea: true },
        { app: 'trial', notReal: true },
        { app: 'broken', error: 'x' },
      ])
      renderBoard()
      await loaded()

      const options = within(screen.getByLabelText('Project')).getAllByRole('option').map((o) => o.textContent)
      expect(options).toEqual(['Pick a project…', 'kit'])

      fireEvent.change(screen.getByLabelText('Describe what should happen'), { target: { value: 'x' } })
      fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'kit' } })
      fireEvent.click(screen.getByRole('button', { name: 'Ask for behaviour' }))
      await screen.findByRole('link', { name: "See it in kit's notes" })
      // kit-ui is listed first, but the request goes to kit.notes.md, the notes the project page shows.
      expect(posts).toEqual([{ url: '/api/projects/kit/notes', body: { text: 'x', kind: 'request' } }])
    } finally {
      for (const k of ['kit-ui', 'kit', 'ideas']) delete details[k]
      Object.assign(details, saved)
    }
  })

  it('caps each column at ten, keeps the top of the order, and shows more on request (BEH-BOARD-7)', async () => {
    // 22 more backlog cards beside alpha's one: 23, ranked so the top ten are the P1s.
    const extra = Array.from({ length: 22 }, (_, i) =>
      beh(`B-${String(i).padStart(2, '0')}`, { pending: true, priority: i < 10 ? 1 : 4 }),
    )
    details.beta.push(...extra)
    try {
      mockApi()
      renderBoard()
      await loaded()
      await column('Backlog').findByText('B-00')

      const ids = () =>
        column('Backlog')
          .getAllByRole('listitem')
          .map((li) => li.querySelector('.id')!.textContent)
      // The header still counts every card; only ten are drawn, and they are the top ten.
      expect(column('Backlog').getByText('23')).toBeInTheDocument()
      expect(ids()).toEqual(extra.slice(0, 10).map((b) => b.id))

      fireEvent.click(column('Backlog').getByRole('button', { name: 'Show 10 more (13 hidden)' }))
      expect(ids()).toHaveLength(20)
      fireEvent.click(column('Backlog').getByRole('button', { name: 'Show 3 more (3 hidden)' }))
      expect(ids()).toHaveLength(23)
      expect(column('Backlog').queryByRole('button', { name: /Show .* more/ })).not.toBeInTheDocument()
      // A column under the cap has no button at all.
      expect(column('Done').queryByRole('button', { name: /Show .* more/ })).not.toBeInTheDocument()
    } finally {
      details.beta.splice(-22)
    }
  })

  it('comes back from a card to that card in its column, not to the first column (BEH-BOARD-15)', async () => {
    const scrolled: Element[] = []
    Element.prototype.scrollIntoView = vi.fn(function (this: Element) {
      scrolled.push(this)
    })
    function Opened() {
      const navigate = useNavigate()
      return <button onClick={() => navigate(-1)}>Back</button>
    }
    try {
      mockApi()
      render(
        <MemoryRouter initialEntries={['/board?project=alpha']}>
          <Routes>
            <Route path="/board" element={<><Board /><Search /></>} />
            <Route path="/projects/:app/behaviours/:id" element={<Opened />} />
          </Routes>
        </MemoryRouter>,
      )
      await loaded()
      fireEvent.click(column('Proposed').getByRole('link', { name: 'title of A-PROPOSED' }))
      fireEvent.click(await screen.findByRole('button', { name: 'Back' }))
      await loaded()

      // The filter he had survives beside it, and the board lands on the card he opened.
      expect(new URLSearchParams(screen.getByTestId('search').textContent!)).toEqual(
        new URLSearchParams('project=alpha&col=proposed&card=alpha/A-PROPOSED'),
      )
      const link = column('Proposed').getByRole('link', { name: 'title of A-PROPOSED' })
      await vi.waitFor(() => expect(document.activeElement).toBe(link))
      expect(scrolled).toEqual([link.closest('li')])
    } finally {
      delete (Element.prototype as { scrollIntoView?: unknown }).scrollIntoView
    }
  })

  it('lands on the column when the card has left it, and shows enough of the column to reach it (BEH-BOARD-15)', async () => {
    // Approved out of Proposed: he is triaging Proposed, so he goes back to Proposed, not after the card.
    mockApi()
    const first = renderBoard('/board?col=proposed&card=alpha/A-BACKLOG')
    await loaded()
    await vi.waitFor(() => expect(document.activeElement).toBe(column('Proposed').getByRole('heading', { level: 2 })))
    first.unmount()

    // A card past the cap is drawn, so coming back to it never needs "Show more" first.
    const extra = Array.from({ length: 24 }, (_, i) => beh(`B-${String(i).padStart(2, '0')}`, { pending: true, priority: 1 }))
    details.beta.push(...extra)
    try {
      renderBoard('/board?col=backlog&card=beta/B-12')
      await loaded()
      const link = await column('Backlog').findByRole('link', { name: 'title of B-12' })
      await vi.waitFor(() => expect(document.activeElement).toBe(link))
      expect(column('Backlog').getByRole('button', { name: 'Show 5 more (5 hidden)' })).toBeInTheDocument()
    } finally {
      details.beta.splice(-24)
    }
  })

  it('puts review requests first, by priority, with what to look at on the card (BEH-BOARD-6)', async () => {
    details.beta.push(
      beh('B-ASK-LATER', { priority: 3, review: { state: 'requested', note: 'the empty state wording' } }),
      beh('B-ASK-FIRST', { priority: 1, review: { state: 'requested', note: 'the sort on a phone' } }),
    )
    try {
      mockApi()
      renderBoard()
      await loaded()
      await column('Review requested').findByText('B-ASK-FIRST')

      const regions = screen.getAllByRole('region').map((r) => r.getAttribute('aria-labelledby'))
      expect(regions[0]).toBe('board-review')
      expect(
        column('Review requested')
          .getAllByRole('listitem')
          .map((li) => li.querySelector('.id')!.textContent),
      ).toEqual(['B-ASK-FIRST', 'B-ASK-LATER'])
      expect(column('Review requested').getByText('the sort on a phone')).toBeInTheDocument()
      // Built, but not Done until he has looked.
      expect(column('Done').queryByText('B-ASK-FIRST')).not.toBeInTheDocument()
    } finally {
      details.beta.splice(-2)
    }
  })

  it('puts questions from the agent above requests that only want a look, whatever their priority (BEH-BOARD-11)', async () => {
    details.beta.push(
      beh('B-LOOK-P1', { priority: 1, review: { state: 'requested', note: 'the sort on a phone' } }),
      beh('B-ASK-P3', { priority: 3, review: { state: 'requested', note: 'Centre or top by default? ' } }),
      // A question mark mid-note is not a question to him.
      beh('B-LOOK-MARK', { priority: 2, review: { state: 'requested', note: 'the "why?" tooltip' } }),
    )
    try {
      mockApi()
      renderBoard()
      await loaded()
      await column('Review requested').findByText('B-ASK-P3')

      const items = column('Review requested').getAllByRole('listitem')
      expect(items.map((li) => li.querySelector('.id')!.textContent)).toEqual(['B-ASK-P3', 'B-LOOK-P1', 'B-LOOK-MARK'])
      expect(within(items[0]).getByText(/question/)).toBeInTheDocument()
      expect(within(items[0]).getByText('Centre or top by default?')).toBeInTheDocument()
      expect(within(items[1]).queryByText(/question/)).toBeNull()
      expect(within(items[2]).queryByText(/question/)).toBeNull()
    } finally {
      details.beta.splice(-3)
    }
  })

  it('CONTROL: a question mark on a card that is not a review request makes it no question (BEH-BOARD-11)', () => {
    expect(isQuestion(beh('X', { pending: true, review: { state: 'unreviewed', note: 'is this right?' } }))).toBe(false)
    expect(isQuestion(beh('X', { review: { state: 'denied', note: 'why?' } }))).toBe(false)
    expect(isQuestion(beh('X', { review: { state: 'requested', note: 'which one?' } }))).toBe(true)
  })

  it('orders Proposed and Backlog by priority, 1 first, across projects, unranked last (BEH-BOARD-5)', async () => {
    // Priorities that disagree with corpus AND project order, so only reading them sorts right.
    details.beta.push(
      beh('B-LATER', { pending: true, priority: 3 }),
      beh('B-FIRST', { pending: true, priority: 1 }),
      beh('B-SUGGESTED', { source: inferred, review: { state: 'unreviewed', note: null }, priority: 2 }),
    )
    try {
      mockApi()
      renderBoard()
      await loaded()
      await column('Backlog').findByText('B-FIRST')

      const order = (name: string) =>
        column(name)
          .getAllByRole('listitem')
          .map((li) => li.querySelector('.id')!.textContent)
      expect(order('Backlog')).toEqual(['B-FIRST', 'B-LATER', 'A-BACKLOG'])
      expect(order('Proposed')).toEqual(['B-SUGGESTED', 'A-PROPOSED'])
      // The score is on the card, so the order can be read without opening anything.
      expect(column('Backlog').getByText('P1')).toBeInTheDocument()
      expect(column('Proposed').getByText('P2')).toBeInTheDocument()
    } finally {
      details.beta.splice(-3)
    }
  })

  it('takes each project in turn within one priority, so equal scores never read as a block of one project (BEH-BOARD-10)', async () => {
    // Two alpha P1s written before two beta P1s: corpus order alone would show alpha, alpha, beta, beta.
    details.alpha.push(beh('A-P1-ONE', { pending: true, priority: 1 }), beh('A-P1-TWO', { pending: true, priority: 1 }))
    details.beta.push(beh('B-P1-ONE', { pending: true, priority: 1 }), beh('B-P1-TWO', { pending: true, priority: 1 }))
    try {
      mockApi()
      renderBoard()
      await loaded()
      await column('Backlog').findByText('B-P1-ONE')

      const order = column('Backlog')
        .getAllByRole('listitem')
        .map((li) => li.querySelector('.id')!.textContent)
      expect(order.slice(0, 4)).toEqual(['A-P1-ONE', 'B-P1-ONE', 'A-P1-TWO', 'B-P1-TWO'])
    } finally {
      details.alpha.splice(-2)
      details.beta.splice(-2)
    }
  })

  it('puts the most recently added first within one priority, but never above a higher one (BEH-BOARD-12)', async () => {
    // Corpus order is old, new, uncommitted, newest-but-P2: only the history sorts it right.
    details.alpha.push(
      beh('A-OLD', { pending: true, priority: 1 }),
      beh('A-NEW', { pending: true, priority: 1 }),
      beh('A-UNCOMMITTED', { pending: true, priority: 1 }),
      beh('A-NEWEST-P2', { pending: true, priority: 2 }),
    )
    try {
      mockApi({
        alpha: {
          ...done('alpha', []),
          added: [
            { id: 'A-OLD', at: '2026-10-01T09:00:00Z' },
            { id: 'A-NEW', at: '2026-10-02T09:00:00Z' },
            { id: 'A-UNCOMMITTED', at: null },
            { id: 'A-NEWEST-P2', at: '2026-10-03T09:00:00Z' },
          ],
        },
      })
      renderBoard()
      await loaded()
      await column('Backlog').findByText('A-OLD')

      const order = column('Backlog')
        .getAllByRole('listitem')
        .map((li) => li.querySelector('.id')!.textContent)
      expect(order.slice(0, 4)).toEqual(['A-UNCOMMITTED', 'A-NEW', 'A-OLD', 'A-NEWEST-P2'])
    } finally {
      details.alpha.splice(-4)
    }
  })

  it('puts a rung card above every priority, and a rung feature lifts the cards in it (BEH-BELL-3)', async () => {
    details.alpha.push(
      beh('A-P1', { pending: true, priority: 1 }),
      beh('A-RUNG-P4', { pending: true, priority: 4 }),
      beh('A-IN-RUNG-FEATURE', { pending: true, priority: 5, in: ['FEAT-LOUD'] }),
    )
    try {
      mockApi({}, { alpha: ['A-RUNG-P4', 'FEAT-LOUD'] })
      renderBoard()
      await loaded()
      await column('Backlog').findByText('A-P1')

      const items = column('Backlog').getAllByRole('listitem')
      expect(items.slice(0, 3).map((li) => li.querySelector('.id')!.textContent)).toEqual(['A-RUNG-P4', 'A-IN-RUNG-FEATURE', 'A-P1'])
      expect(within(items[0]).getByText(/rung/)).toBeInTheDocument()
      expect(within(items[2]).queryByText(/rung/)).toBeNull()
    } finally {
      details.alpha.splice(-3)
    }
  })

  it('starts each run of a ranked column with a heading that counts it, so the order can be read (BEH-BOARD-14)', async () => {
    details.alpha.push(
      beh('A-RUNG-P3', { pending: true, priority: 3 }),
      beh('A-P1', { pending: true, priority: 1 }),
      beh('A-P2-ONE', { pending: true, priority: 2 }),
      beh('A-P2-TWO', { pending: true, priority: 2 }),
    )
    try {
      mockApi({}, { alpha: ['A-RUNG-P3'] })
      renderBoard()
      await loaded()
      await column('Backlog').findByText('A-P1')

      // Headings and cards in the order they render; a heading is not a card, so not a listitem.
      const rows = [...column('Backlog').getByRole('list').children].map((li) =>
        li.classList.contains('board-tier') ? `# ${li.textContent}` : li.querySelector('.id')!.textContent,
      )
      expect(rows.slice(0, 7)).toEqual(['# 🔔 Rung 1', 'A-RUNG-P3', '# Priority 1 1', 'A-P1', '# Priority 2 2', 'A-P2-ONE', 'A-P2-TWO'])
      expect(column('Backlog').getAllByRole('listitem').every((li) => li.querySelector('.id'))).toBe(true)
      // Done is ordered by date, not by priority, so it has no headings.
      expect(column('Done').getByRole('list').querySelector('.board-tier')).toBeNull()
    } finally {
      details.alpha.splice(-4)
    }
  })

  it('keeps every card when a history cannot be read, says so, and puts them after the dated ones', async () => {
    histories = { beta: done('beta', [['B-DONE', '2026-10-05T09:00:00Z']]) }
    vi.stubGlobal(
      'fetch',
      vi.fn(async (path: string) =>
        path.endsWith('/alpha/history')
          ? { ok: false, status: 500, statusText: 'x', json: async () => ({ reason: 'boom' }) }
          : { ok: true, status: 200, statusText: 'OK', json: async () => answer(path) },
      ),
    )
    renderBoard()
    await loaded()
    await column('Done').findByText('A-DONE')

    expect(doneOrder()).toEqual(['B-DONE', 'A-DONE'])
    expect(column('Done').getByText(/Not ordered by date/)).toHaveTextContent('alpha (')
    expect(column('Done').getByText(/Not ordered by date/)).toHaveTextContent('boom')
  })

  it('says why a project has no history when the server could not read one', async () => {
    mockApi({ alpha: { app: 'alpha', file: 'alpha.beh', history: 'none', reason: 'the corpus is not in a git work tree', completed: [], added: [] } })
    renderBoard()
    await loaded()
    await column('Done').findByText('A-DONE')

    expect(column('Done').getByText(/Not ordered by date/)).toHaveTextContent('alpha (the corpus is not in a git work tree)')
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

  it('a review request waits in Review requested whatever else is true of it (BEH-BOARD-6)', () => {
    const requested = { state: 'requested', note: 'the copy' }
    expect(columnOf(beh('X', { review: requested }))).toBe('review')
    expect(columnOf(beh('X', { review: requested, pending: true }), { order: 0, next: true })).toBe('review')
    expect(columnOf(beh('X', { review: requested, source: inferred }))).toBe('review')
    // Answered either way, it leaves: approved is done, denied is a correction on no column.
    expect(columnOf(beh('X', { review: { state: 'approved', note: null } }))).toBe('done')
    expect(columnOf(beh('X', { review: { state: 'denied', note: 'the copy is wrong' } }))).toBeNull()
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

  it('Done sorts ahead-of-history first, then newest, then undated, keeping ties in place', () => {
    const at: (string | null | undefined)[] = [undefined, '2026-01-01T00:00:00Z', null, '2026-03-01T00:00:00Z', undefined, '2026-01-01T00:00:00Z']
    const sorted = at.map((a, i) => ({ a, i })).sort((x, y) => byCompletion(x.a, y.a))
    expect(sorted.map((s) => s.i)).toEqual([2, 3, 1, 5, 0, 4])
  })

  it('priority sorts 1 first, unranked last, keeping ties in place', () => {
    const p: (number | undefined)[] = [undefined, 3, 1, undefined, 3, 5]
    const sorted = p.map((v, i) => ({ v, i })).sort((x, y) => byPriority(x.v, y.v))
    expect(sorted.map((s) => s.i)).toEqual([2, 1, 4, 5, 0, 3])
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

  it('switches between the epic, feature and behaviour views, with one Board link current on all (BEH-STAGE-8, BEH-EPIC-4)', async () => {
    window.history.pushState({}, '', '/stages')
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
    expect(within(nav).queryByRole('link', { name: 'Stages' })).not.toBeInTheDocument()
    const views = screen.getByRole('navigation', { name: 'Board view' })
    expect(within(views).getByRole('link', { name: 'Features' })).toHaveAttribute('aria-current', 'page')
    fireEvent.click(within(views).getByRole('link', { name: 'Behaviours' }))
    expect(await screen.findByRole('navigation', { name: 'Board view' })).toBeInTheDocument()
    expect(within(screen.getByRole('navigation', { name: 'Board view' })).getByRole('link', { name: 'Behaviours' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(window.location.pathname).toBe('/board')

    // BEH-EPIC-4: epics are a third view, above features, and the Board link stays current on it.
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Board view' })).getByRole('link', { name: 'Epics' }))
    expect(await screen.findByRole('region', { name: 'Epics' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/epics')
    expect(within(nav).getByRole('link', { name: 'Board' })).toHaveAttribute('aria-current', 'page')
    expect(
      within(screen.getByRole('navigation', { name: 'Board view' }))
        .getAllByRole('link')
        .map((a) => a.textContent),
    ).toEqual(['Epics', 'Features', 'Behaviours'])
  })

  it('links Ideas from every page, to the Ideas project (BEH-IDEA-1)', async () => {
    window.history.pushState({}, '', '/board')
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
    expect(within(nav).getByRole('link', { name: 'Ideas' })).toHaveAttribute('href', '/projects/ideas')
  })
})
