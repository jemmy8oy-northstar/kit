import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import App from './App'
import { noNotes } from './test/noNotes'
import projectsFixture from './test/fixtures/projects.json'
import snipIt from './test/fixtures/project-snip-it.json'

/**
 * The router's `basename` — kit#49, rule 8.
 *
 * This is the third reader of one value (the other two are vite's `base`, which
 * rewrites the asset URLs, and the API client's prefix). It is tested separately
 * because its failure is the one that looks like nothing is wrong: without a
 * basename every `<Link to="/">` points at the HOST root, and on the shared host
 * Kit is deployed to, the host root is a DIFFERENT APP that answers 200. Pressing
 * "Kit" in the header would quietly leave Kit for the portfolio.
 *
 * No status code anywhere reports that, which is why it is asserted on the `href`
 * the router actually renders rather than on a navigation.
 */

// Answers whatever path the client asks for, so the assertions below are about
// routing and not about the fetch. It also records the paths, which is how the
// prefix reaching the client is observed in the same render as the basename.
function stubServer() {
  const asked: string[] = []
  vi.stubGlobal('fetch', vi.fn((path: string) => {
    asked.push(path)
    const body = path.endsWith('/api/session')
      ? { required: false, signedIn: true }
      : { projects: [] }
    return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) } as Response)
  }))
  return asked
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
  window.history.pushState({}, '', '/')
})

describe('the router under a path prefix', () => {
  it('keeps every link inside Kit when it is served under /kit', async () => {
    // jsdom's location has to be under the prefix too, or react-router correctly
    // refuses to match and renders nothing — which would make this test pass for
    // the wrong reason if it asserted only "no link to /".
    window.history.pushState({}, '', '/kit/')
    vi.stubEnv('BASE_URL', '/kit/')
    const asked = stubServer()

    render(<App />)

    const brand = await screen.findByRole('link', { name: 'Kit' })
    // `/kit` and not `/kit/`: the basename has its trailing slash stripped, for
    // the reason the next test exists. Either spelling navigates, and this is the
    // one the router now emits.
    expect(brand.getAttribute('href')).toBe('/kit')
    // And the same value reached the client in the same render, which is the
    // seam: a basename that worked while the fetches went to the root would be a
    // page that draws and cannot load anything.
    await waitFor(() => expect(asked).toContain('/kit/api/projects'))
  })

  it('🔴 renders at /kit with NO trailing slash — the URL he actually types', async () => {
    // This is the case a real browser caught and the whole server-side suite
    // could not. `BASE_URL` is `/kit/`; react-router matches a basename by
    // `startsWith`, so `/kit/` does not match the location `/kit` and the router
    // renders NOTHING. The symptom was a white page in which the shell, both
    // hashed assets and every API call returned 200 — the server and the bundle
    // were each correct, and only the router had silently declined to match.
    //
    // It is a separate test rather than a tweak to the one above because the two
    // spellings are two different inputs, and the one that was broken is the one
    // a person types ([[feed-it-the-extreme-case]]).
    window.history.pushState({}, '', '/kit')
    vi.stubEnv('BASE_URL', '/kit/')
    stubServer()

    render(<App />)

    // The heading, not the brand link: a router that fails to match still renders
    // the chrome around the <Routes>, so asserting on the header would have
    // passed against the white page.
    expect(await screen.findByRole('heading', { name: 'Projects' })).toBeInTheDocument()
  })

  it('CONTROL: with no prefix the brand link is the root, exactly as it was', async () => {
    // The unset case is the local tool, and it must be untouched. Without this
    // the test above would also pass on a version that hardcoded '/kit/'.
    vi.stubEnv('BASE_URL', '/')
    const asked = stubServer()

    render(<App />)

    const brand = await screen.findByRole('link', { name: 'Kit' })
    expect(brand.getAttribute('href')).toBe('/')
    await waitFor(() => expect(asked).toContain('/api/projects'))
  })
})

describe('BEH-NAV-1: every page carries the navbar', () => {
  // One of each page the card names: the project list, the board, a project, a feature and a behaviour.
  const pages: [string, string][] = [
    ['the project list', '/'],
    ['the board', '/board'],
    ['a project', '/projects/snip-it'],
    ['a feature', '/projects/snip-it/features/FEAT-APP'],
    ['a behaviour', `/projects/snip-it/behaviours/${snipIt.behaviours[0].id}`],
  ]

  it.each(pages)('%s renders the navbar, with the board and the projects in it', async (_name, path) => {
    window.history.pushState({}, '', path)
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        const notes = noNotes(url, init)
        if (notes) return notes
        const body = url.endsWith('/api/session')
          ? { required: false, signedIn: true }
          : url.endsWith('/api/projects')
            ? projectsFixture
            : url.endsWith('/api/projects/snip-it')
              ? snipIt
              : null
        return body
          ? { ok: true, status: 200, statusText: 'OK', json: async () => body }
          : { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-route' }) }
      }),
    )

    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Board' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Projects' })).toBeInTheDocument()
  })
})

