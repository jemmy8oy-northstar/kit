# Project home shows the pipeline as dots on a stage timeline
Work-item: project-pipeline-timeline
Flag: unsure whether he wants this now or later, since the note is a vision and not a fault; and it changes what Kit shows on the project home page (level: high)
Cards:
- BEH-UI-7
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 23:20Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
- ui/src/tickets.ts
- ui/src/components/Tickets.tsx
Context:
Owner wants the project home page to show, at a glance, what the pipeline is working on now: one dot per active work item, coloured by its stage, laid out as a timeline across the stages. New card BEH-UI-7 is unreviewed and sits beside BEH-UI-6 (same area, same data). Its nouns (pipeline:Running, diagram:StageTimeline, page:ProjectHome) are not bound yet. Builder: write the test first, map it in kit.tests.json, and do not edit other cards.
