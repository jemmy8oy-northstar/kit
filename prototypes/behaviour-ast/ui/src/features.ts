import type { Behaviour, Feature, ProjectSummary } from './api/types'

/** A project's tag graph as the projects view reads it (kit#237, docs/design/features.md). */
export interface Tagged {
  features: Feature[]
  behaviours: Behaviour[]
}

/** BEH-FEAT-1: the top level is everything with no `in` line. */
export function topLevel(t: Tagged): Tagged {
  return {
    features: t.features.filter((f) => f.in.length === 0),
    behaviours: t.behaviours.filter((b) => b.in.length === 0),
  }
}

/** BEH-FEAT-2: a child is anything tagged into this feature; one thing in two features is under both. */
export function childrenOf(t: Tagged, id: string): Tagged {
  return {
    features: t.features.filter((f) => f.in.includes(id)),
    behaviours: t.behaviours.filter((b) => b.in.includes(id)),
  }
}

/**
 * Every behaviour anywhere beneath a feature, each counted once — a card in two child
 * features is one behaviour, not two. The parser refuses loops, so the walk ends; the
 * visited set is what keeps a diamond from being walked twice.
 */
export function behavioursUnder(t: Tagged, id: string): Set<string> {
  const found = new Set<string>()
  const seen = new Set<string>()
  const walk = (fid: string) => {
    if (seen.has(fid)) return
    seen.add(fid)
    const c = childrenOf(t, fid)
    c.behaviours.forEach((b) => found.add(b.id))
    c.features.forEach((f) => walk(f.id))
  }
  walk(id)
  return found
}

/**
 * BEH-FEAT-3: the path you came by, from the url. With several parents there is no single
 * path, so it is carried rather than derived — and only kept while each step really is a
 * parent of the next, so a stale or hand-edited link falls back to no path instead of
 * drawing crumbs that are not true.
 */
export function pathFrom(param: string | null, t: Tagged, id: string): string[] {
  const ids = (param ?? '').split(',').filter(Boolean)
  const byId = new Map(t.features.map((f) => [f.id, f]))
  const chain = [...ids, id]
  for (let i = 1; i < chain.length; i++) {
    const f = byId.get(chain[i])
    if (!byId.has(chain[i - 1]) || !f || !f.in.includes(chain[i - 1])) return []
  }
  return ids
}

/** The url of a feature page, reached by `path`, keeping any layer filter (BEH-KIND-2). */
export function featureHref(app: string, id: string, path: string[], layers: string[] = []): string {
  const base = `/projects/${encodeURIComponent(app)}/features/${encodeURIComponent(id)}`
  const query = [
    ...(path.length ? [`path=${encodeURIComponent(path.join(','))}`] : []),
    ...(layers.length ? [`layer=${encodeURIComponent(layers.join(','))}`] : []),
  ]
  return query.length ? `${base}?${query.join('&')}` : base
}

/**
 * BEH-KIND-2: only the behaviours of the chosen layers; every feature stays, so the tag graph
 * is intact and a feature with nothing of those layers beneath it counts zero. None chosen is all.
 */
export function ofLayers(t: Tagged, layers: string[]): Tagged {
  if (!layers.length) return t
  return { features: t.features, behaviours: t.behaviours.filter((b) => layers.includes(b.layer)) }
}

/** The features worth a card under a filter: those with something beneath them. Unfiltered, all of them. */
export function withAnyUnder(t: Tagged, features: Feature[], layers: string[]): Feature[] {
  return layers.length ? features.filter((f) => behavioursUnder(t, f.id).size > 0) : features
}

/**
 * BEH-FEAT-4: rows grouped by project, in the order each project first appears. A file with
 * no `# kit:project` is a project of its own, named by its file, as it always was.
 */
export function byProject(rows: ProjectSummary[]): { project: string; files: ProjectSummary[] }[] {
  const groups = new Map<string, ProjectSummary[]>()
  for (const r of rows) {
    const key = r.project ?? r.app
    groups.set(key, [...(groups.get(key) ?? []), r])
  }
  return [...groups].map(([project, files]) => ({ project, files }))
}
