import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import SchedulePanel from './SchedulePanel'
import type { ScheduleEntry } from '../api/types'

type Post = { url: string; body: { action: string; id: string } }

const pending = (id: string, title: string): ScheduleEntry => ({ id, title, state: 'pending', next: false })

/**
 * A fake server holding one app's schedule, applying each change the way the
 * real one does, so a POST really changes what the next GET reads.
 */
function server(apps: Record<string, { scheduled: ScheduleEntry[]; backlog: ScheduleEntry[] }>, refuse?: string) {
  const posts: Post[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const app = decodeURIComponent(url.split('/').at(-2) ?? '')
      const s = apps[app]
      if (init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Post['body']
        posts.push({ url, body })
        if (refuse) return { ok: false, status: 409, statusText: 'x', json: async () => ({ reason: refuse }) }
        const at = s.scheduled.findIndex((e) => e.id === body.id)
        if (body.action === 'add') {
          const b = s.backlog.findIndex((e) => e.id === body.id)
          s.scheduled.push(s.backlog.splice(b, 1)[0])
        } else if (body.action === 'remove') s.backlog.push(s.scheduled.splice(at, 1)[0])
        else {
          const to = body.action === 'up' ? at - 1 : at + 1
          ;[s.scheduled[at], s.scheduled[to]] = [s.scheduled[to], s.scheduled[at]]
        }
        return {
          ok: true,
          status: 200,
          statusText: 'OK',
          json: async () => ({ app, behaviour: body.id, file: `behaviours/${app}.schedule`, committed: true, pushed: true, commit: 'abc1234', branch: 'kit/hosted', note: 'x' }),
        }
      }
      expect(url).toMatch(/\/api\/projects\/[^/]+\/schedule$/)
      const first = s.scheduled.findIndex((e) => e.state === 'pending')
      const scheduled = s.scheduled.map((e, i) => ({ ...e, next: i === first }))
      return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app, file: `behaviours/${app}.schedule`, scheduled, backlog: [...s.backlog] }) }
    }),
  )
  return posts
}

const schedule = () => screen.getByRole('region', { name: 'Schedule' })
const backlog = () => screen.getByRole('region', { name: 'Backlog' })
const titles = (region: HTMLElement) => within(region).queryAllByRole('link').map((a) => a.textContent)

function show(apps: string[] = ['demo']) {
  render(
    <MemoryRouter>
      <SchedulePanel apps={apps} />
    </MemoryRouter>,
  )
}

describe('SchedulePanel (BEH-SCHED-1..3)', () => {
  it('with nothing scheduled, says nothing is next and lists the backlog', async () => {
    server({ demo: { scheduled: [], backlog: [pending('BEH-B', 'bee'), pending('BEH-C', 'sea')] } })
    show()

    expect(await within(await screen.findByRole('region', { name: 'Schedule' })).findByText(/Nothing is scheduled, so nothing is next/)).toBeInTheDocument()
    expect(within(schedule()).queryByText('next')).not.toBeInTheDocument()
    expect(titles(backlog())).toEqual(['bee', 'sea'])
    expect(within(backlog()).getByText('2 pending behaviours')).toBeInTheDocument()
  })

  it('schedules a backlog behaviour, which moves across and is marked next', async () => {
    const posts = server({ demo: { scheduled: [], backlog: [pending('BEH-B', 'bee'), pending('BEH-C', 'sea')] } })
    show()

    const buttons = await within(await screen.findByRole('region', { name: 'Backlog' })).findAllByRole('button', { name: 'Schedule' })
    fireEvent.click(buttons[1])

    await waitFor(() => expect(titles(schedule())).toEqual(['sea']))
    expect(posts).toEqual([{ url: expect.stringMatching(/\/api\/projects\/demo\/schedule$/), body: { action: 'add', id: 'BEH-C' } }])
    expect(within(schedule()).getByText('next')).toBeInTheDocument()
    expect(titles(backlog())).toEqual(['bee'])
    expect(within(schedule()).getByRole('status')).toHaveTextContent('the schedule')
  })

  it('moves a behaviour up, and the top is next', async () => {
    const posts = server({ demo: { scheduled: [pending('BEH-B', 'bee'), pending('BEH-C', 'sea')], backlog: [] } })
    show()

    const ups = await within(await screen.findByRole('region', { name: 'Schedule' })).findAllByRole('button', { name: 'Move up' })
    expect(ups[0]).toBeDisabled()
    expect(within(schedule()).getAllByRole('button', { name: 'Move down' })[1]).toBeDisabled()
    fireEvent.click(ups[1])

    await waitFor(() => expect(titles(schedule())).toEqual(['sea', 'bee']))
    expect(posts[0].body).toEqual({ action: 'up', id: 'BEH-C' })
    const first = within(schedule()).getAllByRole('listitem')[0]
    expect(within(first).getByText('next')).toBeInTheDocument()
  })

  it('unschedules back to the backlog', async () => {
    server({ demo: { scheduled: [pending('BEH-B', 'bee')], backlog: [] } })
    show()

    fireEvent.click(await within(await screen.findByRole('region', { name: 'Schedule' })).findByRole('button', { name: 'Unschedule' }))

    await waitFor(() => expect(titles(backlog())).toEqual(['bee']))
    expect(within(schedule()).getByText(/Nothing is scheduled/)).toBeInTheDocument()
  })

  it('shows a built or vanished entry as such rather than dropping it, and neither is next', async () => {
    server({
      demo: {
        scheduled: [
          { id: 'BEH-A', title: 'built one', state: 'done', next: false },
          { id: 'BEH-GONE', title: null, state: 'missing', next: false },
          pending('BEH-D', 'dee'),
        ],
        backlog: [],
      },
    })
    show()

    const items = await within(await screen.findByRole('region', { name: 'Schedule' })).findAllByRole('listitem')
    expect(within(items[0]).getByText('built')).toBeInTheDocument()
    expect(within(items[1]).getByText('BEH-GONE')).toBeInTheDocument()
    expect(within(items[1]).getByText('no longer in the spec')).toBeInTheDocument()
    expect(within(items[2]).getByText('next')).toBeInTheDocument()
    expect(within(schedule()).getAllByText('next')).toHaveLength(1)
  })

  it('says why a change was refused', async () => {
    server({ demo: { scheduled: [], backlog: [pending('BEH-B', 'bee')] } }, 'BEH-B is already built; only a pending behaviour can be scheduled')
    show()

    fireEvent.click(await within(await screen.findByRole('region', { name: 'Backlog' })).findByRole('button', { name: 'Schedule' }))

    expect(await within(schedule()).findByRole('alert')).toHaveTextContent('already built')
  })

  it('a project of several files keeps one schedule per file, each under its name', async () => {
    const posts = server({
      kit: { scheduled: [], backlog: [pending('BEH-K', 'cli thing')] },
      'kit-ui': { scheduled: [pending('BEH-U', 'ui thing')], backlog: [pending('BEH-V', 'vee')] },
    })
    show(['kit', 'kit-ui'])

    expect(await within(await screen.findByRole('region', { name: 'Schedule' })).findByRole('heading', { name: 'kit-ui' })).toBeInTheDocument()
    expect(within(backlog()).getByRole('heading', { name: 'kit' })).toBeInTheDocument()
    fireEvent.click(within(backlog()).getAllByRole('button', { name: 'Schedule' })[0])

    await waitFor(() => expect(posts).toHaveLength(1))
    expect(posts[0].url).toMatch(/\/api\/projects\/kit\/schedule$/)
  })
})
