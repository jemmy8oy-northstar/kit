import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import BehaviourPage from './BehaviourPage'
import habits from '../test/fixtures/project-james-habits-app.json'
import snipIt from '../test/fixtures/project-snip-it.json'

function renderAt(body: unknown, app: string, id: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )

  return render(
    <MemoryRouter initialEntries={[`/projects/${app}/behaviours/${id}`]}>
      <Routes>
        <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('BehaviourPage', () => {
  it('shows the behaviour and the test Kit generated from it, together', async () => {
    // His "iterating on the output" needs both halves on one screen; a page
    // that shows the behaviour and links to the code is not the same product.
    renderAt(snipIt, 'snip-it', 'BEH-HOME-1')

    expect(await screen.findByText('The landing page renders')).toBeInTheDocument()
    expect(screen.getByText(/opens page:Home/)).toBeInTheDocument()
    expect(screen.getByText(/page\.goto/)).toBeInTheDocument()
  })

  it('names the unbound nouns that turned steps into comments', async () => {
    // BEH-TODAY-1 generates nothing but comments because three nouns are
    // unbound. "0 generated" alone would look like a Kit failure; the nouns are
    // what tell you it is the app that is missing, not the tool.
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    expect(await screen.findByText(/Unbound nouns:/)).toHaveTextContent('page:Today')
    expect(screen.getByText(/UNGENERATED: when opens page:Today/)).toBeInTheDocument()
  })

  it('distinguishes "no test was generated" from an empty one', async () => {
    const noGenerated = { ...snipIt, generated: [] }
    renderAt(noGenerated, 'snip-it', 'BEH-HOME-1')

    expect(await screen.findByRole('alert')).toHaveTextContent('Kit generated nothing')
  })

  it('says a behaviour id is not in the corpus, rather than rendering a blank page', async () => {
    renderAt(snipIt, 'snip-it', 'BEH-NOPE-9')

    expect(await screen.findByText('No such behaviour')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('BEH-NOPE-9')
  })
})
