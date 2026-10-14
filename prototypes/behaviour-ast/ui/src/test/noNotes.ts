/**
 * The notes panel (kit#118) reads `GET /api/projects/<app>/notes` on every
 * project and behaviour page. A test about something else stubs `fetch` with ONE
 * body, or a queue of them — so the panel would read the project as its notes,
 * or eat a response the test scripted for its own write. This answers the notes
 * read with an empty list, and only that read: a note POST still reaches the stub.
 *
 * The schedule panel (BEH-SCHED-1) reads `GET /api/projects/<app>/schedule` on
 * the project page for the same reason, so its read is answered here too: an
 * empty schedule and an empty backlog.
 */
export function noNotes(url: string, init?: RequestInit) {
  if ((init?.method ?? 'GET') !== 'GET') return null
  const panel = /\/api\/projects\/[^/]+\/(notes|schedule)$/.exec(url)?.[1]
  if (!panel) return null
  const app = decodeURIComponent(url.split('/').at(-2) ?? '')
  const body =
    panel === 'notes'
      ? { app, file: `behaviours/${app}.notes.md`, notes: [] }
      : { app, file: `behaviours/${app}.schedule`, scheduled: [], backlog: [] }
  return { ok: true, status: 200, statusText: 'OK', json: async () => body }
}
