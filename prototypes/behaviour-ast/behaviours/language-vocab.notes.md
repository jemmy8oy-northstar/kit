# Notes on language-vocab

Left in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.

## 2026-10-12 23:00Z · resolve BEH-LANG-1

We have one language for now let’s worry about scaling later when we want to add more languages, do you agree with this or u think worth decoupling now. The other argument is we should build scalable apps. Is there a good middle ground?

## 2026-10-15 23:00Z · new behaviour

first deployment is good! really happy with this

however at the moment it is really hard for me to learn, i think we need some different modes

what we have now seems like a practice mode but i have no learning mode

i think i need a learning mode where i can flick through maybe 5 words where it shows the pinying and the english pairing and you have to click next, then after it questions you on them by showing the pinyin and asking you to guess, and then i guess also the other way round but that is harder so maybe that is another option again

## 2026-10-15 23:08Z · new behaviour

Another idea that I have had for language vocab is vocab packs, i am just thinking idrk how words are grouped yet but i would love to add a mahjong vocab pack. I did at one point know all the numbers and then obiously wan bing idek remember the other one and i am sure the spelling here is bad but you get it. I would love to contribute vocab packs and i think this is a fun and intuitive way to get interested in learning a language and i guess grouping lessons and drills.

## 2026-10-29 13:32Z · BEH-BOOTSTRAP-1

What sites this man

## 2026-10-29 13:32Z · BEH-UNLOCK-2

What does this mean

## 2026-11-04 11:44Z

<!-- from: note-button -->

Can we add a path style to show progression on different types of words. I think this would be useful rather than just random hsk. Might be good to group them. I think greeting is a good start like ni hao and nin hao. Maybe we can do sentences and individual word meanings. We can iterate if it doesn’t go well. You can skip ahead and you can search. Maybe 1-10 numbers is one and mahjong is a later one. No need to block them off like in duo lingo or candy crush this is not paid. Maybe a button to go back to next in order. Maybe sections where if you can do the word and sentence test at the end of a section this section and this moves the next in order section to here. We can refine and add more features later.

## 2026-11-04 18:25Z

<!-- from: note-button -->

A video I found to help with mandarin

Me 

Https://youtu.be/yvBZTBaX0Is?is=iKfKC8vt5osUAlwT


This video really clearly explains some foundational rules for mandarin grammar. Please could you summarise the rules so that I can copy them. You only need to summarise them for mandarin

Gemini

Anthropic prices its Claude 5.5 model tiers based on per-million token usage for input and output, scaling up from the fast and lightweight Haiku to the heavy-duty Opus: [1] (https://www.ayautomate.com/blog/opus-vs-sonnet-vs-haiku), [2] (https://www.truefoundry.com/blog/opus-sonnet-haiku-routing-guide)

Current Pricing per Million Tokens

• Claude Haiku 5.5:
	• $0.10 per million input tokens and $0.50 per million output tokens (for prompts up to 100K tokens).
	• Jumps to $0.50 input / $2.50 output per million tokens for prompts over 100K tokens. [1] (https://www.alekseialeinikov.com/en/blog/topics/ai/claude-haiku-5-5-vs-sonnet-5-5-vs-opus-5-5), [2] (https://www.anthropic.com/claude/haiku)
• Claude Sonnet 5.5:
	• $2.00 per million input tokens and $10.00 per million output tokens. [1] (https://www.ayautomate.com/blog/opus-vs-sonnet-vs-haiku)
• Claude Opus 5.5:
	• $4.00 per million input tokens and $20.00 per million output tokens (roughly double the per-token cost of Sonnet). [1] (https://www.alekseialeinikov.com/en/blog/topics/ai/claude-haiku-5-5-vs-sonnet-5-5-vs-opus-5-5), [2] (https://www.ayautomate.com/blog/opus-vs-sonnet-vs-haiku)

Cost & Capability Comparison


Model Tier	Input Price (per 1M)	Output Price (per 1M)	Best Used For
Claude Haiku 5.5	$0.10 (≤100K) / $0.50 (>100K)	$0.50 (≤100K) / $2.50 (>100K)	High-volume classification, triage, quick extractions, subagent tasks.
Claude Sonnet 5.5	$2.00	$10.00	Everyday coding, multi-step reasoning, production default workhorse.
Claude Opus 5.5	$4.00	$20.00	Complex review, long-running agentic coding, deep architecture design.

Check official updates on the Anthropic Claude Haiku Pricing Page or explore routing strategies on TrueFoundry's Guide. [1] (https://www.truefoundry.com/blog/opus-sonnet-haiku-routing-guide), [2] (https://www.anthropic.com/claude/haiku)
Here is a quick overview comparing Claude Opus, Sonnet, and Haiku models in action:
Are you trying to estimate costs for an API project, or are you deciding which model tier to use inside a tool like Claude Code?

End

Can we use these rules in the app to identify sentence grammar mistakes and identify them for example if we notice the correct words out of order or if specifically if is missing or then or the wrong time particle

## 2026-11-04 18:27Z

<!-- from: note-button -->

Another idea for language vocab once we have the full path which I proposed early we should be able to setup this flash card style game that we already have including vocab and or sentences  from (maybe even toggling whether it includes words sentences or both) each lesson. Maybe we can pull individual lessons in by searching or can select certain groups of lessons. I think this flash card should show at the end how many you got right or wrong and then give you the option to go through the failed ones again until you get them right or review them when you finish through the failed ones it g gives you that breakdown but with how many times you got it wrong. Maybe we keep track of these to keep track what the worse vocab is.
