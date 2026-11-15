# Notifications: home-screen app, feed, five-minute wake, next-wake indicator, iPhone push
Work-item: notifications-and-agent-wake-timing
Flag: the notification feed should also carry wakes and sleeps, which no card says yet, and the note says the feed will be refined later (level: high)
Cards:
- BEH-NOTIFY-2
- BEH-NOTIFY-1
- BEH-NOTIFY-3
- BEH-NOTIFY-4
- BEH-NOTIFY-5
- BEH-NOTIFY-6
- new: the notification feed also lists the agent's wakes and sleeps, such as updates, wakes and sleeps
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 13:59Z (save to home screen, notifications)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z (notification feed)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z (wake after five minutes)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 22:50Z (indicator of next wake)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:32Z (no notifications on iPhone web app)
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
- BEH-NOTIFY-5 covers the next-wake indicator on the notifications page; the 22:50Z note asks for the same.
- The feed's wakes and sleeps entries are the new card. The note says the feed design will be refined later, so build only what the cards say and record the rest as an unreviewed assumption.
