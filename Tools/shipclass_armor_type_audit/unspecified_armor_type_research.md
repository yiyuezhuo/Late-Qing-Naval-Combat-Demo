# ShipClasses.xml Armor Type Audit

Data source: `Assets/StreamingAssets/Scenarios/ShipClasses.xml`

Model source: `Assets/Scripts/NavalCombatCore/ShipClass.cs`, especially
`ArmorType`, `ArmorRating.armorTypeFactor`, and the SK5/Okun factor map.

Filter used: include a `ShipClass` when any armor record has `actualInch > 0`
or `effectInch > 0`.

Generated companion table:
`Tools/shipclass_armor_type_audit/armored_ship_armor_type_audit.csv`

## Scope Summary

| Item | Count |
|---|---:|
| Ship classes with any armor value > 0 | 89 |
| Already specified `armorType` | 62 |
| `NotSpecified` armor type | 27 |
| Specified rows whose enum factor matches `armorTypeFactor` | 62 |

The CSV covers all 89 armored rows and records the XML values, max armor
thickness, current `armorType`, current `armorTypeFactor`, factor-compatible
enum values, and internal factor consistency. The research table below focuses
on the 27 `NotSpecified` rows because those are actionable without changing
armor thicknesses.

## Factor Compatibility Used

This is copied from the C# model, not inferred from history.

| Factor | Compatible enum values |
|---:|---|
| 0.78 | `HarveyNickelSteel` |
| 0.82 | `HighTensileSteel` |
| 0.83 | `KruppCemented1894`; `ClassAArmor1900`; `KruppNickelSteel`; `KruppCementedWW1Era1905` |
| 0.90 | `NickelSteel`; `DSiliconManganeseHTSteel` |

For this scenario period, `DSiliconManganeseHTSteel` is normally ruled out by
the enum comment's 1925-1945 date range, so 0.90 usually points to
`NickelSteel` when evidence supports nickel steel.

## NotSpecified Research Table

