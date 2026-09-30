# syntax=docker/dockerfile:1

# Kit, as one image.
#
# The rest of the estate builds two — a .NET backend and an nginx frontend — and
# `docker-build-push.yml` asserted exactly that for months against a repo that has
# neither, which is why Kit's OCIR build "has never once succeeded". Kit is a
# single Node process: `ui.js` serves the built SPA *and* the API on one port, and
# `start.js --no-build` exists so a container can serve a bundle built here rather
# than rebuilding one at boot.
#
# node:20-alpine because CI pins Node 20, and an image on a different major from
# the one the suite runs on is an untested runtime.

# ── build the bundle ─────────────────────────────────────────────────────────
FROM node:20-alpine AS build

# git, and it is not optional: `@jemmy8oy-northstar/design-system` is a git-URL
# dependency rather than a published package, so `npm ci` clones it. Without this
# the install fails with a bare "git: not found" three layers down.
RUN apk add --no-cache git

WORKDIR /src

# Dependencies before sources, so an edit to a component does not re-run a
# two-minute install.
COPY prototypes/behaviour-ast/ui/package.json prototypes/behaviour-ast/ui/package-lock.json ./prototypes/behaviour-ast/ui/
RUN npm --prefix prototypes/behaviour-ast/ui ci

# The WHOLE repo, not just `ui/`. `npm run build` is `tsc -b && vite build`, and
# the project it typechecks includes `src/test/fixtures.test.ts`, which requires
# `../../../ui.js` — a build scoped to `ui/` alone fails to resolve it. Measured
# in CI, where the job checks out the repo root for the same reason.
COPY . .

# 🔑 The path Kit will be served under, baked into every asset URL in index.html.
# It is a BUILD argument because there is no runtime equivalent: `base` rewrites
# the HTML, so an image built for `/` cannot be served under `/kit` by any amount
# of configuring the container. The workflow passes the value it reads out of
# `helm/values.yaml`, so the ingress path and this cannot drift apart.
ARG KIT_BASE_PATH=""
ENV KIT_BASE_PATH=${KIT_BASE_PATH}
RUN npm --prefix prototypes/behaviour-ast/ui run build

# 🔴 And then it is CHECKED, because the failure it prevents is silent. If the
# prefix did not reach the bundle — an older `vite.config.ts`, a typo, a build arg
# that never arrived — the image still builds, still starts, still answers 200 for
# everything, and serves a blank page: on this shared host every unmatched path is
# answered by the portfolio's SPA, so nothing 404s. Asserting on the built
# artefact rather than on the input is the only check that can tell.
RUN set -eu; \
    index=prototypes/behaviour-ast/ui/dist/index.html; \
    if ! grep -q "\"${KIT_BASE_PATH}/assets/" "$index"; then \
      echo "FATAL: the bundle was not built for '${KIT_BASE_PATH}'. index.html references:"; \
      grep -o '\(src\|href\)="[^"]*"' "$index" || true; \
      exit 1; \
    fi

# ── serve ────────────────────────────────────────────────────────────────────
FROM node:20-alpine

# git again, for a different reason: `git-store.js` shells out to it to commit
# and push a write back to the corpus repo. Write-back is off in this chart until
# the credential question on kit#48 is answered, but the binary being absent
# would turn "not configured" into "crashed", and those must stay different.
RUN apk add --no-cache git

WORKDIR /app

# Only what the server actually loads. Copying `prototypes/behaviour-ast` whole
# would drag in `ui/node_modules` — a few hundred MB of build-time dependencies
# that the runtime never requires.
#
# `--chown=node:node` because the corpus is WRITTEN to: a write lands in the .beh
# file, and root-owned files under a non-root user would refuse every edit with an
# EACCES that reaches the browser as a 500.
COPY --from=build --chown=node:node /src/start.js ./start.js
COPY --from=build --chown=node:node /src/prototypes/behaviour-ast/*.js ./prototypes/behaviour-ast/
COPY --from=build --chown=node:node /src/prototypes/behaviour-ast/bindings.json ./prototypes/behaviour-ast/bindings.json
COPY --from=build --chown=node:node /src/prototypes/behaviour-ast/behaviours ./prototypes/behaviour-ast/behaviours
COPY --from=build --chown=node:node /src/prototypes/behaviour-ast/ui/dist ./prototypes/behaviour-ast/ui/dist

# Set at runtime as well as at build time, and both are read. The build used it to
# write the asset URLs; the server uses it to strip the prefix off incoming
# requests. `ui.js` reads the prefix back out of the bundle at startup and says so
# loudly if the two disagree, which is the check that catches an image built
# before this value changed.
ARG KIT_BASE_PATH=""
ENV KIT_BASE_PATH=${KIT_BASE_PATH}

USER node
EXPOSE 8080

# `--host 0.0.0.0` is safe here and only here. `ui.js`'s loopback rule refuses
# every write from a non-loopback address UNLESS a password is set — and the chart
# sets one from a secret. Password set means the session decides and the bind
# address stops being the gate; password unset on 0.0.0.0 means a READ-ONLY Kit,
# which is a degraded state rather than an open one.
#
# `--no-build` because the bundle was built above. Without it `start.js` would try
# to npm-install in a container that has no dev dependencies and no writable
# node_modules.
CMD ["node", "start.js", "--no-build", "--host", "0.0.0.0", "--port", "8080"]
