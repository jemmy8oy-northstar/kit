// `defineConfig` comes from vitest/config, not vite — it is the same function
// widened to accept the `test` block below. Importing it from 'vite' typechecks
// everything except `test`, which then fails only at `tsc -b`.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// The dev server proxies /api to `node ui.js`, which listens on 127.0.0.1:4321
// by default. Loopback on purpose — ui.js's rule 1 is that binding 0.0.0.0
// would publish every corpus in the working tree, and the proxy must not be the
// thing that quietly undoes it.
//
// No `base` is set. web-template's template pins `base: '/your-app-name/'`
// because it deploys under a sub-path; decision 1 in docs/design/ui.md (local
// tool vs deployed app) is still open, and guessing a sub-path here would be
// answering it in a config file.
export default defineConfig({
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
