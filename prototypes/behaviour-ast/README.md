# Kit prototype — behaviour tree → test

Answers one falsifiable question from [claude-code-bot#68](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/68):

> **Can a behaviour tree generate a runnable test with no hand-written glue?**

Not a product, not a PR, not queued for build. It exists so the design argument
on #68 rests on something that ran. `node kit.js` prints the generated tests and
the measurements quoted on the thread.

## Why this question and not "is the format nice"

James's design — natural language → an abstract tree with holes → tests — is
**Gherkin's shape**, and Gherkin is 20 years old and did not deliver this. It
is worth being precise about why, because the failure is not the syntax:

| | Gherkin / Cucumber | what #68 needs |
|---|---|---|
| binding unit | one step definition **per step phrasing** | — |
| glue grows with | the **spec** (unbounded) | the **app's vocabulary** (bounded) |
| spec constrains the app? | **no** — glue absorbs any drift | must, or it rots |
| holes / unknowns | none | first class |
| cross-document resolution | none | the whole idea |

The rot mechanism is specific: because a step binds to a hand-written function,
a spec sentence can say anything and the glue makes it pass. So the spec becomes
**descriptive**, and a descriptive spec is a wish. `testing-strategy.md` in
web-template is the local example — genuinely good, referenced by nothing, and
it therefore changed nothing.

**The bet in this prototype:** bind by **noun**, not by step. `button:Cut` gets
one binding, reused by every behaviour that mentions it, and a noun the app does
not have is a build failure rather than a passing test.

## What it does

1. **parse** — line-based on purpose. His requirement is that a human can write
   the tree by hand; YAML and JSON both fail that on punctuation alone.
2. **resolve** — builds one symbol table across the whole corpus, so a hole in
   one behaviour is filled by a `provides` in another. This is his *"unknowns can
   get filled in when other parts are filled out"*, and it is the architectural
   consequence worth naming: **behaviours cannot be isolated trees.** A per-file
   AST can never resolve a hole from elsewhere.
3. **generate** — emits Playwright, or emits `// UNGENERATED` and says which
   noun was unbound. It never emits a test that silently asserts less.
4. **coverage** — a behaviour with no test naming its ID is a failure. The only
   part that can go red, and therefore the only part that can't be politely
   ignored.
5. **surface** — James, [kit#3](https://github.com/jemmy8oy-northstar/kit/pull/3):
   *"the api layer is inferred from what needs to be displayed in the ui… expose
   only what is required to display."* So Kit has **no HTTP assertion layer**,
   and instead an inferred behaviour says which documented one it exists for
   (`serves BEH-X`). An inference that serves nothing documented is API surface
   nothing displays — printed, never guessed at. A broken `serves` link exits 1.

## The notation, whole

```
behaviour BEH-ID "one sentence a human would say"
  source   defined docs/DESIGN.md#mvp-3 | inferred backend/Foo.cs:BarTest
  review   unreviewed | approved | denied <the correction, required on a denial>
  serves   BEH-OTHER            # inferred only; the documented behaviour this exists for
  actor    owner
  given / when / then <verb> <kind>:<Noun> [with ?hole]
  provides <kind>:<Noun>.<slot> = <value>
  contract <prose an assertion cannot honestly own>
```

Silence is asymmetric on purpose. No `source` means a human wrote it; no `review`
on an inference means unreviewed; no `serves` on an inference is the finding, and
no `serves` on a defined behaviour is normal — a documented behaviour is served,
it does not serve.

`node mutate.js` breaks each rule above in turn and checks the suite goes red for
it. 11/11 killed today; a SURVIVED line means a rule the tests only appear to
enforce.

## The two design consequences that fell out of building it

**Inference must be a line in the tree, not a fact in a chat log.** James wants
the LLM to infer behaviours and the user to approve/deny by default-include.
Approve/deny needs something to point at *later*, so an inference is written as
an explicit `provides` line attributed to the behaviour that implied it. An
inference that lives only in the conversation cannot be denied six weeks on.

**Structural conflict detection is free; semantic is not.** Two behaviours
asserting different values for the same `noun.slot` is his *"this conflicts with
a previous behaviour, supersede?"* — and it needs no embeddings and no LLM, it
falls straight out of the symbol table. That is worth knowing before scoping the
vectorisation extension: the cheap half of the differentiated feature is a
`Map`.
