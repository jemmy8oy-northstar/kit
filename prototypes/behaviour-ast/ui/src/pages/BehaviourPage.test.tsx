import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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

  it('names the unbound nouns that turned steps into comments, and what each one owes', async () => {
    // BEH-TODAY-1 generates nothing but comments because three nouns are
    // unbound. "0 generated" alone would look like a Kit failure; the nouns are
    // what tell you it is the app that is missing, not the tool.
    //
    // Naming them was all this page did for four slices, and it is a dead end:
    // the requirement is the actionable half, and `requires.js` has been able
    // to state it since kit#22 with nothing reading it. `opens` needs a route
    // and `sees` needs addressability — DIFFERENT keys, which is the finding
    // requires.js exists for, so the page must not collapse them into "unbound".
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    expect(await screen.findByRole('heading', { name: /Bind\s*page:Today/ })).toBeInTheDocument()
    expect(screen.getByText(/UNGENERATED: when opens page:Today/)).toBeInTheDocument()
    expect(screen.getByText(/a route that serves this page/)).toBeInTheDocument()
    // `getAllBy`, because more than one noun on this behaviour owes it — which
    // is the point: the requirement belongs to the VERB, so two nouns reached
    // by `sees` owe the same thing while `page:Today`, reached by `opens`,
    // owes something else entirely.
    expect(screen.getAllByText(/addressable from a test/).length).toBeGreaterThan(1)
  })

  it('offers a form per unbound noun, not one form for all of them', async () => {
    // A single "bind something" box would make the user retype the noun that
    // the page is already showing them — and the noun is exactly the thing
    // that must not be retyped, because a typo silently creates a binding
    // nothing references rather than an error.
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    const binds = await screen.findAllByRole('button', { name: 'Bind' })
    expect(binds.length).toBeGreaterThan(1)
    for (const b of binds) expect(b).toBeDisabled()
  })

  it('orders the bind forms the way the steps name them, not alphabetically', async () => {
    // `requires.js` returns nouns sorted by name, and the panel sits directly
    // under a generated test whose refusals are in STEP order. Alphabetical
    // here makes the reader match the two lists up by name — which is exactly
    // the work having them on one screen is supposed to save. Seen in a 390px
    // screenshot, and invisible whenever the two orders happen to agree, so
    // the fixture chosen is one where they do NOT: the steps run
    // region:HabitList then control:DateStepper, and sorted by name that
    // reverses.
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    const headings = (await screen.findAllByRole('heading', { name: /^Bind/ }))
      .map((h) => h.textContent ?? '')
    const at = (noun: string) => headings.findIndex((h) => h.includes(noun))

    expect(at('page:Today')).toBe(0)
    expect(at('region:HabitList')).toBeLessThan(at('control:DateStepper'))
    // The control: sorted by name, DateStepper would come first — so the
    // assertion above is measuring the ordering and not just any ordering.
    const alphabetical = [...headings].sort()
    expect(headings).not.toEqual(alphabetical)
  })

  it('sorts a noun no step here NAMES to the back, not the front', async () => {
    // The -1 case in `rank()`, which no fixture reaches: `requires.js` counts a
    // noun named only in another behaviour's `provides` value, so it is `usedBy`
    // this behaviour while appearing in none of its step `refs`. `indexOf`
    // returns -1 for it, and -1 sorts FIRST — putting the one requirement the
    // reader cannot see in the generated test above everything they can.
    //
    // Constructed rather than captured, because the real corpus has no such
    // noun today. That is the reason the branch survived: the comment on
    // `rank()` says the case is real, and nothing exercised it.
    const withGhost = {
      ...habits,
      requires: {
        ...habits.requires,
        missing: [
          {
            noun: 'field:Ghost',
            kind: 'field',
            name: 'Ghost',
            usedBy: ['BEH-TODAY-1'],
            bound: false,
            satisfied: false,
            needs: [{ id: 'label', surface: 'a label', verbs: ['fills'], met: false }],
            binding: null,
            sharedWith: [],
          },
          ...habits.requires.missing,
        ],
      },
    }
    renderAt(withGhost, 'james-habits-app', 'BEH-TODAY-1')

    const headings = (await screen.findAllByRole('heading', { name: /^Bind/ }))
      .map((h) => h.textContent ?? '')

    // It must still be OFFERED — it is a real requirement — just not first.
    expect(headings.some((h) => h.includes('field:Ghost'))).toBe(true)
    expect(headings.findIndex((h) => h.includes('field:Ghost'))).toBe(headings.length - 1)
    // And the nouns the steps DO name keep their step order in front of it.
    expect(headings[0]).toContain('page:Today')
  })

  it('shows a correction recorded by a past denial, so the next adjudicator sees it', async () => {
    // `review denied <correction>` is the only place a human's statement about
    // what the behaviour SHOULD say is stored, and his #68 vocabulary requires
    // one on every denial. Hiding it puts the next reader back where the denier
    // started. No fixture carries a note, which is precisely why nothing noticed.
    const denied = {
      ...habits,
      behaviours: habits.behaviours.map((b) =>
        b.id === 'BEH-SEED-1'
          ? { ...b, review: { state: 'denied', note: 'the page is Dashboard, not Home' } }
          : b,
      ),
    }
    renderAt(denied, 'james-habits-app', 'BEH-SEED-1')

    expect(await screen.findByText(/Recorded correction:/)).toHaveTextContent(
      'the page is Dashboard, not Home',
    )
  })

  it('CONTROL: a behaviour with no correction shows no empty "Recorded correction" line', async () => {
    // Otherwise the assertion above is satisfied by a page that always renders
    // the label, with nothing after it.
    renderAt(habits, 'james-habits-app', 'BEH-SEED-1')

    await screen.findByRole('button', { name: 'Approve' })
    expect(screen.queryByText(/Recorded correction:/)).not.toBeInTheDocument()
  })

  it('says which OTHER corpora a binding would reach, before the click', async () => {
    // `bindings.json` is one flat map over every corpus, and its own comment
    // calls the convention that avoids collisions "still a habit rather than a
    // design". This is that habit becoming a mechanism — and it has to be on
    // the form, not only in the response, because after the write the decision
    // has already been made.
    renderAt(habits, 'james-habits-app', 'BEH-TODAY-1')

    await screen.findAllByRole('button', { name: 'Bind' })
    // Every noun on this behaviour is shared, because `trial-habits-a` and
    // `trial-habits-b` are the same app described twice (cc-bot#92's forward
    // trials) — so the assertion is over the whole set rather than over the
    // first match, and it names the noun each caution belongs to.
    const cautions = screen.getAllByText(/The noun namespace is global/)
    expect(cautions.length).toBeGreaterThan(0)
    for (const c of cautions) expect(c).toHaveTextContent(/trial-habits-[ab]/)
    expect(cautions.some((c) => c.textContent?.includes('page:Today'))).toBe(true)
  })

  it('CONTROL: a noun no other corpus uses gets no sharing caution', async () => {
    // Without this, a component that printed the caution unconditionally would
    // pass the test above and be telling every binding it collides.
    renderAt(snipIt, 'snip-it', 'BEH-HOME-1')

    await screen.findByText('The landing page renders')
    expect(screen.queryByText(/The noun namespace is global/)).not.toBeInTheDocument()
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

    // ⚠️ Waiting for the status element is NOT waiting for the re-read, and this
    // line used to assume it was. `useWrite` sets the result and only then calls
    // `onWrote()`, which is `useResource`'s `reload()` — and reload merely bumps
    // a nonce, so the GET is issued by an EFFECT that runs after React commits.
    // The status element is therefore in the DOM one commit before the re-read
    // exists. It passed here every time and lost the race on a 2-core CI runner
    // the first time the suite got slightly busier ([[run-it-in-a-second-environment]]).
    // The sibling test above never had this bug because it waits for content
    // that only the re-read can produce.
    await waitFor(() => expect(calls).toHaveLength(3))

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

  it('keeps the write confirmation on screen when the re-read actually takes a tick', async () => {
    // 🔴 This is the one test in this file whose stub is DELAYED, and the delay
    // is the whole test. Every other stub here resolves inside the same React
    // batch, so the `loading` state never commits, the page never unmounts, and
    // the bug this guards against is invisible.
    //
    // With a real network round-trip it is not: `reload()` put the resource back
    // to `loading`, ResourceView swapped the page for a spinner, the form
    // unmounted, and its `useWrite` state went with it — so the note saying
    // which file was written and that Kit did NOT commit it was destroyed by the
    // very reload the write triggered. Decision 2's guarantee is only a
    // guarantee if he can watch it hold. Found by running the real server in a
    // real browser, not here ([[green-suite-over-a-mock]]).
    const responses = [
      { ok: true, status: 200, body: habits },
      wrote,
      { ok: true, status: 200, body: habits },
    ]
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        await new Promise((r) => setTimeout(r, 0)) // a real tick, so `loading` commits
        const next = responses.shift()
        if (!next) throw new Error('unscripted fetch')
        return { ok: next.ok, status: next.status, statusText: 'x', json: async () => next.body }
      }),
    )

    render(
      <MemoryRouter initialEntries={['/projects/james-habits-app/behaviours/BEH-SEED-1']}>
        <Routes>
          <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }))

    const note = await screen.findByRole('status')
    expect(note).toHaveTextContent('behaviours/snip-it.beh')
    expect(note).toHaveTextContent(/Not committed/)

    // And it is still there after the re-read has landed — the failure was that
    // it appeared and was then thrown away, which an assertion racing the
    // reload could pass over.
    await screen.findByRole('heading', { level: 1 })
    expect(screen.getByRole('status')).toBeInTheDocument()
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
