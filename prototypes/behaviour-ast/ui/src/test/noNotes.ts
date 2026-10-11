/**
 * The notes panel (kit#118) reads `GET /api/projects/<app>/notes` on every
 * project and behaviour page. A test about something else stubs `fetch` with ONE
 * body, or a queue of them — so the panel would read the project as its notes,
 * or eat a response the test scripted for its own write. This answers the notes
 * read with an empty list, and only that read: a note POST still reaches the stub.
 */
export function noNotes(url: string, init?: RequestInit) {
  if (!/\/api\/projects\/[^/]+\/notes$/.test(url) || (init?.method ?? 'GET') !== 'GET') return null
  const app = decodeURIComponent(url.split('/').at(-2) ?? '')
  return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app, file: `behaviours/${app}.notes.md`, notes: [] }) }
}
