import type {
  ActivityFeed, Binding, BindResult, CompletionList, NewBehaviour, NoteList, NoteResult, NotificationFeed, ProjectDetail, ProjectListBody,
  PendingView, ProposeResult, PushStatus, ReviewState, ScheduleList, SessionState, WriteResult,
} from './types'

// One fetcher, one rule: a failed request must produce a message, never an
// empty result. An empty list and a server that is not running look identical
// on screen otherwise, and the second is the one you need to act on
// ([[empty-means-two-things]]).

export class ApiError extends Error {
  readonly status: number | null

  constructor(message: string, status: number | null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * The path Kit is served under, prepended to every request (kit#49, rule 8).
 *
 * Applied HERE, inside the two fetchers, rather than at the ~9 call sites below.
 * That is not tidiness: a route added later would silently skip a per-call-site
 * prefix and fetch from the root, and on a shared host the root answers 200 with
 * a sibling app's SPA rather than failing ([[green-over-the-clients-question]]).
 * Inside `get`/`post` there is nowhere for a new route to go that misses it.
 *
 * `import.meta.env.BASE_URL` is vite's own echo of the `base` it built with — so
 * the browser reads the value the bundle was built for, not one configured twice.
 * It is `/` unless `KIT_BASE_PATH` was set, and the trailing slash is stripped so
 * an unprefixed build requests exactly the strings it always did.
 *
 * Read per call rather than once at module load, so a test can change it.
 */
function url(path: string): string {
  return import.meta.env.BASE_URL.replace(/\/$/, '') + path
}

async function get<T>(requested: string): Promise<T> {
  const path = url(requested)
  let res: Response
  try {
    res = await fetch(path)
  } catch {
    // The server is not running, or the proxy has nothing behind it. This is
    // the most likely failure by far when the UI is a local tool, so it gets
    // the sentence that tells you what to do about it.
    throw new ApiError(
      `Could not reach the Kit read API at ${path}. Start it with \`dotnet run --project backend/Balenthiran.Kit.WebApi\` from the repo root.`,
      null,
    )
  }

  if (!res.ok) {
    let reason = res.statusText
    try {
      const body = (await res.json()) as { reason?: string; error?: string }
      reason = body.reason ?? body.error ?? reason
    } catch {
      // A non-JSON error body is not itself an error; the status still is.
    }
    throw new ApiError(`${path} returned ${res.status}: ${reason}`, res.status)
  }

  return (await res.json()) as T
}

// The write half. `ui.js` shipped these two routes in kit#24 and nothing in the
// browser could reach them, so his step 3 — "creating new assertions based on
// what I see" — was a curl command, not a UI.
//
// ⚠️ `content-type: application/json` is not boilerplate here, and it is not
// what makes the write safe either. A `text/plain` POST is a CORS *simple*
// request that a hostile page can send with no preflight, which is why `ui.js`
// checks the Origin header before it reads the body. This header is declared
// because the server parses JSON; the defence is on the server, where an
// attacker cannot choose it.
async function post<T>(requested: string, body: unknown): Promise<T> {
  const path = url(requested)
  let res: Response
  try {
    res = await fetch(path, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    throw new ApiError(
      `Could not reach the Kit write API at ${path}. Start it with \`dotnet run --project backend/Balenthiran.Kit.WebApi\` from the repo root.`,
      null,
    )
  }

  if (!res.ok) {
    // Every refusal from `writer.js` arrives as a 409 whose `reason` is a
    // sentence about the request — "no behaviour BEH-9 in this corpus", "a step
    // is one line". That sentence IS the feature: it is what tells him why the
    // corpus would not take his edit, so it must reach the screen intact rather
    // than become "409".
    let reason = res.statusText
    try {
      const parsed = (await res.json()) as { reason?: string; error?: string }
      reason = parsed.reason ?? parsed.error ?? reason
    } catch {
      // A non-JSON error body is not itself an error; the status still is.
    }
    throw new ApiError(reason, res.status)
  }

  return (await res.json()) as T
}

export function fetchProjects(): Promise<ProjectListBody> {
  return get<ProjectListBody>('/api/projects')
}

/** What changed recently across every project, newest first, from git (BEH-ACTIVITY-1). */
export function fetchActivity(): Promise<ActivityFeed> {
  return get<ActivityFeed>('/api/activity')
}

/** The agent's messages, newest first, from the feed it appends to in its own repo (BEH-NOTIFY-1). */
export function fetchNotifications(): Promise<NotificationFeed> {
  return get<NotificationFeed>('/api/notifications')
}

/** Can this Kit push to a phone, and with which key (BEH-NOTIFY-3). */
export function fetchPush(): Promise<PushStatus> {
  return get<PushStatus>('/api/push')
}

/** Hand the server this device's push subscription (BEH-NOTIFY-3); sending the same endpoint again is harmless. */
export function subscribePush(subscription: PushSubscriptionJSON): Promise<PushStatus> {
  return post<PushStatus>('/api/push/subscriptions', subscription)
}

/** Ask the server to stop pushing to this device (BEH-NOTIFY-3). A POST: Kit serves GET and POST only. */
export function unsubscribePush(endpoint: string): Promise<PushStatus> {
  return post<PushStatus>('/api/push/subscriptions/remove', { endpoint })
}

/**
 * Is there a lock on this Kit, and am I past it? (kit#46)
 *
 * Asked before the app draws anything, because the two states the page has to
 * tell apart — "you must sign in" and "there is no sign-in here" — are not
 * distinguishable from a failed write. A local Kit is permanently the second
 * one and must never show a password field.
 */
export function fetchSession(): Promise<SessionState> {
  return get<SessionState>('/api/session')
}

/**
 * Sign in. The token never comes back to this code — it arrives as an HttpOnly
 * cookie the browser stores and this script cannot read, which is the point.
 * All we learn is whether it worked.
 */
export function signIn(password: string, username?: string): Promise<{ ok: true; signedIn: true }> {
  // No name sends exactly `{ password }`, as before accounts existed: that is
  // also how the operator signs in, so a Kit with accounts off never sees a new key.
  return post<{ ok: true; signedIn: true }>('/api/session', username ? { username, password } : { password })
}

/** BEH-USERS-1: a non-operator user changes their own password. The server's `reason` is thrown on refusal. */
export function changePassword(current: string, next: string): Promise<{ ok: true; user: string }> {
  return post<{ ok: true; user: string }>('/api/session/password', { current, next })
}

/** BEH-USERS-1: the operator adds a user. */
export function addUser(username: string, password: string): Promise<{ ok: true; user: string }> {
  return post<{ ok: true; user: string }>('/api/users', { username, password })
}

/** BEH-USERS-1: hand back what GitHub returned after the user authorised Kit's App. */
export function connectGitHub(code: string, state: string): Promise<SessionState> {
  return post<SessionState>('/api/github/connected', { code, state })
}

/** BEH-USERS-10: someone signed out hands back what GitHub returned, and gets the session cookie. */
export function signInWithGitHub(code: string, state: string): Promise<{ ok: true; signedIn: true }> {
  return post<{ ok: true; signedIn: true }>('/api/session/github', { code, state })
}

export function signOut(): Promise<{ ok: true; signedIn: false }> {
  return post<{ ok: true; signedIn: false }>('/api/session/end', null)
}

export function addStep(app: string, id: string, step: string): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/steps`,
    { step },
  )
}

/**
 * Replace step `index` of a behaviour (BEH-ACT-2), counted from 0 in the order
 * the read API lists them. The server splices that one line, so every comment
 * around it survives, and refuses a line that is not a step.
 */
export function updateStep(app: string, id: string, index: number, step: string): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/steps/${index}`,
    { step },
  )
}

/** Replace a behaviour's title (BEH-ACT-2), and nothing else on its header line. */
export function retitle(app: string, id: string, title: string): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/title`,
    { title },
  )
}

export function addBehaviour(app: string, behaviour: NewBehaviour): Promise<WriteResult> {
  return post<WriteResult>(`/api/projects/${encodeURIComponent(app)}/behaviours`, behaviour)
}

/**
 * Adjudicate a behaviour — the fourth verb of his loop, "what the desired
 * behaviour really is".
 *
 * `note` is always sent, `null` included. A denial without a correction is
 * refused by `parse()` and must be, but the refusal belongs on the server: this
 * client omitting the key on an empty note would make "he typed nothing" and
 * "the field was not on the form" the same request, and only the first is a
 * mistake worth a sentence.
 */
export function setReview(
  app: string,
  id: string,
  state: ReviewState,
  note: string | null = null,
): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/review`,
    { state, note },
  )
}

