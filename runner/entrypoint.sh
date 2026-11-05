#!/usr/bin/env bash
# BEH-USERS-9, slice B: one agent run. Clone, let Claude Code work headless, push, exit.
#
# Env contract (defined here; the launcher in slice A, IAgentRunLauncher, hands over only a
# run and its token, so slice C's Job must set these):
#   KIT_RUN_ID        required  Kit's id for the run (a branch name and log label)
#   KIT_URL           required  Kit's base URL, e.g. https://balenthiran.co.uk/kit (no trailing slash)
#   KIT_RUN_TOKEN     required  the per-run token; sent as x-api-key to Kit's model proxy
#   KIT_REPO          required  owner/repo to work on
#   KIT_TASK          required  what the user asked for
#   GITHUB_TOKEN      required  short-lived token that can push to KIT_REPO
#   KIT_BASE_BRANCH   optional  branch to start from (default: the repo's default branch)
#   KIT_MODEL         optional  model for the run (default: sonnet)
#   KIT_MAX_TURNS     optional  turn limit (default: 100)
#
# The model is reached only through Kit: ANTHROPIC_BASE_URL is <KIT_URL>/api/agent and the
# run token stands in for the API key. No real Anthropic key ever enters this container.
# Tokens are never echoed; git gets the GitHub token through a credential helper reading env.
set -euo pipefail

missing=()
for v in KIT_RUN_ID KIT_URL KIT_RUN_TOKEN KIT_REPO KIT_TASK GITHUB_TOKEN; do
  [ -n "${!v:-}" ] || missing+=("$v")
done
if [ "${#missing[@]}" -gt 0 ]; then
  echo "kit-run: missing required env: ${missing[*]}" >&2
  exit 64
fi

case "$KIT_REPO" in
  */*) ;;
  *) echo "kit-run: KIT_REPO must be owner/repo" >&2; exit 64 ;;
esac
case "$KIT_RUN_ID" in
  *[!A-Za-z0-9._-]*) echo "kit-run: KIT_RUN_ID has characters a branch name cannot" >&2; exit 64 ;;
esac

export ANTHROPIC_BASE_URL="${KIT_URL%/}/api/agent"
export ANTHROPIC_API_KEY="$KIT_RUN_TOKEN"
unset ANTHROPIC_AUTH_TOKEN
export CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1

# git authenticates from the env at call time, so the token is in no URL, config or log.
export GIT_TERMINAL_PROMPT=0
export GIT_CONFIG_COUNT=1
export GIT_CONFIG_KEY_0="credential.helper"
export GIT_CONFIG_VALUE_0='!f() { echo username=x-access-token; echo "password=$GITHUB_TOKEN"; }; f'

work="$(mktemp -d /work/run.XXXXXX)"
trap 'rm -rf "$work"' EXIT

echo "kit-run[$KIT_RUN_ID]: cloning $KIT_REPO"
git clone --quiet "https://github.com/${KIT_REPO}.git" "$work/repo"
cd "$work/repo"
git config user.name "Kit agent"
git config user.email "agent@kit.invalid"
if [ -n "${KIT_BASE_BRANCH:-}" ]; then
  git checkout --quiet "$KIT_BASE_BRANCH"
fi
branch="kit/run-${KIT_RUN_ID}"
git checkout --quiet -b "$branch"

# The process is served by Kit so a fix reaches every run at once. The route is public, so
# the run token is not sent: it goes only to the model proxy.
process="$work/process.md"
curl --fail --silent --show-error --location "${KIT_URL%/}/api/process" -o "$process"

echo "kit-run[$KIT_RUN_ID]: working"
# bypassPermissions, so the agent can run the repo's tests: acceptEdits refuses every command.
# The Job is the sandbox (scratch disk, no Kubernetes token, a capped run token, a GitHub token
# for one repo), which is the default proposed on kit#684 and met with silence.
claude -p "$KIT_TASK" \
  --append-system-prompt "$(cat "$process")" \
  --model "${KIT_MODEL:-sonnet}" \
  --max-turns "${KIT_MAX_TURNS:-100}" \
  --permission-mode bypassPermissions \
  --output-format text

# Push only if the agent left something behind; an empty run is a success with nothing to push.
if [ "$(git rev-list --count "HEAD" --not --remotes)" -eq 0 ] && git diff --quiet && [ -z "$(git status --porcelain)" ]; then
  echo "kit-run[$KIT_RUN_ID]: nothing to push"
  exit 0
fi
if [ -n "$(git status --porcelain)" ]; then
  git add -A
  git commit --quiet -m "kit run ${KIT_RUN_ID}: ${KIT_TASK:0:60}"
fi
git push --quiet origin "$branch"
echo "kit-run[$KIT_RUN_ID]: pushed $branch"
