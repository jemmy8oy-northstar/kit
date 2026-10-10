import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// kit-hosted.beh BEH-PULL-4, his kit#88: "the frontend has the UI, keep it light let
// the backend do the heavy lifting". The generated test a screen shows is computed by
// the server and arrives as data; the browser never carries the engine. That is true
// today only because nobody has imported `kit.js` from a component yet — nothing held
// it. This file does: every import in code the bundle ships must resolve inside `ui/`.
//
// Test files are exempt on purpose: they read the conformance goldens from
// `../conformance`, which is the server's recorded answer, not the engine.
const HERE = path.dirname(fileURLToPath(import.meta.url))
// src/test -> src -> ui
const UI = path.resolve(HERE, '..', '..')
const SRC = path.join(UI, 'src')

const SPECIFIER = /(?:\bfrom\s*|\bimport\s*\(?\s*)['"]([^'"]+)['"]/g

/** Relative imports in `source` that resolve outside `ui/`. Bare specifiers are npm packages. */
function escapes(file: string, source: string): string[] {
  return [...source.matchAll(SPECIFIER)]
    .map((m) => m[1])
    .filter((s) => s.startsWith('.'))
    .filter((s) => {
      const target = path.resolve(path.dirname(file), s)
      return path.relative(UI, target).startsWith('..')
    })
}

function shipped(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name)
    if (e.isDirectory()) return e.name === 'test' ? [] : shipped(p)
    return /\.tsx?$/.test(e.name) && !/\.test\.tsx?$/.test(e.name) ? [p] : []
  })
}

describe('the browser never carries the engine (BEH-PULL-4)', () => {
  it('CONTROL: the scan finds the files and imports it claims to check', () => {
    // An empty file list, or a regex that matched nothing, would pass the test below vacuously.
    const files = shipped(SRC)
    expect(files).toContain(path.join(SRC, 'App.tsx'))
    expect(files.some((f) => f.endsWith('.test.tsx'))).toBe(false)
    const app = fs.readFileSync(path.join(SRC, 'App.tsx'), 'utf8')
    expect([...app.matchAll(SPECIFIER)].length).toBeGreaterThan(0)
  })

  it('CONTROL: an import of the engine from a component is caught', () => {
    const file = path.join(SRC, 'pages', 'Project.tsx')
    expect(escapes(file, `import { parse } from '../../../kit.js'`)).toEqual(['../../../kit.js'])
    expect(escapes(file, `const k = await import("../../../writer.js")`)).toEqual(['../../../writer.js'])
    expect(escapes(file, `import { api } from '../api/client'`)).toEqual([])
  })

  it('no shipped UI file imports from outside ui/', () => {
    const found = shipped(SRC).flatMap((f) => escapes(f, fs.readFileSync(f, 'utf8')).map((s) => `${path.relative(UI, f)}: ${s}`))
    expect(found).toEqual([])
  })
})
