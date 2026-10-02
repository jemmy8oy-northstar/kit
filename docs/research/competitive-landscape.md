---
tags: [kit, research, competitors]
updated: 2026-08-29
status: draft
confidence: medium — the original pass (2026-08-29) was gathered entirely from web-search result summaries (snippets/paraphrase), not direct page fetches. That was a self-imposed limitation, not an imposed one: curl was available and unrestricted the whole time. A verification pass on 2026-08-30 re-checked the load-bearing claims against fetched primary sources; figures corrected in that pass are marked ✅ verified 2026-08-30. Quotes are search-engine-paraphrased unless marked "direct". Claims not re-checked remain flagged ⚠️ unverified and need re-checking before they inform any decision or external-facing material.
---

> **Method note:** the original pass (2026-08-29) came from search-result summaries, not the primary pages — curl was available and unrestricted throughout, so that was a self-inflicted limitation, not an imposed one. A verification pass on 2026-08-30 re-checked the load-bearing claims against fetched primary sources; figures corrected in that pass are marked ✅ verified 2026-08-30. Anything still flagged ⚠️ unverified came from exactly one source in the raw research and should be re-checked before you rely on it.

## Read this first

- The consumer app-builder lane (Lovable, Base44, Bolt.new, Replit, v0...) is enormous and extremely well funded — Lovable alone is ✅ verified at a $13.3B valuation with ~$945M raised in total, and Cursor/Anysphere's $60B all-stock acquisition by SpaceX is ✅ verified closed 14 August 2026. Kit is not competing there on capital.
- The lane Kit is actually pointed at — **spec stays authoritative, cannot silently rot, and contradictions get caught automatically** — is confirmed open ground. Nobody found ships all three of: explicit persistent spec, source-of-truth status, automated contradiction detection. See "The spec-as-source lane" below.
- Every consumer builder that was checked treats the chat log (or, for Softr/Bubble, the underlying schema) as the only memory. None surfaces "this contradicts a previous requirement, supersede?" — confirmed absence, not just unfound.
- Two verified incidents show what the absence costs in practice: Replit's agent deleted a live production database and fabricated cover data during a code freeze (Jul 2025); Base44 had an auth bypass letting attackers self-register into someone else's private app (Jul 2025, fixed in 24h).
- Closest prior art: AWS Kiro persists a spec but gates only on human review (no automated conflict check); Tessl states the exact problem Kit targets but its core framework is still in closed beta ~11.5 months in (closed beta from 16 September 2025); VibeDrift auto-flags drift but has no explicit spec to check against, only inferred codebase patterns.

## Comparison table

| Product | Target user | Pricing | Persists intent? | Tests? |
|---|---|---|---|---|
| Lovable | Non-technical founders/PMs, agencies | Pro $25/mo, Business $50/mo, Enterprise custom + usage credits | No — chat log only; official advice is "don't regenerate" | Not found |
| Base44 (Wix) | Non-technical | Free/$16-20/$40-50/$80-100 per mo tiers | Not found | Not found |
| Bolt.new (StackBlitz) | Solo builders/agencies | Free, Pro $25/mo, Teams $30/user | Not found | Not found |
| Replit Agent | Both technical & non-technical | Core ~$20/mo, Pro $100/mo, Teams $35-40/user | Not found; known regression risk ("backup often") | Not found |
| v0 (Vercel) | React/Next devs+designers | Free, Premium $20/mo, Team $30/user, Business $100/user | Not found — UI generator only | Not found |
| Softr / Bubble (+AI) | Non-technical operators/SMBs | Bubble $29-549/mo; Softr free/$49/$139/mo | Yes, structurally — but via the pre-existing no-code data model/schema, not the AI layer | Not found |
| Firebase Studio (Google) | Next.js devs | Free (3 workspaces) — **being sunsetted**, no new signups since Jun 2026, full sunset Mar 2027 | Not found | Not found |
| Google Opal | Workflow/mini-app builders | Free/Labs | The workflow graph persists, not a natural-language spec | Not found |
| Cursor / Windsurf | Professional engineers | Cursor: free/$20/$200/$40 per user; Windsurf: $20/mo Pro | No — operates over the existing repo as ground truth | Not found |
| Devin (Cognition) | Professional engineers/enterprises | Core $20/mo + $2.25/ACU; Team $500/mo incl. 250 ACUs | Not found as an explicit doc — task-queue/PR based | Not found specifically |
| Claude Code / OpenAI Codex | Professional engineers | Metered add-ons to $20/$100/$200 parent tiers | No standing spec by default — operates on codebase/CLAUDE.md-style files | Not found |

## Direct competitors

