// `defineConfig` comes from vitest/config, not vite — it is the same function
// widened to accept the `test` block below. Importing it from 'vite' typechecks
// everything except `test`, which then fails only at `tsc -b`.
import { createRequire } from 'node:module'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// ⚠️ Required from `ui.js` rather than reimplemented here, and that is the whole
// design of kit#49's rule 8. The prefix has to be the same string in four places;
// a second normaliser in this file is how `/kit` and `/kit/` both end up "right"
// and the deployment serves a blank page. `createRequire` rather than an `import`
// because ui.js is CommonJS with no type declarations and this file is typechecked
// under `strict`.
//
// 🔑 Anchored on `process.cwd()` and NOT on `import.meta.url`. Vite compiles this
// config into `node_modules/.vite-temp/` before loading it, so `import.meta.url`
// is not this file's location and `../../ui.js` resolves two levels above the
// wrong directory — measured, as `Cannot find module '../../ui.js'`. The cwd is
// the package root for `npm run build` and for CI's `working-directory`, and a
// wrong cwd fails the build loudly here rather than producing a bundle with the
// wrong asset URLs.
const { normaliseBasePath } = createRequire(pathToFileURL(path.join(process.cwd(), 'vite.config.ts')))('../ui.js') as {
  normaliseBasePath: (value: unknown) => string
}

// The dev server proxies /api to `node ui.js`, which listens on 127.0.0.1:4321
// by default. Loopback on purpose — ui.js's rule 1 is that binding 0.0.0.0
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
// under `/kit` by any amount of configuring the server. `ui.js` reads the prefix
// back out of the built bundle at startup and says so loudly when the two
// disagree, because the symptom otherwise is a blank page and a 200.
//
// Unset means `/`, which is every local run and every existing test.
const basePath = normaliseBasePath(process.env.KIT_BASE_PATH)

export default defineConfig({
  // Vite wants a trailing slash; ui.js's spelling deliberately has none, because
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
