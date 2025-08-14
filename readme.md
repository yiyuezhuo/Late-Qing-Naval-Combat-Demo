# Late Qing Naval Combat Demo

<details open>
<summary>Screenshots</summary>

<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcwOC5wbmc=/original/ley724.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcyOC5wbmc=/original/RZoWyh.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5Njc0MC5wbmc=/original/0U7yoB.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcyNS5wbmc=/original/kFq%2Fn1.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcwMC5wbmc=/original/y6momF.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5Njc0Ni5wbmc=/original/MTKVcP.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcwMy5wbmc=/original/VnrPLo.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5NjcwMS5wbmc=/original/ToLgT7.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5Njc1MC5wbmc=/original/pAN66Y.png">
<img src="https://img.itch.zone/aW1hZ2UvMzY4MDI0MC8yMTg5Njc1Mi5wbmc=/original/hsbsH6.png">

</details>

## Introduction

This game is the first installment of a demo trilogy for my Late Qing Dynasty historical simulation . In the final project, all three parts (RTS naval, classical hex land combat and a political simulation) will be merged into a single, interconnected experience. However, to avoid overcomplicating the design or introducing unnecessary abstraction while focusing on specific aspects, I haven't made significant efforts to isolate components for maximum reusability. Some degree of rewriting is expected--especially as I gain more experience and develop a clearer vision for the game. This will help address technical debt and ultimately result in a stronger open-source project.

The core resolution system is primarily inspired by SEEKRIEG and Dawn of the Battleship (DoB), while UI draws influence from RTW, JTS Naval Campaign, and CMO.

Ship data is largely sourced from the Ship Logs of SEEKRIEG's Yalu starter scenario, with additional references taken form DoB's Mahan book. However, I've corrected some evident inaccuracies present in there. Some global string sections may contain inconsistencies, as they pull from multiple sources--SEEKRIEG, DoB data, and general historical materials in English, Japanese and Chinese.

Unfortunately, naval engagement involving those ironclad in the first Sino-Japanese war remain an under-researched topic, so minor contradictions in certain details should be expected.

## Notes on devariance from SK5

While SK5 is renowned as the most detailed tabletop miniature wargame focused on surface gunnery, its reference origin is set on the WWII-era battleship engagements. As a result, it performs poorly when adapted to the its far most extrapolation, the Ironclad era--arguably even worsen than its simpler counterpart, DoB in the trilogy series.

A key example is torpedo effectiveness: in DoB, a 500-yard shot yields a check trigged within 100 yards with 16% hit probability, whereas SK5 resolves the same scenario with a staggering 94% hit probability trigged within 500 yards--a gross overestimation for the period. Given these inaccuracies, I opted to just discard both systems in favor of a physics engine powered collision check, supplemented by a check combining factors such as dud and evasive maneuvers.

On the other hand, gunnery introduce too much attrition compared to historical case. But since it's the core of SK5, I don't modifiy it much and just provide a global hit change coef to modify it.

## Getting Started

- Right-click and drag to move the camera, use the scroll wheel to adjust the zoom level.
- Press 1 to advance by 1 minute
- Control group leader (icon with a direction arrow) to control a group
    - Change direction: Select a group lead and left click a point on globe to set a direction
    - Change speed: Change value in the slider of right panel
    - Change or inspect a lot of details in editors.
- Use F or R to set Follow and relative to relationship, more parameter can be specified in the ShipLog editor.

## Automation

- By default, firing is automated, following a somewhat optimal rule, and can be configured in the editor, ranging from doctrine to manual target specification.
- If a unit follow or is relative to a target, it will adjust its speed and course to reach the desired position.
- By default, an independent unit (usually the group leader) maintains its current speed and course. However, if automatic movement is enabled in the doctrine, the unit will adjust its course to maximize firepower while minimizing incoming damage. (Speed is not controlled at this point).

## Shortcuts

Basic:

- Left Click: Select Unit
- Right Click: Select Unit and open Ship Log Editor for it.
- Shift + Left Click: Set course for the selected unit
- D: Distance measureing line
- S: Line of Sight (check ship masking & Earth curvature)
- I: Detach unit (set control mode to Independent)
- F: Set follow target (extra parameter requires ship log editor)
- R: Set relative to target (extra parameter requires ship log editor)
- L: Open Ship Log Editor for the selected ship
- Esc: Reset UI to idle state.

Edit:

- Insert: Insert a ShipLog on map. (Deploy a "non-deployed" ship to map).
- Delete: Delete selected ship.
- M: Move selected ship to another point.

## TODO List

- [ ] Data Revision
- [ ] Parameter calibration (though the current SK5 vanilla implementation will be keep for comparison)
- [ ] Fog of War
- [ ] Night combat related stuffs
- [ ] Land battery
- [ ] Better AI

## Communities

Discord Server: https://discord.gg/HmDW2XuE

## Devlopment

### Unity related bugs

- UITK sometimes lost cellTemplate reference (need to reattaching templates)
    - A build processor will check this and block building if missing references are detected.
    - To fix: Run `Custom/Build Manifest for platform without File System` menu item to re-check and try re-import them until fixed.
    - Note: This bug typically occurs when siwtching platforms.
- UITK occasionally ignores cellTemplate reference (workaround: touch"  files in Unity or external editor to trigger a refresh)
- Deleting a template instance in UITK's designer may corrupet the Template tag, causing other instance of the same template will become  visually hidden (they still exist actually).
    So it's recommended to delete instance by manually modifiying uxml file entries instead of designer.

## References:

SK5 Community: https://groups.io/g/SEEKRIEG/topics?sidebar=true

## Credits

### Libraries

- [GeographicLib.NET](https://github.com/noelex/GeographicLib.NET)
- [suncalcsharp](https://github.com/webbwebbwebb/suncalcsharp)
- [MathNet.Numerics](https://github.com/mathnet/mathnet-numerics)
- [UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser)
- [UnityNativeFilePicker](https://github.com/yasirkula/UnityNativeFilePicker)
- [JInt](https://github.com/sebastienros/jint)

## Assets

- Stategic Mode Terrain Textures: https://opengameart.org/content/hitw-terrain-textures

