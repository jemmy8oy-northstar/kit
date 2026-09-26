import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import App from './App'

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
