# Notifications: home-screen app, feed, five-minute wake, next-wake indicator, iPhone push
Work-item: notifications-and-agent-wake-timing
Flag: the feed design is to be refined later and the churn-rate options are the owner's call, not yet decided; build only the cards and record the rest as unreviewed assumptions (level: high)
Cards:
- BEH-NOTIFY-2
- BEH-NOTIFY-1
- BEH-NOTIFY-3
- BEH-NOTIFY-4
- BEH-NOTIFY-5
- BEH-NOTIFY-6
- new: the notification feed also lists the agent's wakes and sleeps, such as updates, wakes and sleeps
- new: activity entries in the feed open the item they refer to
- new: a request to work overrides a suspend, not the other way round
- new: the agent does not sleep on its own to save usage unless the owner chose that in settings
- new: settings choose the agent's churn rate: wake on change, work tech debt when idle, suggest ideas when idle, or sit idle until told (owner to decide the options)
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
