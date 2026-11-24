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

import type { RepoFile } from '../stackDiagram'

export interface CoverageCountsAvailable {
  available: true
  covered: number
  uncovered: number
  /** BEH-HEALTH-2: the latest PR that improved this project's test health. Absent, never null, when none is named. */
  lastImprovedBy?: LastImprovedBy
}

export interface LastImprovedBy {
  /** Short name, e.g. `kit#538`. */
  pr: string
  url: string
  /** `yyyy-MM-dd`. */
  on: string
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

/**
 * A repo in the signed-in user's own GitHub that carries a corpus (BEH-USERS-2). Kit keeps no
 * record of it; it has no page of its own yet, so the row links to the repo on GitHub.
 */
export interface GitHubProject {
  name: string
  fullName: string
  url: string
  /** The `.beh` files on its default branch: why it counts as a project. */
  corpus: string[]
}

/** The body of `/api/projects`. */
export interface ProjectListBody {
  projects: ProjectSummary[]
  /** Present only for a user whose GitHub is connected and readable. */
  gitHub?: GitHubProject[]
  /** Present instead of `gitHub` when GitHub could not be read: an empty list would read as "no projects". */
  gitHubError?: string
}

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
  /**
   * The project this corpus is shown under (kit#237): its `# kit:project` directive, or its
   * own name. Files sharing one are one project. Absent on an error row.
   */
  project?: string
  /** A `# kit:idea` file (kit#284): app ideas, not a project. Absent, never false, otherwise. */
  idea?: boolean
  /**
   * `"self"` when the agent may promote this project's `dev` to `main` itself (BEH-SETTINGS-4): its
   * primary file carries `# kit:promote self`. Absent, never false or null, otherwise.
   */
  promote?: 'self'
  /**
   * `"maintained"` when the project is kept going, not worked towards (BEH-NARR-2): its primary
   * file carries `# kit:goal maintained`. Absent, never false or null, otherwise.
   */
  goal?: 'maintained'
  /**
   * `"ask"` or `"wait"` when the project says what the agent does when specs conflict
   * (BEH-CONFLICT-2): its primary file carries `# kit:conflicts ask|wait`. Absent, never null,
   * for the default, build anyway.
   */
  onConflict?: 'ask' | 'wait'
  /**
   * `"goals"` or `"architecture"` when the project says how involved its user is (BEH-ARCH-3): its
   * primary file carries `# kit:depth goals|architecture`. Absent, never null, for the default, behaviours.
   */
  depth?: 'goals' | 'architecture'
}

/** How involved the user chooses to be (BEH-ARCH-3). `behaviours` is the default and is no line. */
export type DepthMode = 'goals' | 'behaviours' | 'architecture'

/** What the agent does when two specs conflict (BEH-CONFLICT-2). `build` is the default and is no line. */
export type ConflictMode = 'build' | 'ask' | 'wait'

/** When he last asked the agent to wake (BEH-SETTINGS-7): ISO 8601 UTC, or null when never asked. */
export interface WakeSetting {
  requested: string | null
}

/** The agent's kinds of idle work (BEH-SETTINGS-6). One not in the order is switched off. */
export type IdleItem = 'health' | 'ideas'

export interface IdleSetting {
  /** The first item on, or `idle` when none is (BEH-SETTINGS-5's field, still served). */
  idle: string
  /** The items switched on, highest priority first. Absent from the corpus means `['health']`. */
  order: IdleItem[]
}

/** The two groups a Kit feature is switched for (BEH-FLAGS-1). */
export type SwitchGroup = 'operator' | 'testers'

export interface FeatureSwitch {
  id: string
  title: string
  /** On for him. On unless a `# kit:switch` line says off. */
  operator: boolean
  /** On for test users. Off unless a `# kit:switch` line says on. */
  testers: boolean
}

