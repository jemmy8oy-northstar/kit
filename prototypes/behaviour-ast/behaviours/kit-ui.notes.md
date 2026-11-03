# Notes on kit-ui

Left in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.

## 2026-10-16 11:45Z · BEH-KIND-1

This is good this is a useful filter addition I was originally hoping for the filter in the project view but I am going to accept this as well

## 2026-10-16 11:53Z · BEH-NOTE-10

Going to approve, do you think we could auto archive them or soemthing and then have an option to show archived. Maybe in the future we will have some expiry where old notes get auto deleted but don’t really need that yet.

## 2026-10-20 07:13Z · BEH-STAGE-7

I think we need some unstated column rather than leaving it off the board so that they can be found and staged

## 2026-10-26 22:50Z · BEH-USERS-3

I added client id and client secret idk whether this was for this ticket feel free to move it if it wasn’t. Also when you finish work but you are waiting for secrets or soemthing this needs to go into blocked not done

## 2026-10-26 23:00Z · BEH-USERS-6

Just thinking about this idk whether this is the best way to do it. Dbs are a lot of admin to manage right. Maybe better to outsource this at the start. Just as lovable does

## 2026-10-27 21:16Z · BEH-STAGE-11

What does this mean

## 2026-10-27 21:16Z · BEH-STAGE-9

What does this mean

## 2026-10-28 23:53Z · BEH-STAGE-11

Placed by how much of it is written? Do you mean spec wise? It’s placed on the board by what stage it’s at. How much of ux beh ui impl or

## 2026-10-28 23:53Z · BEH-USERS-5

This is not about Kubernetes secrets. The future direction is we use some kind of vault

## 2026-10-29 08:04Z · BEH-USERS-6

Do you have any other suggestions here?

## 2026-10-31 01:00Z · BEH-USERS-5

Oh I haven’t heard of openbao or infiacal. I was thinking oci or hashicorp

## 2026-10-31 01:01Z · BEH-GATE-H2

What does this mean?

## 2026-10-31 01:07Z · BEH-USERS-6

For all of these would it be better to let the user integrate their own account or spin up instances for the user? Which would be better for our pricing model as well? I guess we could price the user for what they use. Do you think neon would be good for us?

## 2026-10-31 16:45Z · BEH-USERS-9

Do you have an estimation of cost for how much a feature will take to execute or anything like that using anthropic api with opus 5.5? I am imagining that it will be quite expensive.

## 2026-11-03 08:23Z · BEH-SPLASH-1

Merge it when it ready

## 2026-11-03 08:57Z · BEH-SPLASH-1

Merged

## 2026-11-03 09:17Z · BEH-SPLASH-1

Merged to main

## 2026-11-03 10:58Z · BEH-SPLASH-1

This is complete right? Also why are you not cracking on with the kit backlog. Got to work towards being ready for users

## 2026-11-03 13:36Z

The owner wants one chat for the whole of kit, not three. kit, kit-ui and kit-hosted are all related and should share a single chat. Today each is a separate project with its own chat, so he has to talk to three places about one thing. Please group them so kit's chat covers all three.

## 2026-11-03 13:49Z

The owner wants a button next to Approve and Skip in the chat's floating widget that reposts the current item at the bottom of the chat, so he doesn't have to scroll up to find it. Label: "Show again" (alternatives he may prefer: "Bring to bottom" or "Recap"). Motivation: when an item ends up far up the chat he wants it posted again at the bottom.
