import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import manifest from './fixtures/manifest.json'
import projects from './fixtures/projects.json'
import snipIt from './fixtures/project-snip-it.json'
import habits from './fixtures/project-james-habits-app.json'

// A UI suite over hand-written fixtures proves the fixtures. These three were
// RECORDED from the real read API, and this file is what stops them drifting
// from it: it requires `ui.js` itself and compares ([[green-suite-over-a-mock]]).
//
// It is the reason the fixtures may be trusted everywhere else in this suite.
//
// If it fails, regenerate and then READ THE DIFF:
//
//     node src/test/fixtures/generate.js
//
// A corpus was added or changed => the new rows are expected. The read API
// changed => fix the components the diff implicates. Do not edit a fixture by
// hand to make a component pass.
//
// ⚠️ The generator is not wired into `npm test` and must not be: one that ran
// before this comparison would rewrite the fixture to match whatever `ui.js`
// now says, and report green over a real regression.
const require = createRequire(import.meta.url)
const ui = require('../../../ui.js') as {
  route: (method: string, pathname: string, opts?: object) => { status: number; body: unknown }
}

const FIXTURE_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures')
const REGENERATE = 'run `node src/test/fixtures/generate.js` from ui/, then read the diff'

// Filename -> the import above. The manifest drives the generator; this object
// is how the same manifest drives the assertions, so neither side can gain a
// fixture the other has never heard of (the seam is pinned below).
const recorded: Record<string, unknown> = {
  'projects.json': projects,
  'project-snip-it.json': snipIt,
  'project-james-habits-app.json': habits,
}

describe('the fixtures are what ui.js actually serves', () => {
  for (const entry of manifest.generated) {
    it(`${entry.route} — ${entry.why}`, () => {
      expect(recorded[entry.file], `${entry.file} is in the manifest but this file never imported it`).toBeDefined()
      expect(ui.route('GET', entry.route, {}).body, REGENERATE).toEqual(recorded[entry.file])
    })
  }

  it('the manifest, the generator and this file describe the same set of recordings', () => {
    // The seam. `generate.js` writes exactly `manifest.generated`; the loop
    // above asserts exactly what `recorded` holds. Without this, adding a
    // fourth recording to the manifest alone would produce a fixture that is
    // regenerated forever and compared to nothing — green, and proving nothing.
    expect(Object.keys(recorded).sort(), REGENERATE).toEqual(manifest.generated.map((e) => e.file).sort())
  })

  it('every fixture in the directory is declared — generated, or hand-written and why', () => {
    // The fixture directory is a population, and a file that is in neither list
    // is a fixture nothing regenerates and nothing pins. A JSON file cannot
    // carry a `# kit:` marker the way a corpus can, so `manifest.json` is where
    // membership is declared — with a reason, never as a bare filename.
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
    // The rule the whole CoverageBadge exists for, asserted at the source as
    // well as at the screen. If ui.js ever starts sending 0 here, the badge
    // would render "0/0 covered" and be telling the truth about a lie.
    for (const p of projects.projects) {
      expect(p.coverage.available).toBe(false)
      expect(p.coverage.covered).toBeNull()
      expect(p.coverage.reason).toBeTruthy()
    }
  })
})
