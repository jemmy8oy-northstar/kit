import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// jsdom lays nothing out, so like look.test.ts these read the stylesheet as text and hold the
// rule, not the pixels. The pixels were measured in a real browser when the rules landed
// (2026-10-25): the phone header went from 119px to 93px, and desktop board columns from 144px
// to 240px at 1440px wide.
const here = path.dirname(fileURLToPath(import.meta.url))
const css = fs.readFileSync(path.join(here, '../index.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')

/** The body of the first `selector { ... }` inside the block that starts at `from`. */
function rule(source: string, selector: string, from = 0): string {
  const at = source.indexOf(`${selector} {`, from)
  expect(at, `no rule for ${selector}`).toBeGreaterThanOrEqual(0)
  return source.slice(at, source.indexOf('}', at))
}

describe('layout', () => {
  it('puts the links on their own scrolling line on a phone, and hides the tagline (BEH-NAV-2)', () => {
    const phone = css.indexOf('@media (max-width: 40rem)')
    expect(phone).toBeGreaterThanOrEqual(0)
    expect(rule(css, '.app > header', phone)).toMatch(/flex-wrap:\s*wrap/)
    expect(rule(css, '.app > header > .muted', phone)).toMatch(/display:\s*none/)
    const nav = rule(css, '.app > header > .nav', phone)
    expect(nav).toMatch(/flex:\s*1 0 100%/)
    expect(nav).toMatch(/flex-wrap:\s*nowrap/)
    expect(nav).toMatch(/overflow-x:\s*auto/)
  })

  it('gives the board the whole window, and no column under 15rem on a wide screen (BEH-BOARD-19)', () => {
    expect(rule(css, '.app:has(.board)')).toMatch(/max-width:\s*none/)
    const wide = css.indexOf('@media (min-width: 60rem)', css.indexOf('.board {'))
    expect(rule(css, '.board', wide)).toMatch(/grid-auto-columns:\s*minmax\(15rem,\s*1fr\)/)
    expect(rule(css, '.board')).toMatch(/overflow-x:\s*auto/)
  })
})
