import { ApiError, fetchHistory, fetchNotes, fetchProject, fetchProjects, fetchSchedule } from './api/client'
import type { Behaviour, ProjectSummary } from './api/types'
import type { Place } from './board'

/** One project as the board needs it: its row, and either its behaviours or why it has none. */
export interface Entry {
  summary: ProjectSummary
  behaviours: Behaviour[]
  /** Each scheduled id's place on this file's schedule (BEH-BOARD-3). */
  places: Map<string, Place>
  error: string | null
  /** The schedule alone failed: the behaviours still show, every pending one as backlog, and this says why. */
  scheduleError: string | null
  /** When each built behaviour was completed (BEH-BOARD-4): an ISO time, or null when built ahead of git. */
  completed: Map<string, string | null>
  /** When each behaviour was added (BEH-BOARD-12): an ISO time, or null when no commit holds it yet. */
  added: Map<string, string | null>
  /** Why there is no history: Done shows this project's cards after the dated ones, in corpus order. */
  historyMissing: string | null
  /** The items whose bell is rung and unanswered (BEH-BELL-3); empty when the notes could not be read. */
  bells: Set<string>
}

const messageOf = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : String(e))

/**
 * Every project's detail, schedule and history, fetched after the list. A project
 * that will not parse (the list says so) or whose detail fails keeps its place as
 * an error, so the board never shows fewer projects than Kit reads. A history that
 * fails costs Done its order, never a card. The inbox (BEH-INBOX-1) reads the same entries.
 */
export async function loadBoard(): Promise<Entry[]> {
  const { projects } = await fetchProjects()
  return Promise.all(
    projects.map(async (summary): Promise<Entry> => {
      const empty = {
        summary,
        behaviours: [],
        places: new Map<string, Place>(),
        scheduleError: null,
        completed: new Map<string, string | null>(),
        added: new Map<string, string | null>(),
        historyMissing: null,
        bells: new Set<string>(),
      }
      if (summary.error) return { ...empty, error: summary.error }
      const [detail, schedule, history, notes] = await Promise.allSettled([
        fetchProject(summary.app),
        fetchSchedule(summary.app),
        fetchHistory(summary.app),
        fetchNotes(summary.app),
      ])
      if (detail.status === 'rejected') return { ...empty, error: messageOf(detail.reason) }
      const places = new Map<string, Place>()
      if (schedule.status === 'fulfilled') {
        schedule.value.scheduled.forEach((s, order) => places.set(s.id, { order, next: s.next }))
      }
      const completed = new Map<string, string | null>()
      const added = new Map<string, string | null>()
      let historyMissing: string | null
      if (history.status === 'rejected') historyMissing = messageOf(history.reason)
      else if (history.value.history !== 'git') historyMissing = history.value.reason ?? 'no history'
      else {
        historyMissing = null
        history.value.completed.forEach((c) => completed.set(c.id, c.at))
        history.value.added.forEach((c) => added.set(c.id, c.at))
      }
      return {
        summary,
        behaviours: detail.value.behaviours,
        places,
        error: null,
        scheduleError: schedule.status === 'rejected' ? messageOf(schedule.reason) : null,
        completed,
        added,
        historyMissing,
        // Notes that fail cost the rung cards their lift, never a card.
        bells: new Set(
          notes.status === 'fulfilled' ? notes.value.notes.flatMap((n) => (n.kind === 'bell' && n.behaviour ? [n.behaviour] : [])) : [],
        ),
      }
    }),
  )
}
