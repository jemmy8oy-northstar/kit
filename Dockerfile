# syntax=docker/dockerfile:1

# Kit, as one image.
#
# The rest of the estate builds two — a .NET backend and an nginx frontend — and
# `docker-build-push.yml` asserted exactly that for months against a repo that has
# neither, which is why Kit's OCIR build "has never once succeeded". Kit is still
# ONE process serving the built SPA *and* the API on one port — but since kit#119
# that process is the C# server (`backend/Balenthiran.Kit.WebApi`), scored request
# by request against `ui.js` by the conformance goldens. Node is now build-time only:
# it builds the bundle and nothing in the running container needs it.
#
# node:20-alpine for the bundle because CI pins Node 20.

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

# ── build the server ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src

# The whole `backend/` rather than one COPY per project: a project added to the
# solution and missing from a per-project list fails restore ONLY here, after the
# merge — which is how balenthiran.co.uk's image went two days without building.
COPY backend/ ./backend/
RUN dotnet publish backend/Balenthiran.Kit.WebApi/Balenthiran.Kit.WebApi.csproj -c Release -o /out

# ── serve ────────────────────────────────────────────────────────────────────
# The Debian aspnet image, not Alpine: it ships ICU, and the project view sorts
# with `localeCompare` order (`ProjectReporter`), which invariant globalization
# would silently change.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=api /out ./

# The server finds its corpora by walking up from the working directory to the
# folder holding `prototypes/behaviour-ast/behaviours` (`KitSettings`), and serves
# the bundle from `prototypes/behaviour-ast/ui/dist` beside it — so that is the
# layout, under /app.
#
# `--chown=app:app` because the corpus is WRITTEN to: a write lands in the .beh
# file, and root-owned files under a non-root user would refuse every edit with an
# EACCES that reaches the browser as a 500.
#
# 🔴 Still no `COPY … bindings.json`: the flat file has not existed since bindings
# moved per corpus, and an explicit COPY of an absent path fails the build LATE.
# The per-corpus bindings arrive with the `behaviours` directory.
COPY --from=build --chown=app:app /src/prototypes/behaviour-ast/behaviours ./prototypes/behaviour-ast/behaviours
COPY --from=build --chown=app:app /src/prototypes/behaviour-ast/ui/dist ./prototypes/behaviour-ast/ui/dist

# Read at runtime too: the build wrote it into the asset URLs, the server strips
# it off incoming requests.
ARG KIT_BASE_PATH=""
ENV KIT_BASE_PATH=${KIT_BASE_PATH}

# `KIT_HOST` is the bind address the write gate asks about — `ui.js`'s `--host`.
# 0.0.0.0 is safe here and only here: with no password a non-loopback Kit refuses
# every write (READ-ONLY, degraded rather than open); the chart sets a password
# from a secret, and then the session decides. `ASPNETCORE_URLS` is where Kestrel
# actually listens, and the two must agree.
ENV KIT_HOST=0.0.0.0
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# git, for hosted write-back (kit#117): the entrypoint clones the repo at startup
# when a push credential is configured, and every edit is committed and pushed to
# `kit/hosted`. With no credential the entrypoint does nothing and git sits unused.
# ca-certificates because the clone is HTTPS.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates \
 && rm -rf /var/lib/apt/lists/*

COPY --chmod=755 docker-entrypoint.sh /usr/local/bin/kit-start

USER app
EXPOSE 8080

ENTRYPOINT ["kit-start"]
