/**
 * BEH-ARCH-4: a project's architecture page draws a diagram of its stack, the same picture every time
 * for the same repo. Plain code over the repo's own files, no model: the output depends only on the
 * files' paths and text, never on the order they arrive in.
 */

/** A file of the project's repo: its path from the repo root, and its text. */
export type RepoFile = { path: string; text: string }

export type StackPart =
  | { kind: 'database'; name: string; engine: string; from: string }
  | { kind: 'external-api'; name: string; url: string; from: string }
  | { kind: 'helm'; name: string; from: string }

/**
 * Where a repo declares its databases and external APIs (BEH-ARCH-4, an unreviewed assumption until
 * the owner rules on it). One declaration per line:
 *   database <name> <engine>
 *   external-api <name> <url>
 * Blank lines and `#` comments are skipped; any other line declares nothing.
 */
export const STACK_FILE = '.kit/stack'

const KIND_ORDER: StackPart['kind'][] = ['database', 'external-api', 'helm']

/** The parts a repo declares, in a fixed order: by kind, then by name. A part named twice is kept once. */
export function stackParts(files: readonly RepoFile[]): StackPart[] {
  // Read in path order, so which declaration wins a duplicate never depends on the order of the input.
  const sorted = [...files].sort((a, b) => compare(a.path, b.path))
  const found: StackPart[] = []

  for (const file of sorted) {
    if (file.path === STACK_FILE) found.push(...declared(file))
    if (basename(file.path) === 'Chart.yaml') found.push({ kind: 'helm', name: chartName(file), from: file.path })
  }

  const seen = new Set<string>()
  const unique = found.filter((part) => {
    const key = `${part.kind}\u0000${part.name}`
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })

  return unique.sort((a, b) => KIND_ORDER.indexOf(a.kind) - KIND_ORDER.indexOf(b.kind) || compare(a.name, b.name))
}

/**
 * The diagram as Mermaid flowchart text, or null when the repo declares none of the three, so the
 * page draws nothing rather than an invented part.
 */
export function stackDiagram(files: readonly RepoFile[]): string | null {
  const parts = stackParts(files)
  if (parts.length === 0) return null

  const lines = ['flowchart TD']
  for (const part of parts) {
    const id = `${part.kind.replace('-', '_')}_${part.name.replace(/[^A-Za-z0-9]/g, '_')}`
    if (part.kind === 'database') lines.push(`  ${id}[("${label(part.name)} (${label(part.engine)})")]`)
    else if (part.kind === 'external-api') lines.push(`  ${id}["${label(part.name)} (${label(part.url)})"]`)
    else lines.push(`  ${id}{{"Helm chart ${label(part.name)}"}}`)
  }
  return lines.join('\n')
}

function declared(file: RepoFile): StackPart[] {
  const parts: StackPart[] = []
  for (const raw of file.text.split(/\r?\n/)) {
    const line = raw.trim()
    if (line === '' || line.startsWith('#')) continue
    const [keyword, name, value] = line.split(/\s+/)
    if (!name || !value) continue
    if (keyword === 'database') parts.push({ kind: 'database', name, engine: value, from: file.path })
    else if (keyword === 'external-api') parts.push({ kind: 'external-api', name, url: value, from: file.path })
  }
  return parts
}

/** A chart's `name:` at the left margin; failing that, the directory it sits in. */
function chartName(file: RepoFile): string {
  const match = /^name:\s*["']?([^\s"']+)/m.exec(file.text)
  if (match) return match[1]
  const dir = file.path.split('/').slice(0, -1).pop()
  return dir && dir !== '.' ? dir : 'chart'
}

function basename(path: string): string {
  return path.split('/').pop() ?? path
}

/** Mermaid labels cannot hold a double quote; swap it for an apostrophe so the text stays one label. */
function label(text: string): string {
  return text.replace(/"/g, "'")
}

function compare(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0
}
