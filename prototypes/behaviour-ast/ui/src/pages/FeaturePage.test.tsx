import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import FeaturePage from './FeaturePage'
import Project from './Project'
import Projects from './Projects'
import { noNotes } from '../test/noNotes'
import snipIt from '../test/fixtures/project-snip-it.json'
import type { Behaviour, Feature, ProjectDetail, ProjectSummary } from '../api/types'
import { behavioursUnder, byProject, pathFrom } from '../features'

// Hand-built on the recorded snip-it view: no recorded corpus has features yet, and the
// rules here are about the tag graph, which a re-recorded fixture would move.
const feature = (id: string, title: string, ...parents: string[]): Feature => ({ id, title, in: parents, at: `snip-it.beh:${id}` })
const base = snipIt as unknown as ProjectDetail
const [b0, b1, b2, ...rest] = base.behaviours
const tag = (b: Behaviour, ...parents: string[]): Behaviour => ({ ...b, in: parents })

// FEAT-APP ← FEAT-EDITOR, FEAT-HOME; FEAT-CARD is in both pages (a diamond).
const tagged: ProjectDetail = {
  ...base,
  features: [
    feature('FEAT-APP', 'The app'),
    feature('FEAT-EDITOR', 'Editor', 'FEAT-APP'),
    feature('FEAT-HOME', 'Home', 'FEAT-APP'),
    feature('FEAT-CARD', 'Clip card', 'FEAT-EDITOR', 'FEAT-HOME'),
  ],
  behaviours: [tag(b0, 'FEAT-CARD'), tag(b1, 'FEAT-EDITOR'), tag(b2, 'FEAT-CARD', 'FEAT-HOME'), ...rest.map((b) => tag(b))],
}

