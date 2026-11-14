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

  // BEH-EPIC-3: an epic tag is coloured in the secondary role, which no Badge tone and no project
  // colour uses. Read as text, since jsdom applies no CSS: the rule may paint only with --color-secondary.
  it('draws an epic tag only in the secondary role, apart from badge tones and project hues (BEH-EPIC-3)', () => {
    const css = fs.readFileSync(path.join(here, '../index.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')
    const rule = /(?:^|\})\s*\.epic-tag\s*\{([^}]*)\}/.exec(css)
    expect(rule, 'a .epic-tag rule exists').not.toBeNull()
    const paints = [...rule![1].matchAll(/(?:^|[\s;])(color|background|border)\s*:\s*([^;]+)/g)].map(([, prop, value]) => ({ prop, value: value.trim() }))
    expect(paints.map((p) => p.prop)).toEqual(expect.arrayContaining(['color', 'background', 'border']))
    for (const { value } of paints) {
      const roles = [...value.matchAll(/var\(--(color-[a-z-]+)\)/g)].map((m) => m[1])
      expect(roles.length, `colour in ${value}`).toBeGreaterThan(0)
      expect(roles.every((r) => r.startsWith('color-secondary')), `${value} must use only the secondary role`).toBe(true)
    }
  })
})
