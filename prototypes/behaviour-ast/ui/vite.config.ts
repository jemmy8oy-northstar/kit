// `defineConfig` comes from vitest/config, not vite — it is the same function
// widened to accept the `test` block below. Importing it from 'vite' typechecks
// everything except `test`, which then fails only at `tsc -b`.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// ⚠️ The prefix has to be the same string in two places: here (the build) and
// `KitHost.NormaliseBasePath` in the C# server (backend/Balenthiran.Kit.Services/
// KitHost.cs), which strips it from every request. This is that function, ported
// exactly — it used to be `require`d from the Node server's ui.js, and that file is
// gone (kit#153). Two normalisers is how `/kit` and `/kit/` both end up "right" and
// the deployment serves a blank page, so change them together: the C# theory
// `NormaliseBasePath_matches_ui_js` pins the server side's table, and this side's
// is pinned in `src/test/basePath.test.ts`.
export function normaliseBasePath(value: unknown): string {
  const trimmed = String(value ?? '').trim().replace(/^\/+/, '').replace(/\/+$/, '')
  return trimmed === '' ? '' : `/${trimmed}`
}

// The dev server proxies /api to the C# server, which `npm run dev`'s reader starts
// with `dotnet run --project backend/Balenthiran.Kit.WebApi -- --urls
// http://127.0.0.1:4321` from the repo root. Loopback on purpose — binding 0.0.0.0
// would publish every corpus in the working tree, and the proxy must not be the
// thing that quietly undoes it.
//
// ── `base`, and why it is read from the environment (kit#49) ────────────────
// This used to say no `base` is set, because decision 1 (local tool vs deployed
// app) was open and guessing a sub-path here would have answered it in a config
// file. James answered it on kit#25 — it is deployed — so the sub-path is no
// longer a guess; it is a value, and `KIT_BASE_PATH` is where it comes from.
//
// 🔴 It is read at BUILD time and there is no runtime equivalent: `base` rewrites
// every asset URL inside `index.html`, so a bundle built at `/` cannot be served
// under `/kit` by any amount of configuring the server. The Node server used to read
// the prefix back out of the built bundle at startup and say so loudly when the two
// disagreed; the C# server does not (kit#153), so the symptom of a mismatch is a
// blank page and a 200 — build with the same KIT_BASE_PATH you serve with.
//
// Unset means `/`, which is every local run and every existing test.
const basePath = normaliseBasePath(process.env.KIT_BASE_PATH)

export default defineConfig({
  // Vite wants a trailing slash; the server's spelling deliberately has none, because
  // it concatenates. Converted in exactly one place, here.
  base: basePath === '' ? '/' : `${basePath}/`,
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:4321',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
