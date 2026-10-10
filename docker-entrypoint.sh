#!/bin/sh
# Kit's container entrypoint: hosted edits that survive a restart (kit#117, kit#92).
#
# The image carries the corpus as plain files and no `.git`, so a deployed Kit
# has nothing to commit into and an edit lives on a disk that is discarded on
# restart. That is how James's BEH-SEED-2 approval was lost. So, when a push
# credential is configured, this clones the repo at startup and points Kit at
# the CLONE:
#
#   1. clone `$KIT_GIT_BRANCH` (default `kit/hosted`), the branch every hosted edit is
#      pushed to, so a restart picks up every edit made before it;
#   2. if that branch does not exist yet, clone `$KIT_GIT_BASE` (default `dev`) and
#      start it there. The first push creates it on the remote.
#
# Then every write commits and pushes to that branch (`KIT_GIT=1`), and nothing
# reaches `dev` except through a PR from it (his kit#118).
#
# 🔑 With NO credential this does nothing at all: the image's own corpus, write-back
# off, exactly as before. Merging this before the secret exists changes nothing,
# and a deployed Kit says so on every edit (kit#142).
#
# 🔴 If the clone FAILS (network, a typo in the URL) Kit still starts, on the
# image's corpus with write-back off, and says why on stderr. A Kit that refuses
# to start is an outage. A Kit that cannot save says so on every edit.
#
# The token never appears in a URL or an argument list. git's error text is
# echoed into write responses, so a token in the remote URL would be printed to
# whoever made the edit. It is handed over by a credential helper that reads the
# environment, configured through GIT_CONFIG_* so nothing is written to disk.
set -u

# The GitHub App credential (kit#88: the App does writes too) is a key, not a token, and a shell
# cannot sign its JWT — so the server mints one to clone with. That token expires within the hour;
# every push after it asks the server's token source for a fresh one (GitStore), not this variable.
# A static KIT_GIT_TOKEN, if both are set, is what the clone uses, as before.
if [ -n "${KIT_GIT_CLONE:-}" ] && [ -z "${KIT_GIT_TOKEN:-}" ] && [ -n "${KIT_GITHUB_APP_ID:-}" ]; then
  if minted=$(dotnet Balenthiran.Kit.WebApi.dll github-token) && [ -n "$minted" ]; then
    export KIT_GIT_TOKEN="$minted"
  else
    echo "kit-start: could not mint a GitHub App token to clone with; serving the image's own corpus with write-back OFF" >&2
  fi
fi

if [ -n "${KIT_GIT_CLONE:-}" ] && [ -n "${KIT_GIT_TOKEN:-}" ]; then
  work="${KIT_GIT_WORKTREE:-${HOME:-/tmp}/kit}"
  branch="${KIT_GIT_BRANCH:-kit/hosted}"
  base="${KIT_GIT_BASE:-dev}"

  # Never wait on a terminal that is not there: a refused credential fails, it does not hang.
  export GIT_TERMINAL_PROMPT=0
  export GIT_CONFIG_COUNT=1
  export GIT_CONFIG_KEY_0=credential.helper
  # shellcheck disable=SC2016 # expanded by the helper's shell when git calls it, not here
  export GIT_CONFIG_VALUE_0='!f() { echo username=x-access-token; echo "password=${KIT_GIT_TOKEN}"; }; f'

  rm -rf "$work"
  # Start from the base ONLY when the remote says the branch is absent (ls-remote
  # exits 2). Any other failure of the first clone, such as a network blip, used to
  # fall through to the base and fork a fresh branch beside the real one, so every
  # edit after it was a rejected push, lost on the next restart (kit#165).
  if git clone --quiet --single-branch --branch "$branch" "$KIT_GIT_CLONE" "$work" 2>/dev/null; then
    echo "kit-start: serving ${branch} from a fresh clone; edits are pushed back to it"
  elif git ls-remote --exit-code --heads "$KIT_GIT_CLONE" "$branch" >/dev/null 2>&1; then
    rm -rf "$work"
    echo "kit-start: ${branch} exists on ${KIT_GIT_CLONE} but could not be cloned; serving the image's own corpus with write-back OFF rather than forking it" >&2
  elif [ $? -eq 2 ] && git clone --quiet --single-branch --branch "$base" "$KIT_GIT_CLONE" "$work" \
    && git -C "$work" checkout --quiet -b "$branch"; then
    echo "kit-start: ${branch} does not exist yet; started it from ${base}. The first edit creates it"
  else
    rm -rf "$work"
    echo "kit-start: could not clone ${KIT_GIT_CLONE} (${branch} or ${base}); serving the image's own corpus with write-back OFF" >&2
  fi

  if [ -d "$work/.git" ]; then
    export KIT_DIR="$work/prototypes/behaviour-ast/behaviours"
    export KIT_GIT=1
    export KIT_GIT_BRANCH="$branch"
  else
    unset KIT_GIT KIT_DIR
  fi
fi

exec dotnet Balenthiran.Kit.WebApi.dll "$@"
