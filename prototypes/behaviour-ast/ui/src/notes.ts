import type { Note } from './api/types'

/** A behaviour that names a note in its source line (BEH-NOTE-10). */
export interface Citation {
  app: string
  id: string
  title: string
}

/**
 * A note's anchor, from its heading time: `2026-10-16 07:13Z` → `2026-10-16-0713`. This is the
 * part after `#` in `source defined kit.notes.md#2026-10-16-0713`. Two notes left in the same
 * minute share one, so citing it folds both; that is the price of an anchor he can read.
 */
export function noteAnchor(at: string): string | null {
  const m = /^(\d{4}-\d{2}-\d{2}) (\d{2}):(\d{2})Z$/.exec(at.trim())
  return m ? `${m[1]}-${m[2]}${m[3]}` : null
}

/**
 * Every `<notesApp>.notes.md#<anchor>` source ref across the files, keyed by anchor. Only refs
 * naming THIS notes file count: a behaviour citing another project's note folds that note,
 * not one of ours that happens to share a minute.
 */
export function citationsOf(
  notesApp: string,
  files: { app: string; behaviours: { id: string; title: string; source: { ref: string | null } }[] }[],
): Map<string, Citation[]> {
  const prefix = `${notesApp}.notes.md#`
  const out = new Map<string, Citation[]>()
  for (const f of files) {
    for (const b of f.behaviours) {
      const ref = b.source.ref?.trim()
      if (!ref?.startsWith(prefix)) continue
      const anchor = ref.slice(prefix.length)
      out.set(anchor, [...(out.get(anchor) ?? []), { app: f.app, id: b.id, title: b.title }])
    }
  }
  return out
}

/** The behaviours that fold this note, or none. */
export function foldedBy(note: Note, citations: Map<string, Citation[]> | undefined): Citation[] {
  const anchor = noteAnchor(note.at)
  return (anchor && citations?.get(anchor)) || []
}
