import type { NewBehaviour, ProjectDetail, ProjectSummary, WriteResult } from './types'

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

async function get<T>(path: string): Promise<T> {
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
async function post<T>(path: string, body: unknown): Promise<T> {
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

export function addStep(app: string, id: string, step: string): Promise<WriteResult> {
  return post<WriteResult>(
    `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/steps`,
    { step },
  )
}

export function addBehaviour(app: string, behaviour: NewBehaviour): Promise<WriteResult> {
  return post<WriteResult>(`/api/projects/${encodeURIComponent(app)}/behaviours`, behaviour)
}

export function fetchProject(app: string): Promise<ProjectDetail> {
  // The name is a path segment on a route ui.js matches against the corpora it
  // found, so it is never joined into a filesystem path — but it still has to
  // survive the URL, e.g. an app named with a `#`.
  return get<ProjectDetail>(`/api/projects/${encodeURIComponent(app)}`)
}
