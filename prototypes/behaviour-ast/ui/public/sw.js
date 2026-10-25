// BEH-NOTIFY-3: Kit's service worker. It does one job: show the push the server sends and open
// Kit when it is tapped. It caches nothing and answers no fetch, so it can never serve a stale page.
//
// Served from the bundle's root, so its scope is Kit's own path (/kit/ deployed), never the shared
// host's root, which is another app. The server's payload is {"title", "body", "url"}; url is
// absolute and already carries that path.

self.addEventListener('push', (event) => {
  let data = {}
  try {
    data = event.data ? event.data.json() : {}
  } catch {
    // A payload that is not JSON still deserves a notification: say it as text.
    data = { body: event.data ? event.data.text() : '' }
  }

  // iOS shows a notification for every push or revokes the subscription, so always show one.
  event.waitUntil(
    self.registration.showNotification(data.title || 'Kit', {
      body: data.body || '',
      data: { url: data.url || self.registration.scope },
      icon: 'icon-192.png',
    }),
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const target = new URL(event.notification.data?.url || self.registration.scope, self.location.origin).href

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      // Prefer a window already inside Kit: focus it and take it to the page, rather than opening a second.
      const open = windows.find((w) => w.url.startsWith(self.registration.scope))
      if (open) {
        return open.focus().then((focused) => (focused ?? open).navigate(target))
      }
      return self.clients.openWindow(target)
    }),
  )
})
