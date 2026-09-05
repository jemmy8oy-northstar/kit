import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
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
