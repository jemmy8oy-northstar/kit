import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { STAGES } from '../stages'

// FEAT-STAGE-READING (was BEH-STAGE-2): the agent puts every feature at a rough stage. It is the agent's
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

/** Each behaviour in `source`: the features it is in, and whether it is built (no `pending` line). */
function behaviourBuilt(source: string): { in: string[]; built: boolean }[] {
  const out: { in: string[]; built: boolean }[] = []
  let open: { in: string[]; built: boolean } | null = null
  for (const line of source.split('\n')) {
    if (/^behaviour \S+/.test(line)) { out.push((open = { in: [], built: true })); continue }
    if (!open) continue
    const tag = /^ {2}in (\S+)/.exec(line)
    if (tag) open.in.push(tag[1])
    else if (/^ {2}pending\s*$/.test(line)) open.built = false
    else if (/^\S/.test(line)) open = null
  }
  return out
}

/** Each feature's `in` lines (its parent features and its epics), and each epic with its title. */
function tags(source: string): { features: Map<string, string[]>; epics: { id: string; title: string }[] } {
  const features = new Map<string, string[]>()
  const epics: { id: string; title: string }[] = []
  let open: string[] | null = null
  for (const line of source.split('\n')) {
    const feature = /^feature (\S+)/.exec(line)
    if (feature) { features.set(feature[1], (open = [])); continue }
    const epic = /^epic (\S+) "(.*)"/.exec(line)
    if (epic) { epics.push({ id: epic[1], title: epic[2] }); open = null; continue }
    if (!open) continue
    const tag = /^ {2}in (\S+)/.exec(line)
    if (tag) open.push(tag[1])
    else if (/^\S/.test(line)) open = null
  }
  return { features, epics }
}

const files = fs.readdirSync(CORPORA).filter((f) => f.endsWith('.beh'))
const real = files.filter((f) => !NOT_A_PROJECT.test(fs.readFileSync(path.join(CORPORA, f), 'utf8')))
const read = (f: string) => fs.readFileSync(path.join(CORPORA, f), 'utf8')

describe('stage reading', () => {
  // BEH-STAGE-10: once anything in a feature is built, its stage is development or ui.
  // "Rough" lets a stage lag a wake, never a build: a built behaviour is the strongest
  // reading the agent has, so a feature that has one and still says problem is a miss.
  it('puts a feature with a built behaviour at development or ui (BEH-STAGE-10)', () => {
    const early = real.flatMap((f) => {
      const source = fs.readFileSync(path.join(CORPORA, f), 'utf8')
      const built = new Set(behaviourBuilt(source).filter((b) => b.built).flatMap((b) => b.in))
      return featureStages(source)
        .filter((x) => built.has(x.id) && x.stage !== 'development' && x.stage !== 'ui')
        .map((x) => `${f} ${x.id}: ${x.stage}`)
    })
    expect(early).toEqual([])
  })

  // BEH-STAGE-11: the other side of STAGE-10. A feature with nothing built cannot be
  // at development or ui, because those stages say something in it was built.
  it('puts a feature with nothing built at problem, ux or logic (BEH-STAGE-11)', () => {
    const late = real.flatMap((f) => {
      const source = read(f)
      const behaviours = behaviourBuilt(source)
      const { features } = tags(source)
      // A parent feature is built through its child features, so count those too.
      const anyBuilt = (id: string, seen = new Set<string>()): boolean => {
        if (seen.has(id)) return false
        seen.add(id)
        if (behaviours.some((b) => b.built && b.in.includes(id))) return true
        return [...features].some(([child, parents]) => parents.includes(id) && anyBuilt(child, seen))
      }
      return featureStages(source)
        .filter((x) => (x.stage === 'development' || x.stage === 'ui') && !anyBuilt(x.id))
        .map((x) => `${f} ${x.id}: ${x.stage}`)
    })
    expect(late).toEqual([])
  })

  // BEH-STAGE-5: a feature the agent makes from a note carries that note's words as
  // behaviours from the start, so no feature is an empty heading on the board.
  it('gives every feature at least one behaviour or child feature (BEH-STAGE-5)', () => {
    const empty = real.flatMap((f) => {
      const source = read(f)
      const { features } = tags(source)
      const used = new Set([
        ...behaviourBuilt(source).flatMap((b) => b.in),
        ...[...features.values()].flat(),
      ])
      return [...features.keys()].filter((id) => !used.has(id)).map((id) => `${f} ${id}`)
    })
    expect(empty).toEqual([])
  })

  // BEH-EPIC-5: a drafted epic's children start pending, so nothing under an epic is
  // built while its feature is still a problem statement.
  it('holds only pending behaviours under the problem-stage features of an epic (BEH-EPIC-5)', () => {
    const early = real.flatMap((f) => {
      const source = read(f)
      const { features, epics } = tags(source)
      const epicIds = new Set(epics.map((e) => e.id))
      const problem = new Set(featureStages(source).filter((x) => x.stage === 'problem').map((x) => x.id))
      const drafted = [...features].filter(([id, parents]) => problem.has(id) && parents.some((p) => epicIds.has(p))).map(([id]) => id)
      return behaviourBuilt(source)
        .filter((b) => b.built && b.in.some((id) => drafted.includes(id)))
        .map((b) => `${f} ${b.in.join(',')}`)
    })
    expect(early).toEqual([])
  })

  // BEH-EPIC-6: every epic has its children drafted; his "out of scope" in its title
  // parks one. The agent's wake sweep (tools/kit-backlog.js) lists the same set.
  it('gives every epic a feature, unless its title parks it out of scope (BEH-EPIC-6)', () => {
    const bare = real.flatMap((f) => {
      const { features, epics } = tags(read(f))
      const used = new Set([...features.values()].flat())
      return epics.filter((e) => !used.has(e.id) && !/out of scope/i.test(e.title)).map((e) => `${f} ${e.id}`)
    })
    expect(bare).toEqual([])
  })

  it('has drafted the first epic, Kit for users other than James (BEH-EPIC-7)', () => {
    const { features, epics } = tags(read('kit-ui.beh'))
    expect(epics.map((e) => e.id)).toContain('EPIC-KIT-USERS')
    expect([...features.values()].filter((p) => p.includes('EPIC-KIT-USERS')).length).toBeGreaterThan(0)
  })

  it('reads the parents of a feature and the title of an epic from their own lines', () => {
    const t = tags('epic E "Later (out of scope)"\n\nfeature A "a"\n  in E\n  stage problem\nfeature B "b"\n  in A\nbehaviour X "x"\n  in B\n')
    expect([...t.features]).toEqual([['A', ['E']], ['B', ['A']]])
    expect(t.epics).toEqual([{ id: 'E', title: 'Later (out of scope)' }])
  })

  it('reads a behaviour as built only without a pending line (BEH-STAGE-10)', () => {
    expect(behaviourBuilt('behaviour A "a"\n  in F\n  pending\nbehaviour B "b"\n  in F\n  in G\n')).toEqual([
      { in: ['F'], built: false },
      { in: ['F', 'G'], built: true },
    ])
  })

  it('gives every feature in a real project a stage line (BEH-STAGE-9)', () => {
    const missing = real.flatMap((f) =>
      featureStages(fs.readFileSync(path.join(CORPORA, f), 'utf8'))
        .filter((x) => x.stage === null)
        .map((x) => `${f} ${x.id}`))
    expect(missing).toEqual([])
  })

  it('writes only the stage words the board has columns for (BEH-STAGE-9)', () => {
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
