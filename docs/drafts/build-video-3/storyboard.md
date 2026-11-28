# Build-channel video, draft 3: "Can't code? Ship like an engineer."

- **Model:** Opus (claude-opus-5-5) did the creative pass: concept, script, storyboard, type and motion.
- **Tool:** Remotion 4.0.541, headless shell on linux-arm64, bundled ffmpeg.
- **Format:** 1080×1920, 30 fps, 1012 frames = **33.7 s**, H.264, no audio.
- **Fonts (`@remotion/google-fonts`):** Inter 800 display, Inter 600 support, JetBrains Mono 500. Nothing on screen below 36 px.
- **Palette:** bg #0A0A0B, text #F5F5F4, muted raised to #B8B8BE (≥ 8:1 on card and bg), one accent #C8F560.

## 3 lessons from the Owner's feedback log (kit#723, 2026-11-06)
1. **Typography was the main failure.** Strict loaded type system; every small line (credits, comments) ≥ 36 px or gone.
2. **Stills aren't a video.** Springs/eased interpolate only, staggered entrances, a push-in (1.00→1.04) on every beat, holds ≤ ~1 s.
3. **Speed is not the story.** Engineering tools for non-engineers. No durations on screen; "the same evening" appears only as provenance.

## Draft 2 review points → draft 3
- Hook about engineers → hook about the viewer: **"Can't code? Ship like an engineer."** (6 words). "Engineers don't just write code." moves to beat 2.
- Unintroduced "he" → the note card is headed **"note from James · 2 Nov, 17:53"**, so the spec's "he" has a referent. No claim about his background.
- Mock "Live" beat → **Kit's real chat screen** (`public/chat-acted.png`, 780×1688 render), cropped to message → "Done" → card → Undo, slow push-in 1.12→1.33, accent rings on the request, "Done" and Undo.
- Phone test → credit lines kept only at 36 px mono (`commit fc7f8c7`, `PR #633`); comments 40 px; muted grey brightened.
- Dead air → holds ≤ ~30 f, push-in 1.04 on every beat, highlight sweeps in the holds.
- Top-heavy → hook/now-you centred vertically; product beats run headline → card → footer to y≈1450.
- "Typos and all" (no typo exists) → full verbatim note, line "Plain English. Word for word."

## Beats

| # | Frames (s) | On-screen text | Visual / motion | Real artefact |
|---|---|---|---|---|
| 1 | 0–60 (2.0) | "Can't code? / Ship like an / engineer." | Word springs, centred y≈700–1150; accent underline under "engineer." f28–44; push-in | Hook (no claim) |
| 2 | 60–108 (1.6) | "Engineers don't just write code." | Word springs, centred; push-in | Framing |
| 3 | 108–186 (2.6) | "They also write / Specs. / Tests. / Reviews." + `// what it must do` etc. (40 px) | Cards spring up 9 f apart, fill y 330–1420 | Framing |
| 4 | 186–258 (2.4) | "Now so can you." / "No code. Just say what you want." | Centred word stagger | Framing |
| 5 | 258–398 (4.7) | Tab bar Note·Spec·Tests·Review. "Start with a note." Card "note from James · 2 Nov, 17:53" + full note. "Plain English. Word for word." | Note types at 40 px; approve/deny/undo button underline as typed | kit.notes.md 2026-11-02 17:53Z, verbatim |
| 6 | 398–512 (3.8) | "Kit writes the spec." / "Kit's spec · BEH-CHAT-6" / quoted title / "What it must do, in writing." | Tab springs to Spec; lines rise; highlighter on "and he can undo it." f52–66; support rises f72 | BEH-CHAT-6 title, verbatim |
| 7 | 512–632 (4.0) | "Then the tests." 4 rows / "Merged with 9 tests." | Match-move spec phrase → row 1; checks pass; counter 0→9 | kit-ui.tests.json at merge `8c0f77f` (9 tests); rows paraphrase real test names |
| 8 | 632–736 (3.5) | "Then a review." / "Review found a bug:" / "It could act twice." (struck) / "Fixed. Tested." / `commit fc7f8c7 · "fixes from review"` | Match-move row 4 → struck line | commit fc7f8c7 |
| 9 | 736–882 (4.9) | "Say it. / Kit does it." / Kit screen / "And you can undo it." / `PR #633 · on Kit the same evening` | Real screenshot: ring on request f14, "Done" sweep f40, chat scrolls up f52–78 (request leaves cleanly), slow push 1.24→1.30 on card + Undo, Undo ring + pulse f78 | docs/screenshots/ui/chat-4-agent-acted-390.png; PR #633 merged 18:35Z after 17:53Z note |
| 10 | 882–1012 (4.3) | "Engineering tools. / No engineer required." / "• Kit" | Two-line stagger, underline, Kit mark | Message line |

## Render
`node render-video.mjs` bundles once and renders `out/draft3.mp4`, `out/contact-sheet-3.png`, `stills/` (11 key frames) and `review3/` (every 15 f, at 390 px wide for the phone test) from that same bundle. System/bundled ffprobe was denied, so review frames come from Remotion's renderer, not from decoding the MP4.

## Draft 3b fixes (coordinator)
- Screen beat: heading 116 px at y 236, ≥ 50 px above the panel. Hard cut at f56 from shot A (whole request bubble) to shot B (crop starts at y 414, below the bubble), so no half-cut bubble. Push-in 1.28→1.32.
- Note/spec/tests/review blocks grown down to y≈1450 (note 44 px mono, spec 56 px, rows 112 px apart, review stack lower).
- Test rows now in Inter 600 48 px (paraphrases, not literal test names).
- Outputs: out/draft3b.mp4, out/contact-sheet-3b.png; stills/ and review3/ regenerated from the 3b bundle.

## Still weak
- The screenshot shows Kit acting on a holiday-planning item (a later real screen), not on BEH-CHAT-6 itself; the beat claims only that Kit does it and you can undo it.
- Longest push-in-only holds: they-write ~34 f, screen ~36 f, close ~34 f.
- No audio bed yet.
