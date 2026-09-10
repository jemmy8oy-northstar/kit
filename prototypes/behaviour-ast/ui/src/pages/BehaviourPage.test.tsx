import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import BehaviourPage from './BehaviourPage'
import habits from '../test/fixtures/project-james-habits-app.json'
import snipIt from '../test/fixtures/project-snip-it.json'

function renderAt(body: unknown, app: string, id: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )

  return render(
    <MemoryRouter initialEntries={[`/projects/${app}/behaviours/${id}`]}>
      <Routes>
        <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('BehaviourPage', () => {
  it('shows the behaviour and the test Kit generated from it, together', async () => {
    // His "iterating on the output" needs both halves on one screen; a page
    // that shows the behaviour and links to the code is not the same product.
    renderAt(snipIt, 'snip-it', 'BEH-HOME-1')

    expect(await screen.findByText('The landing page renders')).toBeInTheDocument()
    expect(screen.getByText(/opens page:Home/)).toBeInTheDocument()
    expect(screen.getByText(/page\.goto/)).toBeInTheDocument()
  })

  it('names the unbound nouns that turned steps into comments', async () => {
    // BEH-TODAY-1 generates nothing but comments because three nouns are
    // unbound. "0 generated" alone would look like a Kit failure; the nouns are
    // what tell you it is the app that is missing, not the tool.
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    expect(await screen.findByText(/Unbound nouns:/)).toHaveTextContent('page:Today')
    expect(screen.getByText(/UNGENERATED: when opens page:Today/)).toBeInTheDocument()
  })

  it('distinguishes "no test was generated" from an empty one', async () => {
    const noGenerated = { ...snipIt, generated: [] }
    renderAt(noGenerated, 'snip-it', 'BEH-HOME-1')

    expect(await screen.findByRole('alert')).toHaveTextContent('Kit generated nothing')
  })

  it('says a behaviour id is not in the corpus, rather than rendering a blank page', async () => {
    renderAt(snipIt, 'snip-it', 'BEH-NOPE-9')

    expect(await screen.findByText('No such behaviour')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('BEH-NOPE-9')
  })
})

// ── the write loop: his step 3, on the page where the output is in view ──────

/**
 * A scripted fetch: each call takes the next response off the queue, so a test
 * can say "the read, then the write, then the read again" and assert the order.
 *
 * A single stubbed response cannot express the thing being tested here — that
 * the page RE-READS after a write — because a re-read would hand back the same
 * body and look identical to never having happened.
 */
function scripted(responses: { ok: boolean; status: number; body: unknown }[]) {
  const calls: { url: string; init?: RequestInit }[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({ url, init })
      const next = responses.shift()
      if (!next) throw new Error(`unscripted fetch: ${url}`)
      return { ok: next.ok, status: next.status, statusText: 'x', json: async () => next.body }
    }),
  )
  return calls
}

const wrote = {
  ok: true,
  status: 200,
  body: {
    ok: true,
    app: 'snip-it',
    behaviour: 'BEH-HOME-1',
    file: 'behaviours/snip-it.beh',
    committed: false,
    note: 'written to the working tree',
  },
}

function renderScripted(
  responses: { ok: boolean; status: number; body: unknown }[],
  app: string,
  id: string,
) {
  const calls = scripted(responses)
  render(
    <MemoryRouter initialEntries={[`/projects/${app}/behaviours/${id}`]}>
      <Routes>
        <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
      </Routes>
    </MemoryRouter>,
  )
  return calls
}

const typeStep = (text: string) =>
  fireEvent.change(screen.getByLabelText('Step'), { target: { value: text } })

