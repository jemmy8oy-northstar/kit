import type { ReactNode } from 'react'
import { Card } from '@jemmy8oy-northstar/design-system'
import type { Resource } from '../hooks/useResource'

/**
 * Renders the three states of a resource so no page has to spell them out, and
 * — more to the point — so no page can silently render the error state as an
 * empty one.
 */
export default function ResourceView<T>({
  resource,
  children,
}: {
  resource: Resource<T>
  children: (value: T) => ReactNode
}) {
  if (resource.state === 'loading') {
    return <p className="muted">Loading…</p>
  }

  if (resource.state === 'error') {
    return (
      <Card elevation="flat">
        <h2>Could not read this</h2>
        <p role="alert">{resource.message}</p>
      </Card>
    )
  }

  return <>{children(resource.value)}</>
}
