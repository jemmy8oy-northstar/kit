# Branch rulesets

The two rulesets every repo in this org should carry, as importable JSON.
Apply them in **Settings → Rules → Rulesets → New ruleset → Import a ruleset**,
or with `gh api -X POST /repos/OWNER/REPO/rulesets --input <file>`.

They are split in two deliberately: rulesets can only ever *add* restrictions,
never relax them, so a single ruleset covering both branches cannot say
"require `verify-branch` on `main` but not on `dev`". One ruleset that requires
a check on `dev` which only ever runs on `main` is what left every feature → dev
PR waiting on a check that could never report.

| File | Applies to | What it does |
|---|---|---|
| [`protected-branches.json`](protected-branches.json) | `main` + `dev` | No deletion, no force-push, changes must arrive via PR, and CI must pass |
| [`main-promotion-gate.json`](main-promotion-gate.json) | `main` only | The PR must come from `dev` (`verify-branch`), and it needs an approving review |

## Before you import: two things that will bite

**1. The GitOps App must stay in `bypass_actors`.** Both files carry its id —
the org variable `GIT_OPS_APP_ID`, the same one `docker-build-push.yml` reads.
An App id is not a credential (the private key is), so it is checked in here to
keep the JSON paste-ready. It has to be in `bypass_actors`, because the last
step of the release pipeline pushes the version bump straight to `main`:

```yaml
git commit -m "chore(gitops): release version ${{ ... }} [skip ci]"
git push origin HEAD
```

Without the bypass entry that push is rejected, so a promotion builds and
publishes the images and *then* fails to update the chart — the worst possible
place to stop, since the images exist but nothing points at them.

Be clear-eyed about the trade: a bypass actor is a real hole. That App can push
anything to `main`, not just a tag bump, which makes its private key as
sensitive as `main` itself. The alternative — having the pipeline open a PR
against itself for a one-line version bump — turns every release into a second
click, which is the opposite of what a GitOps loop is for.

**2. Import the rulesets only *after* CI has run at least once.**
`protected-branches.json` requires the `backend`, `frontend` and `e2e` contexts.
A required context that has never been reported does not fail — it stays
pending forever, and every PR is unmergeable with no way to force it. Merge the
PR that adds `.github/workflows/ci.yml` first, let one PR run green, then
import.

For the same reason, **the context strings must match the job ids in
`ci.yml` exactly**. Rename a job and you must rename it here too. Drop the
contexts a repo genuinely doesn't have: a backend-only repo should not require
`frontend` or `e2e`.

## What "require me to promote into main" means here

`main-promotion-gate.json` sets `required_approving_review_count: 1`. GitHub
does not let the author of a PR approve their own PR, so an automated account
cannot open a `dev` → `main` PR and merge it — it needs a human approval that
only a maintainer can give. Repository admins are in `bypass_actors`, so this
does not force *you* to find a second reviewer for your own promotions.

A stricter option, if that isn't enough: add `{ "type": "update" }` to this
ruleset. That blocks *all* writes to `main` for anyone without bypass,
including merges, so only an admin (or the GitOps App) can move the branch at
all. That rule used to be unusable here because it would have blocked the
release pipeline's push — adding the GitOps App to `bypass_actors` is what
makes it viable. It is a bigger hammer: it stops being "you approve the
promotion" and becomes "only you can promote, full stop".
