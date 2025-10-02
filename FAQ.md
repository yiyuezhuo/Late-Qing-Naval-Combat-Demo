
# FAQ

## Localization Quality Issues?

The base development language is English, which remains the most polished version. If you encounter questionable translations in other languages, please verify against the English version first.

Current localization approach:

- **Basic UI**: English and Simplified Chinese versions are manually crafted. Japanese and Traditional Chinese are LLM-translated based on the above two languages.
- **Remarks**: Source content with its orignal language appears at the top; translation follow below.
- **Long Texts** (Help, tutorials, Steam page, etc.): English version are manually written; other languages are LLM-translated from English.
- **Dynamic Content** (Damage effects, combat logs, etc.): Not yet localized.

## Why Don’t Ships Sink Immediately After Reaching 100% Damage Points?

In the SK5 system, reaching 100% Damage Points (DP) does not guarantee a ship will sink. A vessel might survive well beyond 100% DP — or sink instantly from the first hit.

DP primarily drives the generation of General Damage Effects, which can severely impair a ship’s combat capability. The 100% DP mark indicates a high probability that the ship becomes mission-killed (combat ineffective).

Mechanically, 100% DP is the threshold at which General Damage Effect checks stop. Beyond this point, no further General DE rolls occur — making the ship relatively less likely to sink per additional damage taken (imagine critical explosion chances are "used up", so more hole in the shell above water has no more effect to sink the ship). That said, specific damage effects from normal hits can still cause the ship to sink.

## How to Enable Movement AI

Select a group in the OOB Editor, turn off "Inherited" in the automatic movement field, and set it to "Automatic." The group will then change its course according to certain principles. This setting is usually applied to a top-level group, such as "Japan Fleet" or "China Fleet," but it can also be set at a subordinate level for partial automation.

The AI is still in a prototype state (for example, it plans its course but does not modify speed), so it is not enabled by default. The game currently recommends sandbox-style gameplay, where the player controls both sides simultaneously and observes the outcome—similar to how solitary wargamer does play their miniatures, but with help of auto-resolution powered by computer.

## Editing is Not Intuitive

I haven’t written related material since major rework is expected. If you really want to edit something and find it frustrating, contact me (via GitHub issue or Discord server) to let me know someone is really interested, I would write a temp document to explain how to do it in the current stage.

## Why are so many standard UI elements named "Editor"? I don't want to "edit" anything—I just want to play.

I aim to recreate a sandbox experience similar to Vassal and Tabletop Simulator (TTS), where editing allows players to introduce custom rules and house rules without coding. On the ohter hand, in games like Command: Modern Operations (CMO), players often use edit mode to streamline experimentation—a feature I want to incorporate into game.

## Why is so much image loading done at runtime?

I want to emulate Tabletop Simulator's approach of loading images dynamically during gameplay.

## Why Is the Game So Large? Shouldn't It Be a Mini Game liek RTW Given the "Minimalist" Graphics?

Similar to games like CMO, the majority of the installation size comes from GIS elevation data. While titles such as Rule the Wave avoid this overhead by using vector data, this project uses raster data — even though ocean depth data has been clamped.

The reason for retaining elevation data is to support a planned tactical land combat generator, which will utilize terrain elevation in future updates. Also elevation data will render location more recognizable and pretty.

## Is the Strategic Game Playable Now?

No, only the naval tactical game is currently playable (to some extent). The exposed strategic game mode only gives a *feel* for what the full strategic experience will eventually be like. 

This area may also be of interest to open-source contributors, as the strategic game is now the main focus of development.