function renderAt(url: string, body: unknown = tagged) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (u: string, init?: RequestInit) => noNotes(u, init) ?? ({ ok: true, status: 200, statusText: 'OK', json: async () => body })),
  )
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/" element={<Projects />} />
        <Route path="/projects/:app" element={<Project />} />
        <Route path="/projects/:app/features/:id" element={<FeaturePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('the tag graph', () => {
  it('counts a behaviour under two child features once', () => {
    // b0 and b2 are in FEAT-CARD, b2 is also directly in FEAT-HOME, b1 in FEAT-EDITOR.
    expect([...behavioursUnder(tagged, 'FEAT-APP')].sort()).toEqual([b0.id, b1.id, b2.id].sort())
    expect(behavioursUnder(tagged, 'FEAT-HOME').size).toBe(2)
  })

  it('keeps a path only while each step is a parent of the next', () => {
    expect(pathFrom('FEAT-APP,FEAT-HOME', tagged, 'FEAT-CARD')).toEqual(['FEAT-APP', 'FEAT-HOME'])
    // FEAT-APP is not a parent of FEAT-CARD: a stale link draws no crumbs rather than false ones.
    expect(pathFrom('FEAT-APP', tagged, 'FEAT-CARD')).toEqual([])
    expect(pathFrom('FEAT-NOPE,FEAT-HOME', tagged, 'FEAT-CARD')).toEqual([])
    expect(pathFrom(null, tagged, 'FEAT-CARD')).toEqual([])
  })

  it('groups files by project in first-seen order, and a file with none is its own', () => {
    const rows = [
      { app: 'kit' , project: 'kit' },
      { app: 'snip-it', project: 'snip-it' },
      { app: 'kit-ui', project: 'kit' },
      { app: 'broken', error: 'nope' },
    ] as ProjectSummary[]
    expect(byProject(rows).map((g) => `${g.project}:${g.files.map((f) => f.app).join('+')}`)).toEqual([
      'kit:kit+kit-ui',
      'snip-it:snip-it',
      'broken:broken',
    ])
  })
})

describe('the projects view', () => {
  it('BEH-FEAT-1: a project shows its top-level features, then only its untagged behaviours', async () => {
    renderAt('/projects/snip-it')

    const features = await screen.findByRole('region', { name: 'Features' })
    expect(within(features).getAllByRole('link').map((a) => a.textContent)).toEqual(['The app'])
    expect(within(features).getByText('3 behaviours')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Behaviours in no feature' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: b0.title })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: rest[0].title })).toBeInTheDocument()
  })

  it('BEH-FEAT-1: a project with no features lists every behaviour, as before', async () => {
    renderAt('/projects/snip-it', snipIt)

    expect(await screen.findByRole('heading', { name: 'Behaviours' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Features' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: b0.title })).toBeInTheDocument()
  })

  it('BEH-FEAT-2: a feature lists what is tagged into it and every parent as a tag', async () => {
    renderAt('/projects/snip-it/features/FEAT-CARD')

    expect(await screen.findByRole('heading', { name: 'Clip card', level: 1 })).toBeInTheDocument()
    const parents = screen.getByRole('region', { name: 'In' })
    expect(within(parents).getAllByRole('link').map((a) => a.textContent)).toEqual(['Editor', 'Home'])
    const contents = screen.getByRole('region', { name: 'Contents' })
    expect(within(contents).getByRole('link', { name: b0.title })).toBeInTheDocument()
    expect(within(contents).getByRole('link', { name: b2.title })).toBeInTheDocument()
    expect(within(contents).queryByRole('link', { name: b1.title })).not.toBeInTheDocument()
  })

  it('shows a feature its epics as tags coloured apart, and leaves its parents and the project tree alone (BEH-EPIC-2, BEH-EPIC-3)', async () => {
    const withEpic: ProjectDetail = {
      ...tagged,
      epics: [{ id: 'EPIC-SHARE', title: 'Share clips', at: 'snip-it.beh:1' }],
      features: tagged.features.map((f) => (f.id === 'FEAT-CARD' ? { ...f, epics: ['EPIC-SHARE'] } : f)),
    }
    renderAt('/projects/snip-it/features/FEAT-CARD', withEpic)

    const parents = await screen.findByRole('region', { name: 'In' })
    expect(within(parents).getAllByRole('link').map((a) => a.textContent)).toEqual(['Editor', 'Home'])
    const epic = within(parents).getByText('Share clips')
    expect(epic).toHaveClass('epic-tag')
    expect(epic.closest('a')).toBeNull()
  })

  it('BEH-FEAT-2: one behaviour tagged into two features is listed under both', async () => {
    renderAt('/projects/snip-it/features/FEAT-HOME')

    const contents = await screen.findByRole('region', { name: 'Contents' })
    expect(within(contents).getByRole('link', { name: 'Clip card' })).toBeInTheDocument()
    expect(within(contents).getByRole('link', { name: b2.title })).toBeInTheDocument()
  })

  it('BEH-FEAT-3: the crumbs are the path in the url, and a child link carries it on', async () => {
    renderAt('/projects/snip-it/features/FEAT-HOME?path=FEAT-APP')

    const crumbs = await screen.findByRole('navigation', { name: 'Breadcrumb' })
    expect(within(crumbs).getAllByRole('link').map((a) => a.textContent)).toEqual(['Projects', 'snip-it', 'The app'])
    expect(crumbs).toHaveTextContent('Projects / snip-it / The app / Home')
    expect(screen.getByRole('link', { name: 'Clip card' })).toHaveAttribute(
      'href',
      `/projects/snip-it/features/FEAT-CARD?path=${encodeURIComponent('FEAT-APP,FEAT-HOME')}`,
    )
  })

  it('BEH-FEAT-3: with no path the crumbs are the project and the feature, never a picked parent', async () => {
    renderAt('/projects/snip-it/features/FEAT-CARD')

    const crumbs = await screen.findByRole('navigation', { name: 'Breadcrumb' })
    expect(crumbs).toHaveTextContent(/^Projects \/ snip-it \/ Clip card$/)
  })

  it('names a feature the corpus does not have, instead of an empty page', async () => {
    renderAt('/projects/snip-it/features/FEAT-GONE')

    expect(await screen.findByRole('alert')).toHaveTextContent('No feature FEAT-GONE in snip-it.')
  })

  it('BEH-FEAT-4: files naming one project are one card with one link; a file of its own reads as before', async () => {
    renderAt('/', { projects: [row('kit', 'kit'), row('kit-ui', 'kit'), row('snip-it', 'snip-it')] })

    const kit = (await screen.findByRole('heading', { name: 'kit', level: 2 })).closest('li')!
    expect(within(kit).getByText('4 behaviours')).toBeInTheDocument()
    expect(within(kit).getAllByRole('link').map((a) => a.getAttribute('href'))).toEqual(['/projects/kit'])
    expect(within(kit).queryByText('kit-ui')).toBeNull()
    expect(screen.getByRole('link', { name: 'snip-it' })).toBeInTheDocument()
  })
})

describe('BEH-KIND-2: a project filters by layer', () => {
  // b0 (in Clip card) is ui, b1 (in Editor) technical; b2 (Clip card and Home) and the untagged rest stay ux.
  const layered: ProjectDetail = {
    ...tagged,
    behaviours: tagged.behaviours.map((b) => (b.id === b0.id ? { ...b, layer: 'ui' } : b.id === b1.id ? { ...b, layer: 'technical' } : b)),
  }

  it('the fixture carries three layers, or the filter below proves nothing', () => {
    expect(new Set(layered.behaviours.map((b) => b.layer))).toEqual(new Set(['ui', 'technical', 'ux']))
  })

  it('counts only the chosen layer under a feature, drops what is not of it, and keeps the filter in the link', async () => {
    renderAt('/projects/snip-it', layered)
    const features = await screen.findByRole('region', { name: 'Features' })
    expect(within(features).getByText('3 behaviours')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Filter to the technical layer' }))
    expect(within(features).getByText('1 behaviour')).toBeInTheDocument()
    expect(within(features).getByRole('link', { name: 'The app' })).toHaveAttribute('href', '/projects/snip-it/features/FEAT-APP?layer=technical')
    // The untagged behaviours are all ux, so none is listed.
    expect(screen.queryByRole('link', { name: rest[0].title })).not.toBeInTheDocument()
  })

  it('hides a feature with nothing of the layer beneath it, on the feature page too', async () => {
    renderAt('/projects/snip-it/features/FEAT-APP?layer=technical', layered)
    const contents = await screen.findByRole('region', { name: 'Contents' })
    expect(within(contents).getByRole('link', { name: 'Editor' })).toBeInTheDocument()
    expect(within(contents).queryByRole('link', { name: 'Home' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Filter to the technical layer' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('says so when nothing in the project is of the chosen layer, rather than showing a blank tab', async () => {
    // Every behaviour of the recorded snip-it view is ux.
    renderAt('/projects/snip-it?layer=ui', snipIt)
    expect(await screen.findByText('Nothing in this project is of the ui layer.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Behaviours' })).not.toBeInTheDocument()
  })

  it('CONTROL: with no layer chosen every feature shows, as before', async () => {
    renderAt('/projects/snip-it/features/FEAT-APP', layered)
    const contents = await screen.findByRole('region', { name: 'Contents' })
    expect(within(contents).getByRole('link', { name: 'Editor' })).toBeInTheDocument()
    expect(within(contents).getByRole('link', { name: 'Home' })).toBeInTheDocument()
  })
})

const row = (app: string, project: string, behaviours = 2): ProjectSummary => ({
  app,
  project,
  corpus: `behaviours/${app}.beh`,
  notReal: false,
  duplicateOf: null,
  behaviours,
  conflicts: 0,
  coverage: { available: false, covered: null, uncovered: null, reason: 'no repo' },
  unreviewed: 0,
})

/** A fetch that answers by url, so the list and each file's detail can differ. */
function renderRouted(url: string, routes: Record<string, unknown>) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (u: string, init?: RequestInit) => {
      const notes = noNotes(u, init)
      if (notes) return notes
      const path = new URL(u, 'http://x').pathname.replace(/^.*\/api/, '/api')
      const body = routes[path]
      return body === undefined
        ? { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-route', reason: path }) }
        : { ok: true, status: 200, statusText: 'OK', json: async () => body }
    }),
  )
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/projects/:app" element={<Project />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('BEH-FEAT-5: a project of several files is one page', () => {
  const file = (app: string, project: string, features: Feature[], behaviours: Behaviour[]): ProjectDetail => ({
    ...base,
    app,
    project,
    corpus: `behaviours/${app}.beh`,
    features,
    behaviours,
    conflicts: [],
    questions: [],
    adjudication: { ...base.adjudication, unreviewed: [] },
  })
  const cli = file('kit', 'kit', [feature('FEAT-GATE', 'Gate')], [tag(b0, 'FEAT-GATE'), tag(b1)])
  const ui = file('kit-ui', 'kit', [feature('FEAT-BOARD', 'Board')], [tag(b2, 'FEAT-BOARD')])
  const routes = {
    '/api/projects': { projects: [row('kit', 'kit'), row('kit-ui', 'kit'), row('snip-it', 'snip-it')] },
    '/api/projects/kit': cli,
    '/api/projects/kit-ui': ui,
  }

  it('draws every file’s features together, each linking into its own file, and names no file', async () => {
    renderRouted('/projects/kit', routes)

    expect(await screen.findByRole('heading', { name: 'kit', level: 1 })).toBeInTheDocument()
    const features = screen.getByRole('region', { name: 'Features' })
    expect(within(features).getByRole('link', { name: 'Gate' })).toHaveAttribute('href', '/projects/kit/features/FEAT-GATE')
    expect(within(features).getByRole('link', { name: 'Board' })).toHaveAttribute('href', '/projects/kit-ui/features/FEAT-BOARD')
    // The untagged behaviour of one file is listed; the counts are the sum over the files.
    expect(screen.getByRole('link', { name: b1.title })).toHaveAttribute('href', `/projects/kit/behaviours/${b1.id}`)
    expect(screen.getByText('3 behaviours')).toBeInTheDocument()
    expect(screen.queryByText('kit-ui')).toBeNull()
  })

  it('a file that is its own project is still the one-file page', async () => {
    renderRouted('/projects/snip-it', { ...routes, '/api/projects/snip-it': base })

    expect(await screen.findByRole('heading', { name: 'snip-it', level: 1 })).toBeInTheDocument()
    expect(screen.getByText(base.corpus)).toBeInTheDocument()
  })

  it('a list that cannot be read still shows the one file, rather than nothing', async () => {
    const { '/api/projects': _list, ...rest } = routes
    void _list
    renderRouted('/projects/kit', rest)

    expect(await screen.findByRole('heading', { name: 'kit', level: 1 })).toBeInTheDocument()
    expect(screen.getByText(cli.corpus)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Board' })).toBeNull()
  })
})
