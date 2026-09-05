import { createRequire } from 'node:module'
import { describe, expect, it } from 'vitest'
import projects from './fixtures/projects.json'
import snipIt from './fixtures/project-snip-it.json'
import habits from './fixtures/project-james-habits-app.json'

// A UI suite over hand-written fixtures proves the fixtures. These three were
// written by running the real read API, and this file is what stops them
// drifting from it: it requires `ui.js` itself and compares
// ([[green-suite-over-a-mock]]).
//
// It is the reason the fixtures may be trusted everywhere else in this suite.
// If it fails, the API changed — regenerate the fixtures, look at the diff, and
// fix the components the diff implicates. Do not edit the fixtures to match a
// component.
const require = createRequire(import.meta.url)
const ui = require('../../../ui.js') as {
  route: (method: string, pathname: string, opts?: object) => { status: number; body: unknown }
}

describe('the fixtures are what ui.js actually serves', () => {
  it('GET /api/projects', () => {
    expect(ui.route('GET', '/api/projects', {}).body).toEqual(projects)
  })

  it('GET /api/projects/snip-it', () => {
    expect(ui.route('GET', '/api/projects/snip-it', {}).body).toEqual(snipIt)
  })

  it('GET /api/projects/james-habits-app', () => {
    expect(ui.route('GET', '/api/projects/james-habits-app', {}).body).toEqual(habits)
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
