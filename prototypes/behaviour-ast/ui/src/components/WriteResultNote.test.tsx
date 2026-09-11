import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import WriteResultNote from './WriteResultNote'
import type { BindResult, WriteResult } from '../api/types'

/**
 * Written because `mutate-ui.js` found the gap, and the gap was the last hop of
 * the one mechanism `kit#32` exists to provide.
 *
 * `sharedWith` is pinned at the client↔server seam from BOTH sides already:
 * `src/test/fixtures/write-contract.json` declares that binding `region:Shared`
 * from `gamma` answers `sharedWith: ["epsilon"]`, `client.test.ts` asserts the
 * client emits that request, and `kit.test.js` feeds the same literals to a
 * running `ui.js`. Two suites, both green, neither of them able to notice that
 * **the answer never reaches the screen** — `bindings.json` called its own
 * safeguard "still a habit rather than a design", and a warning nobody renders
 * is exactly that habit again.
 *
 * So these assert the hop the contract fixture cannot: response → visible text.
 */

const bind = (over: Partial<BindResult> = {}): BindResult => ({
  ok: true,
  app: 'gamma',
  noun: 'region:Shared',
  file: 'bindings.json',
  committed: false,
  note: 'bound',
  sharedWith: [],
  unreadableCorpora: [],
  ...over,
})

const corpusWrite: WriteResult = {
  ok: true,
  app: 'snip-it',
  behaviour: 'BEH-HOME-1',
  file: 'behaviours/snip-it.beh',
  committed: false,
  note: 'added',
}

describe('WriteResultNote', () => {
  it('says which file was written and that Kit did not commit it', () => {
    render(<WriteResultNote result={corpusWrite} />)

    const note = screen.getByRole('status')
    expect(note).toHaveTextContent('behaviours/snip-it.beh')
    expect(note).toHaveTextContent(/Not committed — Kit does not run git/)
    expect(note).not.toHaveTextContent(/^Committed\.$/)
  })

  it('names the OTHER corpora a bind just changed — the namespace is global', () => {
    // The moment the global namespace stops being a habit. The person who just
    // clicked is the only one who can say whether sharing this noun with those
    // corpora is what they meant, and this is when they are looking.
    render(<WriteResultNote result={bind({ sharedWith: ['epsilon', 'delta'] })} />)

    const warning = screen.getByRole('alert')
    expect(warning).toHaveTextContent('epsilon')
    expect(warning).toHaveTextContent('delta')
    // Not just the names: the sentence has to say what it MEANS, or it reads as
    // a list of unrelated projects.
    expect(warning).toHaveTextContent(/generate against this binding too/)
  })

  it('CONTROL: a bind that collides with nothing raises no warning', () => {
    // Without this, the assertion above would pass just as well if the warning
    // rendered on every bind — which would train him to ignore it.
    render(<WriteResultNote result={bind({ sharedWith: [] })} />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('says a corpus could not be parsed, so "could not look" is not read as "nothing else uses it"', () => {
    // `sharedWith` is computed by reading every other corpus. One that will not
    // parse is a hole in that answer, and silence would turn an incomplete
    // search into a clean bill of health ([[empty-means-two-things]]).
    render(<WriteResultNote result={bind({ unreadableCorpora: ['broken'] })} />)

    const alerts = screen.getAllByRole('alert')
    const incomplete = alerts.find((a) => a.textContent?.includes('broken'))
    expect(incomplete).toBeDefined()
    expect(incomplete).toHaveTextContent(/could not be parsed/)
    expect(incomplete).toHaveTextContent(/may be\s+incomplete/)
  })

  it('reports an incomplete search even when the collisions it DID find are empty', () => {
    // The dangerous combination, and the reason the two alerts are independent:
    // "no collisions found" plus "one corpus was unreadable" must not render as
    // a silent success.
    render(<WriteResultNote result={bind({ sharedWith: [], unreadableCorpora: ['broken'] })} />)

    expect(screen.getByRole('alert')).toHaveTextContent('broken')
  })

  it('a corpus write carries no namespace warning — it cannot have one', () => {
    // `AnyWriteResult` is a union precisely so a bind cannot be rendered without
    // its `sharedWith`. This pins the other direction: a behaviour write must
    // not grow one.
    render(<WriteResultNote result={corpusWrite} />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByText(/noun namespace is global/)).not.toBeInTheDocument()
  })
})
