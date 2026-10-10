import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import contract from './fixtures/write-contract.json'

// The half of the write-contract pin that checks the FIXTURE, rather than the client
// against it (that half is client.test.ts). It lived in kit.test.js until the Node
// engine was deleted (kit#119), and moved here unchanged in substance: the fixture is
// the shared artefact both halves read, so its population is itself worth asserting —
// a loop over an empty list passes, silently and forever.

type Request = {
  what: string
  call: { fn: string; args: string[] }
  path: string
  targetFile?: string
  expect: { status: number; sharedWith?: string[] }
}
type Read = { path: string; expect: { status: number } }
const requests = contract.requests as Request[]
const reads = (contract as { reads?: Read[] }).reads ?? []

describe('the write contract fixture', () => {
  it('is not empty, and covers every write route', () => {
    expect(requests.length).toBeGreaterThanOrEqual(4)
    const paths = requests.map((r) => r.path)
    for (const route of [/\/steps$/, /\/review$/, /\/behaviours$/, /\/bindings$/, /%20/]) {
      expect(paths.some((p) => route.test(p)), `nothing in the contract matches ${route}`).toBe(true)
    }
    expect(requests.some((r) => r.expect.status === 409), 'the contract only covers the happy path').toBe(true)

    // The bind route writes a DIFFERENT file, and the app in its name must be the app
    // in the PATH — kit#66 in one assertion. A target naming another corpus would be the
    // old flat map wearing a new name.
    const binds = requests.filter((r) => /\/bindings$/.test(r.path))
    expect(binds.length, 'the contract binds nothing').toBeGreaterThanOrEqual(1)
    for (const r of binds) {
      const app = decodeURIComponent(/^\/api\/projects\/([^/]+)\/bindings$/.exec(r.path)![1])
      expect(r.targetFile, `${r.path} must write the bindings of the app it is routed under`).toBe(`${app}.bindings.json`)
    }
    // Both directions, or the empty sharing report is never told apart from an unimplemented one.
    expect(binds.some((r) => (r.expect.sharedWith ?? []).length > 0), 'nothing binds a shared noun').toBe(true)
    expect(binds.some((r) => r.expect.sharedWith?.length === 0), 'nothing binds an unshared noun').toBe(true)
  })

  it('pins the READ path too, not only the writes', () => {
    // `mutate-ui.js` dropped encodeURIComponent from fetchProject and the whole suite
    // stayed green: the one GET the client builds was pinned by nobody.
    expect(reads.length, 'the contract declares no reads').toBeGreaterThanOrEqual(2)
    expect(reads.some((r) => /%20/.test(r.path)), 'no read exercises a percent-encoded name').toBe(true)
    expect(reads.some((r) => r.expect.status === 404), 'the reads only cover the happy path').toBe(true)
  })

  it('names the paths the CLIENT builds, character for character', () => {
    // Re-derived from the same arguments, so a hand-edited fixture path cannot make
    // both halves agree on something the client would never send — the assertion that
    // catches a red test being "fixed" by editing the pin.
    const built: Record<string, (app: string, id?: string) => string> = {
      addStep: (app, id) => `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id!)}/steps`,
      setReview: (app, id) => `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id!)}/review`,
      addBehaviour: (app) => `/api/projects/${encodeURIComponent(app)}/behaviours`,
      addBinding: (app) => `/api/projects/${encodeURIComponent(app)}/bindings`,
    }
    for (const req of requests) {
      const [app, id] = req.call.args
      // Looked up, never defaulted: an unknown fn must not fall through to another spelling.
      const build = built[req.call.fn]
      expect(build, `the contract calls ${req.call.fn}, which this test cannot build a path for`).toBeDefined()
      expect(req.path, `${req.what}: the fixture path is not what the client builds`).toBe(build(app, id))
    }
  })
})

// src/test -> src -> ui -> behaviour-ast -> prototypes -> repo root
const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '..')

describe('the header', () => {
  it('does not say Kit never commits, while the server can commit', () => {
    // "writes the corpus file — never commits" was true until hosted write-back, after
    // which a deployed Kit rendered "Committed as … and pushed to kit/hosted" under it.
    // The header is the same in every Kit, so while the server can run `git commit` it
    // may not deny committing.
    const store = fs.readFileSync(path.join(ROOT, 'backend', 'Balenthiran.Kit.Database', 'GitStore.cs'), 'utf8')
    if (!store.includes('"commit", "-m"')) return // the claim would be true; nothing to enforce
    const header = fs.readFileSync(path.join(ROOT, 'prototypes', 'behaviour-ast', 'ui', 'src', 'App.tsx'), 'utf8')
      .replace(/\{\/\*[\s\S]*?\*\/\}/g, ' ')
    expect(/never commits/i.test(header), 'App.tsx says Kit never commits, but GitStore.cs commits every write').toBe(false)
  })
})
