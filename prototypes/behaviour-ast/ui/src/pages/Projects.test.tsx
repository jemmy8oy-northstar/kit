import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Projects from './Projects'
import { SessionContext } from '../components/session'
import type { SessionState } from '../api/types'
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

const user = { type: async (el: HTMLElement, text: string) => fireEvent.change(el, { target: { value: text } }) }

function renderProjects(session: SessionState | null = null) {
  return render(
    <SessionContext.Provider value={session}>
      <MemoryRouter>
        <Projects />
      </MemoryRouter>
    </SessionContext.Provider>,
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

  it('leaves the Ideas file off the project list: ideas are not projects yet (BEH-IDEA-1)', async () => {
    mockFetch({ projects: [...projectsFixture.projects, { ...projectsFixture.projects[0], app: 'ideas', project: 'ideas', idea: true }] })
    renderProjects()

    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'ideas' })).not.toBeInTheDocument()
  })

  it('marks a trial corpus, so an invented app is not shown as a project', async () => {
    // Every notReal corpus must appear in the list (hiding one would make the
    // list lie about what Kit reads) AND be distinguishable, because 0% against
    // a real app and 0% against an app that does not exist mean opposite things
    // (claude-code-bot#92).
    //
    // ⚠️ This asserted `toHaveLength(1)` until `longlist.beh` became the second
    // trial on 2026-09-24. The count was never the claim — it is a property of
    // the corpus DIRECTORY, so pinning it made adding a trial corpus look like a
    // UI regression. Asserting one badge PER trial keeps it red-capable (drop
    // the badge and the two numbers diverge) without coupling it to how many
    // trials happen to exist. The `>= 1` is what stops it passing vacuously if
    // the fixture ever carries none.
    mockFetch(projectsFixture)
    renderProjects()

    const trials = projectsFixture.projects.filter((p) => p.notReal)
    expect(trials.length).toBeGreaterThanOrEqual(1)
    for (const trial of trials) {
      expect(await screen.findByRole('link', { name: trial.app })).toBeInTheDocument()
    }
    expect(screen.getAllByText(/trial — no app/)).toHaveLength(trials.length)
  })

  it('marks a duplicate corpus with the app it specifies, not just "trial"', async () => {
    // The third axis (claude-code-bot#92). These corpora describe an app that
    // DOES exist, so `notReal` is false of them and the badge above cannot
    // carry them — without their own marker the list would show a spec written
    // forwards and the project it was written against as two equal projects.
    // The badge names the subject because "which one is the real project" is
    // the only question a reader has on seeing two rows for one app.
    mockFetch(projectsFixture)
    renderProjects()

    const dups = projectsFixture.projects.filter((p) => p.duplicateOf)
    expect(dups.length).toBeGreaterThan(0)
    for (const d of dups) {
      expect(await screen.findByRole('link', { name: d.app })).toBeInTheDocument()
      // The subject must be present in the row, or the marker is a bare flag.
      expect(d.duplicateOf).toBeTruthy()
    }
    expect(screen.getAllByText(/trial — spec for james-habits-app/)).toHaveLength(dups.length)
  })

  it('CONTROL: a real corpus carries no duplicate marker', async () => {
    mockFetch({ projects: projectsFixture.projects.filter((p) => !p.duplicateOf) })
    renderProjects()

    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    expect(screen.queryByText(/trial — spec for/)).not.toBeInTheDocument()
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

  it('BEH-USERS-2: lists the repos in a user’s own GitHub, each linking to the repo', async () => {
    mockFetch({
      projects: [],
      gitHub: [{ name: 'snip-it', fullName: 'ann-gh/snip-it', url: 'https://github.com/ann-gh/snip-it', corpus: ['behaviours/snip-it.beh'] }],
    })
    renderProjects()

    const link = await screen.findByRole('link', { name: 'ann-gh/snip-it' })
    expect(link).toHaveAttribute('href', 'https://github.com/ann-gh/snip-it')
    expect(screen.getByText('behaviours/snip-it.beh')).toBeInTheDocument()
  })

  it('BEH-USERS-2: says so when GitHub could not be read, and does not show an empty list', async () => {
    mockFetch({ projects: [], gitHubError: 'GitHub answered 503 when listing your repositories' })
    renderProjects()

    expect(await screen.findByRole('alert')).toHaveTextContent('GitHub answered 503 when listing your repositories')
    expect(screen.queryByText(/none of the repos/)).not.toBeInTheDocument()
  })

  it('BEH-USERS-2: a connected GitHub with no corpus says so, and a list without the field shows no section', async () => {
    mockFetch({ projects: [], gitHub: [] })
    const { unmount } = renderProjects()
    expect(await screen.findByText(/none of the repos it can see carries a/)).toBeInTheDocument()
    unmount()

    mockFetch(projectsFixture)
    renderProjects()
    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    expect(screen.queryByText('From your GitHub')).not.toBeInTheDocument()
    expect(screen.queryByText(/none of the repos/)).not.toBeInTheDocument()
  })

  it('BEH-USERS-3: the links carry the normalised name and the account, and are shown for an empty list', async () => {
    mockFetch({ projects: [], gitHub: [] })
    renderProjects({ gitHub: 'ann-gh' } as SessionState)

    await user.type(await screen.findByLabelText('Project name'), '  my app!  ')
    const create = screen.getByRole('link', { name: 'Create the repo on GitHub' })
    expect(create).toHaveAttribute('href', 'https://github.com/new?name=my-app&description=A%20Kit%20project')
    expect(create).toHaveAttribute('target', '_blank')
    expect(create).toHaveAttribute('rel', 'noreferrer')
    const starter =
      '# my-app, described in Kit.\n#\n# Kit lists this repo once this file is on its default branch. Replace the behaviour below with your own.\n\n' +
      'behaviour BEH-START-1 "Someone opens my-app"\n  pending\n  actor user\n  when opens page:Home\n  then sees region:Main\n'
    expect(screen.getByRole('link', { name: 'Add its first behaviour file' })).toHaveAttribute(
      'href',
      `https://github.com/ann-gh/my-app/new/main?filename=${encodeURIComponent('behaviours/my-app.beh')}&value=${encodeURIComponent(starter)}`,
    )
  })

  it('BEH-USERS-3: the links are absent while the name is empty', async () => {
    mockFetch({ projects: [], gitHub: [] })
    renderProjects({ gitHub: 'ann-gh' } as SessionState)

    await screen.findByLabelText('Project name')
    expect(screen.queryByRole('link', { name: 'Create the repo on GitHub' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Add its first behaviour file' })).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Project name'), '!!')
    expect(screen.queryByRole('link', { name: 'Create the repo on GitHub' })).not.toBeInTheDocument()
  })

  it('BEH-USERS-3: without a connected account only the first step shows', async () => {
    mockFetch({ projects: [], gitHub: [] })
    renderProjects(null)

    await user.type(await screen.findByLabelText('Project name'), 'x')
    expect(screen.getByRole('link', { name: 'Create the repo on GitHub' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Add its first behaviour file' })).not.toBeInTheDocument()
  })

  it('BEH-USERS-3: the form shows beside a non-empty list, and not for the operator or a GitHub that could not be read', async () => {
    mockFetch({
      projects: [],
      gitHub: [{ name: 'snip-it', fullName: 'ann-gh/snip-it', url: 'https://github.com/ann-gh/snip-it', corpus: ['behaviours/snip-it.beh'] }],
    })
    const first = renderProjects({ gitHub: 'ann-gh' } as SessionState)
    expect(await screen.findByLabelText('Project name')).toBeInTheDocument()
    first.unmount()

    mockFetch(projectsFixture)
    const operator = renderProjects()
    expect(await screen.findByRole('link', { name: 'snip-it' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Project name')).not.toBeInTheDocument()
    operator.unmount()

    mockFetch({ projects: [], gitHubError: 'GitHub answered 503' })
    renderProjects()
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByLabelText('Project name')).not.toBeInTheDocument()
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

    expect(await screen.findByRole('alert')).toHaveTextContent('dotnet run --project backend/Balenthiran.Kit.WebApi')
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
