import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import LayerChips from './LayerChips'

describe('LayerChips (BEH-KIND-1)', () => {
  it('tooltips the technical chip as logic implementation, the words of his note', () => {
    render(<LayerChips layers={[]} onToggle={() => {}} />)

    const technical = screen.getByRole('button', { name: 'Filter to the technical layer' })
    expect(technical.getAttribute('title') ?? '').toMatch(/logic implementation/)
  })

  it('tooltips ui and ux with what they are, so the three chips read as the three card words', () => {
    render(<LayerChips layers={[]} onToggle={() => {}} />)

    expect(screen.getByRole('button', { name: 'Filter to the ui layer' }).getAttribute('title') ?? '').toMatch(/how it looks/)
    expect(screen.getByRole('button', { name: 'Filter to the ux layer' }).getAttribute('title') ?? '').toMatch(/how it works/)
  })

  it('a chip reports its layer when pressed, and shows whether it is on', () => {
    const onToggle = vi.fn()
    render(<LayerChips layers={['technical']} onToggle={onToggle} />)

    expect(screen.getByRole('button', { name: 'Filter to the technical layer' })).toHaveAttribute('aria-pressed', 'true')
    fireEvent.click(screen.getByRole('button', { name: 'Filter to the ui layer' }))
    expect(onToggle).toHaveBeenCalledWith('ui')
  })
})
