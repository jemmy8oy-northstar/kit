# BEH-USERS-9, slice B: the image one agent run starts from. A run is a short-lived
# Kubernetes Job (slice C) that clones the user's repo into scratch disk, lets Claude Code
# work headless, pushes, and exits. Nothing in this image is a secret: the run's env is.
FROM node:22-bookworm-slim

ARG CLAUDE_CODE_VERSION=2.1.318

RUN apt-get update \
 && apt-get install -y --no-install-recommends git curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && npm install -g "@anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}" \
 && npm cache clean --force

# Not root, and a home that is scratch: the Job mounts an emptyDir over /work.
RUN useradd --create-home --uid 10001 --shell /bin/bash runner \
 && mkdir /work && chown runner:runner /work
USER runner
WORKDIR /work
ENV HOME=/home/runner

COPY --chown=runner:runner entrypoint.sh /usr/local/bin/kit-run
RUN chmod 0555 /usr/local/bin/kit-run

ENTRYPOINT ["/usr/local/bin/kit-run"]
