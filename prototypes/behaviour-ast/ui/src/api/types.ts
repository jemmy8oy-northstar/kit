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
}

/** The body `POST /api/projects/<app>/behaviours` takes. */
export interface NewBehaviour {
  id: string
  title: string
  actor?: string
  steps?: string[]
}

/**
 * What both write routes return.
 *
 * `committed` is always `false` and is sent on every write on purpose — decision
 * 2 in `docs/design/ui.md` stops at the working tree, and a caller that assumes
 * otherwise should find out here rather than when the branch turns out empty.
 * The UI renders it for the same reason: the boundary is only a guarantee to him
 * if he can see it holding.
 */
export interface WriteResult {
  ok: true
  app: string
  behaviour: string
  file: string
  committed: false
  note: string
}