/**
 * Pause a behaviour, or resume it (BEH-BOARD-16): the state is named, not toggled, so a double press
 * writes nothing new. It adds or removes the corpus's `paused` line.
 */
export function setPaused(app: string, id: string, paused: boolean): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/paused`,
    { paused },
  )
}

/**
 * Let the agent promote a project's `dev` to `main` itself, or stop it (BEH-SETTINGS-4): named, not
 * toggled. It adds or removes the `# kit:promote self` line in the project's own corpus. Operator only.
 */
export function setPromote(project: string, promote: boolean): Promise<WriteResult> {
  return post<WriteResult>(`/api/projects/${encodeURIComponent(project)}/promote`, { promote })
}

/** Set a behaviour's priority, 1 (first) to 5 (BEH-INBOX-1): it rewrites the corpus's `priority` line. */
export function setPriority(app: string, id: string, priority: number): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/priority`,
    { priority },
  )
}

/**
 * Remove a behaviour (BEH-ACT-3). The id is in the path and the body is an empty
 * object: the server refuses any field at all, so there is nothing here that
 * could be mistaken for a way to say more than "remove this".
 *
 * The refusal that matters — another behaviour still `serves` or `cites` this
 * one — is a 409 whose reason names the referrer, and it reaches the screen
 * through `post` like every other refusal.
 */
export function removeBehaviour(app: string, id: string): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/remove`,
    {},
  )
}