/** What `GET /api/settings/features` returns: the caller's group, what is off for it, and (operator only) every switch. */
export interface FeatureSwitches {
  group: SwitchGroup
  off: string[]
  features?: FeatureSwitch[]
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
  /** Marked `pending` in the corpus: specified, not built. */
  pending: boolean
  /** The layer the behaviour belongs to, as the server derives it. */
  layer: string
  /** `<corpus>.beh:<line>` — where a human goes to edit it. */
  at: string
  /** The features it is tagged into, one per `in` line (kit#237); empty when untagged. */
  in: string[]
  /** The architecture decisions it rests on, one per `depends-on` line (BEH-ARCH-1 slice B2); absent when none. */
  dependsOn?: string[]
  /** 1 (first) to 5, from a `priority` line (kit#284); absent when there is none. */
  priority?: number
  /** A `paused` line (BEH-BOARD-16): held on the Backlog, not built. Absent when not paused. */
  paused?: boolean
  /** `paused until YYYY-MM-DD` (BEH-BOARD-26): when it leaves his Inbox no longer. Absent for a bare `paused`. */
  pausedUntil?: string
  /** A `needs-context` line (BEH-BOARD-17): what he must add before the agent can go on. Absent when there is none. */
  needsContext?: string
  /** An `on-dev` line (BEH-BOARD-22): built on the project's dev, not live; what will ship it. Absent when there is none. */
  onDev?: string
}

/** A `feature` block (kit#237): a page or part of one. Its parents are tags, so there can be several. */
export interface Feature {
  id: string
  title: string
  in: string[]
  at: string
  /** Its `stage` line (FEAT-STAGES); absent when it is not on the stages board. */
  stage?: Stage
  /** The epics it is in (BEH-EPIC-2), kept apart from `in`; absent when none. */
  epics?: string[]
}

/** An `epic` block (BEH-EPIC-2): defined top down, joined by features. */
export interface Epic {
  id: string
  title: string
  at: string
}

/** The five stages, in his order (BEH-STAGE-1). */
export type Stage = 'problem' | 'ux' | 'logic' | 'architecture' | 'development' | 'ui'

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

/** The four states `parse()` accepts on a `review` line, and nothing else; `requested` is kit#284's. */
export type ReviewState = 'unreviewed' | 'approved' | 'requested' | 'denied'

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
  /** Empty for a corpus that declares no features — it is then shown as a flat list, as before. */
  features: Feature[]
  /** See `ProjectSummary.project`. */
  project: string
  /** Its `# kit:principle` lines (kit#284 slice 5), in file order. Absent when it declares none. */
  principles?: Principle[]
  /** Its `epic` blocks (BEH-EPIC-2), in file order. Absent when it declares none. */
  epics?: Epic[]
  /** Its `decision` blocks (BEH-ARCH-1), in file order. Absent when it declares none. */
  decisions?: Decision[]
  /** `"maintained"` on a project's primary file that carries `# kit:goal maintained` (BEH-NARR-2). Absent otherwise. */
  goal?: 'maintained'
  /** BEH-HEALTH-4: whether its recorded test links still hold. Absent when no link has a hash recorded. */
  links?: LinkHealth
  /** BEH-ARCH-4: the repo's own files the stack diagram is drawn from. Absent until the server serves them. */
  repoFiles?: RepoFile[]
}

/** A test link whose recorded hash no longer matches the code it exercises. */
export interface BrokenLink {
  behaviourId: string
  title: string
  reason: string
}

export type LinkHealth =
  | { available: true; recorded: number; broken: BrokenLink[] }
  | { available: false; reason: string }

/** The parts of a system an architecture decision belongs to, in the order the page shows them. */
export type DecisionPart = 'frontend' | 'backend' | 'data' | 'deployment'

/** A `decision` block (BEH-ARCH-1): what was chosen, what else was considered, and why. */
export interface Decision {
  id: string
  title: string
  part: DecisionPart
  chosen: string
  considered: string[]
  because: string[]
  review: { state: ReviewState; note: string | null }
  source: { origin: string; ref: string | null }
  at: string
}

