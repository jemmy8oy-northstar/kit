import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Projects from './Projects'
import projectsFixture from '../test/fixtures/projects.json'

function mockFetch(body: unknown, init: { ok?: boolean; status?: number } = {}) {
  const ok = init.ok ?? true
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({
      ok,
      status: init.status ?? (ok ? 200 : 500),
      statusText: ok ? 'OK' : 'Server Error',
      json: async () => body,
    })),
  )
}

function renderProjects() {
  return render(
    <MemoryRouter>
      <Projects />
    </MemoryRouter>,
  )
}

describe('Projects', () => {
  it('lists every corpus the read API returned', async () => {
    mockFetch(projectsFixture)
    renderProjects()

    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    for (const p of projectsFixture.projects) {
      expect(screen.getByRole('link', { name: p.app })).toBeInTheDocument()
    }
  })

  it('marks a trial corpus, so an invented app is not shown as a project', async () => {
    // The fixture carries exactly one notReal corpus. It must appear in the
    // list (hiding it would make the list lie about what Kit reads) AND be
    // distinguishable, because 0% against a real app and 0% against an app that
    // does not exist mean opposite things (claude-code-bot#92).
    mockFetch(projectsFixture)
    renderProjects()

    const trials = projectsFixture.projects.filter((p) => p.notReal)
    expect(trials).toHaveLength(1)
    expect(await screen.findByRole('link', { name: trials[0].app })).toBeInTheDocument()
    expect(screen.getAllByText(/trial — no app/)).toHaveLength(1)
  })

  it('CONTROL: a real corpus carries no trial marker', async () => {
    // Otherwise the badge could be rendering on every row and the test above
    // would still pass on the count alone.
    mockFetch({ projects: projectsFixture.projects.filter((p) => !p.notReal) })
    renderProjects()

    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    expect(screen.queryByText(/trial — no app/)).not.toBeInTheDocument()
  })

  it('renders unavailable coverage as "not measured" and never as a zero', async () => {
    // The load-bearing assertion of this whole page. Every fixture project has
    // coverage.available === false, which is what the real API sends when it
    // was given no --repo. Rendering that as 0 would report five untested apps.
    mockFetch(projectsFixture)
    renderProjects()

    const badges = await screen.findAllByText('not measured')
    expect(badges).toHaveLength(projectsFixture.projects.length)
    expect(screen.queryByText(/0\/0 covered/)).not.toBeInTheDocument()
    expect(screen.queryByText(/0 covered/)).not.toBeInTheDocument()
  })

  it('carries the reason coverage was unavailable, rather than swallowing it', async () => {
    mockFetch(projectsFixture)
    renderProjects()

    const badge = (await screen.findAllByText('not measured'))[0]
    expect(badge).toHaveAttribute('title', projectsFixture.projects[0].coverage.reason)
  })

  it('pluralises its counts — "1 conflicts" reads like a bug in the tool', async () => {
    mockFetch(projectsFixture)
    renderProjects()

    // james-habits-app has exactly one conflict in the real corpus.
    expect(await screen.findByText('1 conflict')).toBeInTheDocument()
    expect(screen.queryByText('1 conflicts')).not.toBeInTheDocument()
    expect(screen.getByText('23 behaviours')).toBeInTheDocument()
  })

  it('reports a corpus that will not parse as an error, not as zero behaviours', async () => {
    mockFetch({
      projects: [{ app: 'broken', error: 'broken.beh:3 — expected a verb' }],
    })
    renderProjects()

    expect(await screen.findByText('corpus will not parse')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('expected a verb')
    // It must NOT look like a project you can open and read.
    expect(screen.queryByRole('link', { name: 'broken' })).not.toBeInTheDocument()
  })

  it('shows a reachable-server message when the read API is not running', async () => {
    // The commonest failure for a local tool, and the one an empty list would
    // disguise as "Kit knows about no projects".
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed')
      }),
    )
    renderProjects()

    expect(await screen.findByRole('alert')).toHaveTextContent('node ui.js')
  })

  it('shows the server’s own reason on a non-OK response', async () => {
    mockFetch({ error: 'no-such-route', reason: 'nothing is served at /api/projects' }, {
      ok: false,
      status: 404,
    })
    renderProjects()

    expect(await screen.findByRole('alert')).toHaveTextContent('nothing is served at /api/projects')
  })
})
