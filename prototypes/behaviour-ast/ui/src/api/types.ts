// The shapes `ui.js` serves. Hand-written on purpose: web-template generates
// its client from an OpenAPI document, and Kit's read API is a plain Node
// server with no spec to generate from. Keeping these types next to the
// fetchers means one file to change if a route's payload changes.
//
// ⚠️ THE TWO COVERAGE SHAPES ARE DIFFERENT OBJECTS UNDER ONE NAME, and the
// difference is not the one you would guess. `/api/projects` counts:
// `{ covered: 3, uncovered: 5 }`. `/api/projects/<app>` passes project.js's raw
// result through, so `covered` and `uncovered` are ARRAYS OF BEHAVIOUR IDS.
//
// Modelling both as `number` typechecks (JSON is `unknown` at the boundary) and
// renders `BEH-HOME-1BEH-EDIT-1… covered` on the screen — which is how this was
// found, by looking at the page rather than by the suite. Both shapes also
// signal unavailable differently: the list sends `covered: null`, the detail
// endpoint omits the key. Neither ever means zero.

export interface CoverageCountsAvailable {
  available: true
  covered: number
  uncovered: number
}

export interface CoverageIdsAvailable {
  available: true
  covered: string[]
  uncovered: string[]
}

export interface CoverageUnavailable {
  available: false
  /** `null` on the list endpoint, absent on the detail endpoint. Never 0. */
  covered?: null
  uncovered?: null
  /** Why nothing could be read — this is the sentence the UI must show. */
  reason: string
}

/** `/api/projects` — already counted. */
export type SummaryCoverage = CoverageCountsAvailable | CoverageUnavailable

/** `/api/projects/<app>` — the ids themselves. */
export type DetailCoverage = CoverageIdsAvailable | CoverageUnavailable

export type Coverage = SummaryCoverage | DetailCoverage

/** One row of `/api/projects`. */
export interface ProjectSummary {
  app: string
  /**
   * Set when the corpus would not parse. `ui.js` reports that AS an error
   * rather than as an app with zero behaviours, and so must the UI — every
   * other field is absent in that case.
   */
  error?: string
  corpus?: string
  /**
   * True when the corpus declares `# kit:not-a-real-app` — it describes an app
   * that does not exist (a trial). The list still shows it; the row marks it,
   * because 0% against a real app and 0% against nothing mean opposite things.
   */
  notReal?: boolean
  /**
   * The app this corpus is a second description of, when it declares
   * `# kit:duplicate-corpus <app>` — a spec written forwards from a brief for
   * software that DOES exist (claude-code-bot#92). `notReal` is false of these,
   * so it cannot carry them, and without a marker of their own the list would
   * show a trial and the project it was written against as two equal projects.
   * Names the app rather than being a boolean, because "which one is the real
   * project" is the only question a reader has on seeing it.
   */
  duplicateOf?: string | null
  behaviours?: number
  conflicts?: number
  coverage?: SummaryCoverage
  unreviewed?: number
}

export interface StepRef {
  kind: string
  name: string
}

export interface Step {
  kind: string
  verb: string
  text: string
  refs: StepRef[]
  holes: string[]
}

export interface Behaviour {
  id: string
  title: string
  actor: string
  steps: Step[]
  open: unknown[]
  filled: unknown[]
  source: { origin: string; ref: string | null }
  review: { state: string; note: string | null }
  asks: unknown
  /** `<corpus>.beh:<line>` — where a human goes to edit it. */
  at: string
}

export interface Generated {
  id: string
  code: string
  /** Nouns the corpus names that nothing binds — the reason a step is a comment. */
  missing: string[]
  stats: { generated: number; contract: number; ungenerated: number }
}

export interface Conflict {
  key: string
  held: string[]
  holders: string[]
  challengers: { from: string; value: string[]; at: string }[]
}

export interface Adjudication {
  defined: number
  inferred: number
  unreviewed: string[]
  approved: string[]
  denied: string[]
  untraceable: string[]
}

/** The three states `parse()` accepts on a `review` line, and nothing else. */
export type ReviewState = 'unreviewed' | 'approved' | 'denied'

/**
 * One entry of the question sheet `kit.js` already builds, which `ui.js` has
 * always sent and this UI typed as `unknown[]` and never rendered.
 *
 * Two tiers, and the difference decides what the screen may offer:
 *
 * - **`review`** — an inference nobody has adjudicated. The answer is a
 *   vocabulary that already exists (`review approved` / `review denied <what is
 *   actually true>`), so the UI can write it.
 * - **`decision`** — two behaviours disagree, or an inference serves nothing.
 *   Kit has no syntax for recording "BEH-A supersedes BEH-B"; there is no
 *   keyword and no writer function, and inventing one would be a change to the
 *   corpus LANGUAGE. So these render with everything the sheet knows — the
 *   question, the options and their consequences, the recommendation — and no
 *   button. Showing the pack is new; deciding its grammar is not mine.
 */
export interface QuestionOption {
  label: string
  consequence: string
  at?: string
}

export interface QuestionSide {
  id: string
  title: string
  ref: string | null
  value: string[]
}

export interface Question {
  kind: string
  tier: 'decision' | 'review'
  key: string
  title: string
  /** Present on `review`-tier entries: the behaviour being adjudicated. */
  id?: string
  source?: { origin: string; ref: string | null }
  serves?: string[]
  contracts?: string[]
  asks: string | null
  options: QuestionOption[]
  recommend: { label: string; why: string } | null
  against: string | null
  /** Present on a conflict: the behaviours on each side of it. */
  sides?: QuestionSide[]
  owner?: string | null
}

