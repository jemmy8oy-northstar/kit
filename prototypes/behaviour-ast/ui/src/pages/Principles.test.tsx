import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import Principles from './Principles'

const summary = (app: string, extra: Record<string, unknown> = {}) => ({ app, project: app, behaviours: 0, ...extra })

const views: Record<string, unknown> = {
  kit: {
    app: 'kit',
    principles: [
      { name: 'Readability', text: 'a human can read it', global: true, at: 'kit.beh:24' },
      { name: 'Small diffs', text: 'one slice per PR', global: false, at: 'kit.beh:25' },
    ],
  },
  vocab: { app: 'vocab', principles: [{ name: 'One language', text: 'Mandarin first', global: false, at: 'vocab.beh:3' }] },
  quiet: { app: 'quiet' },
}

function mockApi(projects: unknown[]) {
  const fetch = vi.fn(async (path: string) => {
    const app = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
    return {
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () =>
        path.endsWith('/api/session') ? { required: false, signedIn: true } : app ? views[app] : { projects },
    }
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}

afterEach(() => vi.unstubAllGlobals())

describe('Principles', () => {
  it('lists global principles first, then each project with its own (BEH-PRIN-1)', async () => {
    mockApi([summary('kit'), summary('vocab'), summary('quiet')])
    render(
      <MemoryRouter>
        <Principles />
      </MemoryRouter>,
    )

    const global = await screen.findByRole('region', { name: 'Global' })
    expect(within(global).getByText('Readability')).toBeInTheDocument()
    expect(within(global).queryByText('Small diffs')).not.toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'kit' })).getByText('Small diffs')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'vocab' })).getByText('vocab.beh:3')).toBeInTheDocument()
    // A project with none gets no empty heading.
    expect(screen.queryByRole('region', { name: 'quiet' })).not.toBeInTheDocument()
  })

  it('never reads the Ideas file for principles (BEH-PRIN-1)', async () => {
    const fetch = mockApi([summary('kit'), summary('ideas', { idea: true })])
    render(
      <MemoryRouter>
        <Principles />
      </MemoryRouter>,
    )

    await screen.findByRole('region', { name: 'Global' })
    expect(fetch.mock.calls.map((c) => c[0] as string).some((p) => p.endsWith('/api/projects/ideas'))).toBe(false)
  })

  it('links Principles from every page (BEH-PRIN-1)', async () => {
    window.history.pushState({}, '', '/board')
    mockApi([])
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Principles' })).toHaveAttribute('href', '/principles')
  })
})
