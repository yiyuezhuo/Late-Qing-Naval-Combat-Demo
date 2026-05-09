# First Sino-Japanese War

- Releases:
    - Steam (PC): https://store.steampowered.com/app/3996220/First_SinoJapanese_War/
    - GitHub Release (PC/Android): https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo/releases
    - Itch (Webgl, version is much slower than the current version): https://yiyuezhuo.itch.io/battle-of-yalu-river-1894
- Manuals:
    - <a href="https://github.com/yiyuezhuo/First-Sino-Japanese-War-Manual">Game Manual</a>
- Game Communities
    - Discord: https://discord.gg/2yqbyGwsdQ

## Introduction

The game is a wargame based on the First Sino-Japanese War (日清戦争 / 甲午战争 / 甲午戰爭) of 1894–1895, featuring both a naval tactical combat mode and a strategic game mode. The tactical combat includes historical scenarios such as the Battle of the Yalu River (黄海海戦 (1894) / 大东沟海战), which can also be generated from the strategic game.

The core resolution system for the tactical naval combat is primarily inspired by SEEKRIEG 5 and Dawn of the Battleship (DoB), while the UI draws influence from RTW, JTS Naval Campaign, and CMO. The strategic game is a WEGO-style game with 1-day turns and 50km hexes, heavily inspired by War in the Pacific (WITP), where naval combat is resolved through manual tactical gameplay.

Ship data is largely sourced from the Ship Logs of SEEKRIEG's Yalu starter scenario, with additional references drawn from DoB's Mahan book. However, I have corrected some evident inaccuracies present in those sources. Some global string sections may contain inconsistencies, as they pull from multiple references—SEEKRIEG, DoB data, and general historical materials in English, Japanese, and Chinese.

Unfortunately, naval engagements involving ironclads in the First Sino-Japanese War remain an under-researched topic, so minor contradictions in certain details should be expected.

## Notes on devariance from SK5

While SK5 is renowned as the most detailed tabletop miniature wargame focused on surface gunnery, its reference origin is set on the WWII-era battleship engagements. As a result, it performs poorly when adapted to the its far most extrapolation, the Ironclad era--arguably even worsen than its simpler counterpart, DoB in the trilogy series.

A key example is torpedo effectiveness: in DoB, a 500-yard shot yields a check trigged within 100 yards with 16% hit probability, whereas SK5 resolves the same scenario with a staggering 94% hit probability triggered within 500 yards--a gross overestimation for the period. Given these inaccuracies, I opted to just discard both systems in favor of a physics engine powered collision check, supplemented by a check combining factors such as dud and evasive maneuvers.

On the other hand, gunnery introduce too much attrition compared to historical case. But since it's the core of SK5, I don't modifiy it much and just provide a global hit change coef to do correction.

## Devlopment

### Doc

Deepwiki (LLM) automatically generates doc (not very accurate but useful): https://deepwiki.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo

Manual is not version controlled, dev should place latest version of manual as `Assets/StreamingAssets/Manuals/readme.pdf`. The current manual is generated from: https://github.com/yiyuezhuo/First-Sino-Japanese-War-Manual using Obsidian's "Export to PDF" feature (It's better to use [Better Export PDF](https://github.com/l1xnan/obsidian-better-export-pdf) plugin to include proper bookmarking for the exported file).

### Unity related bugs

- UITK sometimes lost cellTemplate reference (need to reattaching templates)
    - A build processor will check this and block building if missing references are detected.
    - To fix: Run `Custom/Build Manifest for platform without File System` menu item to re-check and try re-import them until fixed.
    - Note: This bug typically occurs when switching platforms.
- UITK occasionally ignores cellTemplate reference (workaround: touch files in Unity or external editor to trigger a refresh)
- Deleting a template instance in UITK's designer may corrupt the Template tag, causing other instance of the same template will become visually hidden (they still exist actually).
    So it's recommended to delete instance by manually modifiying uxml file entries instead of designer.

### UITK Templates

- Left ListView right content editor (2-columns UI) (Example: `LandUnitEditor`):
    - Template: `LeftObjectPickerRightEditor.uxml`
    - Binder: `LeftObjectPickerRightEditorStrategic`
- Selector Dialog (Example: `LeaderSelector`):
    - Template: `INamedSelectorDialog.uxml`
    - Binder: `NamedSelector<T>`
    - Placeholder datasource: `PlaceholderNamedObject` (implement `INamed`)

### Platforms

Desktop is the major platform, WebGL will work but the huge file size will not deliver a decent experience, Mobile and basic touch screen support is implemented.

## Credits

### Libraries

- [GeographicLib.NET](https://github.com/noelex/GeographicLib.NET)
- [suncalcsharp](https://github.com/webbwebbwebb/suncalcsharp)
- [MathNet.Numerics](https://github.com/mathnet/mathnet-numerics)
- [UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser)
- [UnityNativeFilePicker](https://github.com/yasirkula/UnityNativeFilePicker)
- [JInt](https://github.com/sebastienros/jint)

### Assets

- Textures:
    - Stategic Mode Terrain Textures and water textre in the Naval Tactical (piper_flatline, CC): https://opengameart.org/content/hitw-terrain-textures
    - smoke particle assets (Kenney.nl, CC0): https://opengameart.org/content/smoke-particle-assets
- Sounds:
    - Gunfire (qubodup (Freesound), Pixabay Content License): https://pixabay.com/sound-effects/artillery-gunfire-14607/
    - Ocean Waves (Mike Koenig, Attribution 3.0): https://soundbible.com/1936-Crisp-Ocean-Waves.html
    - Ship Bell (Mike Koenig, Attribution 3.0): https://soundbible.com/1746-Ship-Bell.html
    - Splash Rock In Lake (Ploor, Public Domain): https://soundbible.com/2100-Splash-Rock-In-Lake.html
    - Explosion (SoundReality, Pixabay Content License): https://pixabay.com/sound-effects/explosion-fx-343683/

### References:

- Naval Armor and Ballistics program (Game's NAAB-like calculator emulate it and use some data from it): http://www.panzer-war.com/Naab/NAaB.html
- McCoy's Modern Exterior Ballistics: https://www.mori.bz.it/Balistica/Mc%20Coy%20Modern%20Exterior%20Ballistic.pdf
- Homogeneous Armor Penetration Computer Program M79APCLC: http://www.navweaps.com/index_nathan/M79apdoc.php

### Related Wargame material

- SK5 Rulebook: https://www.wargamevault.com/en/product/303736/seekrieg-5-rulebook
- SK5 forum: https://groups.io/g/SEEKRIEG/topics?sidebar=true