/** `/api/projects/<app>`. */
export interface ProjectDetail {
  app: string
  corpus: string
  behaviours: Behaviour[]
  conflicts: Conflict[]
  generated: Generated[]
  coverage: DetailCoverage
  adjudication: Adjudication
  surface: { errors: string[]; served: string[]; unserved: string[] }
  questions: Question[]
  requires: Requires
}

/**
 * One key a binding must carry, and whether it does.
 *
 * `surface` is a sentence, not a code — it is the thing a person reads to know
 * what to type, and it comes from `requires.js` rather than being written again
 * here. A second copy in the frontend would be a second definition of the
 * grammar, which is the drift `writer.js` records against itself.
 */
export interface Need {
  id: string
  surface: string
  /** The verbs that asked for it. A noun owes `route` *because* something opens it. */
  verbs: string[]
  met: boolean
}

/** What one noun owes the app, and who else feels it being bound. */
export interface NounRequirement {
  noun: string
  kind: string
  name: string
  /** The behaviour ids that reference it. */
  usedBy: string[]
  /** Is there a binding at all — what `boundNouns()` counts. */
  bound: boolean
  /** Does that binding carry what every verb needs — what actually decides generation. */
  satisfied: boolean
  needs: Need[]
  binding: Binding | null
  /**
   * The OTHER corpora that reference this same noun name.
   *
   * `bindings.json` is one flat map over every corpus, so binding a noun here
   * changes what these generate too. Always an array: "nothing collides" and
   * "nobody looked" must not both arrive as `undefined`.
   */
  sharedWith: string[]
}

/**
 * Stage 4 of `docs/design/process.md`, projected.
 *
 * `missing` and `insufficient` are deliberately separate populations, because
 * collapsing them is the defect `requires.js` was written to expose: a binding
 * that EXISTS and carries nothing the verb needs is counted as bound by every
 * other measurement, and is the case a screen has no other way to explain.
 */
export interface Requires {
  nouns: NounRequirement[]
  /** No binding at all. */
  missing: NounRequirement[]
  /** A binding exists and satisfies no verb. */
  insufficient: NounRequirement[]
  /** Generatable — noun keys only, since there is nothing left to say about them. */
  satisfied: string[]
}

/**
 * A binding value, as `bindings.json` stores it and `emit()` reads it.
 *
 * Left open rather than a union of the six known shapes. `emit()` is the only
 * definition of which keys mean what, and a closed type here would have to be
 * edited in lockstep with it — the frontend would then be a second, quietly
 * drifting grammar. `requires.js` already tells the screen which keys THIS noun
 * owes, which is the question the form actually has.
 */
export type Binding = Record<string, unknown>

/** The body `POST /api/projects/<app>/behaviours` takes. */
export interface NewBehaviour {
  id: string
  title: string
  actor?: string
  steps?: string[]
}

/**
 * What git did with a write, on both write routes.
 *
 * Until kit#43 `committed` was the literal `false`: decision 2 in
 * `docs/design/ui.md` stopped at the working tree, and the type said so. James
 * then chose git over a database for a deployed Kit (kit#41), so `committed` is
 * now a real boolean and these fields say which of three things happened.
 *
 * 🔴 `committed` and `pushed` are separate because the interesting state is the
 * one between them. By the time git runs the file is already written, so a
 * failed push does not mean "your edit was lost" — it means "your edit is on a
 * disk nobody will read again". Collapsing them into one flag would report that
 * as success, and the person who typed the behaviour has already closed the tab.
 */
export interface GitOutcome {
  committed: boolean
  /** Absent when git write-back is off — the local default. */
  pushed?: boolean
  /** Short sha, when there is a commit to name. */
  commit?: string | null
  branch?: string | null
  note: string
  /**
   * Set ONLY when the edit is committed and did not reach the remote. Present
   * rather than inferred: a UI should not have to deduce trouble from the
   * absence of something.
   */
  warning?: string
}

/**
 * What both write routes return.
 *
 * The UI renders the git fields rather than assuming them, for the same reason
 * it always did: the boundary is only a guarantee to him if he can see it
 * holding, and now it is a boundary that can move.
 */
export interface WriteResult extends GitOutcome {
  ok: true
  app: string
  behaviour: string
  file: string
}

/**
 * What `POST /api/projects/<app>/bindings` returns.
 *
 * Not a `WriteResult`: it names a `noun` rather than a `behaviour`, and it
 * carries the two facts no corpus write has. `sharedWith` is the moment the
 * global namespace stops being a habit — the person who just clicked is the
 * only one who can say whether sharing this noun with those corpora is what
 * they meant, and this is when they are looking.
 */
export interface BindResult extends GitOutcome {
  ok: true
  app: string
  noun: string
  file: string
  sharedWith: string[]
  /**
   * Corpora that would not parse, so `sharedWith` is an INCOMPLETE answer.
   * Silence here would turn "could not look" into "nothing collides".
   */
  unreadableCorpora: string[]
}

/**
 * Any successful write, whatever it wrote.
 *
 * A union rather than a common base with `subject: string`, because the two
 * results genuinely differ in more than a name — a bind carries `sharedWith`
 * and a corpus write cannot — and flattening them would let a component render
 * a bind without its namespace warning and still typecheck. Narrow on `'noun'
 * in result` where the difference matters.
 */
export type AnyWriteResult = WriteResult | BindResult
