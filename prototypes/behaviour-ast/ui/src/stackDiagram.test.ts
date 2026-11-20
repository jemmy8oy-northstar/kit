import { describe, expect, it, vi } from 'vitest'
import { stackDiagram, stackParts, type RepoFile } from './stackDiagram'

const STACK = ['database accounts postgres', 'external-api GitHub https://api.github.com'].join('\n')
const CHART = 'apiVersion: v2\nname: kit\nversion: 0.1.0\n'

const repo: RepoFile[] = [
  { path: 'README.md', text: '# kit' },
  { path: '.kit/stack', text: STACK },
  { path: 'helm/kit/Chart.yaml', text: CHART },
  { path: 'backend/src/Program.cs', text: 'var x = 1;' },
]

describe('stack diagram (BEH-ARCH-4)', () => {
  it('BEH-ARCH-4: generating the diagram twice for the same repo gives byte-identical output, and a repo whose files are read in a different order gives the same output too', () => {
    const first = stackDiagram(repo)
    const second = stackDiagram(repo)
    const reversed = stackDiagram([...repo].reverse())

    expect(first).not.toBeNull()
    expect(second).toBe(first)
    expect(reversed).toBe(first)
  })

  it('BEH-ARCH-4: a repo that declares a database, an external API and a Helm chart shows each one as a part, taken from the repo’s own files', () => {
    const parts = stackParts(repo)

    expect(parts).toEqual([
      { kind: 'database', name: 'accounts', engine: 'postgres', from: '.kit/stack' },
      { kind: 'external-api', name: 'GitHub', url: 'https://api.github.com', from: '.kit/stack' },
      { kind: 'helm', name: 'kit', from: 'helm/kit/Chart.yaml' },
    ])
  })

  it('BEH-ARCH-4: a repo with none of the three shows no diagram and draws no invented part', () => {
    const bare: RepoFile[] = [
      { path: 'README.md', text: 'database postgres is mentioned here, not declared' },
      { path: 'src/index.ts', text: 'fetch("https://api.example.com")' },
      { path: '.kit/stack', text: '# nothing declared yet\n\nunknown thing\ndatabase onlyname\n' },
    ]

    expect(stackParts(bare)).toEqual([])
    expect(stackDiagram(bare)).toBeNull()
    expect(stackDiagram([])).toBeNull()
  })

  it('BEH-ARCH-4: the diagram is produced by code and makes no model call, so it works with the model unavailable', () => {
    const fetchSpy = vi.fn(() => {
      throw new Error('no network or model in the diagram')
    })
    vi.stubGlobal('fetch', fetchSpy)
    try {
      expect(stackDiagram(repo)).toContain('flowchart')
      expect(fetchSpy).not.toHaveBeenCalled()
    } finally {
      vi.unstubAllGlobals()
    }
  })
})