/** The server for the navbar and board tests below: the session, the project list, and one project's detail. */
function stubKitServer(detail: object = snipIt) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const notes = noNotes(url, init)
      if (notes) return notes
      const body = url.endsWith('/api/session')
        ? { required: false, signedIn: true }
        : url.endsWith('/api/projects')
          ? projectsFixture
          : url.endsWith('/api/projects/snip-it')
            ? detail
            : url.endsWith('/tickets')
              ? { on: true, tickets: [] }
              : null
      return body
        ? { ok: true, status: 200, statusText: 'OK', json: async () => body }
        : { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-route' }) }
    }),
  )
}

describe('BEH-TICKET-6: the navbar and the boards show only the epic board and the ticket board', () => {
  it('the navbar has one Board link and no feature or spec board in it (BEH-TICKET-6)', async () => {
    window.history.pushState({}, '', '/board')
    stubKitServer()
    render(<App />)
    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Board' })).toHaveAttribute('href', '/board')
    expect(within(nav).queryByRole('link', { name: 'Features' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Stages' })).not.toBeInTheDocument()
  })

  it('the board switch offers the epic board and the ticket board and no feature or spec view (BEH-TICKET-6)', async () => {
    window.history.pushState({}, '', '/board')
    stubKitServer()
    render(<App />)
    const views = await screen.findByRole('navigation', { name: 'Board view' })
    expect(within(views).getAllByRole('link').map((a) => a.textContent)).toEqual(['Epics', 'Tickets'])
    expect(within(views).getByRole('link', { name: 'Tickets' })).toHaveAttribute('aria-current', 'page')
  })

  it('BEH-EPIC-4 is its own page: /epics opens the epic board, with no switch option standing for it (BEH-TICKET-6)', async () => {
    window.history.pushState({}, '', '/epics')
    stubKitServer()
    render(<App />)
    expect(await screen.findByRole('region', { name: 'Epics' })).toBeInTheDocument()
    const views = screen.getByRole('navigation', { name: 'Board view' })
    expect(within(views).getByRole('link', { name: 'Epics' })).toHaveAttribute('aria-current', 'page')
    expect(within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name: 'Board' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('a spec a ticket names still has its page, opened by its id (BEH-TICKET-6)', async () => {
    const spec = snipIt.behaviours[0]
    window.history.pushState({}, '', `/projects/snip-it/behaviours/${spec.id}`)
    stubKitServer()
    render(<App />)
    expect(await screen.findByRole('heading', { level: 1, name: spec.title })).toBeInTheDocument()
  })

  it("a ticket's feature page still opens from its feature branch (BEH-TICKET-6)", async () => {
    window.history.pushState({}, '', '/projects/snip-it/features/FEAT-APP')
    stubKitServer({ ...snipIt, features: [{ id: 'FEAT-APP', title: 'The app feature', in: [], at: 'snip-it.beh:1' }] })
    render(<App />)
    expect(await screen.findByRole('heading', { level: 1, name: 'The app feature' })).toBeInTheDocument()
  })
})

describe('BEH-TICKET-1: the navbar and the board page show no feature board', () => {
  it('the board page keeps its ticket view and the navbar has no Features link (BEH-TICKET-1)', async () => {
    window.history.pushState({}, '', '/board?project=snip-it')
    stubKitServer()
    render(<App />)
    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).queryByRole('link', { name: 'Features' })).not.toBeInTheDocument()
    const views = screen.getByRole('navigation', { name: 'Board view' })
    expect(within(views).queryByRole('link', { name: 'Features' })).not.toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: /^Tickets/ })).toBeInTheDocument()
  })
})

describe('BEH-SPACE-5: the main navigation keeps only the global places above projects', () => {
  // The same stub as BEH-NAV-1: the project list and the session answer, and the flags cannot be read,
  // so every entry is shown. What is asserted is which entries are in the bar, not whether they are on.
  function stubAll() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        const notes = noNotes(url, init)
        if (notes) return notes
        const body = url.endsWith('/api/session')
          ? { required: false, signedIn: true }
          : url.endsWith('/api/projects')
            ? projectsFixture
            : null
        return body
          ? { ok: true, status: 200, statusText: 'OK', json: async () => body }
          : { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-route' }) }
      }),
    )
  }

  it('BEH-SPACE-5 keeps Projects, Notifications and Settings in the main navigation, and not the Inbox or Runs', async () => {
    window.history.pushState({}, '', '/')
    stubAll()
    render(<App />)

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    expect(within(nav).getByRole('link', { name: 'Projects' })).toHaveAttribute('href', '/')
    expect(within(nav).getByRole('link', { name: 'Notifications' })).toHaveAttribute('href', '/notifications')
    expect(within(nav).getByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/settings')
    expect(within(nav).queryByRole('link', { name: 'Inbox' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Runs' })).not.toBeInTheDocument()
  })

  it('BEH-SPACE-5 reaches the all-projects Inbox from the entry page, not from the main navigation', async () => {
    window.history.pushState({}, '', '/')
    stubAll()
    render(<App />)

    expect(await screen.findByRole('link', { name: 'Inbox across every project' })).toHaveAttribute('href', '/inbox')
  })
})
