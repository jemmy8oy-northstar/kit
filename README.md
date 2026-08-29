# .NET Repo Template

A GitHub repository template for full-stack .NET + Node.js projects with CI, Docker builds, GitOps, and Claude Code integration pre-configured.

## Creating a New Project From This Template

Run the following command to scaffold a new project:

```
dotnet new web-template -n YourProjectName --appName your-project-name --no-includeWorkflows --force
```

- `--no-includeWorkflows` skips the workflow files so the ones already in this repo are preserved as-is
- `--force` allows the template to overwrite existing files — this README will be replaced, which is expected

After scaffolding, push the generated code to a feature branch and raise a PR into `dev`. Do not raise PRs directly into `main` — the branch protection rules will block it.

## Workflow Files

This template includes four pre-configured GitHub Actions workflows that are intentionally excluded from the scaffold command above.

| File | Purpose |
|------|---------|
| `ci.yml` | Builds **and tests** backend (.NET) and frontend (Node.js), plus Playwright e2e, on every pull request |
| `check-source-branch.yml` | Enforces that PRs into `main` must come from `dev` |
| `docker-build-push.yml` | Builds and pushes ARM64 Docker images to Oracle Container Registry (OCIR), then auto-bumps the Helm chart version via GitOps |
| `claude.yml` | Enables `@claude` mentions in issues and PRs to trigger Claude Code |

### Note: Writing to `.github/workflows/` needs an explicit permission

A GitHub App can only create or update files under `.github/workflows/` if it has been granted
**Workflows: Read & write**; without it the push is rejected outright. That is intentional GitHub
security behaviour, not a bug — a token that can rewrite CI can change what runs against your
secrets.

So unless that permission is currently granted, let Claude push everything else (source, configs,
Dockerfiles, Helm charts) and hand you any workflow change as file contents to commit yourself. The
workflows in this template are already correct and need no modification for a normal project.

## Branch Protection Rules

Apply **two** rulesets in **Settings → Rules → Rulesets → New ruleset → Import a ruleset**,
using the JSON checked into this template:

| File | Applies to | What it enforces |
|------|------------|------------------|
| [`docs/rulesets/protected-branches.json`](docs/rulesets/protected-branches.json) | `main` + `dev` | No deletion, no force-push, changes only via PR, **and CI must pass** |
| [`docs/rulesets/main-promotion-gate.json`](docs/rulesets/main-promotion-gate.json) | `main` only | The PR must come from `dev` (`verify-branch`), **and it needs an approving review** |

They were inlined in this README until now, which meant two copies drifting apart. The files are
the source of truth; see [`docs/rulesets/README.md`](docs/rulesets/README.md) for the full notes.

Together these enforce:
- `main` and `dev` cannot be deleted or force-pushed
- All changes to either branch go through a pull request
- Every PR is gated on the `backend`, `frontend` and `e2e` checks from `ci.yml`
- PRs into `main` must come from `dev` (the `verify-branch` check) and be approved by a maintainer

### Import them only after CI has run once

`protected-branches.json` requires the `backend`, `frontend` and `e2e` contexts. A required context
that has never been reported does not fail — it sits at *"Expected — waiting for status to be
reported"* forever, and there is no way to merge past it except an admin override. Merge a PR that
runs `ci.yml` first, then import.

The context strings must match the **job ids** in `ci.yml` exactly, and you should delete the ones a
repo doesn't have — requiring `frontend` in a backend-only repo blocks every PR.

### Why two rulesets

`check-source-branch.yml` only triggers `on: pull_request: branches: [main]`. It never runs for a
PR into `dev`, so it never posts a `verify-branch` status there.

If a single ruleset requires the `verify-branch` context on `main` **and** `dev`, every
`feature → dev` pull request waits forever on a check that will never report, and the only way to
merge is an admin override. That is not a visible failure — the PR simply sits at "Expected —
waiting for status to be reported" — so it reads as a flaky CI rather than a misconfiguration, and
overriding it becomes routine.

Splitting the required check onto a `main`-only ruleset removes the dead wait without weakening
anything: `main` still cannot be reached except by a PR from `dev`, and `dev` still cannot be
deleted, force-pushed, or written to outside a PR.

Rulesets stack, and stacking only ever *adds* restrictions — which is why the split is necessary
rather than merely tidy.

## Required Secrets and Variables

For the Docker build workflow to function, configure the following in **Settings → Secrets and variables**:

| Type | Name | Value |
|------|------|-------|
| Variable | `OCIR_REGISTRY` | e.g. `lhr.ocir.io` |
| Variable | `OCIR_NAMESPACE` | Your OCI tenancy namespace |
| Secret | `OCIR_USERNAME` | e.g. `tenancy/oracleidentitycloudservice/you@email.com` |
| Secret | `OCIR_AUTH_TOKEN` | OCI auth token (not your account password) |
| Secret | `CLAUDE_CODE_OAUTH_TOKEN` | OAuth token for Claude Code GitHub Actions |

The `GIT_OPS_APP_ID` variable and `GITOPS_APP_PRIVATE_KEY` secret are pulled from org-level settings and do not need to be set per-repo.
