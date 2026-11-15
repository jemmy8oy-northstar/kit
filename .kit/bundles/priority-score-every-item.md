# Priority score on every item, including unscored ones
Work-item: priority-score-every-item
Flag: the note also says many items are unscored today; whether to backfill existing items or only score new ones is not stated; and the 10-30 ordering note may be a separate card from scoring (level: low)
Cards:
- new: every item, new or existing, carries a priority score
- new: a user can order idle priority as well as pick one, and can mark options as not a priority or as not for Kit to work on
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:34Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-30 00:38Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The owner asks that every item gets a priority score, because many items are unscored now. Check how scores are stored and where unscored items come from before building.
- Default until he says otherwise: score new items when they arrive, and record the backfill of existing unscored items as an unreviewed assumption rather than a silent migration.
- 10-30 00:38Z: the owner wants to order idle priority, not just pick one, and to decide which options are not a priority or that Kit should not work on them. Default: an ordered list of options, with "not for Kit" as a choice that keeps the item out of the Queue. Record the default as an unreviewed assumption.
