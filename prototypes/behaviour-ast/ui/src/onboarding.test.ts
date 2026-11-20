import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// EPIC-ONBOARD and BEH-LOVABLE-1 live in the corpus, not in the UI. These tests read the
// corpus as text, the way stageReading.test.ts reads the corpora, so the epic's scope and
// the feature's place under it are held by the file the board reads.
const here = path.dirname(fileURLToPath(import.meta.url))
const corpus = fs.readFileSync(path.join(here, '..', '..', 'behaviours', 'kit-ui.beh'), 'utf8')

/** The header line for a block (`epic`, `feature`, `behaviour`) and the lines that belong to it. */
function block(kind: string, id: string): string[] {
  const lines = corpus.split('\n')
  const start = lines.findIndex((l) => l.startsWith(`${kind} ${id} `))
  if (start < 0) return []
  const body: string[] = []
  for (let i = start + 1; i < lines.length; i++) {
    if (/^\S/.test(lines[i]) && lines[i].trim() !== '') break
    body.push(lines[i])
  }
  return [lines[start], ...body]
}

describe('EPIC-ONBOARD: the epic is in scope and holds the Lovable feature', () => {
  it('EPIC-ONBOARD: its title no longer carries "out of scope" or "backlog"', () => {
    const [header] = block('epic', 'EPIC-ONBOARD')
    expect(header, 'EPIC-ONBOARD is not declared in kit-ui.beh').toBeTruthy()
    expect(header).not.toMatch(/out of scope|backlog/i)
  })

  it('EPIC-ONBOARD: FEAT-ONBOARD-LOVABLE sits under the epic', () => {
    const [header, ...body] = block('feature', 'FEAT-ONBOARD-LOVABLE')
    expect(header, 'FEAT-ONBOARD-LOVABLE is not declared in kit-ui.beh').toBeTruthy()
    expect(body).toContain('  in EPIC-ONBOARD')
  })

  it('EPIC-ONBOARD: BEH-LOVABLE-1 is the one card under that feature', () => {
    const [header, ...body] = block('behaviour', 'BEH-LOVABLE-1')
    expect(header, 'BEH-LOVABLE-1 is not declared in kit-ui.beh').toBeTruthy()
    expect(body).toContain('  in FEAT-ONBOARD-LOVABLE')
    const cards = corpus.split('\n').filter((l) => /^behaviour BEH-LOVABLE-/.test(l))
    expect(cards).toHaveLength(1)
  })
})

// Pending: the bundle's other five BEH-LOVABLE-1 tests need an onboarding entry, a repo
// source with an ownership check, and a first-spec writer. None exists yet, and the choices
// are the owner's (see the contract question on BEH-LOVABLE-1 in kit-ui.beh).
describe('BEH-LOVABLE-1: a user can onboard an existing app built in Lovable onto Kit', () => {
  it.todo('BEH-LOVABLE-1: opening the projects page (KitProjects) shows the onboarding entry in KitMain')
  it.todo('BEH-LOVABLE-1: the onboarding source is a GitHub repo the user owns; a Lovable export file is not accepted')
  it.todo('BEH-LOVABLE-1: a repo the user does not own is refused as an onboarding source')
  it.todo('BEH-LOVABLE-1: onboarding produces a first spec of unreviewed cards; none is approved by default')
  it.todo('BEH-LOVABLE-1: a non-Lovable repo or other tech stack is not offered in this slice')
})
