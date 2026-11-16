# Kitstudio splash page with a register-interest flow, served from the preprod cluster
Work-item: kitstudio-splash-register-interest
Flag: BEH-SPLASH-1 already holds both notes and is built and approved; it is pending only because its test lives in balenthiran.co.uk, so no spec was edited (level: low)
Review: agreed — BEH-SPLASH-1 holds all three notes, is built and approved, and the www fix is balenthiran.co.uk#42 (promotion of #40); nothing to build, no spec edit.
Cards:
- BEH-SPLASH-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 20:15Z (splash page for production kit, register-interest page, preprod cluster for now, public IP question)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 23:50Z (kitstudio splash with register-interest flow, powered by the balenthiran backend?)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 14:22Z (www.kitstudio.dev seems broken; what do I need to change)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
BEH-SPLASH-1 in kit-ui.beh holds both notes. Its contracts say kitstudio.dev is served by the balenthiran.co.uk pods, whose frontend renders the splash, and interest is stored by balenthiran's existing register-interest API under a hidden "kit" project. So the balenthiran backend question is answered, and the public IP question is answered by using the same pods on the preprod cluster, not a new one. Built in balenthiran.co.uk#33 and #34, promoted in #37 on 2026-11-03, and the owner approved it.
Still open: the card stays pending only because its test is in balenthiran.co.uk. Unless the owner says otherwise by 2026-11-09, the card moves to balenthiran's spec. The www.kitstudio.dev fix is balenthiran.co.uk#42, which goes live when he merges it. The wording stays unpublished until he approves it.
Nothing to build here. A builder should not change the splash page unless the owner asks.
2026-11-16: the owner reports www.kitstudio.dev broken and asks what to change. Answer from this bundle: the fix already in hand is balenthiran.co.uk#42, which goes live when he merges it. No spec change; kit.beh is not edited, and BEH-SPLASH-1 stays as it is.
