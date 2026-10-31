import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Project from './Project'
import { noNotes } from '../test/noNotes'
import snipIt from '../test/fixtures/project-snip-it.json'
import habits from '../test/fixtures/project-james-habits-app.json'

function renderApp(body: unknown, app: string, search = '') {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => noNotes(url, init) ?? ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )

  return render(
    <MemoryRouter initialEntries={[`/projects/${app}${search}`]}>
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
    // BEH-TITLE-1: the card links by its title; the id is still on it, quieter.
    const edit = snipIt.behaviours.find((b) => b.id === 'BEH-EDIT-1')!
    const link = screen.getByRole('link', { name: edit.title })
    expect(link).toHaveAttribute('href', '/projects/snip-it/behaviours/BEH-EDIT-1')
    expect(within(link.closest('h3')!).getByText('BEH-EDIT-1')).toHaveClass('muted')
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

  it('marks each inferred behaviour in the LIST, not only in the summary count', async () => {
    // Two different renders say "inferred" on this page: the summary badge
    // ("3 inferred") and a per-row badge. Only the summary was pinned, so the
    // row badge could vanish and the page would still read as though it said
    // which behaviours a machine wrote — the distinction the whole adjudication
    // queue rests on.
    //
    // The exact-text query is what separates them: the summary badge's text is
    // "N inferred", not "inferred".
    renderApp(habits, 'james-habits-app')

    await screen.findByRole('heading', { name: 'james-habits-app', level: 1 })
    const rowFlags = screen.getAllByText('inferred', { exact: true })
    const inferredCount = habits.behaviours.filter((b) => b.source.origin === 'inferred').length
    expect(inferredCount).toBeGreaterThan(0) // or this test is vacuous
    expect(rowFlags).toHaveLength(inferredCount)
  })

  it('CONTROL: a corpus with no inferences shows no row marked inferred', async () => {
    // snip-it is entirely `defined`. Without this, the count above is satisfied
    // by a badge rendered on every row.
    expect(snipIt.behaviours.every((b) => b.source.origin !== 'inferred')).toBe(true)
    renderApp(snipIt, 'snip-it')

    await screen.findByRole('heading', { name: 'snip-it', level: 1 })
    expect(screen.queryByText('inferred', { exact: true })).not.toBeInTheDocument()
  })
})

// ── BEH-FEAT-6: features and questions are two tabs, features first ─────────

