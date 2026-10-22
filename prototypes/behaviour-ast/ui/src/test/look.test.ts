import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// BEH-LOOK-2: the theme is one attribute on <html>, and jsdom loads no CSS, so
// the attribute is the whole of what a test can hold. Without it every --color-*
// role is unset and Kit renders as unstyled HTML.
const here = path.dirname(fileURLToPath(import.meta.url))
const html = fs.readFileSync(path.join(here, '../../index.html'), 'utf8')

describe('the look', () => {
  it('wears the design system professional theme (BEH-LOOK-2)', () => {
    expect(html).toMatch(/<html[^>]*\sdata-theme="professional"/)
  })

  // BEH-LOOK-1: "every gap between blocks comes from one spacing scale, so a page has no one-off
  // margins". Read as text, since jsdom applies none of it: every margin, padding and gap value is
  // a --space-* step, or 0 / auto. The one exception is a tab's -1px margin-bottom, which pulls
  // its underline onto the tablist's border. That is a border overlap, not a gap.
  it('takes every margin, padding and gap from one spacing scale (BEH-LOOK-1)', () => {
    const css = fs.readFileSync(path.join(here, '../index.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')
    const decls = [...css.matchAll(/(?:^|[;{\s])((?:margin|padding|gap|row-gap|column-gap)(?:-[a-z]+)?)\s*:\s*([^;}]+)/g)]
    expect(decls.length).toBeGreaterThan(20)
    const offScale = decls
      .map(([, prop, value]) => ({ prop, value: value.trim() }))
      .filter(({ prop, value }) => !(prop === 'margin-bottom' && value === '-1px'))
      .filter(({ value }) => value.split(/\s+/).some((v) => !/^(0|auto|var\(--space-\d+\))$/.test(v)))
    expect(offScale).toEqual([])
  })
})
