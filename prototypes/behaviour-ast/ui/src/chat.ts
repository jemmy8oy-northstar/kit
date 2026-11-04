import type { Note, TriageMessage } from './api/types'

/**
 * A line on the Chat page (BEH-CHAT-1): his or the agent's, each with the project and the item in hand it
 * was about; the note that the chat moved to another project (`named` when he named it in a message
 * rather than picking it); the item itself, in the thread as soon as it is in hand (his 19:04Z note); or
 * Kit's own line after he acted on an item (his 17:54Z note), with the way to undo it while it can be
 * (BEH-CHAT-6). An item or Kit line carries `tell`, what the agent is told about it, at the head of his next
 * message, so it reads the chat in the order he saw it. A `local` user line (he typed "help", his 19:28Z
 * note) is answered by the page alone: the agent never sees it, and it is not his last message to it.
 */
export type Line =
  | { from: 'user'; text: string; sent: string; app: string; item: string | null; local?: boolean }
  | { from: 'agent'; text: string; app: string; item: string | null; options?: string[] }
  | { from: 'switch'; app: string; named?: boolean }
  | { from: 'item'; app: string; id: string; title: string; ask: string | null; tell: string }
  | { from: 'kit'; text: string; tell: string; undo?: () => Promise<unknown>; undone?: boolean }
  // BEH-CHAT-12: a note put into the thread on request, with its text, answer and origin; read-only, so no undo.
  | { from: 'note'; app: string; note: Note; tell: string }

/** What the agent has not yet been told: the `tell` of every item, note or Kit line since his last message. */
export function untold(lines: Line[]): string[] {
  const last = lines.map((l) => (l.from === 'user' && l.local ? 'local' : l.from)).lastIndexOf('user')
  // An empty tell is a line the agent need not hear of (the help text).
  return lines.slice(last + 1).flatMap((l) => ((l.from === 'item' || l.from === 'kit' || l.from === 'note') && l.tell !== '' ? [l.tell] : []))
}

/**
 * BEH-CHAT-15: `text` cut into plain strings and the item ids `known` says Kit has (an epic, feature or
 * behaviour in any project). An id Kit does not know stays plain text.
 */
export function splitIds(text: string, known: (id: string) => boolean): (string | { id: string })[] {
  const out: (string | { id: string })[] = []
  let at = 0
  for (const m of text.matchAll(/(?<![\w-])[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+(?![\w-])/g)) {
    if (!known(m[0])) continue
    if (m.index > at) out.push(text.slice(at, m.index))
    out.push({ id: m[0] })
    at = m.index + m[0].length
  }
  return at === 0 ? [text] : [...out, ...(at < text.length ? [text.slice(at)] : [])]
}

/** The server's limits (KitRouter.Triage.cs): at most 40 turns, each at most 4000 characters. */
export const MAX_TURNS = 40
export const MAX_TEXT = 4000

/**
 * What a turn sends: his and the agent's lines, the newest that fit. The chat keeps its history across
 * projects, so it can outgrow the server's 40 turns; an odd count keeps it starting and ending with his.
 */
export function toMessages(lines: Line[]): TriageMessage[] {
  const all = lines.flatMap((l): TriageMessage[] =>
    l.from === 'user' && !l.local ? [{ from: 'user', text: l.sent }] : l.from === 'agent' ? [{ from: 'agent', text: l.text.slice(0, MAX_TEXT) }] : [],
  )
  return all.slice(-(MAX_TURNS - 1))
}

/**
 * BEH-CHAT-1 (his 14:12Z note): the project a message names, when it names one other than the one being
 * talked about. An app is named by its own name, or by its project's name when that project has only the
 * one app. Null when it names none, names more than one, or also names the current project: a message
 * comparing two projects should not move the chat.
 */
export function named(text: string, apps: { app: string; project: string }[], current: string): string | null {
  const said = (name: string) => new RegExp(`(^|[^\\w-])${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}($|[^\\w-])`, 'i').test(text)
  const alone = (project: string) => apps.filter((a) => a.project === project).length === 1
  const hits = apps.filter((a) => (a.app !== '' && said(a.app)) || (a.project !== '' && alone(a.project) && said(a.project)))
  const here = apps.find((a) => a.app === current)
  if (hits.some((a) => a.app === current || (here && a.project === here.project && said(a.project)))) return null
  return hits.length === 1 ? hits[0].app : null
}
