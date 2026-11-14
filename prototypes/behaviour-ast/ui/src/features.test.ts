import { describe, expect, it } from 'vitest'
import { childrenOf, topLevel, type Tagged } from './features'
import type { Behaviour, Feature } from './api/types'

function feat(id: string, over: Partial<Feature> = {}): Feature {
  return { id, title: `title of ${id}`, in: [], at: 'x.beh:1', ...over }
}

function beh(id: string, over: Partial<Behaviour> = {}): Behaviour {
  return {
    id,
    title: `title of ${id}`,
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'defined', ref: null },
    review: { state: 'approved', note: null },
    asks: null,
    pending: false,
    layer: 'ux',
    at: 'x.beh:1',
    in: [],
    ...over,
  }
}

// A project tree: FEAT-TOP holds FEAT-MID, which holds FEAT-DEEP, which holds BEH-A.
const tree = (epics: boolean): Tagged => ({
  features: [
    feat('FEAT-TOP'),
    feat('FEAT-MID', { in: ['FEAT-TOP'] }),
    feat('FEAT-DEEP', { in: ['FEAT-MID'], ...(epics ? { epics: ['EPIC-USERS'] } : {}) }),
  ],
  behaviours: [beh('BEH-A', { in: ['FEAT-DEEP'] })],
})

const ids = (t: Tagged) => ({ features: t.features.map((f) => f.id), behaviours: t.behaviours.map((b) => b.id) })

describe('the project tree with epics (BEH-EPIC-1)', () => {
  it('lists the same top level, and the same children of every feature, with or without an epic tag (BEH-EPIC-1)', () => {
    const plain = tree(false)
    const tagged = tree(true)
    expect(ids(topLevel(tagged))).toEqual(ids(topLevel(plain)))
    for (const id of ['FEAT-TOP', 'FEAT-MID', 'FEAT-DEEP']) {
      expect(ids(childrenOf(tagged, id))).toEqual(ids(childrenOf(plain, id)))
    }
  })

  it('keeps the epic-tagged feature under its own parent, and does not lift it to the top level (BEH-EPIC-1)', () => {
    const tagged = tree(true)
    expect(ids(topLevel(tagged)).features).toEqual(['FEAT-TOP'])
    expect(ids(childrenOf(tagged, 'FEAT-MID')).features).toEqual(['FEAT-DEEP'])
  })
})