| ShipClass.xml name | Current factor | Evidence found | Compatible recommendation | Confidence | Notes |
|---|---:|---|---|---|---|
| Gromoboi | 0.83 | [Wikipedia: Russian cruiser Gromoboi](https://en.wikipedia.org/wiki/Russian_cruiser_Gromoboi) says several sources state Krupp cemented armour was used; [Military Wiki](https://military-history.fandom.com/wiki/Russian_cruiser_Gromoboi) records the contrary Harvey-armour account. | `KruppCemented1894` | Medium | Factor-compatible, but sources conflict. Keep as a review candidate if stricter sourcing is needed. |
| Idzumo | 0.83 | [Izumo-class cruiser](https://en.wikipedia.org/wiki/Izumo-class_cruiser) says the later Six-Six Fleet armored cruisers used Krupp cemented armor. | `KruppCemented1894` | High | XML spelling is `Idzumo`; source page uses `Izumo`. |
| Azuma | 0.83 | [Japanese cruiser Azuma](https://en.wikipedia.org/wiki/Japanese_cruiser_Azuma) says the later Six-Six Fleet armored cruisers used Krupp cemented armor. | `KruppCemented1894` | High | Same Six-Six Fleet armor scheme as Izumo/Yakumo. |
| Tsushima | 0.83 | [Japanese cruiser Tsushima](https://en.wikipedia.org/wiki/Japanese_cruiser_Tsushima) gives deck and conning tower thicknesses but no material type. | Undetermined | Low | Factor permits several 0.83 enums, but source found does not identify material. |
| Bogatry | 0.83 | [Bogatyr-class cruiser](https://en.wikipedia.org/wiki/Bogatyr-class_cruiser) and [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/russia/bogatyr-class-cruisers.php) identify the design/build context and armor layout, but not the armor material clearly enough. | Undetermined | Low | XML appears to mean `Bogatyr`. No factor-compatible material was confirmed. |
| Yakumo | 0.83 | [Japanese cruiser Yakumo](https://en.wikipedia.org/wiki/Japanese_cruiser_Yakumo) says the later Six-Six Fleet armored cruisers used Krupp cemented armor. | `KruppCemented1894` | High | Same Six-Six Fleet armor scheme as Izumo/Azuma. |
| Poltava | 0.83 | [Russian battleship Poltava (1894)](https://en.wikipedia.org/wiki/Russian_battleship_Poltava_(1894)) says Poltava was the first Russian battleship to use Krupp cemented armor and also used nickel-steel protective decks. | `KruppCemented1894` | High | Mixed armor materials; recommendation follows the main belt/heavy armor. |
| Tsessarevitch | 0.83 | [Russian battleship Tsesarevich](https://en.wikipedia.org/wiki/Russian_battleship_Tsesarevich) says the ship used the latest Krupp armor. | `KruppCemented1894` | High | XML spelling differs from common `Tsesarevich`. |
| Retvizan | 0.83 | [Russian battleship Retvizan](https://en.wikipedia.org/wiki/Russian_battleship_Retvizan) records the total weight of Krupp armour; [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/russia/retvizan.php) says vertical armor was Krupp steel and horizontal armor nickel steel. | `KruppCemented1894` | High | Mixed armor materials; recommendation follows the main vertical armor. |
| Askold | 0.83 | [Naval Encyclopedia: Askold](https://naval-encyclopedia.com/ww1/russia/askold-1900.php) describes the armored deck as a nickel steel alloy plate over shipbuilding steel. | `KruppNickelSteel` | Medium | Factor-compatible and plausible for protective deck armor; source says nickel alloy, not the exact enum name. |
| Pallada | 0.83 | [Naval Encyclopedia: Pallada class](https://naval-encyclopedia.com/ww1/russia/pallada-class-cruisers.php) gives protective deck and conning tower thicknesses but not material type. | Undetermined | Low | No material evidence found for a 0.83 enum. |
| Boyarin | 0.83 | [Russian cruiser Boyarin](https://en.wikipedia.org/wiki/Russian_cruiser_Boyarin) says the armor used was Krupp plate. | `KruppNickelSteel` | Medium | Protected-cruiser armor is deck/conning-tower oriented; `KruppNickelSteel` is the 0.83 enum whose comment mentions protective decks. |
| Mikasa | 0.83 | [Japanese battleship Mikasa](https://en.wikipedia.org/wiki/Japanese_battleship_Mikasa) says the waterline armor belt consisted of Krupp cemented armor. | `KruppCemented1894` | High | Primary belt evidence is direct. |
| Hai Yung | 0.83 | [Hai Yung-class cruiser](https://en.wikipedia.org/wiki/Hai_Yung-class_cruiser) and [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/china/hai-yung-class-cruisers.php) give armor layout/thickness but not material type. | Undetermined | Low | German builder alone is not enough to assign Krupp material. |
| Esmeralda | 0.82 | [USNI Proceedings, 1897](https://www.usni.org/magazines/proceedings/1897/january/professional-notes) says the conning tower was Harveyized armor. | Review factor/type | Medium | The only 0.82 enum is `HighTensileSteel`, but the evidence found points to Harveyized armor, not HT steel. |
| Presidente Errazuriz | 0.90 | [Naval Encyclopedia: Presidente Pinto class](https://naval-encyclopedia.com/ww1/chile/presidente-pinto-class-cruisers.php) gives armor layout/thickness but not material type. | Undetermined | Low | 0.90 would usually mean `NickelSteel`, but evidence was not strong enough. |
| Admiral Kornilov | 0.78 | [Military Wiki: Admiral Kornilov](https://military-history.fandom.com/wiki/Russian_cruiser_Admiral_Kornilov) gives deck and command tower thicknesses; [Okun table mirror](https://www.combinedfleet.com/metalprp2002.htm) dates Harveyized nickel-steel armor to 1890-1891. | Review factor/type | Medium | 0.78 maps only to `HarveyNickelSteel`, but the ship predates Harvey armor; current factor looks suspect. |
| Borodino | 0.83 | [Borodino-class battleship](https://en.wikipedia.org/wiki/Borodino-class_battleship) says the waterline armor belt consisted of Krupp armor. | `KruppCemented1894` | High | Primary belt evidence is direct. |
| Sissoi Veliki | 0.90 | [Russian battleship Sissoi Veliky](https://en.wikipedia.org/wiki/Russian_battleship_Sissoi_Veliky) says the ship used nickel steel rather than compound armor. | `NickelSteel` | High | 0.90 is compatible with `NickelSteel`; `DSiliconManganeseHTSteel` is anachronistic here. |
| Sissoi Veliki (1905) | 0.90 | Same source as the base Sissoi Veliki row: [Russian battleship Sissoi Veliky](https://en.wikipedia.org/wiki/Russian_battleship_Sissoi_Veliky). | `NickelSteel` | High | Same class/ship after refit/date variant. |
| Dreadnought | 0.83 | [HMS Dreadnought (1906)](https://en.wikipedia.org/wiki/HMS_Dreadnought_(1906)) says Krupp cemented armor was used throughout unless otherwise mentioned; [Benjidog](https://benjidog.co.uk/battleships/HMS%20Dreadnought.php) distinguishes KC vertical armor and KNC deck armor. | `KruppCementedWW1Era1905` | High | British 1906 heavy vertical armor matches the enum comment for the 1905-era KC row. |
| Invincible | 0.83 | [Invincible-class battlecruiser](https://en.wikipedia.org/wiki/Invincible-class_battlecruiser) says Krupp cemented armor was used throughout unless otherwise mentioned, with KNC exceptions. | `KruppCementedWW1Era1905` | High | British 1907-era heavy vertical armor matches the enum comment for the 1905-era KC row. |
| Edgar | 0.90 | [Edgar-class cruiser](https://en.wikipedia.org/wiki/Edgar-class_cruiser) says the protective deck used steel armor, but does not identify nickel steel. | Undetermined | Low | 0.90 would usually mean `NickelSteel`; current evidence is not enough. |
| Hertha | 0.83 | [Victoria Louise-class cruiser](https://en.wikipedia.org/wiki/Victoria_Louise-class_cruiser) says armor protection was composed of Krupp steel; [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/germany/victoria-luise-class.php) says armor protection was entirely Krupp steel. | `KruppNickelSteel` | Medium | Source says Krupp steel, not exact subtype; protective deck context favors `KruppNickelSteel` among 0.83 choices. |
| Kaiserin Augusta (1897) | 0.83 | [SMS Kaiserin Augusta](https://en.wikipedia.org/wiki/SMS_Kaiserin_Augusta) says the curved armor deck was Krupp steel; [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/germany/sms-kaiserin-augusta.php) repeats Krupp steel deck. | `KruppNickelSteel` | Medium | Source says Krupp steel, not exact subtype; protective deck context favors `KruppNickelSteel` among 0.83 choices. |
| Calabria | 0.90 | [Italian cruiser Calabria](https://en.wikipedia.org/wiki/Italian_cruiser_Calabria) and [Naval Encyclopedia](https://naval-encyclopedia.com/ww1/italy/calabria-1894.php) give armor layout/thickness but not material type. | Undetermined | Low | 0.90 would usually mean `NickelSteel`; evidence was not strong enough. |
| Zenta | 0.83 | [Zenta-class cruiser](https://en.wikipedia.org/wiki/Zenta-class_cruiser) says the armored deck consisted of steel layers, but does not identify Krupp/nickel/KC material. | Undetermined | Low | Factor permits several 0.83 enums, but material was not confirmed. |

## Actionable Candidates

High-confidence direct replacements while preserving `armorTypeFactor`:

| ShipClass.xml name | Suggested `armorType` |
|---|---|
| Idzumo | `KruppCemented1894` |
| Azuma | `KruppCemented1894` |
| Yakumo | `KruppCemented1894` |
| Poltava | `KruppCemented1894` |
| Tsessarevitch | `KruppCemented1894` |
| Retvizan | `KruppCemented1894` |
| Mikasa | `KruppCemented1894` |
| Borodino | `KruppCemented1894` |
| Sissoi Veliki | `NickelSteel` |
| Sissoi Veliki (1905) | `NickelSteel` |
| Dreadnought | `KruppCementedWW1Era1905` |
| Invincible | `KruppCementedWW1Era1905` |

Medium-confidence candidates that preserve the factor but should be reviewed
before XML edit:

| ShipClass.xml name | Suggested `armorType` | Reason for caution |
|---|---|---|
| Gromoboi | `KruppCemented1894` | Sources conflict between Krupp cemented and Harvey armor. |
| Askold | `KruppNickelSteel` | Source confirms nickel alloy armor plate, not exact enum label. |
| Boyarin | `KruppNickelSteel` | Source says Krupp plate, not exact subtype. |
| Hertha | `KruppNickelSteel` | Source says Krupp steel, not exact subtype. |
| Kaiserin Augusta (1897) | `KruppNickelSteel` | Source says Krupp steel, not exact subtype. |

Rows that likely need more research or a factor review before assigning a type:

| ShipClass.xml name | Current factor | Reason |
|---|---:|---|
| Tsushima | 0.83 | Material not found. |
| Bogatry | 0.83 | Material not found. |
| Pallada | 0.83 | Material not found. |
| Hai Yung | 0.83 | Material not found. |
| Presidente Errazuriz | 0.90 | Material not found. |
| Edgar | 0.90 | Only generic steel armor found. |
| Calabria | 0.90 | Material not found. |
| Zenta | 0.83 | Only generic steel layers found. |
| Esmeralda | 0.82 | Found Harveyized armor evidence, not the only factor-compatible `HighTensileSteel`. |
| Admiral Kornilov | 0.78 | Current factor implies Harvey nickel steel, but the ship predates Harvey armor. |

## Source Notes

The armor factor list itself ultimately traces to Nathan Okun's table, linked in
the Unity editor help and model comments:

- [NavWeaps / Nathan Okun armor material table](https://www.navweaps.com/index_nathan/metalprpsept2009.php)
- [CombinedFleet mirror of Okun table](https://www.combinedfleet.com/metalprp2002.htm)

Historical ship-page sources vary in precision. Some identify exact materials
such as Krupp cemented armor or nickel steel; others only list thicknesses. This
report intentionally does not infer a material from thickness alone.
