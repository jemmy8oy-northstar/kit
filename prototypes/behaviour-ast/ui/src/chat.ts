import type { TriageMessage } from './api/types'

/** A line on the Chat page (BEH-CHAT-1): his, the agent's (with the project it was about), or the note that he picked another project. */
export type Line = { from: 'user'; text: string; sent: string } | { from: 'agent'; text: string; app: string } | { from: 'switch'; app: string }

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