/** The project's open notes (kit#118), oldest first. */
/** When each built behaviour of the project was completed, from git (BEH-BOARD-4). */
export function fetchHistory(app: string): Promise<CompletionList> {
  return get<CompletionList>(`/api/projects/${encodeURIComponent(app)}/history`)
}

export function fetchNotes(app: string): Promise<NoteList> {
  return get<NoteList>(`/api/projects/${encodeURIComponent(app)}/notes`)
}

/**
 * Leave free text on a project, or on one behaviour of it (kit#118). It goes to
 * `<app>.notes.md` beside the corpus, never into the `.beh`: a note is not a
 * behaviour, and nothing generates from it. `behaviour` is omitted, not sent
 * null, when the note is about the project.
 */
export function leaveNote(app: string, text: string, behaviour?: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes`, {
    text,
    ...(behaviour ? { behaviour } : {}),
  })
}

/**
 * Ask for a behaviour in prose (BEH-NOTE-4). It is a note of kind `request`:
 * whoever folds it writes the id, title, actor and steps, so none is asked for.
 */
export function askForBehaviour(app: string, text: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes`, { text, kind: 'request' })
}

/** Say how a decision should go (BEH-NOTE-8): a note naming the decision's key. */
export function resolveDecision(app: string, key: string, text: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes`, { text, kind: 'resolution', resolves: key })
}

/**
 * Ring an item's bell (BEH-BELL-1): a note of kind `bell` asking the agent to look at the item
 * again. It needs no words. Ringing an item that is already rung is refused with 409.
 */
export function ringBell(app: string, item: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes`, { kind: 'bell', behaviour: item })
}

/**
 * Answer a note's questions (BEH-NOTE-6). The note is named by its place in the
 * list AND its date, so a file that changed underneath is refused, not misapplied.
 */
export function replyToNote(app: string, index: number, at: string, text: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes/reply`, { index, at, text })
}

/** Dismiss a note (BEH-NOTE-7). Git history keeps it. */
export function dismissNote(app: string, index: number, at: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/notes/dismiss`, { index, at })
}

/** The project's schedule and backlog (BEH-SCHED-1). */
export function fetchSchedule(app: string): Promise<ScheduleList> {
  return get<ScheduleList>(`/api/projects/${encodeURIComponent(app)}/schedule`)
}

/**
 * Change the schedule (BEH-SCHED-2): `add` a backlog behaviour to the end, `remove`
 * one back to the backlog, or move one `up` or `down` a place. Only `<app>.schedule`
 * is written; the corpus is not touched, so the result has a note's shape.
 */
export function reschedule(app: string, action: 'add' | 'remove' | 'up' | 'down', id: string): Promise<NoteResult> {
  return post<NoteResult>(`/api/projects/${encodeURIComponent(app)}/schedule`, { action, id })
}

/**
 * Bind a noun — stage 4, and the write that touches this app's bindings rather
 * than its corpus.
 *
 * `app` selects the FILE since kit#66 (`<app>.bindings.json`, beside the corpus),
 * where it used to select only whose warning you got: bindings were one flat map
 * over every corpus. It is still what makes `sharedWith` answerable either way —
 * the server cannot work out which corpora are "other" without being told which
 * one you are in.
 */
export function addBinding(app: string, noun: string, binding: Binding): Promise<BindResult> {
  return post<BindResult>(`/api/projects/${encodeURIComponent(app)}/bindings`, { noun, binding })
}

/**
 * Propose every pushed edit for merging, as one pull request from the edits
 * branch (kit#155). There is nothing to choose, so the body is empty: the server
 * knows the one edits branch and the one base.
 */
export function proposeEdits(): Promise<ProposeResult> {
  return post<ProposeResult>('/api/commit', {})
}

/** This project's edits on the edits branch that have not reached the base, and their pull request (BEH-COMMIT-5). */
export function fetchPending(app: string): Promise<PendingView> {
  return get<PendingView>(`/api/projects/${encodeURIComponent(app)}/pending`)
}

export function fetchProject(app: string): Promise<ProjectDetail> {
  // The name is a path segment on a route ui.js matches against the corpora it
  // found, so it is never joined into a filesystem path — but it still has to
  // survive the URL, e.g. an app named with a `#`.
  return get<ProjectDetail>(`/api/projects/${encodeURIComponent(app)}`)
}
