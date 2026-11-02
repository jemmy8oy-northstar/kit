import type { TriageMessage } from './api/types'

/**
 * A line on the Chat page (BEH-CHAT-1): his or the agent's, each with the project and the item in hand it
 * was about, or the note that the chat moved to another project (`named` when he named it in a message
 * rather than picking it).
 */
export type Line =
  | { from: 'user'; text: string; sent: string; app: string; item: string | null }
  | { from: 'agent'; text: string; app: string; item: string | null }
  | { from: 'switch'; app: string; named?: boolean }

/** The server's limits (KitRouter.Triage.cs): at most 40 turns, each at most 4000 characters. */
export const MAX_TURNS = 40
export const MAX_TEXT = 4000

/**
 * What a turn sends: his and the agent's lines, the newest that fit. The chat keeps its history across
 * projects, so it can outgrow the server's 40 turns; an odd count keeps it starting and ending with his.
 */
export function toMessages(lines: Line[]): TriageMessage[] {
  const all = lines.flatMap((l): TriageMessage[] =>
    l.from === 'user' ? [{ from: 'user', text: l.sent }] : l.from === 'agent' ? [{ from: 'agent', text: l.text.slice(0, MAX_TEXT) }] : [],
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
  const hits = apps.filter((a) => said(a.app) || (alone(a.project) && said(a.project)))
  const here = apps.find((a) => a.app === current)
  if (hits.some((a) => a.app === current || (here && a.project === here.project && said(a.project)))) return null
  return hits.length === 1 ? hits[0].app : null
}
