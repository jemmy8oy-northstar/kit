import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { STAGES } from '../stages'

// BEH-STAGE-2: the agent puts every feature at a rough stage. It is the agent's
// job, not a screen's, so nothing in the UI would notice a feature it forgot: that
// feature just sits in Unstaged (BEH-STAGE-7) until someone looks. This reads the
// real corpora and holds the agent to it. Only a corpus edit creates a feature
// (Kit has no route that adds one), so a red here is always the agent's own miss.
//
// It checks that a stage is written, not that it is right: "rough" lets a stage lag
// the spec by a wake, and a pending behaviour he adds from his phone must not turn
// the build red.
const HERE = path.dirname(fileURLToPath(import.meta.url))
// src/test -> src -> ui -> behaviour-ast
const CORPORA = path.join(HERE, '..', '..', '..', 'behaviours')

// The same directives the server reads (ProjectViewer.cs, BEH-IDEA-API-1): only the
// directive line counts, never a comment that mentions it.
const NOT_A_PROJECT = /^#\s*kit:(?:idea\s*$|not-a-real-app\b|duplicate-corpus\s+\S)/m

/** Each feature in `source` with the stage written directly under it, or null. */
function featureStages(source: string): { id: string; stage: string | null }[] {
  const out: { id: string; stage: string | null }[] = []
  let open: { id: string; stage: string | null } | null = null
  for (const line of source.split('\n')) {
    const feature = /^feature (\S+)/.exec(line)
    if (feature) { out.push((open = { id: feature[1], stage: null })); continue }
    if (!open) continue
    const stage = /^ {2}stage (\S+)/.exec(line)
    if (stage) open.stage = stage[1]
    else if (/^\S/.test(line)) open = null
  }
  return out
}

const files = fs.readdirSync(CORPORA).filter((f) => f.endsWith('.beh'))
const real = files.filter((f) => !NOT_A_PROJECT.test(fs.readFileSync(path.join(CORPORA, f), 'utf8')))

describe('stage reading', () => {
  it('gives every feature in a real project a stage line (BEH-STAGE-2)', () => {
    const missing = real.flatMap((f) =>
      featureStages(fs.readFileSync(path.join(CORPORA, f), 'utf8'))
        .filter((x) => x.stage === null)
        .map((x) => `${f} ${x.id}`))
    expect(missing).toEqual([])
  })

  it('writes only the stage words the board has columns for (BEH-STAGE-2)', () => {
    const words = new Set(STAGES.map((s) => s.id as string))
    const wrong = real.flatMap((f) =>
      featureStages(fs.readFileSync(path.join(CORPORA, f), 'utf8'))
        .filter((x) => x.stage !== null && !words.has(x.stage))
        .map((x) => `${f} ${x.id}: ${x.stage}`))
    expect(wrong).toEqual([])
  })

  // Pins the seam: a filter that skipped every file, or a reader that found no
  // features, would pass both tests above while checking nothing.
  it('reads the real projects and skips the ideas and trial corpora', () => {
    expect(real).toContain('kit-ui.beh')
    expect(real).toContain('snip-it.beh')
    expect(real).not.toContain('ideas.beh')
    expect(real).not.toContain('trial-lend.beh')
    expect(real).not.toContain('trial-habits-a.beh')
    expect(featureStages(fs.readFileSync(path.join(CORPORA, 'kit-ui.beh'), 'utf8')).length).toBeGreaterThan(10)
  })

  it('reads a stage only from directly under its own feature', () => {
    expect(featureStages('feature A "a"\nfeature B "b"\n  stage ui\n')).toEqual([
      { id: 'A', stage: null },
      { id: 'B', stage: 'ui' },
    ])
    expect(featureStages('feature A "a"\n\nbehaviour X "x"\n  stage ui\n')).toEqual([{ id: 'A', stage: null }])
  })
})
