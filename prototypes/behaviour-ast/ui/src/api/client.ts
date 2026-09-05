import type { ProjectDetail, ProjectSummary } from './types'

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

export function fetchProjects(): Promise<{ projects: ProjectSummary[] }> {
  return get<{ projects: ProjectSummary[] }>('/api/projects')
}

export function fetchProject(app: string): Promise<ProjectDetail> {
  // The name is a path segment on a route ui.js matches against the corpora it
  // found, so it is never joined into a filesystem path — but it still has to
  // survive the URL, e.g. an app named with a `#`.
  return get<ProjectDetail>(`/api/projects/${encodeURIComponent(app)}`)
}
