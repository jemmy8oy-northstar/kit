import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// BEH-AGENTPAGE-1 is deleted: the owner said the agents page adds nothing. These tests keep the
// removal honest: the page, its route and its navbar link are gone, and Runs is untouched.
const HERE = path.dirname(fileURLToPath(import.meta.url))
const UI_SRC = path.join(HERE, '..')
const BEHAVIOURS = path.join(HERE, '..', '..', '..', 'behaviours')

const read = (file: string) => fs.readFileSync(file, 'utf8')

describe('the agents page is removed (BEH-AGENTPAGE-1)', () => {
  it('BEH-AGENTPAGE-1 the page and its test are gone from pages/', () => {
    expect(fs.existsSync(path.join(UI_SRC, 'pages', 'Agents.tsx'))).toBe(false)
    expect(fs.existsSync(path.join(UI_SRC, 'pages', 'Agents.test.tsx'))).toBe(false)
  })

  it('BEH-AGENTPAGE-1 App.tsx has no KitAgents route, no import of pages/Agents, and no agents navbar link', () => {
    const app = read(path.join(UI_SRC, 'App.tsx'))
    expect(app).not.toMatch(/pages\/Agents/)
    expect(app).not.toMatch(/path="\/agents"/)
    expect(app).not.toMatch(/to="\/agents"/)
    expect(app).not.toMatch(/AgentsNavLink/)
  })

  it('BEH-AGENTPAGE-1 kit-ui.tests.json has no mapping for the card, so no mapping names a missing file', () => {
    const mappings = JSON.parse(read(path.join(BEHAVIOURS, 'kit-ui.tests.json'))) as Record<string, unknown>
    expect(Object.keys(mappings)).not.toContain('BEH-AGENTPAGE-1')
  })

  it('BEH-AGENTPAGE-1 kit-ui.beh has no card and no FEAT-AGENTS feature', () => {
    const corpus = read(path.join(BEHAVIOURS, 'kit-ui.beh'))
    expect(corpus).not.toMatch(/^behaviour BEH-AGENTPAGE-1\b/m)
    expect(corpus).not.toMatch(/^feature FEAT-AGENTS\b/m)
  })

  it('BEH-AGENTPAGE-1 the navbar still shows Runs (BEH-USERS-9), and the Runs page still reads GET /api/runs', () => {
    const app = read(path.join(UI_SRC, 'App.tsx'))
    expect(app).toMatch(/<NavLink to="\/runs">Runs<\/NavLink>/)
    expect(app).toMatch(/<RunsNavLink \/>/)
    const runs = read(path.join(UI_SRC, 'pages', 'Runs.tsx'))
    expect(runs).toMatch(/useResource\(fetchRuns/)
    const client = read(path.join(UI_SRC, 'api', 'client.ts'))
    expect(client).toMatch(/\/api\/runs/)
  })
})
