import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Project from '../pages/Project'
import { noNotes } from '../test/noNotes'
import snipIt from '../test/fixtures/project-snip-it.json'

function renderApp(body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => noNotes(url, init) ?? ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )
  render(
    <MemoryRouter initialEntries={['/projects/snip-it']}>
      <Routes>
        <Route path="/projects/:app" element={<Project />} />
      </Routes>
    </MemoryRouter>,
  )
}

const ids = snipIt.behaviours.map((b) => b.id)

describe('WorkingTowards on the project page (BEH-NARR-1)', () => {
  it('BEH-NARR-1: names the goal epic as a link, with how many of its behaviours are built', async () => {
    const body = {
      ...snipIt,
      epics: [{ id: 'EPIC-GO', title: 'Ship the editor', at: 'x:1' }],
      features: [{ id: 'FEAT-GO', title: 'Editor', in: [], at: 'x:1', epics: ['EPIC-GO'] }],
      behaviours: snipIt.behaviours.map((b, i) => ({ ...b, in: ['FEAT-GO'], pending: i < 2 })),
    }
    renderApp(body)

    const link = await screen.findByRole('link', { name: 'Ship the editor' })
    expect(link).toHaveAttribute('href', '/projects/snip-it/epics/EPIC-GO')
    expect(screen.getByText(`${ids.length - 2} of ${ids.length} behaviours built`)).toBeInTheDocument()
  })

  it('BEH-NARR-1: says every goal is built when no epic has a pending behaviour', async () => {
    const body = {
      ...snipIt,
      epics: [{ id: 'EPIC-GO', title: 'Ship the editor', at: 'x:1' }],
      features: [{ id: 'FEAT-GO', title: 'Editor', in: [], at: 'x:1', epics: ['EPIC-GO'] }],
      behaviours: snipIt.behaviours.map((b) => ({ ...b, in: ['FEAT-GO'], pending: false })),
    }
    renderApp(body)

    expect(await screen.findByText('Every goal is built')).toBeInTheDocument()
  })

  it('BEH-NARR-1: shows nothing for a project with no epics', async () => {
    renderApp(snipIt)

    await screen.findByRole('heading', { name: 'snip-it', level: 1 })
    expect(screen.queryByText(/Working towards/)).not.toBeInTheDocument()
    expect(screen.queryByText('Every goal is built')).not.toBeInTheDocument()
  })
})
