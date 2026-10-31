import { fetchProject, fetchProjects } from './api/client'
import type { ProjectDetail } from './api/types'

/**
 * BEH-FEAT-5 (kit#246): the files a project page draws from. A name that is a project of
 * several files — or whose one file has another name — is every file naming it; anything
 * else is the one file, as it always was. A list that cannot be read falls back to the one
 * file: the page is still true of it, only no longer of its siblings.
 *
 * `known` is a file the caller has already read (an item page has its own): it is reused, not
 * fetched again, so a single-file project costs only the list.
 */
export async function filesOf(app: string, known?: ProjectDetail): Promise<ProjectDetail[]> {
  const rows = await fetchProjects().then(
    (r) => r.projects ?? [],
    () => [],
  )
  const names = rows.filter((r) => !r.error && (r.project ?? r.app) === app).map((r) => r.app)
  const read = (name: string) => (known && known.app === name ? known : fetchProject(name))
  if (names.length > 1 || (names.length === 1 && names[0] !== app)) {
    return Promise.all(names.map(read))
  }
  return [await read(app)]
}
