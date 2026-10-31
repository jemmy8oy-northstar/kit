import { describe, expect, it } from 'vitest'
import type { ProjectDetail } from './api/types'
import { goalOf } from './goal'

const beh = (id: string, feature: string, pending = false) => ({ id, in: [feature], pending })
const file = (epics: object[], features: object[], behaviours: object[], app = 'a') =>
  ({ app, project: 'p', epics, features, behaviours }) as unknown as ProjectDetail
const epic = (id: string) => ({ id, title: id, at: 'x:1' })
const feat = (id: string, epicId: string) => ({ id, title: id, in: [], at: 'x:1', epics: [epicId] })

describe('goalOf (BEH-NARR-1)', () => {
  it('BEH-NARR-1: no epics gives null', () => {
    expect(goalOf([file([], [], [beh('B', 'F', true)])])).toBeNull()
  })

  it('BEH-NARR-1: falls back to the first open epic in corpus order', () => {
    const f = file(
      [epic('E1'), epic('E2')],
      [feat('F1', 'E1'), feat('F2', 'E2')],
      [beh('B1', 'F1', true), beh('B2', 'F2', true), beh('B3', 'F2')],
    )
    const g = goalOf([f])!.goal!
    expect(g.epic.id).toBe('E1')
    expect([g.built, g.total]).toEqual([0, 1])
  })

  it('BEH-NARR-1: skips epics that are fully built', () => {
    const f = file(
      [epic('E1'), epic('E2')],
      [feat('F1', 'E1'), feat('F2', 'E2')],
      [beh('B1', 'F1'), beh('B2', 'F2', true), beh('B3', 'F2')],
    )
    const g = goalOf([f])!.goal!
    expect(g.epic.id).toBe('E2')
    expect([g.built, g.total]).toEqual([1, 2])
  })

  it('BEH-NARR-1: every epic built gives a goal of null, not no epics', () => {
    const f = file([epic('E1')], [feat('F1', 'E1')], [beh('B1', 'F1')])
    expect(goalOf([f])).toEqual({ goal: null })
  })
})
