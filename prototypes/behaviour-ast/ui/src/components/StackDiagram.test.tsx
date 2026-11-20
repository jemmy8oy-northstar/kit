import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import StackDiagram from './StackDiagram'

describe('StackDiagram (BEH-ARCH-4)', () => {
  it('BEH-ARCH-4: a repo with none of the three shows the page without error and draws no invented part', () => {
    render(<StackDiagram files={[{ path: 'README.md', text: 'nothing declared' }]} />)

    expect(screen.getByText('The repo declares no databases, external APIs or Helm chart.')).toBeInTheDocument()
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })
})
