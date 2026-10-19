import { ApiError, fetchProject, fetchProjects } from './api/client'
import type { ProjectDetail } from './api/types'
import { isIdea, isTrial, projectOf } from './board'

export interface RealProjects {
  /** Each readable real project's detail, with the project it belongs to. */
  read: { app: string; project: string; detail: ProjectDetail }[]
  /** Files that could not be read, by name, so a board never shows less than Kit reads without saying so. */
  failed: { app: string; error: string }[]
}

const messageOf = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : String(e))

/** Every real project's detail, for the feature and epic boards. Ideas and trials are not work, so not here. */
export async function loadRealProjects(): Promise<RealProjects> {
  const { projects } = await fetchProjects()
  const wanted = projects.filter((p) => !isIdea(p) && !isTrial(p))
  const failed = wanted.filter((p) => p.error).map((p) => ({ app: p.app, error: p.error! }))
  const readable = wanted.filter((p) => !p.error)
  const details = await Promise.allSettled(readable.map((p) => fetchProject(p.app)))
  const read: RealProjects['read'] = []
  details.forEach((r, i) => {
    const p = readable[i]
    if (r.status === 'fulfilled') read.push({ app: p.app, project: projectOf(p), detail: r.value })
    else failed.push({ app: p.app, error: messageOf(r.reason) })
  })
  return { read, failed }
}