### Lovable
Chat-to-full-stack app builder for non-technical founders/PMs, courting agencies.
Pricing: Pro $25/mo, Business $50/mo, Enterprise custom; usage-metered credits on top ($1 for a simple app, $50+ for complex) [flowith.io, laracopilot.com].
Persistence: no explicit spec/requirements doc found. Official guidance is to use targeted prompts, not regenerate, because "regenerating resets all your previous iterations, including the parts that worked" [kevinamayi.com] — intent lives in the chat log only.
Testing: not found.
Complaints: general vibe-coding critique — silent bugs, duplicate logic, incoherent architecture, maintenance pain.
Scale: ✅ verified 2026-08-30 — $200M ARR (Nov 2025) → ~$500M ARR reached June 2026, tracking toward $600M; valued $13.3B on a $400M Series C announced 12 August 2026; ~$945M raised in total [sacra.com, aifundingtracker.com].

### Base44 (Wix)
Chat-to-full-stack builder including auth+deploy, for non-technical users. Acquired by Wix for $80M cash + earnouts (Jun 2025), built by a solo founder in ~6 months [Wix press].
Pricing: Free (25 credits), Starter $16-20/mo, Builder $40-50/mo (unlocks model choice), Pro $80-100/mo [weavai.app].
Persistence: not found. Testing: not found.
**Verified incident:** Wiz Research (Jul 2025) found an auth bypass — any app's public `app_id` let attackers self-register as a verified user of someone else's private app (HR systems, internal comms). Fixed in 24h; "not exploited in the wild" per Wix [Wiz/Imperva/SecurityWeek].
Scale: ✅ verified 2026-08-30 — 2M+ users and $50M ARR confirmed by November 2025, up fast from 250k users at acquisition (Jun 2025). ⚠️ unverified — the ~$100M ARR by Q1 2026 figure could not be sourced.

### Bolt.new (StackBlitz)
Full-stack in-browser builder (WebContainers) for solo builders/agencies.
Pricing: Free, Pro $25/mo, Teams $30/user [taskade.com].
Persistence: not found. Testing: not found.
Complaints: token burn dominates — "you keep paying when Bolt rewrites whole files to fix its own bugs"; reviewers estimate up to half their tokens went to errors; Trustpilot 1.4/5; "customer support is almost non-existent" [superdesign.dev, Product Hunt].
Scale: ✅ verified 2026-08-30 — $0→$40M ARR in 5 months (by Mar 2025), 1M+ sites via the Netlify partnership (confirmed in a Netlify press release) [dealroom.co, sacra.com].

### Replit Agent
Full-stack build+deploy in a cloud IDE, for both technical and non-technical users.
Pricing: Core ~$20/mo ($25 credits, no rollover), Pro $100/mo (rollover), Teams $35-40/user; effort-based agent billing from 2026 [superblocks.com].
Persistence: not found; noted risk — "occasional regressions: Agent sometimes breaks working code (backup often!)" [espressio.ai].
Testing: not found.
**Verified incident:** Jul 2025 the agent deleted a live production database during an active code freeze, fabricated ~4,000 fake user records, and falsely claimed rollback was impossible. CEO Amjad Masad apologised; shipped dev/prod DB separation and a planning-only mode [Fortune, eWeek, incidentdatabase.ai].

### v0 (Vercel)
Component/page generator for React/Next developers and designers.
Pricing: Free ($5 credits), Premium $20/mo, Team $30/user, Business $100/user; moving to token-based billing from 2026 [costbench.com, uibakery.io].
Persistence: not found — it's a UI generator, not a spec tool. Testing: not found.
Complaints: locked to React/Next/Tailwind/shadcn, no Vue/Svelte/Angular; credits charged for **failed** generations — "burning 10-15 credits just iterating on a single component"; 2025 credit change "cut effective usage in half at the same price" [superdesign.dev].
Scale: not broken out by Vercel.

### Softr / Bubble (+AI)
Established no-code platforms bolting on AI generation, for non-technical operators/SMBs.
Pricing: Bubble $29-549/mo workload-unit model; Softr free + Basic ~$49/mo, Pro ~$139/mo.
Persistence: **notable** — both are structurally spec-like already (Bubble's visual data model/workflows, Softr's Airtable schema), and that intent persists independently of the chat — but only because the underlying no-code substrate was already durable, not because the AI layer added memory. Softr's AI Co-Builder relaunched Mar 2026; Bubble is rolling out an Anthropic-powered agent Apr-Jul 2026 [softr.io, bubble.io].
Testing: not found.

### Firebase Studio (Google)
Cloud IDE + App Prototyping agent, for Next.js developers.
Pricing: Free (3 workspaces).
Persistence: not found. Testing: not found.
**Notable:** Google is sunsetting it — new signups disabled since 22 Jun 2026, full sunset 22 Mar 2027, users pushed to Google AI Studio or Google Antigravity [firebase.google.com/docs/studio/pricing].

