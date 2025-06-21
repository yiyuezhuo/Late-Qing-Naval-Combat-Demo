# Late Qing Naval Combat Demo

This game is the first installment of a demo trilogy for my Late Qing Dynasty historical simulation . In the final project, all three parts (RTS naval, classical hex land combat and a political simulation) will be merged into a single, interconnected experience. However, to avoid overcomplicating the design or introducing unnecessary abstraction while focusing on specific aspects, I haven't made significant efforts to isolate components for maximum reusability. Some degree of rewriting is expected--especially as I gain more experience and develop a clearer vision for the game. This will help address technical debt and ultimately result in a stronger open-source project.

The core resolution system is primarily inspired by SEEKRIEG and Dawn of the Battleship (DoB), while UI draws influence from RTW, JTS Naval Campaign, and CMO.

Ship data is largely sourced from the Ship Logs of SEEKRIEG's Yalu starter scenario, with additional references taken form DoB's Mahan book. However, I've corrected some evident inaccuracies present in there. Some global string sections may contain inconsistencies, as they pull from multiple sources--SEEKRIEG, DoB data, and general historical materials in English, Japanese and Chinese.

Unfortunately, naval engagement involving those ironclad in the first Sino-Japanese war remain an under-researched topic, so minor contradictions in certain details should be expected.

## Notes on devariance from SK5

While SK5 is renowned as the most detailed tabletop miniature wargame focused on surface gunnery, its reference origin is set on the WWII-era battleship engagements. As a result, it performs poorly when adapted to the its far most extrapolation, the Ironclad era--arguably even worsen than its simpler counterpart, DoB in the trilogy series.

A key example is torpedo effectiveness: in DoB, a 500-yard shot yields a check trigged within 100 yards with 16% hit probability, whereas SK5 resolves the same scenario with a staggering 94% hit probability trigged within 500 yards--a gross overestimation for the period. Given these inaccuracies, I opted to just discard both systems in favor of a physics engine powered collision check, supplemented by a check combining factors such as dud and evasive maneuvers.

## Hotkeys

- Left Click: Select Unit
- Right Click: Select Unit and open Ship Log Editor Viewer for it.
- Shift + Left Click: Set Course for the selected unit
- D: Distance measureing line
- S: Line of Sight (check ship masking & earth curvature)
- F: Set follow target (extra parameter requires ship log editor)
- R: Set relative to target (extra parameter requires ship log editor)
- L: Open Ship Log editor for the selected ship

## Devlopment Info

### Unity related bugs

- UITK sometimes lost cellTemplate reference (need to attach templates again)
- UITK sometime ignore cellTemplate reference (need to "touch" files in Unity or external editor for some reason)
- UITK's sorting order not work properly (though "most" works so I just don't rely it heavyly)
= When delete a instance of a template from UITK's designer, Template tag may be corrupeted so other instance of the same template will be hidden visually (though they still exist). So it's recommended to delete instance by manually modifiying uxml file entries.

### Unity related warning

- When resources is rewritten in edit time (for example, overrie xml file), Asset-refresh is needed, otherwise `Resources.Load` may load old version of assets.

## References:

SK5 Community: https://groups.io/g/SEEKRIEG/topics?sidebar=true

## Credits

### Libraries

- [GeographicLib.NET](https://github.com/noelex/GeographicLib.NET)
- [suncalcsharp](https://github.com/webbwebbwebb/suncalcsharp)
- [MathNet.Numerics](https://github.com/mathnet/mathnet-numerics)
- [UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser)
- [JInt](https://github.com/sebastienros/jint)
