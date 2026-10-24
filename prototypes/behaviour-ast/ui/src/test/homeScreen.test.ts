import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// BEH-NOTIFY-2: Add to Home Screen opens Kit like an app. What the phone reads is the page's
// head and the manifest; the server already serves any file at the bundle's root.
const UI = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const html = fs.readFileSync(path.join(UI, 'index.html'), 'utf8')
const manifest = JSON.parse(fs.readFileSync(path.join(UI, 'public', 'manifest.json'), 'utf8')) as {
  name: string
  display: string
  start_url: string
  scope: string
  icons: { src: string; sizes: string; type: string }[]
}

/** A PNG's width and height, from its IHDR chunk. */
function pngSize(file: string): [number, number] {
  const b = fs.readFileSync(file)
  expect(b.subarray(1, 4).toString('latin1')).toBe('PNG')
  return [b.readUInt32BE(16), b.readUInt32BE(20)]
}

describe('home screen (BEH-NOTIFY-2)', () => {
  it('links a manifest and a home-screen icon that open Kit full screen', () => {
    expect(html).toMatch(/<link rel="manifest" href="\/manifest\.json"/)
    expect(html).toMatch(/<link rel="apple-touch-icon" href="\/apple-touch-icon\.png"/)
    expect(html).toMatch(/<meta name="apple-mobile-web-app-capable" content="yes"/)
    expect(manifest.name).toBe('Kit')
    expect(manifest.display).toBe('standalone')
    // iOS draws the touch icon at 180px and ignores SVG there.
    expect(pngSize(path.join(UI, 'public', 'apple-touch-icon.png'))).toEqual([180, 180])
  })

  it("keeps the installed app inside Kit's own path on the shared host", () => {
    // Relative to the manifest, which is served under KIT_BASE_PATH: an absolute "/" here
    // would claim the host's root, which is the portfolio, not Kit.
    expect(manifest.start_url).toBe('./')
    expect(manifest.scope).toBe('./')
    for (const icon of manifest.icons) {
      expect(icon.src.startsWith('/')).toBe(false)
      expect(fs.existsSync(path.join(UI, 'public', icon.src))).toBe(true)
      if (icon.type === 'image/png') {
        const [w, h] = pngSize(path.join(UI, 'public', icon.src))
        expect(`${w}x${h}`).toBe(icon.sizes)
      }
    }
  })
})
