# Build-channel video playbook (snip-it BEH-BUILD-1, kit#723)

Every build-channel video goes through this file. It lives in kit, not snip-it, because its sources are his Kit notes and kit is private.

Loop: **CREATE → REVIEW → (owner watches) → RETURN → CREATE…**
The Owner's feedback log at the bottom is part of the loop. Read it at the start of CREATE and REVIEW. RETURN is not done until it has been updated.

## 0. Defaults (change only via RETURN)

- **Model:** the creative pass (concept, script, storyboard, type and motion choices) runs on **Fable**: it is less defined work, the one place his claude-code-bot#138 (2026-11-11) keeps Fable. Review runs on Opus. Cheaper models may do mechanical work only: rendering, frame extraction, file shuffling.
- **Tool:** Remotion (React, frame-driven), using the official `remotion-best-practices` agent skill rules. HyperFrames (HTML + GSAP, Apache-2.0) is the fallback. See Research notes §2.
- **Start from the template, not a blank project:** `video-templater/remotion/` on its `dev` branch (the shared Remotion kit: `lib.mjs`, `render-stills.mjs`, `render-video.mjs`) and `video-templater/videos/kit-build-channel-short/` (the first Short made with it). Copy, don't fork it into kit; a tool improved for one video goes back into video-templater by PR, so the next one starts from it (his #138: "pushing the tools you use to make videos to git so that they can be reused").
- **Format:** 1080×1920 vertical, 30 fps, 30–45 s. Make a 16:9 cut only as a derivative. His word (kit#723, 2026-11-06): "Yh shorts works tbh. We can have some long form content later".
- **Message spine:** *engineering tools for people who aren't engineers.* Kit gives a non-engineer the things engineers rely on: a spec, tests, review and a safe release. **Don't sell speed.** "Note to live in 48 minutes" was rejected as unimpressive.
- **Type system** (one family, two weights max):
  - Display: **Inter Display** or **Geist**, weight 700–800, 120–180 px on a 1080 px-wide frame, tracking −2 % to −4 %, line-height 0.95–1.05, at most 5 words per screen.
  - Support text: same family at 500–600 weight, 48–64 px, at most 2 lines.
  - Code and UI: **JetBrains Mono** or **Geist Mono**, 36–44 px, never below 32 px.
  - Load fonts explicitly (`@remotion/google-fonts` / `loadFont`). A fallback system font fails the video automatically.
- **Safe zone (Shorts):** keep text inside roughly x 60–960 and y 220–1500. Nothing important goes in the bottom ~420 px or the right ~120 px, where the app UI sits.
- **Palette:** dark neutral background, one accent colour, high contrast (≥ 7:1 for text). No gradients-as-decoration.

## 1. CREATE: checklist

Before writing anything:
- [ ] Re-read the **Owner's feedback log**, every entry. Write the 3 most relevant lessons at the top of the storyboard file.
- [ ] Confirm the creative session is on Opus. Record the model in the storyboard header.
- [ ] Pick a true story from Kit's real history: a real note, the real spec it became, real tests, the real live URL. Nothing staged that didn't happen.

Script:
- [ ] **Hook in 0–1.5 s** names the viewer's problem or a surprise in ≤ 7 words. It must not be a stat about speed.
- [ ] One idea per beat. Each beat is 2–4 s. Something moves or changes at least every ~1.5 s.
- [ ] Arc: *person without the tool → the tool engineers have → the same person, now with it → payoff (it's live, and it's protected).*
- [ ] The ending lands the message line ("Engineering tools. No engineer required." or similar), not a logo for 3 s.
- [ ] Read the whole script aloud at speaking pace. It should fit ≤ 45 s, even if there's no voice yet.

Storyboard and motion:
- [ ] Storyboard is one line per beat: on-screen text, visual, motion verb, duration.
- [ ] Show the product's real artefacts (note → spec lines → green tests → live page), restyled large. **No full-screen UI screenshots** at phone size.
- [ ] Motion: springs or eased `interpolate()` only. No linear easing, no opacity-only fades, no simultaneous entrances. Stagger elements 60–120 ms.
- [ ] Use kinetic type for the hook and the key line: word-by-word or line-by-line reveals, at most one emphasis colour per screen.
- [ ] Transitions are cuts or match-moves. No stock wipes or spins.
- [ ] Sound: plan beats against a ~110–125 BPM bed. Use a subtle tick or whoosh only on key reveals. It must still make sense muted, because captions carry everything. Licensed or self-generated audio only.

Build:
- [ ] All timing is frame-driven from `useCurrentFrame()`. No CSS transitions or animations, `Date.now()` or unseeded randomness.
- [ ] Render **stills at each beat's key frame first** and look at them before rendering the full MP4.

## 2. REVIEW: checklist (self-review before the owner sees it)

Run it as a blind-ish pass. A fresh Opus agent gets only the MP4 frames, this checklist and the feedback log, not my reasoning.
- [ ] **Feedback log replay:** for each logged complaint, does this cut repeat it? Answer each entry yes or no with a frame number.
- [ ] **Phone test:** view frames scaled to 390 px wide. Every word must be readable and none can sit in the UI danger zone.
- [ ] **Typography:** fonts actually loaded (check for fallback glyphs), at most 2 weights, display tracking tightened, no orphaned single words, no line over ~22 characters on display type.
- [ ] **Hook test:** do frames 0–45 (1.5 s) alone make someone want the next second? If the hook is a duration or metric, rewrite it.
- [ ] **Message test:** would a non-engineer come away thinking "I could do that"? Is "engineering tools for non-engineers" stated or shown?
- [ ] **Dead air:** no stretch over 1.5 s without change. Extract frames at 0.5 s intervals and look.
- [ ] **Truth:** every claim (live URL, test counts, timings) matches a real artefact, and you can point to it.
- [ ] **Benchmark:** put 3 frames next to an exemplar (Research notes §1). Would it sit in that company's feed without embarrassment? If not, name the gap and fix it before handing over.
- [ ] Hand over privately: MP4 plus a contact sheet image embedded on the Kit card or issue. **Nothing is posted publicly.** Publishing is the owner's decision.

## 3. RETURN: checklist (after the owner's feedback)

- [ ] **Append his feedback verbatim** to the Owner's feedback log: date, thread, the video it was about, and the quote.
- [ ] Distil every point into one or more **rules**, and edit the CREATE and REVIEW checklists or Defaults so each rule is enforced at a concrete step. A rule that lives only in the log hasn't been learnt.
- [ ] If he contradicts an existing default, his newest word wins. Change the default and note the old one in the log entry.
- [ ] Classify each point: *taste* (visual/type), *message*, *process* (model, tools) or *truth*. If two entries in a row are in the same class, that area is a standing weakness. Add a REVIEW item specifically for it.
- [ ] Commit the playbook change before starting the next cut, and reference the thread in the commit subject.
- [ ] Reply on the thread: TL;DR of what changed in the playbook plus the next cut's plan. No re-render goes up without the playbook diff.

## Owner's feedback log

### 2026-11-06, kit#723, draft 1 ("note to live in 48 minutes", 7 vertical HTML stills, no motion, browser fallback sans)
> "I don't like it... Which model built this? I think we definitely need to be using opus for this. The creative spark that opus has is crucial. It is worth researching workflows and skills used to make the best videos and follow that. Do not be afraid to use inspiration from online... Honestly mainly the fonts. The text looks really bad. This just looks like a really poorly made video. ... I don't really like the message of the video. I think the headline note to live in 48 minutes is not that impressive. ... A stronger message would be something like bringing engineering tools to non engineers. And this sort of message will become the message that we use to make kit. ... It is worth recording what my feedback is on a video and including it in the video creation playbook/loop. Maybe in the create stage and the review stage so that you actively know what you need to keep in mind. Maybe in the return stage as well make it clear that you need to update the playbook with my feedback otherwise u won't improve."

Rules derived:
- **process:** creative work runs on Opus. Research and borrow from strong published work.
- **taste:** typography is the main failure. Use the explicit type system and load fonts. Stills aren't a video, so real motion is required.
- **message:** speed is not the story. The story is engineering tools for non-engineers, and that becomes Kit's message too.
- **process:** this log feeds CREATE, REVIEW and RETURN.

### 2026-11-06, kit#723, format
> "Yh shorts works tbh. We can have some long form content later"

Rules derived:
- **process:** Shorts are the format (Defaults, Format). Long-form waits until he asks for it.

---

## Research notes

WebFetch was unavailable in this session, so these notes come from web-search result summaries. Items marked *(secondary)* come from vendor or community blogs and need checking against the primary page before anyone relies on them.

### §1 What strong short-form and launch videos do

- **Hook:** first 1–3 s. YouTube Studio reports "viewed vs swiped away" for Shorts. Vendor guides put a good hook at ≥ 70–80 % stayed. The figures vary by source *(secondary)*: https://www.opus.pro/blog/youtube-shorts-hook-formulas, https://www.vidiq.com/blog/post/viral-video-hooks-youtube-shorts, https://www.bytecap.io/research/youtube-shorts-retention-benchmarks
- **Captions:** bold sans (Montserrat/Inter/Helvetica class), outline or backing box on busy backgrounds, lower-middle placement above the bottom ~20 % UI band. Sizes quoted range from 48 to 75 px at 1080×1920, and display headlines should be far larger. Test on a phone *(secondary)*: https://blog.eklipse.gg/beginner-guide-2/youtube-shorts-caption-placement-safe-zone.html, https://blitzcutai.com/blog/best-caption-size-youtube-shorts-2026, https://www.opus.pro/blog/youtube-shorts-caption-subtitle-best-practices
- **Type in motion:** animate one property at a time, keep it subtle, and let hierarchy carry attention. Tracking and leading are design tools, not cleanup: https://schoolofmotion.com/blog/tiny-typography-decisions-motion-graphics, https://creativepro.com/typetalk-dos-and-donts-type-motion/. Kinetic type works when the motion reinforces meaning: https://hera.video/blog/kinetic-typography-video-generator-guide
- **Exemplars and what to borrow:**
  - **Linear:** dark base, neutral sans plus editorial serif, motion "present but never performative". Borrow the restraint and the single accent: https://one-page-orpin.vercel.app/blog/linear *(secondary essay on the site's motion)*.
  - **Vercel / Geist:** Swiss-style minimal type. Vercel Labs publishes a Remotion skill that encodes its look (dark theme, Geist via CDN, spring utilities). Borrow it as a ready type and motion baseline: https://playbooks.com/skills/vercel-labs/skill-remotion-geist/create-remotion-geist, https://basement.studio/post/the-birth-of-geist-a-typeface-crafted-for-the-web
  - **Fireship:** entertainment first, density over polish, and "if a frame doesn't entertain, teach, or transition, it gets cut". Borrow the cut discipline and the dry tone for code beats: https://skills.sh/jarmen423/skills/fireship-video-style *(secondary)*.
  - **Anthropic / Claude launch films:** minimalist editorial type and refined pacing, and these are cited as an inspiration by other launch-film studios. A 2026 Claude launch video was edited by Claude Code itself, so this is a credible precedent for an agent-made film: https://contra.com/p/o7lgr3QP-magic-patterns-screens-launch-video, https://jc-aiwiki.pages.dev/ai-video-content/fable-edits-own-launch-video *(secondary)*.
  - **Arc (Browser Company) / Raycast:** known for polished launch films, but I found no design breakdown of either. Watch them directly before borrowing anything.
- **Why agent-made Remotion videos look flat:** fallback fonts, linear easing, opacity-only fades, simultaneous entrances, flat colour. Fixes: load real fonts, use spring/interpolate motion and a shared theme object, and check stills before the full render: https://autoae.online/blog/claude-remotion-videos-look-flat, https://claudskills.com/skills/remotion-motion-graphics/ *(secondary)*.

### §2 Tooling

| Tool | Licence | Headless on linux-arm64 | Type and animation |
|---|---|---|---|
| **Remotion 4** | Source-available "Remotion License". **Free for individuals, for-profit orgs with ≤ 3 employees, and non-profits**. A company licence is needed only by entities outside that group. Collaborating teams' headcounts are combined (4+ people means pay). **v5.0 (announced) also counts contractors.** | Yes. It uses Chrome Headless Shell (Linux arm64 is listed as headless-shell only). **FFmpeg is bundled since v4** via `@remotion/compositor-linux-arm64-gnu` (H.264/H.265/VP8/VP9/ProRes), so the system ffmpeg isn't needed. `npx remotion browser ensure` fetches the browser. | React + `useCurrentFrame`/`interpolate`/`spring`, `@remotion/google-fonts`, `@remotion/captions` (`createTikTokStyleCaptions`), TikTok template |
| **HyperFrames** (HeyGen) | Apache-2.0 | Headless Chrome, but **requires system FFmpeg** (and Node ≥ 22, which the pod has: v22.23.4). arm64 support is not documented. | Plain HTML/CSS + GSAP, which is closest to the rejected draft's HTML pipeline. 21 agent skills including per-word kinetic type and deterministic-seek rules |
| **Revideo** | MIT (per listing) | Fork of Motion Canvas with function-call / CLI rendering. Needs ffmpeg. | Canvas scene graph and generators. Weaker on web fonts and CSS layout |
| **Motion Canvas** | MIT | Rendering is UI-button based; headless rendering needs a separate Puppeteer renderer | Canvas-only, great for code-explainer animation |

Sources: https://github.com/remotion-dev/remotion/blob/main/LICENSE.md, https://www.remotion.dev/docs/license/faq, https://www.remotion.dev/docs/5-0-migration, https://www.remotion.dev/docs/pricing, https://www.remotion.dev/docs/miscellaneous/chrome-headless-shell, https://www.remotion.dev/docs/ffmpeg, https://socket.dev/npm/package/@remotion/renderer/diff/4.0.187, https://www.remotion.dev/docs/captions/displaying, https://themenonlab.blog/blog/heygen-hyperframes-video-as-code, https://silenceper.com/en/article/2026-05-02-hyperframes-html-video-rendering/, https://github.com/redotvideo/revideo, https://www.remotion.dev/docs/compare/motion-canvas, https://github.com/motion-canvas/motion-canvas/pull/595/files

**Recommendation: Remotion.**
1. Its bundled arm64 FFmpeg sidesteps the pod's broken system ffmpeg. Every alternative needs a working system ffmpeg.
2. It has the strongest agent skill ecosystem: the official skill, the Vercel Geist skill and the digitalsamba toolkit.
3. Captions and fonts are first-class.
4. It's free for a solo dev or a company of ≤ 3.

Caveats: (a) the licence is source-available, not OSI, and v5 counts contractors, so re-check whenever headcount or the version changes. (b) Adopting a tool and installing packages is a platform choice. Raise it with the owner before installing. (c) If ffmpeg-static (npm; claims linux-arm64, Van Sickle builds; https://github.com/eugeneware/ffmpeg-static) proves to work, HyperFrames becomes viable and keeps the HTML skills already in use.

### §3 Published agent skills and workflows

- **Remotion official `remotion-best-practices`** (`npx skills add remotion-dev/skills`): rule files loaded on demand (fonts: Google/local; timing: interpolate, easing, spring; captions). It steers away from non-deterministic CSS animation, and durations are written as seconds × fps: https://www.remotion.dev/docs/ai/skills, https://mcpservers.org/claude-skills/remotion/remotion-best-practices
- **Vercel Labs `create-remotion-geist`:** a Geist dark theme, CDN fonts and spring utilities, i.e. a house style as a skill: https://playbooks.com/skills/vercel-labs/skill-remotion-geist/create-remotion-geist
- **HyperFrames skills** (`npx skills add heygen-com/hyperframes`): paused GSAP timelines, deterministic seeking, no `Date.now()`/random/timers, per-word kinetic type recipe: https://www.ui-skills.com/skills/heygen-com/hyperframes-animation, https://agskills.dev/heygen-com/hyperframes/gsap
- **digitalsamba/claude-code-video-toolkit** (MIT): `/video` scaffolds from templates (product-demo, sprint-review), a multi-session stage tracker, `/scene-review` (live preview), `/design` per-scene refinement, plus optional ElevenLabs voice. Its stage tracker is a model for this playbook's loop: https://awesome.ecosyste.ms/projects/github.com%2Fdigitalsamba%2Fclaude-code-video-toolkit, https://claudemarketplaces.com/skills/digitalsamba/claude-code-video-toolkit/video_toolkit
- **Common prescriptions across these:** write a precise brief (format, length, scene order, exact copy, how things move); render key stills and inspect them; iterate per scene: https://dev.to/remocn/making-a-launch-video-for-claude-opus-5-2jlo, https://autoae.online/blog/claude-code-remotion-tutorial *(secondary)*.

### §4 Messaging: "engineering tools for non-engineers"

Context: the competitors' line is "anyone can build apps in plain English". Replit's creator brief uses it, and Lovable positions itself for "the 99% who don't code": https://www.passionfroot.me/plc/replit-campaign, https://recapio.com/digest/the-startup-letting-99-of-people-build-apps-without-code-by-marketing-against-the-grain. Kit can't win on "builds an app from a prompt". Its difference is the **discipline**: spec, tests and review are what keep the thing working after day one. Make that the story.

Three storylines, all true to the real flow (plain-English note → agent writes spec, tests, code → live):

1. **"Engineers don't just write code."** (best hook)
   - 0–1.5 s: the line types itself out: *"Engineers don't just write code."*
   - Then: *"They write specs. Tests. Reviews."* Each word lands as a card.
   - Then: *"Now so can you."* Cut to the real typed note in plain English.
   - The note turns into spec lines, then tests going green, then the live page.
   - End: *"Engineering tools. No engineer required."*
2. **"This is the whole brief."** Open on the real one-sentence note, huge, with the hook *"This is all I wrote."* Pull back to reveal what the agent produced from it (N spec lines, N tests, a release), counted up with real numbers. End: *"You write the note. Kit does the engineering."* (Counts, not minutes.)
3. **"Vibe-coded vs engineered."** A split screen. Left: "an app from a prompt" breaks on the second change. Right: Kit's spec plus tests catch the same change, shown as a real failing-then-passing test. Hook: *"Your AI app works. Until you change it."* This is riskier: it must use a real Kit test failure, not a staged one.
