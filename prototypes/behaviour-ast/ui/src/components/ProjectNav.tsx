import { Link, useLocation } from 'react-router-dom'
import { useFlags } from './flags'
import { useSession } from './session'

/**
 * BEH-SPACE-6 (claude-code-bot#151): one tab bar for a project's pages, as on GitHub, so its runs,
 * inbox and settings are reachable from each other. Visibility follows the links it replaced:
 * Chat, Inbox and Board when their feature is on; Runs not for a signed-in non-operator (BEH-USERS-9).
 */
export default function ProjectNav({ app }: { app: string }) {
  const flags = useFlags()
  const session = useSession()
  const { pathname, search } = useLocation()
  const name = encodeURIComponent(app)
  const base = `/projects/${name}`
  const onNotes = new URLSearchParams(search).get('tab') === 'notes'
  // BEH-NAV-4: a behaviour, feature or epic page is part of the project's spec, so Overview is the tab it sits under.
  const onSpec = /^\/projects\/[^/]+\/(features|behaviours|epics)\//.test(pathname)

  const tabs: { label: string; to: string; current: boolean }[] = [
    { label: 'Overview', to: base, current: (pathname === base && !onNotes) || onSpec },
    ...(flags.isOn('FEAT-CHAT') ? [{ label: 'Chat', to: `/chat?project=${name}`, current: pathname === '/chat' }] : []),
    ...(flags.isOn('FEAT-INBOX') ? [{ label: 'Inbox', to: `${base}/inbox`, current: pathname === `${base}/inbox` }] : []),
    ...(flags.isOn('FEAT-BOARD') ? [{ label: 'Board', to: `/board?project=${name}`, current: pathname === '/board' }] : []),
    ...(!(session?.accounts && !session.operator)
      ? [{ label: 'Runs', to: `${base}/runs`, current: pathname === `${base}/runs` }]
      : []),
    ...(flags.isOn('FEAT-HEALTH') ? [{ label: 'Health', to: `${base}/health`, current: pathname === `${base}/health` }] : []),
    { label: 'Notes', to: `${base}?tab=notes`, current: pathname === base && onNotes },
    { label: 'Settings', to: `${base}/settings`, current: pathname === `${base}/settings` },
  ]

  return (
    <nav className="tabs project-nav" aria-label="This project">
      {tabs.map((t) => (
        <Link key={t.label} to={t.to} aria-current={t.current ? 'page' : undefined}>
          {t.label}
        </Link>
      ))}
    </nav>
  )
}
