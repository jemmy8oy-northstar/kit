# Notes on snip-it

Left in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.

## 2026-10-20 22:42Z · new behaviour

snip it needs feature organisation

## 2026-10-20 22:43Z · new behaviour

in the ios app - it looks incredible as a tele prompter but currently the speach models arent loading

maybe we could have some errors messages when this happens

maybe even model management in settings rather than it just trying to download when you hit prompt

## 2026-10-20 22:43Z · new behaviour

please set up approprriate code magic build hooks as well maybe we need a ios release branch

## 2026-10-20 22:44Z · new behaviour

I would like some camera controls as well I would like to be able to use the iphone 17s landscape selfie video recording

## 2026-10-20 22:45Z · new behaviour

Maybe some in app video viewing as well - is this difficult?

## 2026-10-20 23:52Z · new behaviour

The teleprompter should auto progress as the user speaks

## 2026-10-20 23:53Z

The teleprompter should highlight words that it has heard being spoken in a take. Currently the feedback is low I can tell if it’s hearing me at all.

## 2026-10-21 00:01Z · new behaviour

It would be good to be able to work on different projects or productions or videos videos might get confused with takes 

Anyway would be good to enter and click new production or select an existing one

## 2026-10-21 00:02Z · new behaviour

Would be good to show from a script what text has been covered by what cuts I don’t know whether this is too advanced

## 2026-10-21 00:03Z · new behaviour

Would be good to be able to fold script sections or have an option to fold all sections

Maybe we can use a simple # for names or —- for unnamed break and borrow this from markdown to split sections in the script. They should be able to teleprompt seamlessly but good for planning

## 2026-10-21 00:05Z · new behaviour

Would be good to have a delete all button in the script. Maybe a clear section button would be better. Maybe a section management where u can rearrange sections and then in here there is a delete all sections button. All delete buttons should have confirmation

## 2026-10-21 07:08Z · new behaviour

Settings to change teleprompting location and size

When close up you can see my eyes flickering as I read

Settings to change the screen width that it uses

Settings to also change on which point of the screen it shows

I feel that when recording in a selfie camera it can look more natural to be looking at yourself

So maybe we move the focus down to the centre of the screen

## 2026-10-21 16:47Z · new behaviour

Yours studio has a configurable ui with some premium or experimentation features

For now this experience can be toggled in settings but later this can be configured by accounts or build args - maybe I can just use a test version but certain features get switched off for rc builds

Anyway for now it is configurable

I guess whenever it is in premium/experimental mode for me I can always optionally toggle it off

## 2026-10-21 16:49Z · new behaviour

Base general release version: for now no api integration, fully on device

App premium feature: integrates with kit, kit has a volume on which it takes can be saved to - not saved by default - kit can then operate on the videos on server with low friction.

## 2026-10-21 16:52Z · new behaviour

Kit shows transcript with take locally

## 2026-10-22 07:27Z · BEH-EDITION-3

Oh I didn’t mean the volume is on kit I meant the volume is on snip it

## 2026-10-29 08:05Z

I just wrote something I want to take it backish. Only as an experimental feature should scripts come from snip it. For general access it’s just written in the app

## 2026-10-31 01:08Z · BEH-EDITION-3

Let’s pause for now I don’t have any footage to add yet. But when I do we can add it

## 2026-11-02 18:50Z

Snip it idea. I was saying about being blocked on filming. I am finding it difficult to film and release content. I am thinking we can keep the current channel as a more personal channel where I post videos when I have time or have something significant that I want to update on. And then create a new channel where the posting frequency is higher and we can use my interactions with kit to form the videos! Using messages from kit chat or from kit notes and show maybe show them being typed out. And then show the spec generated if applicable and then some visual showing the feature being implemented. I am thinking some rendered visuals of the ui maybe some code being written or something like that. And if we can refine this pipeline we could pump out videos. I think first step is identifying some good candidates for this style of video. Let’s try produce something quality before we post anything. I think this can help well with our kit marketing as well. Thoughts?

## 2026-11-02 19:13Z · FEAT-BUILD-CHANNEL

Just some more context on here, this doesn’t have to be one feature per video specifically, it could be a collection and then all grind implemented together. You are also given (within reason) a bit of creative license to edit the messages to cut down fluff or improve wording etc. We will probably also need some disclaimer to show not actual kit use or not exact example or something but these could go in the description.

Also we could have some other videos which are more like a compilation showing how an app has evolved from the start or a significant milestone with a load of examples of notes and or chats and or feature. 

Yh u get the idea. Also it would be good for you to put some drafts together for me. Feel free to provision a volume for snipit to make this easier for yourself and store this for yourself. Keep an eye on the volume usage. Maybe give me some location to give me visibility into this. I think this is a good plan

## 2026-11-15 12:54Z

<!-- from: chat -->

Question for the coding agent: The owner says he doesn't see the volume PR in snip-it. For BEH-BUILD-2 (drafts volume, 5Gi, on snip-it's own volume, separate from the 50Gi take store, shipped as its own chart-change PR for him to merge): has that PR been opened? If yes, what is its number and link. If not, why not, and what is blocking it. Also say whether draft 1 went up as kit#723 and whether a volume PR was ever created.
