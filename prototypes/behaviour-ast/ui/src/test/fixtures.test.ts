import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import manifest from './fixtures/manifest.json'
import projects from './fixtures/projects.json'
import snipIt from './fixtures/project-snip-it.json'
import habits from './fixtures/project-james-habits-app.json'

// A UI suite over hand-written fixtures proves the fixtures. These three were
// RECORDED from the real read API, and this file is what stops them drifting from
// it: it compares them with the server's recorded answers ([[green-suite-over-a-mock]]).
//
// 🔑 "Real server output" is now the CONFORMANCE GOLDEN, not a live server. The Node
// server these were first compared against (ui.js) is deleted (kit#153); the C# server
// is scored on `conformance/routes/read.json` request by request
// (RoutesConformanceTests.cs), so a fixture equal to that golden is a fixture equal to
// what the server that ships answers. The fixtures are NOT compared with themselves.
//
// That golden is a frozen spec and nothing can re-record it from Node any more. When a
// corpus is added or changed the C# conformance tests go red first; the golden, the
// three fixtures below and the C# expectations are then edited together, in one PR
// that says why. Do not edit a fixture by hand to make a component pass.
const HERE = path.dirname(fileURLToPath(import.meta.url))
const FIXTURE_DIR = path.join(HERE, 'fixtures')
// src/test -> src -> ui -> behaviour-ast
const GOLDEN = path.join(HERE, '..', '..', '..', 'conformance', 'routes', 'read.json')

type Recorded = { method: string; path: string; dist?: boolean; response: { status: number; body: unknown } }
const golden = JSON.parse(fs.readFileSync(GOLDEN, 'utf8')) as { requests: Recorded[] }

// Filename -> the import above. The manifest names the routes; this object is how the
// same manifest drives the assertions, so neither side can gain a fixture the other has
// never heard of (the seam is pinned below).
const recorded: Record<string, unknown> = {
  'projects.json': projects,
  'project-snip-it.json': snipIt,
  'project-james-habits-app.json': habits,
}

describe('the fixtures are what the server answers', () => {
  it('the golden is there and is not empty', () => {
    // A comparison over an empty golden would compare every fixture with `undefined`
    // and fail for the wrong reason; an empty `requests` is "could not look".
    expect(golden.requests.length).toBeGreaterThan(20)
  })

  for (const entry of manifest.generated) {
    it(`${entry.route} — ${entry.why}`, () => {
      expect(recorded[entry.file], `${entry.file} is in the manifest but this file never imported it`).toBeDefined()
      const served = golden.requests.find((q) => q.method === 'GET' && q.path === entry.route && !q.dist)
      expect(served, `the golden has no GET ${entry.route}`).toBeDefined()
      expect(served!.response.status).toBe(200)
      expect(served!.response.body, `${entry.file} no longer matches what the server answers`).toEqual(recorded[entry.file])
    })
  }

  it('the manifest and this file describe the same set of recordings', () => {
    // The seam. The manifest lists the recordings; the loop above asserts what
    // `recorded` holds. Without this, adding a fourth recording to the manifest alone
    // would produce a fixture that is compared to nothing — green, and proving nothing.
    expect(Object.keys(recorded).sort()).toEqual(manifest.generated.map((e) => e.file).sort())
  })

  it('every fixture in the directory is declared — recorded, or hand-written and why', () => {
    // The fixture directory is a population, and a file that is in neither list is a
    // fixture nothing compares and nothing pins. A JSON file cannot carry a `# kit:`
    // marker the way a corpus can, so `manifest.json` is where membership is declared
    // — with a reason, never as a bare filename.
    const onDisk = fs
      .readdirSync(FIXTURE_DIR)
      .filter((f) => f.endsWith('.json') && f !== 'manifest.json')
      .sort()
    const declared = [
      ...manifest.generated.map((e) => e.file),
      ...manifest.handWritten.map((e) => e.file),
    ].sort()
    expect(onDisk, 'add it to manifest.json as generated, or as handWritten with the reason').toEqual(declared)
    for (const e of [...manifest.generated, ...manifest.handWritten]) {
      expect(e.why, `${e.file} is declared with no reason`).toBeTruthy()
    }
  })

  it('serves coverage as unavailable-with-a-reason, never as zero', () => {
    // The rule the whole CoverageBadge exists for, asserted at the source as well as
    // at the screen. If the server ever sends 0 here, the badge would render "0/0
    // covered" and be telling the truth about a lie.
    for (const p of projects.projects) {
      expect(p.coverage.available).toBe(false)
      expect(p.coverage.covered).toBeNull()
      expect(p.coverage.reason).toBeTruthy()
    }
  })
})
