// BEH-NOTIFY-3: this device's side of web push. Every call here is the browser's own API; what the
// server holds is in api/client.ts.

/** Kit's path with a trailing slash (`/kit/`), which vite echoes from the `base` it built with. */
export function scope(): string {
  return import.meta.env.BASE_URL
}

/**
 * Can this browser push at all? iOS gives `PushManager` only to an app saved to the Home Screen, so
 * on an iPhone this is false in Safari itself and true once Kit is opened from its icon.
 */
export function browserCanPush(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
}

/** The VAPID key as the bytes `pushManager.subscribe` wants: it is sent base64url, which `atob` does not read. */
export function applicationServerKey(base64url: string): Uint8Array<ArrayBuffer> {
  const padded = base64url.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(base64url.length / 4) * 4, '=')
  const raw = atob(padded)
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i)
  return bytes
}

/** This device's subscription for Kit, or null when it has none (or has never registered the worker). */
export async function currentSubscription(): Promise<PushSubscription | null> {
  const registration = await navigator.serviceWorker.getRegistration(scope())
  return (await registration?.pushManager.getSubscription()) ?? null
}

/**
 * Register the worker under Kit's own path and subscribe with the server's key. The worker file
 * sits at the bundle's root, so its scope can be no wider than Kit's path on the shared host.
 */
export async function subscribeThisDevice(publicKey: string): Promise<PushSubscription> {
  await navigator.serviceWorker.register(`${scope()}sw.js`, { scope: scope() })
  const registration = await navigator.serviceWorker.ready
  return registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: applicationServerKey(publicKey) })
}
