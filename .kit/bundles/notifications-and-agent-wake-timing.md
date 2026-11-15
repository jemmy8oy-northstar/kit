# Notifications: home-screen app, feed, five-minute wake, next-wake indicator, iPhone push
Work-item: notifications-and-agent-wake-timing
Flag: the feed design is to be refined later and the churn-rate options are the owner's call, not yet decided; build only the cards and record the rest as unreviewed assumptions; the paused-item reask rule is my reading of 10-31 09:46Z (level: high)
Cards:
- BEH-NOTIFY-2
- BEH-NOTIFY-1
- BEH-NOTIFY-3
- BEH-NOTIFY-4
- BEH-NOTIFY-5
- BEH-NOTIFY-6
- BEH-NOTIFY-1 (already on the card: the feed lists wake and sleep kinds)
- BEH-ACTIVITY-2 (already a card: an activity entry links to the item it names)
- BEH-NOTIFY-7 (new: a request to work overrides a suspend)
- BEH-NOTIFY-8 (new: no sleep to save usage unless chosen in Settings)
- BEH-SETTINGS-6 (already built: the idle order in Settings is the churn-rate choice; no new card)
- BEH-BOARD-26 (already built: a paused card leaves the Inbox until its reask date; the bell lifts a paused card, BEH-BOARD-16)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 13:59Z (save to home screen, notifications)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z (notification feed)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z (wake after five minutes)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 22:50Z (indicator of next wake)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:32Z (no notifications on iPhone web app)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 13:12Z (activity navigates to the referred item)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:09Z (Kit did not wake after changes)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:13Z (suspend supersedes an action)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 15:05Z (Kit slept to conserve usage; asks for a churn-rate setting)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 09:46Z (paused Kit keeps reappearing in the inbox; reask date; bell may override)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Services/PushNotifier.cs
- backend/Balenthiran.Kit.Services/KitRouter.Push.cs
- backend/Balenthiran.Kit.Services/NotificationReader.cs
- backend/Balenthiran.Kit.WebApi/PushPoller.cs
- backend/Balenthiran.Kit.Database/WebPushSender.cs
- backend/Balenthiran.Kit.Tests/PushTests.cs
- backend/Balenthiran.Kit.Tests/NotificationTests.cs
Context:
- Five of the six cards already say these things (NOTIFY-1 to -6). The notes are asks for the same behaviour, so the gap is probably the build. Check each against the code before writing new code.
- The iPhone report (10-25 12:32Z) is a bug on BEH-NOTIFY-3. On iPhone, web push only works for a web app saved to the Home Screen, so BEH-NOTIFY-2 is a prerequisite. Test on a Home Screen install first.
- BEH-NOTIFY-4 is the five-minute wake from his last action. PushPoller.cs is where the timing lives. The 14:00Z note's reason is that Kit wakes quickly after changes and acts on them.
- The 10-26 23:09Z report (Kit did not wake after his changes) is a bug on BEH-NOTIFY-4. Reproduce it in PushPoller.cs before changing the timing.
- BEH-NOTIFY-5 covers the next-wake indicator on the notifications page; the 22:50Z note asks for the same.
- The feed's wakes and sleeps entries are the new card. The note says the feed design will be refined later, so build only what the cards say and record the rest as an unreviewed assumption.
- 10-25 13:12Z: an activity entry should link to the item it refers to. Add it to the feed.
- 10-26 23:13Z: a request to work should win over a suspend. Check the suspend check on the poller path before changing it.
- 10-28 15:05Z: Kit went to sleep to save the usage limit without being asked. The owner wants the churn rate configurable in Settings. Do not pick the options. Default until he decides: wake on change, no idle work, and record the open options as a question in the corpus.
- 10-31 09:46Z: a paused Kit should not keep appearing in the inbox. The owner suggests a reask-date tag, and that the bell can override the pause. The reask-date format is not decided: default to a reask date the owner sets when pausing, record it as an unreviewed assumption, and check how the inbox lists paused items before changing it.
- Spec stage, 2026-11-15 (what I found and did): the NOTIFY cards live in kit-ui.beh, not kit.beh. Feed wake and sleep kinds, the activity link (BEH-ACTIVITY-2), the idle-order churn choice (BEH-SETTINGS-6) and the paused reask date (BEH-BOARD-26) are already on cards and built, so I mapped those notes to them rather than adding duplicates. The built churn default is "work on test health when idle" and the built reask is "Ask again in 2 weeks" (pause writes today plus 14 days); the corpus wins over the bundle's defaults, so I did not change either.
- PushPoller.cs is Kit's 60-second web-push poll, not the five-minute wake. The wake timing lives in claude-code-bot (BEH-NOTIFY-4 says so), so the 10-26 23:09Z reproduction is a claude-code-bot test, not a Kit one.
- BEH-NOTIFY-6's pause date (2026-11-14) has passed, so the Inbox asks again; it stays on the Backlog as his 10-26 note said.
- Next step toward the goal: the build stage reads these cards and tests, starting with BEH-NOTIFY-2 on a Home Screen install.

Test plan:
- BEH-NOTIFY-2: the manifest and icon are served under Kit's path, the installed app's scope is that path, and a Home Screen install opens full screen with the K icon
- BEH-NOTIFY-3: a new feed entry is pushed once within one 60-second poll, an entry already pushed is not pushed again, and a restart starts from the newest entry without pushing it (existing card test, see kit-ui.tests.json)
- BEH-NOTIFY-3: a Safari tab (not a Home Screen install) shows "Add Kit to your Home Screen" and does not offer push, tested on a Home Screen install first
- BEH-NOTIFY-1: the feed lists wake and sleep entries with their time, kind and message, newest first, and an unknown kind reads as note (Notifications.test.tsx has the list test; add wake and sleep rows)
- BEH-NOTIFY-4: a burst of his actions every few minutes wakes the agent within 10 minutes, and five quiet minutes after the last action wakes it; reproduces the 2026-10-26 23:09Z report in claude-code-bot's scheduler tests, not in Kit's PushPoller.cs
- BEH-NOTIFY-5: the next-wake line reads state/suspend.json, says his pause is his, says an agent sleep wakes on his next action, and never shows an unreadable schedule as awake (Notifications.test.tsx, existing)
- BEH-NOTIFY-7: a note or approval wakes the agent during its own suspend; his own pause (set_by not bot) still holds it asleep (claude-code-bot scheduler tests)
- BEH-NOTIFY-8: a usage limit alone does not make the agent sleep; with an idle item on and Queue work absent it stays awake and works idle items (claude-code-bot scheduler tests)
- BEH-ACTIVITY-2: an activity entry naming a behaviour id links to that item's page in its project, and an id no longer in the project stays plain text (existing)
- BEH-SETTINGS-6: the idle order line round-trips, an old single-mode line still reads, and every item switched off means idle (existing)
- BEH-BOARD-26: a paused card with a later reask date is left out of the Inbox, and "Ask again in 2 weeks" re-dates it (existing, Inbox.test.tsx); ringing its bell shows it again is not tested yet, so add that row
