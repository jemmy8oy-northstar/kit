// @vitest-environment node
// (jsdom replaces TextEncoder, and importing vite.config loads esbuild, which refuses to run under it.)
import { describe, expect, it } from 'vitest'
import { normaliseBasePath } from '../../vite.config'

// The build half of kit#49's rule 8. The server half is `KitHost.NormaliseBasePath`
// in C#, pinned by `NormaliseBasePath_matches_ui_js` over THIS SAME TABLE: if you
// add a row here, add it there. The two must agree or a Kit served under /kit builds
// asset URLs the server does not recognise, and the deployment serves a blank page.
describe('normaliseBasePath', () => {
  it.each([
    [undefined, ''],
    [null, ''],
    ['', ''],
    ['/', ''],
    ['kit', '/kit'],
    ['/kit/', '/kit'],
    ['//kit//', '/kit'],
    [' kit ', '/kit'],
    ['﻿kit', '/kit'],
    ['/a/b/', '/a/b'],
  ])('%j is %j', (value, expected) => {
    expect(normaliseBasePath(value)).toBe(expected)
  })
})
