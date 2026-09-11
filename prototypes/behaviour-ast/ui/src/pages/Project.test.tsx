import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Project from './Project'
import snipIt from '../test/fixtures/project-snip-it.json'
import habits from '../test/fixtures/project-james-habits-app.json'

function renderApp(body: unknown, app: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )

  return render(
    <MemoryRouter initialEntries={[`/projects/${app}`]}>
      <Routes>
        <Route path="/projects/:app" element={<Project />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Project', () => {
  it('lists the corpus', async () => {
    renderApp(snipIt, 'snip-it')

    expect(await screen.findByRole('heading', { name: 'snip-it', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('8 behaviours')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'BEH-EDIT-1' })).toBeInTheDocument()
  })

  it('COUNTS the detail endpoint’s coverage instead of concatenating its ids', async () => {
    // The detail endpoint sends `covered`/`uncovered` as arrays of behaviour
    // ids, not counts — unlike the list endpoint. Treating them as numbers
    // renders "BEH-HOME-1BEH-EDIT-1…BEH-CUT-1,BEH-UP-2 covered", which is what
    // the page actually did until a screenshot showed it. No amount of the list
    // page's tests could see this.
    const covered = { available: true, covered: ['A', 'B'], uncovered: ['C'] }
    renderApp({ ...snipIt, coverage: covered }, 'snip-it')

    expect(await screen.findByText('2/3 covered')).toBeInTheDocument()
    // The concatenation bug's actual output shape: the ids run together next to
    // the word "covered". (A bare /BEH-/ would match the behaviour links, which
    // are supposed to be there.)
    expect(screen.queryByText(/AB.*covered/)).not.toBeInTheDocument()
  })

  it('still says "not measured" when the detail endpoint could not look', async () => {
    // The detail endpoint OMITS covered/uncovered rather than sending null, so
    // this is a different code path from the list page's identical-looking test.
    expect(snipIt.coverage).not.toHaveProperty('covered')
    renderApp(snipIt, 'snip-it')

    expect(await screen.findByText('not measured')).toBeInTheDocument()
  })

  it('shows conflicts, with what is held and who challenges it', async () => {
    renderApp(habits, 'james-habits-app')

    // Scoped to the conflict card: the challenger's id is also a behaviour in
    // the list below, so an unscoped query matches twice and proves nothing
    // about the conflicts panel.
    const card = (await screen.findByText('region:CompletionGrid.days')).closest('div')!
    expect(within(card).getByText('BEH-WINDOW-API', { exact: false })).toBeInTheDocument()
    expect(within(card).getByText('30')).toBeInTheDocument()
  })

  it('marks behaviours nobody has reviewed', async () => {
    renderApp(habits, 'james-habits-app')

    const flags = await screen.findAllByText('unreviewed')
    expect(flags).toHaveLength(habits.adjudication.unreviewed.length)
  })
})

// ── the question sheet, which ui.js has always sent and this UI ignored ──────

describe('the question sheet', () => {
  it('the fixture carries both tiers, or everything below is vacuous', () => {
    const tiers = habits.questions.map((q) => q.tier)
    expect(tiers).toContain('decision')
    expect(tiers).toContain('review')
  })

  it('renders the decision pack Kit already built — the ask, both options, the recommendation', async () => {
    // This is the most considered artefact Kit produces and the UI was
    // discarding it: `questions` was typed `unknown[]` and read by nothing.
    renderApp(habits, 'james-habits-app')

    // Scoped to the one card. There are three decision-tier questions in this
    // fixture and each renders its own recommendation, so an unscoped query for
    // "recommended" matches every one of them and asserts nothing about this
    // conflict.
    const card = (await screen.findByText(/Two behaviours disagree about/)).closest('div')!

    expect(within(card).getByText(/Which one is the contract the frontend gets built against/)).toBeInTheDocument()

    // Both options, each with the consequence that makes it a choice rather
    // than a preference.
    const items = within(card).getAllByRole('listitem').map((li) => li.textContent ?? '')
    expect(items.some((t) => /Rename the design to historyDays/.test(t))).toBe(true)
    expect(within(card).getByText(/No code moves, no client regenerates/)).toBeInTheDocument()

    // The recommended label appears TWICE inside this card on purpose — once as
    // an option and once as the recommendation — so an exact count is the
    // assertion, not a nuisance to work around.
    expect(within(card).getAllByText(/Rename the code to days/)).toHaveLength(2)
    expect(within(card).getByText('recommended')).toBeInTheDocument()
    expect(within(card).getByText(/^Against:/)).toBeInTheDocument()
  })

  it('offers NO control on a conflict, and says why', async () => {
    // Kit has no syntax for "BEH-A supersedes BEH-B" — no keyword, no writer
    // function. A resolve button here would mean inventing the grammar it wrote
    // into, which is a change to the corpus language and James's call. The page
    // must say that rather than look unfinished.
    renderApp(habits, 'james-habits-app')

    const card = (await screen.findByText(/Two behaviours disagree about/)).closest('div')!
    expect(within(card).queryByRole('button')).not.toBeInTheDocument()
    expect(within(card).getByText(/no syntax for recording a resolution/)).toBeInTheDocument()
  })

  it('links every never-adjudicated inference to the page where it can be answered', async () => {
    renderApp(habits, 'james-habits-app')

    const reviews = habits.questions.filter((q) => q.tier === 'review')
    const queue = (await screen.findByRole('heading', { name: 'Never adjudicated' })).parentElement!
    for (const q of reviews) {
      expect(within(queue).getByRole('link', { name: q.id! })).toHaveAttribute(
        'href',
        `/projects/james-habits-app/behaviours/${q.id}`,
      )
    }
  })

  it('will not submit a new behaviour without an id, and will not without a title', async () => {
    // Both halves, because the button guards both and a test for one alone is
    // satisfied by a form that dropped the other. An id is not a field the
    // corpus can default: it is the key `check.js` gates on and the anchor every
    // `serves`/`cites` line refers to, so a blank one is a behaviour nothing can
    // ever name.
    renderApp(snipIt, 'snip-it')

    const add = await screen.findByRole('button', { name: 'Add behaviour' })
    expect(add).toBeDisabled()

    // Title only — still refused.
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'the cut is downloadable' } })
    expect(add).toBeDisabled()

    // Id as whitespace is the same as no id; `.trim()` is what makes that true.
    fireEvent.change(screen.getByLabelText('Id'), { target: { value: '   ' } })
    expect(add).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Id'), { target: { value: 'BEH-CUT-2' } })
    expect(add).toBeEnabled()

    // And the other direction: a title cleared after the fact re-disables it.
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: '' } })
    expect(add).toBeDisabled()
  })

  it('shows no sheet at all when there is nothing to ask', async () => {
    // snip-it's corpus is entirely `defined` and conflict-free. An empty heading
    // over an empty list reads as "the sheet is broken"; the absence of a
    // question is not a thing to render.
    expect(snipIt.questions).toHaveLength(0)
    renderApp(snipIt, 'snip-it')

    await screen.findByRole('heading', { name: 'snip-it', level: 1 })
    expect(screen.queryByRole('heading', { name: 'Question sheet' })).not.toBeInTheDocument()
  })
})
