# Late Qing Naval Combat Demo

This game is the first installment of a demo trilogy for my Late Qing Dynasty historical simulation . In the final project, all three parts (RTS naval, classical hex land combat and a political simulation) will be merged into a single, interconnected experience. However, to avoid overcomplicating the design or introducing unnecessary abstraction while focusing on specific aspects, I haven't made significant efforts to isolate components for maximum reusability. Some degree of rewriting is expected--especially as I gain more experience and develop a clearer vision for the game. This will help address technical debt and ultimately result in a stronger open-source project.

The core resolution system is primarily inspired by SEEKRIEG and Dawn of the Battleship (DOB), while UI draws influence from RTW, JTS Naval Campaign, and CMO.

Ship data is largely sourced from the Ship Logs of SEEKRIEG's Yalu start scenario, with additional references taken form DoB's Mahan book. However, I've corrected some evident inaccuracies present in there. Some global string sections may contain inconsistencies, as they pull from multiple sources--SEEKRIEG, DoB data, and general historical materials in English, Japanese and Chinese.

Unfortunately, naval engagement involving those ironclad in the first Sino-Japanese war remain an under-researched topic, so minor contradictions in certain details should be expected.

## Devlopment info

### Unity related bugs

- UITK sometimes lost cellTemplate reference (need to attach templates again)
- UITK sometime ignore cellTemplate reference (need to "touch" files in Unity or external editor for some reason)
- UITK's sorting order not work properly (though "most" works so I just don't rely it heavyly)
= When delete a instance of a template from UITK's designer, Template tag may be corrupeted so other instance of the same template will be hidden visually (though they still exist). So it's recommended to delete instance by manually modifiying uxml file entries.

### Unity related warning

- When resources is rewritten in edit time (for example, overrie xml file), Asset-refresh is needed, otherwise `Resources.Load` may load old version of assets.