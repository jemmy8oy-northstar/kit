import { useEffect, useState } from 'react'
import { Button } from '@jemmy8oy-northstar/design-system'
import { fetchPush, subscribePush, unsubscribePush } from '../api/client'
import { browserCanPush, currentSubscription, subscribeThisDevice } from '../push'

type View =
  | { state: 'checking' }
  | { state: 'server-off' }
  | { state: 'unsupported' }
  | { state: 'blocked' }
  | { state: 'off'; publicKey: string }
  | { state: 'on'; publicKey: string }
  | { state: 'failed'; message: string }

/**
 * "Phone notifications" (BEH-NOTIFY-3): turn web push on or off for THIS device, from the page the
 * messages are on. Four honest answers rather than a button that may do nothing: the server has no
 * key yet; this browser cannot push (an iPhone's Safari outside the Home Screen app); it can and is
 * off; it is on. Permission is asked inside the click, because iOS refuses it without a gesture.
 */
export default function PushControl() {
  const [view, setView] = useState<View>({ state: 'checking' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let live = true
    async function look(): Promise<View> {
      const status = await fetchPush()
      if (!status.configured || status.publicKey === null) return { state: 'server-off' }
      if (!browserCanPush()) return { state: 'unsupported' }
      if (Notification.permission === 'denied') return { state: 'blocked' }
      const subscription = await currentSubscription()
      return subscription !== null && Notification.permission === 'granted'
        ? { state: 'on', publicKey: status.publicKey }
        : { state: 'off', publicKey: status.publicKey }
    }
    look()
      .then((v) => live && setView(v))
      .catch((e: unknown) => live && setView({ state: 'failed', message: e instanceof Error ? e.message : String(e) }))
    return () => {
      live = false
    }
  }, [])

  async function turnOn(publicKey: string) {
    setBusy(true)
    try {
      // First thing in the click handler, before any other await: iOS counts only that as the gesture.
      const permission = await Notification.requestPermission()
      if (permission !== 'granted') {
        setView(permission === 'denied' ? { state: 'blocked' } : { state: 'off', publicKey })
        return
      }
      const subscription = await subscribeThisDevice(publicKey)
      try {
        await subscribePush(subscription.toJSON())
      } catch (e) {
        // A subscription the server never heard of would say "on" and push nothing.
        await subscription.unsubscribe()
        throw e
      }
      setView({ state: 'on', publicKey })
    } catch (e: unknown) {
      setView({ state: 'failed', message: e instanceof Error ? e.message : String(e) })
    } finally {
      setBusy(false)
    }
  }

  async function turnOff(publicKey: string) {
    setBusy(true)
    try {
      const subscription = await currentSubscription()
      if (subscription !== null) {
        await unsubscribePush(subscription.endpoint)
        await subscription.unsubscribe()
      }
      setView({ state: 'off', publicKey })
    } catch (e: unknown) {
      setView({ state: 'failed', message: e instanceof Error ? e.message : String(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-label="Phone notifications">
      <h2>Phone notifications</h2>
      {view.state === 'checking' && <p className="muted">Checking…</p>}
      {view.state === 'server-off' && (
        <p>Push is not switched on yet on the server, so Kit cannot send notifications to a phone. It switches on once the server has its push keys.</p>
      )}
      {view.state === 'unsupported' && (
        <p>Add Kit to your Home Screen, then open it from there to turn on notifications. A browser tab cannot receive them on an iPhone.</p>
      )}
      {view.state === 'blocked' && (
        <p>
          Notifications are blocked for Kit on this device. To allow them, open Settings, choose Notifications, then Kit, and switch Allow
          Notifications on; or, in a browser, allow notifications for this site. Then come back here.
        </p>
      )}
      {view.state === 'off' && (
        <>
          <p className="muted">Get each new message from the agent as a notification on this device.</p>
          <Button type="button" onClick={() => turnOn(view.publicKey)} disabled={busy}>
            Turn on notifications
          </Button>
        </>
      )}
      {view.state === 'on' && (
        <>
          <p role="status">Notifications are on for this device</p>
          <Button type="button" onClick={() => turnOff(view.publicKey)} disabled={busy}>
            Turn off
          </Button>
        </>
      )}
      {view.state === 'failed' && <p role="alert">Could not change phone notifications: {view.message}</p>}
    </section>
  )
}