describe('adding a step from the page that shows the output', () => {
  it('POSTs the line as typed, then re-reads so the regenerated test is what you see', async () => {
    // The re-read IS the loop. Kit derives the test from the corpus on every
    // read, so without it the page shows his new step beside the test that
    // predates it — which looks like the generator ignored him, and is worse
    // than showing nothing at all.
    const second = structuredClone(snipIt)
    second.generated[0].code = 'test("[BEH-HOME-1] regenerated after the write", () => {})'

    const calls = renderScripted(
      [{ ok: true, status: 200, body: snipIt }, wrote, { ok: true, status: 200, body: second }],
      'snip-it',
      'BEH-HOME-1',
    )

    await screen.findByRole('heading', { name: 'The landing page renders', level: 1 })
    typeStep('then sees button:Save')
    fireEvent.click(screen.getByRole('button', { name: 'Add step' }))

    expect(await screen.findByText(/regenerated after the write/)).toBeInTheDocument()

    // Three calls in this order: the read, the write carrying the typed line,
    // and the read that followed it.
    expect(calls.map((c) => c.init?.method ?? 'GET')).toEqual(['GET', 'POST', 'GET'])
    expect(calls[1].url).toBe('/api/projects/snip-it/behaviours/BEH-HOME-1/steps')
    expect(JSON.parse(String(calls[1].init?.body))).toEqual({ step: 'then sees button:Save' })
  })

  it('says which file was written, and that Kit did not commit it', async () => {
    // Decision 2's boundary is only a guarantee to him if he can watch it hold.
    renderScripted(
      [{ ok: true, status: 200, body: snipIt }, wrote, { ok: true, status: 200, body: snipIt }],
      'snip-it',
      'BEH-HOME-1',
    )

    await screen.findByRole('heading', { name: 'The landing page renders', level: 1 })
    typeStep('then sees button:Save')
    fireEvent.click(screen.getByRole('button', { name: 'Add step' }))

    const note = await screen.findByRole('status')
    expect(note).toHaveTextContent('behaviours/snip-it.beh')
    expect(note).toHaveTextContent(/Not committed/)
  })

  it('shows the corpus’s own refusal, and keeps what he typed so he can fix it', async () => {
    // A refused edit is the most useful thing the write path produces: the
    // sentence names what is wrong with the line. Clearing the input on refusal
    // would make him retype it to find out.
    renderScripted(
      [
        { ok: true, status: 200, body: snipIt },
        { ok: false, status: 409, body: { error: 'bad-step', reason: 'kit.js cannot parse: wibble region:Main' } },
      ],
      'snip-it',
      'BEH-HOME-1',
    )

    await screen.findByRole('heading', { name: 'The landing page renders', level: 1 })
    typeStep('wibble region:Main')
    fireEvent.click(screen.getByRole('button', { name: 'Add step' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('kit.js cannot parse: wibble region:Main')
    expect(screen.getByLabelText('Step')).toHaveValue('wibble region:Main')
    // And no success note beside the failure — the two states must not co-exist.
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('will not send a blank step at all', async () => {
    // The server refuses it too, and that refusal is the real guard. This only
    // keeps a pointless round-trip out of the loop.
    const calls = renderScripted([{ ok: true, status: 200, body: snipIt }], 'snip-it', 'BEH-HOME-1')

    await screen.findByRole('heading', { name: 'The landing page renders', level: 1 })
    expect(screen.getByRole('button', { name: 'Add step' })).toBeDisabled()
    expect(calls).toHaveLength(1)
  })
})

// ── his step 4: "what the desired behaviour really is" ──────────────────────

const typeCorrection = (text: string) =>
  fireEvent.change(screen.getByLabelText(/Correction/), { target: { value: text } })

describe('adjudicating an inference', () => {
  it('the fixture really does carry an unreviewed inference to adjudicate', () => {
    // The population, before anything renders it. Every test below is vacuous if
    // the behaviour it opens stops being an unreviewed inference, and a vacuous
    // test passes.
    const b = habits.behaviours.find((x) => x.id === 'BEH-SEED-1')!
    expect(b.source.origin).toBe('inferred')
    expect(b.review.state).toBe('unreviewed')
  })

  it('approves it, then re-reads so the corpus on screen is the corpus on disk', async () => {
    const second = structuredClone(habits)
    second.behaviours.find((b) => b.id === 'BEH-SEED-1')!.review.state = 'approved'

    const calls = renderScripted(
      [{ ok: true, status: 200, body: habits }, wrote, { ok: true, status: 200, body: second }],
      'james-habits-app',
      'BEH-SEED-1',
    )

    await screen.findByRole('button', { name: 'Approve' })
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expect(calls.map((c) => c.init?.method ?? 'GET')).toEqual(['GET', 'POST', 'GET'])
    expect(calls[1].url).toBe('/api/projects/james-habits-app/behaviours/BEH-SEED-1/review')
    // `note: null` explicitly, not an omitted key — the client and the contract
    // fixture agree on the spelling, and the server distinguishes the two.
    expect(JSON.parse(String(calls[1].init?.body))).toEqual({ state: 'approved', note: null })
  })

  it('will not deny without the correction, and sends it when there is one', async () => {
    // His #68 rule: a bare denial deletes a line, a denial with a correction
    // compounds into the corpus. The disabled button is the affordance; the
    // server's refusal is the rule.
    const calls = renderScripted(
      [{ ok: true, status: 200, body: habits }, wrote, { ok: true, status: 200, body: habits }],
      'james-habits-app',
      'BEH-SEED-1',
    )

    await screen.findByRole('button', { name: 'Deny' })
    expect(screen.getByRole('button', { name: 'Deny' })).toBeDisabled()
    expect(calls).toHaveLength(1)

    typeCorrection('the starter set is seeded on every login, not once')
    expect(screen.getByRole('button', { name: 'Deny' })).toBeEnabled()
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }))

    await screen.findByRole('status')
    expect(JSON.parse(String(calls[1].init?.body))).toEqual({
      state: 'denied',
      note: 'the starter set is seeded on every login, not once',
    })
  })

  it('shows the corpus’s own refusal rather than reporting a write that did not happen', async () => {
    renderScripted(
      [
        { ok: true, status: 200, body: habits },
        { ok: false, status: 409, body: { error: 'would-not-parse', reason: 'a denied behaviour must state the correction' } },
      ],
      'james-habits-app',
      'BEH-SEED-1',
    )

    await screen.findByRole('button', { name: 'Approve' })
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('a denied behaviour must state the correction')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('says where the inference came from, so the answer rests on something', async () => {
    // An approve/deny with no visible provenance is a coin toss. `source.ref` is
    // the line of code Kit read, and it is the whole reason `source` exists.
    renderAt(habits, 'james-habits-app', 'BEH-SEED-1')

    expect(await screen.findByText(/EnsureSeeded_creates_the_starter_set_once/)).toBeInTheDocument()
  })

  it('offers the control on a human-written behaviour too, and says a human wrote it', async () => {
    // `defined` behaviours are approved by default with no line saying so, and
    // `denied` counts across ALL behaviours in kit.js — so denying one is
    // meaningful and must not be hidden. What changes is the sentence above it.
    renderAt(snipIt, 'snip-it', 'BEH-HOME-1')

    expect(await screen.findByText(/A human wrote this/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled()
  })
})
