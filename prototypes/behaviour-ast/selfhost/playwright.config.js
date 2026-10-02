// Config for `selfhost/run.js`, which sets both env vars below and refuses to
// run without them.
//
// ⚠️ NO `require('@playwright/test')` HERE, DELIBERATELY. The obvious spelling
// of this file is `defineConfig({...})` — but `defineConfig` is a types helper
// that returns its argument unchanged, and requiring it means resolving
// `@playwright/test` from THIS directory, which has no `node_modules` and never
// will, because the repo does not depend on Playwright (see run.js). The
// scratch version of this harness only worked because it sat next to a
// symlinked `node_modules` borrowed from another repository. A plain object is
// what Playwright actually consumes.
module.exports = {
  testDir: process.env.KIT_SELFHOST_SPECS,
  timeout: 15000,
  expect: { timeout: 5000 },
  reporter: [['list']],
  use: {
    baseURL: process.env.KIT_SELFHOST_BASE_URL,
    // Chromium, and the choice is not arbitrary: the question here is whether
    // the GENERATED locators find real elements, which is not browser-specific.
    browserName: 'chromium',
  },
};
