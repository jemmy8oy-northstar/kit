import fs from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

// BEH-TICKET-6 marks the kit-ui.beh cards that draw the retired feature and spec boards as to be deleted, and
// does not remove them until the owner confirms the list. This reads the corpus file itself, so the marks and the
// untouched BEH-BOARD cards are asserted on the text that ships, not on a copy.

const corpus = fs.readFileSync(path.join(process.cwd(), '..', 'behaviours', 'kit-ui.beh'), 'utf8')

/** A card's lines, from its `behaviour` header to the next card's header or the end of the file. */
function cardOf(id: string): string | null {
  const lines = corpus.split('\n')
  const start = lines.findIndex((l) => l.startsWith(`behaviour ${id} `))
  if (start === -1) return null
  const next = lines.findIndex((l, i) => i > start && (l.startsWith('behaviour ') || l.startsWith('# ')))
  return lines.slice(start, next === -1 ? undefined : next).join('\n')
}

const MARK = 'contract to be deleted (BEH-TICKET-6):'
const NAMED = ['BEH-STAGE-1', 'BEH-STAGE-7', 'BEH-STAGE-8', 'BEH-STAGE-9', 'BEH-STAGE-10', 'BEH-STAGE-11', 'BEH-STAGE-12', 'BEH-BOARD-1']

describe('the retired boards are marked, not removed (BEH-TICKET-6)', () => {
  it('marks the eight cards BEH-TICKET-6 names as to be deleted, with a reason beside each, and removes none (BEH-TICKET-6)', () => {
    for (const id of NAMED) {
      const card = cardOf(id)
      expect(card, `${id} is in the corpus`).not.toBeNull()
      const line = card!.split('\n').find((l) => l.includes(MARK))
      expect(line, `${id} is marked`).toBeDefined()
      expect(line!.split(MARK)[1].trim().length, `${id} has a reason`).toBeGreaterThan(10)
    }
  })

  it('BEH-BOARD-10 to BEH-BOARD-21 carry no deletion mark and are still in the corpus (BEH-TICKET-6)', () => {
    for (let n = 10; n <= 21; n++) {
      const card = cardOf(`BEH-BOARD-${n}`)
      expect(card, `BEH-BOARD-${n} is in the corpus`).not.toBeNull()
      expect(card, `BEH-BOARD-${n} is not marked`).not.toContain('to be deleted')
    }
  })

  it('BEH-EPIC-4 and BEH-NARR-1 are kept, unmarked (BEH-TICKET-6)', () => {
    expect(cardOf('BEH-EPIC-4')).not.toContain('to be deleted')
    expect(cardOf('BEH-NARR-1')).not.toContain('to be deleted')
  })
})
