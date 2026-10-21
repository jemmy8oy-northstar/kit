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
})