/** One `# kit:principle [global] <Name>: <what it means>` line. */
export interface Principle {
  name: string
  text: string
  /** Holds for every project, not just the one that declares it. */
  global: boolean
  /** `<corpus>.beh:<line>`, where it is changed. */
  at: string
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
   * ⚠️ It used to mean binding a noun here CHANGED what these generate, because
   * bindings were one flat map over every corpus. Since kit#66 each corpus binds
   * its own nouns in `<app>.bindings.json`, so this is a fact about naming and no
   * longer a hazard. Always an array: "nothing shares this name" and "nobody
   * looked" must not both arrive as `undefined`.
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
 * A binding value, as `<app>.bindings.json` stores it and `emit()` reads it.
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
 * carries the two facts no corpus write has. `sharedWith` used to be the moment
 * the global namespace stopped being a habit — under one flat map, the person
 * who just clicked was the only one who could say whether sharing the noun with
 * those corpora was what they meant. kit#66 removed the sharing, so it now
 * reports which other projects use the same NAME and bind it themselves.
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
 * a bind without its shared-name note and still typecheck. Narrow on `'noun'
 * in result` where the difference matters.
 */
export type AnyWriteResult = WriteResult | BindResult | NoteResult

/** Where a note says it came from (BEH-NOTE-15). */
export type NoteOrigin = 'chat' | 'note-button' | 'agent'

/** One open note (kit#118): free text, not a behaviour, waiting to be folded into the spec. */
export interface Note {
  /** Where it came from (BEH-NOTE-15); absent or null when it did not say, which shows no tag. */
  from?: NoteOrigin | null
  /** UTC, to the minute: `2026-10-10 20:41Z`. */
  at: string
  /**
   * `note`, a free comment; `request`, a behaviour asked for in prose (BEH-NOTE-4);
   * `resolution`, how a decision on the question sheet should go (BEH-NOTE-8); `bell`, an item
   * rung for the agent to look at again (BEH-BELL-1), whose text may be empty.
   */
  kind: 'note' | 'request' | 'resolution' | 'bell'
  /** The behaviour it was left on, or the item a bell rings; null for one left on the project. */
  behaviour: string | null
  /** The question-sheet key a resolution answers; null for every other kind. */
  resolves: string | null
  /** What was typed, without anything written under it since. */
  text: string
  /** Questions and answers written under the note, oldest first (BEH-NOTE-5/6). */
  thread: NoteEntry[]
  /** The latest round of questions while it is unanswered; empty otherwise. */
  questions: string[]
}

/** One section under a note: the folder's questions, or the answer to them. */
export interface NoteEntry {
  kind: 'questions' | 'answer'
  at: string | null
  text: string
}

/** `GET /api/projects/<app>/notes`: every note still open, oldest first. */
/** `GET /api/projects/<app>/history` (BEH-BOARD-4): when each built behaviour was completed. */
export interface CompletionList {
  app: string
  file: string
  /** `none` when this server has no git history for the corpus, and `reason` says why. */
  history: 'git' | 'none'
  reason: string | null
  /**
   * Every built behaviour, in corpus order. `at` is ISO 8601, or null when the corpus is
   * built ahead of its newest commit — newer than anything git holds.
   */
  completed: { id: string; at: string | null }[]
  /**
   * BEH-BOARD-12: every behaviour, in corpus order, with when it was added — the first commit
   * holding it, or null when none does yet (the newest of all).
   */
  added: { id: string; at: string | null }[]
}

/** One commit's effect on one project (BEH-ACTIVITY-1). */
export interface ActivityEntry {
  commit: string
  /** ISO 8601 UTC. */
  at: string
  app: string
  /** The commit's own subject line. */
  summary: string
}

/** A project this server has no git history for, and why. */
export interface ActivityGap {
  app: string
  reason: string
}

/** `GET /api/activity` (BEH-ACTIVITY-1): recent commits that touched any corpus or its notes, newest first. */
export interface ActivityFeed {
  entries: ActivityEntry[]
  noHistory: ActivityGap[]
}

/** One agent run (BEH-USERS-9). */
export interface AgentRun {
  id: string
  project: string
  model: string
  status: string
  /** ISO 8601 UTC. */
  started: string
  /** Absent or null while the run is queued or running. */
  ended?: string | null
  inputTokens: number
  outputTokens: number
  cacheWriteTokens: number
  cacheReadTokens: number
  costUsd: number
  /** Why a failed run failed, as its runner reported it: `<step> exit <code>`. Null otherwise. */
  failure?: string | null
  /** The work-item slug a pipeline run was for (BEH-ORCH-6); null for triage and runs not started for one item. */
  workItem?: string | null
  /** The pipeline stage the run was for: triage, spec, build or review (BEH-ORCH-9); null for a run started by hand. */
  stage?: string | null
}

/** A run a runner has taken and not yet reported (BEH-ORCH-17). */
export interface RunClaim {
  runId: string
  /** ISO 8601 UTC: when a runner took it, the moment it began to run. */
  claimedAt: string
  /** ISO 8601 UTC: when its runner ends it for running too long; absent or null when its stage has no limit of its own. */
  givesUpAt?: string | null
  /** ISO 8601 UTC: when Kit counts its runner lost if it has still not reported (BEH-ORCH-14). */
  lostAt: string
}

/** `GET /api/runs` (BEH-USERS-9): every run newest first, and what one run may spend. */
export interface AgentRunList {
  capUsd: number
  runs: AgentRun[]
  /** The runs a runner is on now (BEH-ORCH-17); absent from a Kit older than it. */
  claims?: RunClaim[]
}

/** One run on the runs page's cards (BEH-UI-7, BEH-UI-8), from GET /api/runs/visibility. */
export interface RecentRun {
  id: string
  project: string
  workItem: string | null
  stage: string | null
  model: string
  /** The state the run record holds: queued, running, finished or failed. */
  status: string
  /** ISO 8601 UTC. */
  started: string
  ended: string | null
  /** Input, output and cache tokens together. */
  tokens: number
  failure: string | null
  /** The run's recorded session cost (BEH-COST-1). Null is unknown, never zero. */
  costUsd: number | null
}

/** An agent running now (BEH-VIS-1). */
export interface RunningAgent {
  id: string
  project: string
  workItem: string | null
  stage: string | null
  model: string
  started: string
  flag: string | null
  flaggedBy: string | null
}

/** One stage run of a finished work item (BEH-VIS-2). `costUsd` null is unknown, never zero. */
export interface FinishedStage {
  runId: string
  stage: string | null
  model: string
  costUsd: number | null
}

/** A review a finished work item requested: the stage that asked and the agent that did. */
export interface RequestedReview {
  runId: string
  stage: string | null
  requestedBy: string | null
}

/** A work item whose pipeline run has finished (BEH-VIS-2). */
export interface FinishedRun {
  project: string
  workItem: string
  stages: FinishedStage[]
  reviews: RequestedReview[]
}

/** `GET /api/runs/visibility` (BEH-VIS-1, BEH-VIS-2, BEH-UI-7, BEH-UI-8). Operator only. */
export interface RunVisibility {
  running: RunningAgent[]
  finished: FinishedRun[]
  /** Every run, newest start first. */
  recent: RecentRun[]
}

/** One work item of the pipeline (BEH-ORCH-9). */
export interface PipelineItem {
  project: string
  slug: string
  /** triage, spec, build or review. */
  stage: string
  /** queued, running, stopped or done. */
  status: string
  stoppedBecause?: string | null
  pullRequest?: string | null
  /** Kit merged the pull request (BEH-ORCH-10). */
  merged?: boolean
  /** Why a built pull request is not merged yet: merging is off, or what it still waits for (BEH-ORCH-10). */
  mergeNote?: string | null
  /** The bundle's Flag line, without "Flag: ". */
  flag?: string | null
  /** agreed, revised or escalated; null when it was not reviewed. */
  review?: string | null
  reviewNote?: string | null
}

/** `GET /api/pipeline` (BEH-ORCH-9): whether the pipeline is on, and its items since Kit last started, oldest first. */
export interface PipelineItemList {
  on: boolean
  items: PipelineItem[]
}

/** One comment on a ticket (BEH-ORCH-16): a question, a block reason, or a reply. */
export interface TicketComment {
  /** "kit" for a comment Kit posted. */
  author: string
  body: string
  /** ISO 8601 UTC. */
  at: string
}

/** One ticket on `GET /api/projects/:project/tickets` (BEH-ORCH-16). */
export interface Ticket {
  slug: string
  title: string
  /** new, ready, planned, built, merged, or stopped (a row from before BEH-ORCH-16). */
  state: string
  priority: number
  cards: string[]
  pullRequest?: string | null
  /** The blocked tag's reason; null when the ticket is not blocked. */
  blockedBecause?: string | null
  /** Oldest first. */
  comments: TicketComment[]
}

export interface TicketList {
  on: boolean
  tickets: Ticket[]
}

/** One message from the agent (BEH-NOTIFY-1). */
export interface AgentNotification {
  /** ISO 8601 UTC, as the agent wrote it. */
  at: string
  kind: 'wake' | 'sleep' | 'pr' | 'blocker' | 'done' | 'note'
  /** Plain text: never rendered as HTML. */
  text: string
}

/** `GET /api/notifications` (BEH-NOTIFY-1): the agent's messages, newest first, or why they could not be read. */
export interface NotificationFeed {
  entries: AgentNotification[]
  /** Lines of the feed that were not a notification. */
  skipped: number
  /** Null when the feed was read, even if it is empty. */
  reason: string | null
  /** When the agent next wakes (BEH-NOTIFY-5); null only on another user's feed, where there is no agent. */
  schedule?: AgentSchedule | null
}

/** `GET /api/push` (BEH-NOTIFY-3): can this Kit push to a phone, and the key a browser subscribes with. */
export interface PushStatus {
  configured: boolean
  /** VAPID public key, base64url; null when push is not configured. */
  publicKey: string | null
}

/** `GET /api/triage` (BEH-SPECAGENT-4): can the Inbox talk a card through, with which model, and how many replies are left today. */
export interface TriageStatus {
  configured: boolean
  model: string | null
  remaining: number | null
}

/** One message in a triage chat: his, or the spec agent's. */
export interface TriageMessage {
  from: 'user' | 'agent'
  text: string
}

/** A triage turn's answer: the spec agent's reply, and the replies left today. */
export interface TriageAnswer {
  /** May be empty when the agent only acted. */
  reply: string
  remaining: number
  /**
   * BEH-CHAT-6, the Chat page only: what the agent asked to be done with the item in hand, which the page
   * carries out with his own writes and offers to undo. Absent for none.
   */
  action?: TriageAction
  /**
   * BEH-CHAT-10, the Chat page only: short answers the agent proposes to the question it asked, which the page
   * shows as buttons; tapping one sends it as his message. Absent for none.
   */
  options?: string[]
  /** BEH-CHAT-16: the model stopped for length, so the reply ends where the limit cut it. Absent otherwise. */
  truncated?: boolean
}

/**
 * BEH-CHAT-6: approve, deny or answer the item in hand (deny and answer with his words), undo the last, show
 * the item in hand again, or save a note on the project (not tied to the item; his 19:04Z note).
 */
export type TriageAction =
  | { kind: 'approve'; text?: null }
  | { kind: 'undo'; text?: null }
  | { kind: 'deny'; text: string }
  | { kind: 'answer'; text: string }
  | { kind: 'note'; text: string }
  /** BEH-SPECAGENT-3: a technical question for the coding agent, left as a note it answers. */
  | { kind: 'question'; text: string }
  | { kind: 'show'; text?: null }
  /** BEH-CHAT-12: a note, found by the item id it is on or its time, into the thread; read-only. */
  | { kind: 'shownote'; text: string }

/** The agent's pause file as its gate reads it (BEH-NOTIFY-5). */
export interface AgentSchedule {
  suspended: boolean
  /** ISO 8601 UTC the pause ends; null for a pause with no end. */
  until: string | null
  /** The agent paused itself, so his next action in Kit also wakes it (BEH-NOTIFY-4). */
  byAgent: boolean
  /** The agent's own reason for the pause. */
  why: string | null
  /** Why Kit cannot say; null when the file was read. */
  reason: string | null
}

export interface NoteList {
  app: string
  /** Where the notes live, whether or not the file exists yet. */
  file: string
  notes: Note[]
}

/** One behaviour on the schedule or in the backlog (BEH-SCHED-1..3). */
export interface ScheduleEntry {
  id: string
  /** Null when the corpus no longer has the id. */
  title: string | null
  /** `pending` still to build; `done` built since it was scheduled; `missing` gone from the corpus. Backlog is always `pending`. */
  state: 'pending' | 'done' | 'missing'
  /** The first still-pending entry on the schedule: the one an agent picks up. Never true in the backlog. */
  next: boolean
}

/** `GET /api/projects/<app>/schedule`: the schedule in order, then every pending behaviour it does not list. */
export interface ScheduleList {
  app: string
  /** Where the schedule lives, whether or not the file exists yet. */
  file: string
  scheduled: ScheduleEntry[]
  backlog: ScheduleEntry[]
}

/** What `POST /api/projects/<app>/notes` returns: a corpus write's shape, with the behaviour optional. */
export interface NoteResult extends GitOutcome {
  app: string
  behaviour: string | null
  file: string
}

/**
 * Whether this Kit has a password, and whether this browser is past it (kit#46).
 *
 * Two booleans rather than one tri-state, because they answer independent
 * questions and collapsing them loses the case that matters: a LOCAL Kit is
 * `{ required: false, signedIn: true }` and must never be shown a sign-in form,
 * while a deployed one before sign-in is `{ required: true, signedIn: false }`.
 * A single "canWrite" flag would make those two look the same on the way in.
 */
export interface SessionState {
  required: boolean
  signedIn: boolean
  /** BEH-USERS-1: present (true) only when the server has user accounts on. Absent means exactly the old Kit. */
  accounts?: true
  /** The signed-in user's name; only with `accounts`. */
  user?: string
  /** Only with `accounts`, and only when the signed-in person is the operator. Absent, never false. */
  operator?: true
  /** BEH-USERS-1: the GitHub account a non-operator user has connected; absent until they do. */
  gitHub?: string
  /** Where that user installs Kit's GitHub App; absent for the operator, or when this Kit cannot connect one. */
  gitHubInstall?: string
  /** Where they authorise it afterwards; carries a state signed for them alone. */
  gitHubAuthorize?: string
  /** BEH-USERS-10: where someone signed out signs in with GitHub; absent once signed in, or when this Kit cannot. */
  gitHubSignIn?: string
}

/**
 * What `POST /api/commit` returns (kit#155): the pull request that proposes every
 * pushed edit to the base branch. `opened` and `alreadyOpen` are both success and
 * kept apart, so a second press reads as "still waiting on review" rather than as
 * a second proposal. A refusal is a 409 whose `reason` the client throws.
 */
/** One commit on the edits branch that the base lacks (BEH-COMMIT-5), read back from Kit's own message. */
export interface PendingEdit {
  sha: string
  /** `adjudicate BEH-ORDER-1`: the message without `kit: ` or `(app)`. */
  summary: string
  app: string | null
  behaviour: string | null
  when: string | null
}

/** What `GET /api/projects/<app>/pending` returns: this project's share of the edits branch, newest first. */
export interface PendingView {
  app: string
  head: string | null
  base: string
  edits: PendingEdit[]
  pullNumber: number | null
  pullUrl: string | null
  /** Why the list may be incomplete or empty — write-back off, or GitHub's own words. */
  reason: string | null
}

export interface ProposeResult {
  opened: boolean
  alreadyOpen: boolean
  number: number | null
  url: string | null
}