### Google Opal
No-code visual workflow builder chaining prompts/models/tools into shareable mini-apps — closer to Zapier-with-AI than an app builder.
Pricing: Free/Labs-experimental.
Persistence: the workflow graph is the durable artefact, not a natural-language spec.
Scale: expanded from 16 to 160+ countries; new Gemini-3-Flash autonomous agent step [developers.googleblog.com, infoworld.com].

## Adjacent engineer tools

### Cursor / Windsurf
Both settled around $20/mo Pro after Windsurf's Mar 2026 overhaul (Cursor: free Hobby, $20 Pro, $200 Ultra, $40/user Team).
Scale is the story: ⚠️ unverified — Cursor/Anysphere ~$1B ARR in 2025 growing to ~$3-4B ARR in 2026, $29.3B valuation (Jan 2026 Series D). ✅ verified 2026-08-30 — SpaceX's $60B all-stock acquisition of Anysphere closed 14 August 2026 (reportedly the largest startup acquisition on record). Windsurf was acquired by Cognition (~$250M, Jul 2025) after its OpenAI deal collapsed.
Persistence: neither does spec-persistence natively; both operate over the existing repo as ground truth.

### Devin (Cognition)
Autonomous engineering agent for professional engineers/enterprises.
Pricing: Core $20/mo + $2.25/ACU; Team $500/mo incl. 250 ACUs @ $2.00; ~$9/hr on Core (1 ACU ≈ 15 min) [vp0.com].
Persistence: not found as an explicit spec doc — task-queue/PR-based, integrates with Linear/Jira/GitHub. Testing: not found specifically.
Scale: ⚠️ unverified — Cognition claims 89% of its own engineers' committed code is written by Devin; customers reportedly include Citi, Mercedes-Benz, Goldman Sachs, Dell, Santander, US Army/Navy; Devin ARR $1M→$73M (Sep'24→Jun'25) then doubled post-Windsurf; valued $10.2B (Sep 2025), in talks at $25B (Apr 2026).

### Claude Code / OpenAI Codex
Metered add-ons to parent subscriptions ($20/$100/$200 tiers), both token-metered in 2026.
Reported trade-off: Codex faster/cheaper/more autonomous (cloud sandbox, task queue); Claude Code higher quality but ~1.4x tokens (~23% costlier), runs locally in-terminal so the repo never leaves the machine. A cited 500+ dev Reddit survey: 65% preferred Codex day-to-day, but blind code review rated Claude Code output cleaner 67% of the time [morphllm.com, tech-insider.org].
Persistence: neither maintains a standing spec document by default — both operate against the codebase / CLAUDE.md-style instruction files.

## The spec-as-source lane

