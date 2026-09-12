import type {
  Binding, BindResult, NewBehaviour, ProjectDetail, ProjectSummary, ReviewState, SessionState,
  WriteResult,
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
      `Could not reach the Kit read API at ${path}. Start it with \`node ui.js\`.`,
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
      `Could not reach the Kit write API at ${path}. Start it with \`node ui.js\`.`,
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

export function fetchProjects(): Promise<{ projects: ProjectSummary[] }> {
  return get<{ projects: ProjectSummary[] }>('/api/projects')
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
export function signIn(password: string): Promise<{ ok: true; signedIn: true }> {
  return post<{ ok: true; signedIn: true }>('/api/session', { password })
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
 * Bind a noun — stage 4, and the only write here whose file is not this app's
 * corpus.
 *
 * `app` is still in the path and is not decoration: `bindings.json` is one flat
 * map over every corpus, so the server answers with `sharedWith` — the OTHER
 * corpora that reference this noun and will now generate against this binding —
 * and it cannot work out which corpora are "other" without being told which one
 * you are in.
 */
export function addBinding(app: string, noun: string, binding: Binding): Promise<BindResult> {
  return post<BindResult>(`/api/projects/${encodeURIComponent(app)}/bindings`, { noun, binding })
}

export function fetchProject(app: string): Promise<ProjectDetail> {
  // The name is a path segment on a route ui.js matches against the corpora it
  // found, so it is never joined into a filesystem path — but it still has to
  // survive the URL, e.g. an app named with a `#`.
  return get<ProjectDetail>(`/api/projects/${encodeURIComponent(app)}`)
}
