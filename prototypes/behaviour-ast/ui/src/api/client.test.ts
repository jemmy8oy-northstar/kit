import { describe, expect, it, vi } from 'vitest'
import { ApiError, addBehaviour, addBinding, addStep, setReview } from './client'
import contract from '../test/fixtures/write-contract.json'
import type { ReviewState } from './types'

type Recorded = { url: string; init: RequestInit }

function recorder(response: { ok: boolean; status: number; body: unknown }) {
  const calls: Recorded[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init: RequestInit) => {
      calls.push({ url, init })
      return {
        ok: response.ok,
        status: response.status,
        statusText: 'x',
        json: async () => response.body,
      }
    }),
  )
  return calls
}

const ok = (body: unknown) => ({ ok: true, status: 200, body })

function invoke(req: (typeof contract.requests)[number]) {
  const { fn, args } = req.call
  if (fn === 'addStep') {
    const [app, id, step] = args as [string, string, string]
    return addStep(app, id, step)
  }
  if (fn === 'setReview') {
    const [app, id, state, note] = args as [string, string, ReviewState, string | undefined]
    // `note` is absent from the fixture args for an approval, and the client
    // turns that into an explicit `null` in the body — which the contract
    // asserts, so the two spellings cannot drift apart.
    return setReview(app, id, state, note ?? null)
  }
  if (fn === 'addBinding') {
    const [app, noun, binding] = args as [string, string, Record<string, unknown>]
    return addBinding(app, noun, binding)
  }
  if (fn === 'addBehaviour') {
    const [app, behaviour] = args as [string, Parameters<typeof addBehaviour>[1]]
    return addBehaviour(app, behaviour)
  }
  // Named, never defaulted. This used to fall through to `addBehaviour`, so a
  // fixture naming a function nobody had written here would have been tested
  // against the wrong client call and passed.
  throw new Error(`the contract calls ${fn}, which this test does not know how to invoke`)
}

describe('the write contract, from the client side', () => {
  // Half of a two-sided pin. kit.test.js sends these same literals down a real
  // socket into ui.js and asserts the file on disk; this half asserts the client
  // is what produces them. Either side drifting reddens exactly one of the two.
  for (const req of contract.requests) {
    it(`sends ${req.method} ${req.path} — ${req.what}`, async () => {
      const calls = recorder(
        req.expect.status === 200
          ? ok({ ok: true, app: 'x', behaviour: 'y', file: 'f', committed: false, note: 'n' })
          : { ok: false, status: req.expect.status, body: { reason: 'refused' } },
      )

      await invoke(req).catch(() => undefined) // a 409 rejects; the request still went out

      expect(calls).toHaveLength(1)
      expect(calls[0].url).toBe(req.path)
      expect(calls[0].init.method).toBe(req.method)
      expect(JSON.parse(String(calls[0].init.body))).toEqual(req.body)
    })
  }

  it('declares a content-type the server will parse, on every write', async () => {
    // Not decoration. `ui.js` reads a JSON body, and — separately — its Origin
    // check is what stops a cross-origin write, precisely BECAUSE a `text/plain`
    // POST is a CORS simple request an attacker can send with no preflight. The
    // header here is about the server parsing us; it is not the defence, and a
    // test that treated it as one would be measuring the wrong thing.
    const calls = recorder(ok({ ok: true, app: 'a', behaviour: 'B', file: 'f', committed: false, note: 'n' }))
    await addStep('gamma', 'BEH-G', 'then sees region:Main')

    const headers = calls[0].init.headers as Record<string, string>
    expect(headers['content-type']).toBe('application/json')
  })
})

describe('what a refusal does to the caller', () => {
  it("surfaces writer.js's sentence, not the status code", async () => {
    // The whole value of the 409 path. `writer.js` refuses with "no behaviour
    // BEH-NOPE in this corpus" — that sentence tells him how to fix what he
    // typed, and a client that reported "409" or "Conflict" would throw away the
    // only guidance the corpus can give.
    recorder({
      ok: false,
      status: 409,
      body: { error: 'no-such-behaviour', reason: 'no behaviour BEH-NOPE in this corpus' },
    })

    await expect(addStep('gamma', 'BEH-NOPE', 'then sees region:Main')).rejects.toThrow(
      'no behaviour BEH-NOPE in this corpus',
    )
  })

  it('falls back to the error code when the server sends no reason', async () => {
    recorder({ ok: false, status: 409, body: { error: 'duplicate-id' } })
    await expect(addBehaviour('gamma', { id: 'BEH-G', title: 't' })).rejects.toThrow('duplicate-id')
  })

  it('says the server is not running, rather than reporting a write that never happened', async () => {
    // A rejected fetch and a refused edit are different problems with different
    // fixes; the failure that must never occur is either one reading as success.
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('fetch failed') }))

    await expect(addStep('gamma', 'BEH-G', 'then sees region:Main')).rejects.toThrow(/node ui\.js/)
    await expect(addStep('gamma', 'BEH-G', 'then sees region:Main')).rejects.toBeInstanceOf(ApiError)
  })
})
