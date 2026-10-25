import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import PushControl from './PushControl'
import { applicationServerKey } from '../push'

// BEH-NOTIFY-3: the Phone notifications control on the Notifications page. The browser's push APIs
// do not exist in jsdom, so each test builds the device it is about: what the server says, and what
// the browser can do.

const ENDPOINT = 'https://push.example/device'

interface Device {
  /** Present when the browser has the push APIs at all. */
  push?: boolean
  permission?: NotificationPermission
  /** What requestPermission answers when he taps. */
  answer?: NotificationPermission
  subscribed?: boolean
}

function setUp(server: { configured: boolean; publicKey: string | null }, device: Device = {}) {
  const calls: { path: string; method: string; body: unknown }[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      calls.push({ path, method: init?.method ?? 'GET', body: init?.body ? JSON.parse(init.body) : undefined })
      // Every route answers with the push status, which is also the shape of a subscribe's answer.
      return { ok: true, status: 200, statusText: 'OK', json: async () => server }
    }),
  )

  const subscription = {
    endpoint: ENDPOINT,
    toJSON: () => ({ endpoint: ENDPOINT, keys: { p256dh: 'k', auth: 'a' } }),
    unsubscribe: vi.fn(async () => true),
  }
  const state = { current: device.subscribed ? subscription : null }
  const pushManager = {
    getSubscription: vi.fn(async () => state.current),
    subscribe: vi.fn(async () => {
      state.current = subscription
      return subscription
    }),
  }
  const registration = { pushManager }
  const serviceWorker = {
    register: vi.fn(async () => registration),
    ready: Promise.resolve(registration),
    getRegistration: vi.fn(async () => registration),
  }
  const notification = {
    permission: device.permission ?? (device.subscribed ? 'granted' : 'default'),
    requestPermission: vi.fn(async () => device.answer ?? 'granted'),
  }
  if (device.push !== false) {
    Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: serviceWorker })
    vi.stubGlobal('PushManager', class {})
    vi.stubGlobal('Notification', notification)
  }
  return { calls, subscription, pushManager, serviceWorker, notification }
}

afterEach(() => {
  vi.unstubAllGlobals()
  Reflect.deleteProperty(navigator, 'serviceWorker')
})

const KEY = 'BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U'

describe('Phone notifications (BEH-NOTIFY-3)', () => {
  it('says push is not switched on yet when the server has no keys', async () => {
    setUp({ configured: false, publicKey: null })
    render(<PushControl />)

    expect(await screen.findByText(/not switched on yet on the server/)).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('tells an iPhone browser tab to add Kit to the Home Screen and open it from there', async () => {
    setUp({ configured: true, publicKey: KEY }, { push: false })
    render(<PushControl />)

    expect(await screen.findByText(/Add Kit to your Home Screen, then open it from there to turn on notifications/)).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers Turn on notifications, asks permission in the tap, subscribes with the server key and tells the server', async () => {
    const d = setUp({ configured: true, publicKey: KEY })
    render(<PushControl />)

    fireEvent.click(await screen.findByRole('button', { name: 'Turn on notifications' }))

    expect(await screen.findByText('Notifications are on for this device')).toBeInTheDocument()
    expect(d.notification.requestPermission).toHaveBeenCalledOnce()
    expect(d.serviceWorker.register).toHaveBeenCalledWith('/sw.js', { scope: '/' })
    const options = (d.pushManager.subscribe.mock.calls[0] as unknown as [{ userVisibleOnly: boolean; applicationServerKey: Uint8Array }])[0]
    expect(options.userVisibleOnly).toBe(true)
    expect(options.applicationServerKey).toEqual(applicationServerKey(KEY))
    expect(d.calls.find((c) => c.method === 'POST')).toEqual({
      path: '/api/push/subscriptions',
      method: 'POST',
      body: { endpoint: ENDPOINT, keys: { p256dh: 'k', auth: 'a' } },
    })
    expect(screen.getByRole('button', { name: 'Turn off' })).toBeInTheDocument()
  })

  it('shows the device as on when it already holds a subscription, and Turn off removes it here and on the server', async () => {
    const d = setUp({ configured: true, publicKey: KEY }, { subscribed: true })
    render(<PushControl />)

    fireEvent.click(await screen.findByRole('button', { name: 'Turn off' }))

    expect(await screen.findByRole('button', { name: 'Turn on notifications' })).toBeInTheDocument()
    expect(d.calls.find((c) => c.method === 'POST')).toEqual({ path: '/api/push/subscriptions/remove', method: 'POST', body: { endpoint: ENDPOINT } })
    expect(d.subscription.unsubscribe).toHaveBeenCalledOnce()
  })

  it('says how to allow notifications in Settings when permission is denied, in the tap or beforehand', async () => {
    setUp({ configured: true, publicKey: KEY }, { permission: 'denied' })
    const { unmount } = render(<PushControl />)
    expect(await screen.findByText(/open Settings, choose Notifications, then Kit/)).toBeInTheDocument()
    unmount()
    vi.unstubAllGlobals()

    const d = setUp({ configured: true, publicKey: KEY }, { answer: 'denied' })
    render(<PushControl />)
    fireEvent.click(await screen.findByRole('button', { name: 'Turn on notifications' }))

    expect(await screen.findByText(/open Settings, choose Notifications, then Kit/)).toBeInTheDocument()
    expect(d.pushManager.subscribe).not.toHaveBeenCalled()
  })

  it('does not call a device subscribed when the server refused the subscription', async () => {
    const d = setUp({ configured: true, publicKey: KEY })
    render(<PushControl />)
    const button = await screen.findByRole('button', { name: 'Turn on notifications' })
    vi.mocked(fetch).mockImplementationOnce(async () => {
      throw new Error('offline')
    })

    fireEvent.click(button)

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not change phone notifications')
    expect(screen.queryByText('Notifications are on for this device')).not.toBeInTheDocument()
    expect(d.subscription.unsubscribe).toHaveBeenCalledOnce()
  })

  it('reads the base64url server key into bytes', () => {
    expect(Array.from(applicationServerKey('AQID_w'))).toEqual([1, 2, 3, 255])
    expect(applicationServerKey(KEY)).toHaveLength(65)
  })

  it("ships a worker that shows the push and opens Kit's notifications page on a tap, with no fetch handler", () => {
    const ui = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
    const sw = fs.readFileSync(path.join(ui, 'public', 'sw.js'), 'utf8')

    expect(sw).toMatch(/addEventListener\('push'/)
    expect(sw).toMatch(/showNotification\(/)
    expect(sw).toMatch(/addEventListener\('notificationclick'/)
    expect(sw).toMatch(/openWindow\(/)
    // A worker that answered fetches could serve a stale page; this one must never.
    expect(sw).not.toMatch(/addEventListener\('fetch'/)
  })
})