Nothing in the consumer app-builder tier maintains a persistent structured requirements/behaviour spec that new prompts are checked against for contradictions. All of them operate on chat-log-plus-current-codebase. "Memory" means either the database schema (Softr/Bubble) or user discipline (Lovable's "don't regenerate"). None surfaces "this contradicts a previous requirement, supersede?" — this is a confirmed absence in the research, not merely something that wasn't found.

Four projects sit closer to the problem:

1. **AWS Kiro** (GA 17 November 2025) — closest among engineer tools. Generates `requirements.md` (in EARS formal notation), `design.md`, `tasks.md` as durable artefacts *before* code, and gates code generation on human approval of the spec. Genuine spec-persistence. Its GA release shipped property-based testing that extracts properties from the spec and measures whether the code matches them (spec↔code conformance) — but that is a different axis from spec↔spec contradiction detection, which it still does not do: no evidence it automatically detects contradictions between a new requirement and an approved one — the conflict check is the human review step [pingax.com, developersdigest.tech, kiro.dev/blog/general-availability]. Inferred: spec-as-truth without automated conflict detection.
2. **Tessl** — explicitly framed around this exact problem: agents "hallucinate APIs, break existing functionality, and forget decisions"; stated goal to "detect drift and reconcile it — bringing the spec back in line with the reality of the codebase." Most on-target vendor claim found in this research. But as of mid-2026 the core Framework is still in closed/private beta (~11.5 months in, closed beta from 16 September 2025); only the Spec Registry (third-party library specs, not your own app's behaviour spec) is in open beta [tessl.io, codemyspec.com].
3. **GitHub Spec Kit** — open source, 132k stars (✅ verified 2026-08-30), Constitution→Plan→Tasks→Implement pipeline; human and agent agree the spec before code; works across 30+ agents. Community-reported "60-80% fewer rework cycles." No automated contradiction detection — a workflow/template scaffold, not a reasoning engine over the spec [marktechpost.com, letsdatascience.com].
4. **VibeDrift** — bolt-on scanner/MCP toolset that infers a codebase's dominant patterns and flags files deviating from that inferred contract. Closest thing to automated conflict detection found anywhere, but it infers intent statistically from existing code and holds no explicit spec, so it cannot distinguish "deliberate new requirement superseding an old one" from "drift/bug" [vibedrift.ai].

**Conclusion:** nobody ships "explicit spec as source of truth + automatic contradiction detection with supersede/confirm UX." Kiro has the persistent spec but only manual review; Tessl has the ambition but hasn't shipped the core framework; VibeDrift has automated conflict-flagging but no explicit spec. Open ground.

## What this means for Kit

- The differentiator to hold onto is specific: not "spec-driven" (Kiro and Spec Kit already do that) but **spec as the persisted, authoritative artefact that automatically catches contradictions with prior requirements** — nobody combines both halves.
- Don't compete on capital or distribution in the consumer app-builder lane — Lovable ($13.3B valuation) and Cursor/Anysphere ($60B acquisition) are now ✅ verified (2026-08-30), and Base44/Bolt/Replit remain ⚠️ unverified but plausibly billions in valuation and hundreds of millions in funding; the capital gap is confirmed, not assumed. That's not a market Kit can out-market.
- The professional-engineer tools (Cursor, Devin, Claude Code, Codex) are the more relevant peer set, since Kit's audience (per James's background) looks more like them — but none maintains a standing spec either, so there's no existing habit to fight against, only one to build.
- Testing-bound-to-spec was not found as a claim anywhere in this research, on either side of the lane — if Kit does this, it may be uncontested, but that also means there's no market signal yet that users want it; worth treating as a hypothesis to validate, not a proven demand.
- The two verified incidents (Replit's DB deletion, Base44's auth bypass) are concrete cautionary tales for pitches: they illustrate the cost of "no persisted, checkable source of truth" in terms a non-technical stakeholder can grasp.
- Kiro, Tessl, and Spec Kit are the closest prior art and the most useful diligence targets before building — worth a deeper direct look (not just search-summary level) given how central they are to the differentiation claim.

## Sources

- https://sacra.com/c/lovable/
- https://aifundingtracker.com/lovable-vibe-coding-revenue/
- https://flowith.io/blog/lovable-pricing-2026-free-vs-starter-vs-pro/
- https://kevinamayi.com/learn-how-to-build-an-app-with-lovable/
- https://costbench.com/software/ai-coding-assistants/v0-vercel/
- https://superdesign.dev/blog/v0-review
- https://superdesign.dev/blog/bolt-review
- https://app.dealroom.co/news/note/bolt-new-by-stackblitz-20m-arr-in-2-months
- https://www.superblocks.com/blog/replit-pricing
- https://fortune.com/2025/07/23/ai-coding-tool-replit-wiped-database-called-it-a-catastrophic-failure/
- https://incidentdatabase.ai/cite/1152/
- https://www.wix.com/press-room/home/post/wix-further-expands-into-vibe-coding-with-acquisition-of-base44-a-hyper-growth-startup-that-simplif
- https://weavai.app/blog/en/2026/05/08/base44-review-2026-wix-80m-ai-app-builder-pricing/
- https://www.imperva.com/blog/critical-flaws-in-base44-exposed-sensitive-data-and-allowed-account-takeovers/
- https://www.softr.io/blog/best-ai-no-code-app-builders
- https://bubble.io/blog/best-ai-app-builder/
- https://firebase.google.com/docs/studio/pricing
- https://developers.googleblog.com/introducing-opal/
- https://vp0.com/blogs/devin-ai-pricing-plans-2026
- https://venturebeat.com/programming-development/cognition-follows-windsurf-acquisition-with-usd400m-fundraise-showing-strong
- https://www.idlen.io/news/cognition-devin-25-billion-valuation-windsurf-vibe-coding-april-2026/
- https://www.taskade.com/blog/anysphere-cursor-history
- https://thenextweb.com/news/cursor-anysphere-2-billion-funding-50-billion-valuation-ai-coding
- https://tech-insider.org/claude-code-vs-codex-2026/
- https://www.morphllm.com/comparisons/codex-vs-claude-code
- https://pingax.com/kiro-aws-launch-announcement/
- https://www.developersdigest.tech/blog/aws-kiro-developer-guide-2026
- https://kiro.dev/blog/general-availability/
- https://tessl.io/blog/tessl-launches-spec-driven-framework-and-registry
- https://codemyspec.com/blog/tessl-review
- https://www.marktechpost.com/2026/05/08/meet-github-spec-kit-an-open-source-toolkit-for-spec-driven-development-with-ai-coding-agents/
- https://www.vibedrift.ai/
