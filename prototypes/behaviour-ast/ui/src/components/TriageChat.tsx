import { useEffect, useState } from 'react'
import { Button, Input } from '@jemmy8oy-northstar/design-system'
import { fetchTriage, triageTurn } from '../api/client'
import type { TriageMessage, TriageStatus } from '../api/types'

/**
 * BEH-SPECAGENT-4: talk an Inbox card through with the spec agent before deciding it. The chat lives
 * in this page only and is sent whole each turn; the agent writes nothing. A reply he agrees with goes
 * into his words with "Use as my words", and the card's own buttons post it. With no model key on the
 * server, or none of today's replies left at load, the chat is not offered at all.
 */
export default function TriageChat({ app, behaviour, onUse }: { app: string; behaviour: string; onUse: (text: string) => void }) {
  const [status, setStatus] = useState<TriageStatus | null>(null)
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<TriageMessage[]>([])
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    // Signed out, or an older server: no chat, and nothing to say about it.
    fetchTriage().then((s) => live && setStatus(s), () => live && setStatus(null))
    return () => {
      live = false
    }
  }, [])

  if (!status?.configured) return null

  if (!open) {
    return (
      <div className="badges">
        <Button type="button" variant="secondary" onClick={() => setOpen(true)}>
          Talk it through
        </Button>
      </div>
    )
  }

  async function send() {
    const text = draft.trim()
    if (text === '' || busy) return
    const next: TriageMessage[] = [...messages, { from: 'user', text }]
    setMessages(next)
    setDraft('')
    setBusy(true)
    setError(null)
    try {
      const answer = await triageTurn(app, behaviour, next)
      setMessages([...next, { from: 'agent', text: answer.reply }])
      setStatus((s) => (s ? { ...s, remaining: answer.remaining } : s))
    } catch (e) {
      // His message goes back in the box, so a failed turn costs him nothing to retry.
      setMessages(messages)
      setDraft(text)
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="write" aria-label="Talk it through" data-testid="triage-chat">
      <p className="muted">
        Talking this card through with the spec agent ({status.model}). It changes nothing; use a reply as your words to answer with it.
      </p>
      <ol className="triage-messages">
        {messages.map((m, i) => (
          <li key={i} data-from={m.from}>
            <strong>{m.from === 'user' ? 'You' : 'Spec agent'}:</strong> {m.text}
            {m.from === 'agent' && (
              <>
                {' '}
                <Button type="button" size="sm" variant="ghost" onClick={() => onUse(m.text)}>
                  Use as my words
                </Button>
              </>
            )}
          </li>
        ))}
        {busy && <li className="muted">The spec agent is replying…</li>}
      </ol>
      <label htmlFor="triage-draft">Ask the spec agent</label>
      <Input
        id="triage-draft"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') void send()
        }}
      />
      <div className="badges">
        <Button type="button" onClick={() => void send()} disabled={busy || draft.trim() === ''}>
          Send
        </Button>
        <Button type="button" variant="ghost" onClick={() => setOpen(false)} disabled={busy}>
          Close
        </Button>
        {status.remaining !== null && <span className="muted">{status.remaining} replies left today</span>}
      </div>
      {error && (
        <p role="alert">
          {error}
        </p>
      )}
    </section>
  )
}