describe('the project tabs', () => {
  const waiting = habits.conflicts.length + habits.questions.length

  it('opens on Features, with the questions a tap away and counted', async () => {
    renderApp(habits, 'james-habits-app')

    const features = await screen.findByRole('tab', { name: 'Features' })
    expect(features).toHaveAttribute('aria-selected', 'true')
    const questions = screen.getByRole('tab', { name: `Questions ${waiting}` })
    expect(questions).toHaveAttribute('aria-selected', 'false')
    // Hidden, not merely below: role queries skip a hidden panel, so this fails
    // if the sheet is still on screen under the features.
    expect(screen.queryByRole('heading', { name: 'Problems in the spec' })).not.toBeInTheDocument()
    expect(screen.getByRole('tabpanel', { name: 'Features' })).toBeVisible()
  })

  it('switches to Questions on a tap, and back', async () => {
    renderApp(habits, 'james-habits-app')

    fireEvent.click(await screen.findByRole('tab', { name: `Questions ${waiting}` }))
    expect(screen.getByRole('heading', { name: 'Problems in the spec' })).toBeInTheDocument()
    expect(screen.getByRole('tabpanel', { name: `Questions ${waiting}` })).toBeVisible()
    expect(screen.queryByRole('tabpanel', { name: 'Features' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('tab', { name: 'Features' }))
    expect(screen.queryByRole('heading', { name: 'Problems in the spec' })).not.toBeInTheDocument()
  })

  it('opens on Questions from a link carrying ?tab=questions', async () => {
    renderApp(habits, 'james-habits-app', '?tab=questions')

    expect(await screen.findByRole('tab', { name: `Questions ${waiting}` })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('heading', { name: 'Problems in the spec' })).toBeInTheDocument()
  })

  it('CONTROL: with nothing waiting the tab carries no count and says so when opened', async () => {
    expect(snipIt.questions.length + snipIt.conflicts.length).toBe(0)
    renderApp(snipIt, 'snip-it', '?tab=questions')

    expect(await screen.findByRole('tab', { name: 'Questions' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Nothing is waiting on you.')).toBeVisible()
  })

  it('leads the Questions tab with what Kit is asking him, above the problems, and counts it (BEH-SHEET-2)', async () => {
    const pending = snipIt.behaviours[0]
    const body = {
      ...snipIt,
      behaviours: [
        { ...pending, id: 'BEH-ASK-1', title: 'needs him', pending: true, needsContext: 'Invite-only? I would start invite-only.' },
        { ...pending, id: 'BEH-ASK-2', title: 'asks him', review: { state: 'requested', note: 'Which colour?' } },
        // Not asks: a look that is no question, a paused card, a built one, a denied one.
        { ...pending, id: 'BEH-LOOK', title: 'a look', review: { state: 'requested', note: 'the copy' } },
        { ...pending, id: 'BEH-HELD', title: 'held', pending: true, paused: true, needsContext: 'x?' },
        { ...pending, id: 'BEH-BUILT', title: 'built', pending: false, needsContext: 'y?' },
        { ...pending, id: 'BEH-DENIED', title: 'denied', pending: true, needsContext: 'z?', review: { state: 'denied', note: 'no' } },
      ],
    }
    renderApp(body, 'snip-it', '?tab=questions')

    // snip-it has no conflicts and no sheet, so the count is the two asks alone.
    expect(await screen.findByRole('tab', { name: 'Questions 2' })).toHaveAttribute('aria-selected', 'true')
    const section = screen.getByRole('heading', { name: 'Kit is asking you' }).closest('section')!
    const items = within(section).getAllByRole('listitem')
    expect(items.map((li) => li.querySelector('.ask')!.textContent)).toEqual(['Invite-only? I would start invite-only.', 'Which colour?'])
    expect(within(items[0]).getByRole('link', { name: 'BEH-ASK-1' })).toHaveAttribute('href', '/projects/snip-it/behaviours/BEH-ASK-1')
    expect(screen.queryByText('Nothing is waiting on you.')).not.toBeInTheDocument()
  })

  it('puts the questions before the problems in the spec (BEH-SHEET-2)', async () => {
    const body = { ...habits, behaviours: [...habits.behaviours, { ...habits.behaviours[0], id: 'BEH-ASK-1', pending: true, needsContext: 'Which?' }] }
    renderApp(body, 'james-habits-app', '?tab=questions')

    const asks = await screen.findByRole('heading', { name: 'Kit is asking you' })
    const problems = screen.getByRole('heading', { name: 'Problems in the spec' })
    expect(asks.compareDocumentPosition(problems) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('replies under a question as a note on its card, and shows a reply still waiting for Kit (BEH-SHEET-3)', async () => {
    const pending = snipIt.behaviours[0]
    const body = {
      ...snipIt,
      behaviours: [
        { ...pending, id: 'BEH-ASK-1', title: 'needs him', pending: true, needsContext: 'Invite-only?' },
        { ...pending, id: 'BEH-ASK-2', title: 'asks him', pending: true, needsContext: 'Which colour?' },
      ],
    }
    const open = { at: '2026-10-23 00:10Z', kind: 'note', behaviour: 'BEH-ASK-1', resolves: null, text: 'yes, invite-only', thread: [], questions: [] }
    // A note on another card, and a bell on this one, are not replies.
    const other = { ...open, behaviour: 'BEH-OTHER', text: 'elsewhere' }
    const bell = { ...open, kind: 'bell', behaviour: 'BEH-ASK-2', text: '' }
    const posts: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        const ok = (json: unknown) => ({ ok: true, status: 200, statusText: 'OK', json: async () => json })
        if (init?.method === 'POST') {
          posts.push({ url, body: JSON.parse(String(init.body)) })
          return ok({ app: 'snip-it', behaviour: 'BEH-ASK-2', file: 'snip-it.notes.md', note: 'x' })
        }
        if (url.endsWith('/notes')) return ok({ app: 'snip-it', file: 'snip-it.notes.md', notes: [open, other, bell] })
        return noNotes(url, init) ?? ok(body)
      }),
    )
    render(
      <MemoryRouter initialEntries={['/projects/snip-it?tab=questions']}>
        <Routes>
          <Route path="/projects/:app" element={<Project />} />
        </Routes>
      </MemoryRouter>,
    )

    const section = (await screen.findByRole('heading', { name: 'Kit is asking you' })).closest('section')!
    const [first, second] = within(section).getAllByRole('listitem')
    expect(await within(first).findByText('yes, invite-only')).toBeInTheDocument()
    expect(within(first).getByLabelText('Add to your answer')).toBeInTheDocument()
    expect(within(second).queryByText('You replied')).not.toBeInTheDocument()
    expect(within(section).queryByText('elsewhere')).not.toBeInTheDocument()

    const reply = within(second).getByRole('button', { name: 'Reply' })
    expect(reply).toBeDisabled()
    fireEvent.change(within(second).getByLabelText('Your answer'), { target: { value: 'teal' } })
    fireEvent.click(reply)
    await waitFor(() =>
      expect(posts).toEqual([{ url: '/api/projects/snip-it/notes', body: { text: 'teal', behaviour: 'BEH-ASK-2' } }]),
    )
  })

  // BEH-FEAT-7 (his note 2026-10-16 07:13Z): "Let's leverage the tab view more".
  it('keeps only the describe box above the tabs; notes and the schedule are tabs of their own', async () => {
    renderApp(snipIt, 'snip-it')

    expect(await screen.findByLabelText('Describe what should happen')).toBeVisible()
    expect(screen.getByRole('tab', { name: 'Schedule' })).toBeInTheDocument()
    // Hidden behind the Notes tab until it is picked.
    expect(screen.queryByRole('region', { name: 'Notes' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('tab', { name: 'Notes' }))
    expect(screen.getByRole('region', { name: 'Notes' })).toBeVisible()
    expect(screen.queryByRole('tabpanel', { name: 'Features' })).not.toBeInTheDocument()
  })

  it('opens on Notes from ?tab=notes, and has no Principles tab for a project with none', async () => {
    expect(snipIt).not.toHaveProperty('principles')
    renderApp(snipIt, 'snip-it', '?tab=notes')

    expect(await screen.findByRole('tab', { name: 'Notes' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.queryByRole('tab', { name: 'Principles' })).not.toBeInTheDocument()
  })

  it('CONTROL: a project with principles has the tab, and an unknown ?tab falls back to Features', async () => {
    renderApp({ ...snipIt, principles: [{ name: 'Readability', text: 'a human can read it', global: false, at: 'x:1' }] }, 'snip-it', '?tab=nonsense')

    expect(await screen.findByRole('tab', { name: 'Principles' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Features' })).toHaveAttribute('aria-selected', 'true')
  })

  it('has an Architecture tab only for a project with decisions (BEH-ARCH-1)', async () => {
    expect(snipIt).not.toHaveProperty('decisions')
    const { unmount } = renderApp(snipIt, 'snip-it')
    await screen.findByRole('tab', { name: 'Notes' })
    expect(screen.queryByRole('tab', { name: 'Architecture' })).not.toBeInTheDocument()
    unmount()

    const decision = {
      id: 'ADR-DATA-1',
      title: 'Specs are files',
      part: 'data',
      chosen: 'text files in git',
      considered: [],
      because: ['he reviews a diff'],
      review: { state: 'unreviewed', note: null },
      source: { origin: 'defined', ref: null },
      at: 'x.beh:1',
    }
    renderApp({ ...snipIt, decisions: [decision] }, 'snip-it', '?tab=architecture')

    expect(await screen.findByRole('tab', { name: 'Architecture' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Chosen: text files in git')).toBeInTheDocument()
    expect(screen.getByText('Unreviewed suggestion')).toBeInTheDocument()
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
    renderApp(habits, 'james-habits-app', '?tab=questions')

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

  it('resolves a decision in prose, as a note naming its key, never as corpus syntax', async () => {
    // Kit still has no syntax for "BEH-A supersedes BEH-B" (BEH-NOTE-8). The
    // answer is a note, and the fold is what edits the behaviours.
    const posts: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'POST') {
          posts.push({ url, body: JSON.parse(String(init.body)) })
          return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app: 'james-habits-app', behaviour: null, file: 'x.notes.md', note: 'x' }) }
        }
        return noNotes(url, init) ?? { ok: true, status: 200, statusText: 'OK', json: async () => habits }
      }),
    )
    render(
      <MemoryRouter initialEntries={['/projects/james-habits-app?tab=questions']}>
        <Routes>
          <Route path="/projects/:app" element={<Project />} />
        </Routes>
      </MemoryRouter>,
    )

    const card = (await screen.findByText(/Two behaviours disagree about/)).closest('div')!
    const resolve = within(card).getByRole('button', { name: 'Resolve' })
    expect(resolve).toBeDisabled()
    fireEvent.change(within(card).getByLabelText('How should this be resolved?'), { target: { value: 'the newer one wins' } })
    fireEvent.click(resolve)

    const key = habits.questions.find((q) => q.tier === 'decision')!.key
    await waitFor(() =>
      expect(posts).toEqual([
        { url: '/api/projects/james-habits-app/notes', body: { text: 'the newer one wins', kind: 'resolution', resolves: key } },
      ]),
    )
  })

  it('shows the question sheet Kit built for a project, with its review queue and the way to ask for more', async () => {
    // BEH-SHEET-1's three `then`s, on the project its `provides` names: the sheet's heading, the
    // never-adjudicated queue under it, and the button that asks for a new behaviour. The sheet
    // is behind the Questions tab since BEH-FEAT-6, so the test opens on it.
    renderApp(habits, 'james-habits-app', '?tab=questions')

    expect(await screen.findByRole('heading', { name: 'Problems in the spec' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Never adjudicated' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ask for behaviour' })).toBeInTheDocument()
  })

  it('links every never-adjudicated inference to the page where it can be answered', async () => {
    renderApp(habits, 'james-habits-app', '?tab=questions')

    const reviews = habits.questions.filter((q) => q.tier === 'review')
    const queue = (await screen.findByRole('heading', { name: 'Never adjudicated' })).parentElement!
    for (const q of reviews) {
      expect(within(queue).getByRole('link', { name: q.id! })).toHaveAttribute(
        'href',
        `/projects/james-habits-app/behaviours/${q.id}`,
      )
    }
  })

  it('asks for a new behaviour in prose: no id, title or actor, and nothing sent while it is blank', async () => {
    // BEH-NOTE-4: the Id / Title / Actor form is gone. Whoever folds the request
    // writes those, so asking for them here would be the chore he named.
    const posts: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'POST') {
          posts.push({ url, body: JSON.parse(String(init.body)) })
          return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app: 'snip-it', behaviour: null, file: 'x.notes.md', note: 'x' }) }
        }
        return noNotes(url, init) ?? { ok: true, status: 200, statusText: 'OK', json: async () => snipIt }
      }),
    )
    render(
      <MemoryRouter initialEntries={['/projects/snip-it']}>
        <Routes>
          <Route path="/projects/:app" element={<Project />} />
        </Routes>
      </MemoryRouter>,
    )

    const ask = await screen.findByRole('button', { name: 'Ask for behaviour' })
    expect(screen.queryByLabelText('Id')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
    expect(ask).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Describe what should happen'), { target: { value: '  ' } })
    expect(ask).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Describe what should happen'), { target: { value: 'a cut can be downloaded' } })
    fireEvent.click(ask)

    await waitFor(() =>
      expect(posts).toEqual([{ url: '/api/projects/snip-it/notes', body: { text: 'a cut can be downloaded', kind: 'request' } }]),
    )
    await waitFor(() => expect(screen.getByLabelText('Describe what should happen')).toHaveValue(''))
    // BEH-FEAT-7: asking opens the Notes tab, so the note just left is in view.
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Notes' })).toHaveAttribute('aria-selected', 'true'))
    expect(screen.getByRole('region', { name: 'Notes' })).toBeVisible()
  })

  it('shows no sheet at all when there is nothing to ask', async () => {
    // snip-it's corpus is entirely `defined` and conflict-free. An empty heading
    // over an empty list reads as "the sheet is broken"; the absence of a
    // question is not a thing to render.
    expect(snipIt.questions).toHaveLength(0)
    renderApp(snipIt, 'snip-it')

    await screen.findByRole('heading', { name: 'snip-it', level: 1 })
    expect(screen.queryByRole('heading', { name: 'Problems in the spec' })).not.toBeInTheDocument()
  })
})
