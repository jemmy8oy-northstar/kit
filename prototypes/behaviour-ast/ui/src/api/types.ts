// The shapes `ui.js` serves. Hand-written on purpose: web-template generates
// its client from an OpenAPI document, and Kit's read API is a plain Node
// server with no spec to generate from. Keeping these types next to the
// fetchers means one file to change if a route's payload changes.
//
// ⚠️ The two coverage shapes are NOT the same object. `/api/projects` reports
// `covered: null` explicitly; `/api/projects/<app>` omits the key entirely when
// coverage is unavailable. Both mean the same thing and neither means zero.

export interface CoverageAvailable {
  available: true
  covered: number
  uncovered: number
}

export interface CoverageUnavailable {
  available: false
  /** Present on the list endpoint, absent on the detail endpoint. Never 0. */
  covered?: null
  uncovered?: null
  /** Why nothing could be read — this is the sentence the UI must show. */
  reason: string
}

export type Coverage = CoverageAvailable | CoverageUnavailable

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
  behaviours?: number
  conflicts?: number
  coverage?: Coverage
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

/** `/api/projects/<app>`. */
export interface ProjectDetail {
  app: string
  corpus: string
  behaviours: Behaviour[]
  conflicts: Conflict[]
  generated: Generated[]
  coverage: Coverage
  adjudication: Adjudication
  surface: { errors: string[]; served: string[]; unserved: string[] }
  questions: unknown[]
}
