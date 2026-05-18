# Specified Armor Type Material Audit

This file corrects an important limitation of the first pass: factor consistency
is not material consistency. The rows below audit only the 62 armored
`ShipClass` rows whose XML `armorType` is already specified.

Filter used: include a `ShipClass` when any armor record has `actualInch > 0`
or `effectInch > 0`, and `armorRating/armorType` is neither empty nor
`NotSpecified`.

Verdict meanings:

| Verdict | Meaning |
|---|---|
| `Consistent` | The cited source names material compatible with the current enum. |
| `Likely inconsistent` | The cited source names a different material family or a better current enum. |
| `Insufficient` | Search found thickness/layout, generic steel, or ambiguous terms only. |

When a source says only "Harvey steel" or "Krupp steel", the row is treated as
compatible with the closest current enum only if the game's enum has no exact
generic alternative and the period/use fits. These are called out in notes.

## Summary

| Result | Count |
|---|---:|
| Specified armored rows audited | 62 |
| Consistent or broadly compatible | 37 |
| Likely inconsistent | 17 |
| Insufficient material evidence | 8 |

## Material Audit Table

| ShipClass.xml name | Current armorType | Current factor | Material found in search | Verdict | Recommended armorType if changing | Evidence links | Notes |
|---|---|---:|---|---|---|---|---|
| Yoshino | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Yoshino](https://navypedia.org/ships/japan/jap_cr_yoshino.htm) | Source says Harvey steel, not explicitly nickel; compatible with current Harvey-family enum. |
| Yoshino (1901) | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Yoshino](https://navypedia.org/ships/japan/jap_cr_yoshino.htm) | Same ship/date variant. |
| Akitsushima | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Akitsushima](https://www.navypedia.org/ships/japan/jap_cr_akitsushima.htm), [Naval Encyclopedia Akitsushima](https://naval-encyclopedia.com/ww1/japan/akitsushima.php) | Source says Harvey steel, not explicitly nickel; compatible with current Harvey-family enum. |
| Akitsushima (1902) | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Akitsushima](https://www.navypedia.org/ships/japan/jap_cr_akitsushima.htm) | Same ship/date variant. |
| Naniwa | `MildSteel` | 0.75 | Deck/barbette/shield thickness found; material not explicit in Navypedia; Naval Encyclopedia says compound armor likely | Insufficient |  | [Navypedia Naniwa](https://www.navypedia.org/ships/japan/jap_cr_naniwa.htm), [Naval Encyclopedia Naniwa](https://naval-encyclopedia.com/ww1/japan/naniwa-class-protected-cruisers.php) | Current mild steel is plausible for early protected cruiser deck, but source evidence is not decisive. |
| Naniwa (1903) | `MildSteel` | 0.75 | Same as Naniwa | Insufficient |  | [Navypedia Naniwa](https://www.navypedia.org/ships/japan/jap_cr_naniwa.htm) | Same class/date variant. |
| Chi Yuan | `CompoundHardSteelFacedWroughtIron` | 0.68 | Steel deck/barbettes | Likely inconsistent | `MildSteel` | [Chinese cruiser Jiyuan](https://en.wikipedia.org/wiki/Chinese_cruiser_Jiyuan), [HistoryOfWar Tsi Yuen](https://www.historyofwar.org/articles/weapons_tsi_yuen.html) | Search found steel armor, not compound armor. |
| Kuang Yi | `MildSteel` | 0.75 | Steel | Consistent |  | [Navypedia Kuang Ping / Kuang Yi](https://navypedia.org/ships/china/ch_dd_kuang_ping.htm) | Current mild steel is a reasonable game mapping for steel armor. |
| Ting Yuen | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor / steel over iron | Consistent |  | [HKSW Ting Yuen](https://www.hksw.org/Ting%20Yuen.htm), [Chinese ironclad Dingyuan](https://en.wikipedia.org/wiki/Chinese_ironclad_Dingyuan) | Direct match to compound hard steel-faced wrought iron concept. |
| Chin Yen (1896) | `MildSteel` | 0.75 | Compound armor as original Chen Yuan/Dingyuan-class material; some sources claim Krupp steel after Japanese service, likely anachronistic/confused | Likely inconsistent | `CompoundHardSteelFacedWroughtIron` | [Chinese ironclad Dingyuan](https://en.wikipedia.org/wiki/Chinese_ironclad_Dingyuan), [Navypedia Chin Yen](https://navypedia.org/ships/japan/jap_bb_chin_yen.htm) | Captured sister of Ting Yuen; current mild steel looks unsupported. |
| Chao Yung | `MildSteel` | 0.75 | Steel plating; little/no true armor | Consistent |  | [Naval Encyclopedia Chaoyong class](https://naval-encyclopedia.com/ww1/china/chaoyong-class-cruisers.php), [Chinese cruiser Chaoyong](https://en.wikipedia.org/wiki/Chinese_cruiser_Chaoyong) | Current mild steel is plausible for the limited steel protection represented in XML. |
| Chih Yuan | `MildSteel` | 0.75 | Deck/gun-shield armor found; material not explicit | Insufficient |  | [Chinese cruiser Zhiyuan](https://en.wikipedia.org/wiki/Chinese_cruiser_Zhiyuan), [Zhiyuan-class cruiser](https://en.wikipedia.org/wiki/Zhiyuan-class_cruiser) | Current mild steel remains unverified. |
| King Yuan | `CompoundHardSteelFacedWroughtIron` | 0.68 | Armored belt/deck found, material not explicit | Insufficient |  | [Chinese cruiser Jingyuan (1887)](https://en.wikipedia.org/wiki/Chinese_cruiser_Jingyuan_(1887)) | Search did not confirm compound material. |
| Ping Yuen | `MildSteel` | 0.75 | Thickness/layout found; material not explicit | Insufficient |  | [Chinese cruiser Pingyuan](https://en.wikipedia.org/wiki/Chinese_cruiser_Pingyuan) | Current mild steel remains unverified. |
| Fuso | `MildSteel` | 0.75 | Iron armor | Likely inconsistent | `WroughtIron` | [Navypedia Fuso 1878](https://www.navypedia.org/ships/japan/jap_bb_fuso78.htm), [Japanese ironclad Fuso](https://en.wikipedia.org/wiki/Japanese_ironclad_Fus%C5%8D) | Direct source says iron, not mild steel. |
| Itsukushima | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Matsushima class](https://navypedia.org/ships/japan/jap_cr_hashidate.htm), [Naval Encyclopedia Matsushima class](https://naval-encyclopedia.com/industrial-era/1890-fleets/japan/matsushima-class_cruiser.php) | Source says Harvey steel; current Harvey-family enum is compatible. |
| Itsukushima (1902) | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Matsushima class](https://navypedia.org/ships/japan/jap_cr_hashidate.htm) | Same class/date variant. |
| Matsushima | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Matsushima class](https://navypedia.org/ships/japan/jap_cr_hashidate.htm) | Source says Harvey steel; current Harvey-family enum is compatible. |
| Matsushima (1902) | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Matsushima class](https://navypedia.org/ships/japan/jap_cr_hashidate.htm) | Same class/date variant. |
| Kongo | `WroughtIron` | 0.60 | Wrought iron; Navypedia table says compound but other sources name wrought-iron belt | Consistent |  | [Japanese ironclad Kongo](https://en.wikipedia.org/wiki/Japanese_ironclad_Kong%C5%8D), [Navypedia Kongo 1878](https://www.navypedia.org/ships/japan/jap_cr_kongo.htm) | Mixed source wording; current wrought iron is supported. |
| Chiyoda | `HarveyNickelSteel` | 0.78 | Conflicting source wording: Navypedia table says compound; same page's protection text and Wikipedia say Harvey/Harvey nickel steel | Insufficient |  | [Navypedia Chiyoda](https://www.navypedia.org/ships/japan/jap_cr_chiyoda.htm), [Japanese cruiser Chiyoda](https://en.wikipedia.org/wiki/Japanese_cruiser_Chiyoda) | Needs source-priority decision before editing; current type may still be right. |
| Fuji | `HarveyNickelSteel` | 0.78 | Harvey nickel steel | Consistent |  | [Navypedia Fuji](https://www.navypedia.org/ships/japan/jap_bb_fuji.htm), [Japanese battleship Fuji](https://en.wikipedia.org/wiki/Japanese_battleship_Fuji) | Direct match. |
| Fuji (1902) | `HarveyNickelSteel` | 0.78 | Harvey nickel steel | Consistent |  | [Navypedia Fuji](https://www.navypedia.org/ships/japan/jap_bb_fuji.htm) | Same class/date variant. |
| Rossiya | `HarveyNickelSteel` | 0.78 | Harvey-nickel steel | Consistent |  | [Navypedia Rossiya](https://navypedia.org/ships/russia/ru_cr_rossiya.htm) | Direct match. |
| Rurik | `HarveyMildSteel` | 0.74 | Nickel steel / steel-nickel armor | Likely inconsistent | `NickelSteel` | [Russian cruiser Rurik (1892)](https://en.wikipedia.org/wiki/Russian_cruiser_Rurik_(1892)), [Naval Encyclopedia Rurik](https://naval-encyclopedia.com/ww1/russia/cruiser-rurik-1892.php) | Source material is not Harvey mild steel. |
| Asama | `HarveyNickelSteel` | 0.78 | Harvey nickel steel / Harvey armor | Consistent |  | [Navypedia Asama](https://navypedia.org/ships/japan/jap_cr_asama.htm), [Asama-class cruiser](https://en.wikipedia.org/wiki/Asama-class_cruiser) | Direct or family match. |
| Suma | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Suma](https://navypedia.org/ships/japan/jap_cr_suma.htm) | Source says Harvey steel; current Harvey-family enum is compatible. |
| Variag | `HighTensileSteel` | 0.82 | Nickel steel | Likely inconsistent | `NickelSteel` | [Navypedia Varyag](https://navypedia.org/ships/russia/ru_cr_varyag.htm) | Source explicitly points to nickel steel, not HT steel. |
| Peresviet | `HarveyNickelSteel` | 0.78 | Krupp cemented and Harvey | Likely inconsistent | `KruppCemented1894` | [Navypedia Peresvet](https://www.navypedia.org/ships/russia/ru_bb_peresvet.htm) | Mixed armor; if single type follows main belt/heavy armor, KC is better. |
| Peresviet (1905) | `HarveyNickelSteel` | 0.78 | Krupp cemented and Harvey | Likely inconsistent | `KruppCemented1894` | [Navypedia Peresvet](https://www.navypedia.org/ships/russia/ru_bb_peresvet.htm) | Same class/date variant. |
| Bayan | `HarveyNickelSteel` | 0.78 | Harvey-nickel steel for Bayan (i) | Consistent |  | [Navypedia Bayan](https://www.navypedia.org/ships/russia/ru_cr_bayan.htm) | Direct match for the Port Arthur-era ship. |
| Novik | `HighTensileSteel` | 0.82 | Nickel steel; conning tower Krupp steel | Likely inconsistent | `NickelSteel` | [Navypedia Novik](https://www.navypedia.org/ships/russia/ru_cr_novik.htm) | Source explicitly points to nickel steel for most armor. |
| Asahi | `HarveyNickelSteel` | 0.78 | Harvey nickel steel | Consistent |  | [Navypedia Shikishima / Asahi generation](https://navypedia.org/ships/japan/jap_bb_shikishima.htm), [Japanese battleship Asahi](https://en.wikipedia.org/wiki/Japanese_battleship_Asahi) | Same British-built Harvey NS generation as Shikishima; verify exact class page if needed. |
| Shikishima | `HarveyNickelSteel` | 0.78 | Harvey nickel steel | Consistent |  | [Navypedia Shikishima](https://navypedia.org/ships/japan/jap_bb_shikishima.htm) | Direct match. |
| Kasagi | `HarveyNickelSteel` | 0.78 | Armor thickness found; material not explicit | Insufficient |  | [Kasagi-class cruiser](https://en.wikipedia.org/wiki/Kasagi-class_cruiser) | Harvey is plausible by date/builder, but the source found does not prove material. |
| Takasago | `HarveyNickelSteel` | 0.78 | Harvey steel | Consistent |  | [Navypedia Takasago](https://www.navypedia.org/ships/japan/jap_cr_takasago.htm) | Source says Harvey steel; current Harvey-family enum is compatible. |
| Kasuga | `KruppCemented1894` | 0.83 | Harvey-type case-hardened steel | Likely inconsistent | `HarveyNickelSteel` | [Naval Encyclopedia Kasuga class](https://naval-encyclopedia.com/ww1/japan/kasuga-class-armoured-cruisers.php), [Navypedia Kasuga](https://navypedia.org/ships/japan/jap_cr_kasuga.htm) | Source contradicts Krupp; verify with a second high-quality source before applying because changing also changes factor. |
| Nisshin | `KruppCemented1894` | 0.83 | Harvey-type case-hardened steel | Likely inconsistent | `HarveyNickelSteel` | [Naval Encyclopedia Kasuga class](https://naval-encyclopedia.com/ww1/japan/kasuga-class-armoured-cruisers.php), [Navypedia Kasuga](https://navypedia.org/ships/japan/jap_cr_kasuga.htm) | Same class/source issue as Kasuga. |
| Idzumi (1900) | `MildSteel` | 0.75 | Steel hull/deck armor | Consistent |  | [Chilean cruiser Esmeralda (1883)](https://en.wikipedia.org/wiki/Chilean_cruiser_Esmeralda_(1883)), [Japanese cruiser Izumi](https://military-history.fandom.com/wiki/Japanese_cruiser_Izumi) | Current mild steel is a reasonable game mapping for steel armor. |
| Hai Chi | `HarveyNickelSteel` | 0.78 | Harvey-steel armor | Consistent |  | [Navypedia Hai Chi](https://www.navypedia.org/ships/china/ch_cr_hai_chi.htm), [Hai Chi-class cruiser](https://en.wikipedia.org/wiki/Hai_Chi-class_cruiser) | Nickel not explicit, but Harvey family matches. |
| Blanco Encalada | `HighTensileSteel` | 0.82 | Steel | Likely inconsistent | `MildSteel` | [Naval Balance 1920](https://www.sas.cglnm.com.ar/public/PAC/166/NavalBalance1920.pdf), [Chilean cruiser Blanco Encalada](https://en.wikipedia.org/wiki/Chilean_cruiser_Blanco_Encalada) | Found steel, not high-tensile steel. |
| Capitan Prat | `HarveyMildSteel` | 0.74 | Steel | Likely inconsistent | `MildSteel` | [Naval Balance 1920](https://www.sas.cglnm.com.ar/public/PAC/166/NavalBalance1920.pdf), [Chilean battleship Capitán Prat](https://en.wikipedia.org/wiki/Chilean_battleship_Capit%C3%A1n_Prat) | Earlier quick pass treated Harvey-family as plausible, but cited source found only steel. |
| Veinticinco de Mayo | `MildSteel` | 0.75 | Steel | Consistent |  | [Naval Balance 1920](https://www.sas.cglnm.com.ar/public/PAC/166/NavalBalance1920.pdf), [Naval Encyclopedia Veinticinco de Mayo / Nueve de Julio](https://naval-encyclopedia.com/ww1/argentina/veinticinco-de-mayo-class-cruisers.php) | Current mild steel is a reasonable game mapping for steel armor. |
| Nueve de Julio | `MildSteel` | 0.75 | Steel | Consistent |  | [Naval Balance 1920](https://www.sas.cglnm.com.ar/public/PAC/166/NavalBalance1920.pdf), [Naval Encyclopedia Veinticinco de Mayo / Nueve de Julio](https://naval-encyclopedia.com/ww1/argentina/veinticinco-de-mayo-class-cruisers.php) | Same design family. |
| Almirante Brown | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Argentine cruiser Almirante Brown](https://en.wikipedia.org/wiki/Argentine_cruiser_Almirante_Brown), [Naval Encyclopedia Almirante Brown](https://naval-encyclopedia.com/industrial-era/1890-fleets/argentina/almirante-brown.php) | Direct family match. |
| Imperator Nicolai I | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Navypedia Imperator Aleksandr II / Imperator Nikolai I](https://navypedia.org/ships/russia/ru_bb_imerator_alexandr_ii.htm) | Direct family match. |
| Admiral Nakhimov (1887) | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Navypedia Admiral Nakhimov](https://navypedia.org/ships/russia/ru_cr_admiral_nakhimov88.htm) | Direct family match. |
| Admiral Nakhimov (1900) | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Navypedia Admiral Nakhimov](https://navypedia.org/ships/russia/ru_cr_admiral_nakhimov88.htm) | Same hull after modernization. |
| Pamiat Azova | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Navypedia Pamyat Azova](https://www.navypedia.org/ships/russia/ru_cr_pamyat_azova.htm) | Direct family match. |
| Vladimir Monomakh | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor | Consistent |  | [Russian cruiser Vladimir Monomakh](https://en.wikipedia.org/wiki/Russian_cruiser_Vladimir_Monomakh), [Naval Encyclopedia Vladimir Monomakh](https://naval-encyclopedia.com/industrial-era/1890-fleets/russia/vladimir-monomakh.php) | Direct family match. |
| Navarin(1905) | `CompoundHardSteelFacedWroughtIron` | 0.68 | Compound armor; turrets nickel steel | Consistent |  | [Navypedia Navarin](https://www.navypedia.org/ships/russia/ru_bb_navarin.htm), [Russian battleship Navarin](https://en.wikipedia.org/wiki/Russian_battleship_Navarin) | Dominant vertical armor is compound. |
| Inflexible | `CompoundHardSteelFacedWroughtIron` | 0.68 | Iron belt/citadel; compound turret sandwich | Likely inconsistent | `WroughtIron` | [Navypedia Inflexible](https://www.navypedia.org/ships/uk/brit_bb1_inflexible.html) | Overall armor table indicates iron; compound is limited. |
| Centurion | `HarveyNickelSteel` | 0.78 | Compound, nickel steel, and Harvey nickel steel | Likely inconsistent | `CompoundHardSteelFacedWroughtIron` | [Navypedia Centurion](https://www.navypedia.org/ships/uk/brit_bb1_centurion.html) | Main belt/barbettes/CT are compound; Harvey is only some upper/gun-house armor. |
| Orlando | `MildSteel` | 0.75 | Compound belt; wrought iron bulkheads | Likely inconsistent | `CompoundHardSteelFacedWroughtIron` | [Navypedia Orlando](https://www.navypedia.org/ships/uk/brit_cr_orlando.htm) | Current mild steel is not supported for main belt. |
| Apollo | `MildSteel` | 0.75 | Steel | Consistent |  | [Navypedia Apollo](https://www.navypedia.org/ships/uk/brit_cr_apollo.htm) | Source says steel, not a special hardened type. |
| Sachsen | `WroughtIron` | 0.60 | Wrought iron backed with teak | Consistent |  | [Sachsen-class ironclad](https://en.wikipedia.org/wiki/Sachsen-class_ironclad), [Naval Encyclopedia Sachsen class](https://naval-encyclopedia.com/industrial-era/1890-fleets/germany/sachsen-class.php) | Direct match. |
| Gefion | `MildSteel` | 0.75 | Steel | Consistent |  | [Navypedia Gefion](https://www.navypedia.org/ships/germany/ger_cr_gefion.htm) | Source says steel, not a special hardened type. |
| D'Entrecasteaux | `HarveyNickelSteel` | 0.78 | Harvey nickel steel | Consistent |  | [Navypedia D'Entrecasteaux](https://www.navypedia.org/ships/france/fr_cr_d_entrecasteaux.htm) | Direct match. |
| Descartes | `HarveyNickelSteel` | 0.78 | Extra-mild steel armor deck | Likely inconsistent | `MildSteel` | [Descartes-class cruiser](https://en.wikipedia.org/wiki/Descartes-class_cruiser) | No Harvey evidence found. |
| Lombardia | `MildSteel` | 0.75 | Deck armor thickness found, material not explicit | Insufficient |  | [Italian cruiser Lombardia](https://en.wikipedia.org/wiki/Italian_cruiser_Lombardia), [Naval Encyclopedia Umbria/Lombardia class](https://naval-encyclopedia.com/ww1/italy/umbria-class-cruisers-1891.php) | Current mild steel is plausible but not confirmed. |
| Newark (C1) | `MildSteel` | 0.75 | Steel protective deck | Consistent |  | [Navypedia Newark](https://www.navypedia.org/ships/usa/us_cr_newark.htm), [GlobalSecurity Newark](https://www.globalsecurity.org/military/systems/ship/c-1.htm) | Source says steel, not a special hardened type. |
| Royal Sovereign | `HarveyNickelSteel` | 0.78 | Compound main armor; nickel steel upper belt | Likely inconsistent | `CompoundHardSteelFacedWroughtIron` | [Navypedia Royal Sovereign](https://www.navypedia.org/ships/uk/brit_bb1_royal_sovereign.html) | Main belt/bulkheads/barbettes are compound; no original Harvey evidence found. |

## Strongest Review Candidates

These are the already-specified rows where the current XML type appears most
likely to disagree with the searched material evidence:

| ShipClass.xml name | Current armorType | Suggested direction |
|---|---|---|
| Fuso | `MildSteel` | `WroughtIron` |
| Chin Yen (1896) | `MildSteel` | `CompoundHardSteelFacedWroughtIron` |
| Chi Yuan | `CompoundHardSteelFacedWroughtIron` | `MildSteel` |
| Rurik | `HarveyMildSteel` | `NickelSteel` |
| Variag | `HighTensileSteel` | `NickelSteel` |
| Novik | `HighTensileSteel` | `NickelSteel` |
| Peresviet | `HarveyNickelSteel` | `KruppCemented1894` if modeling main/heavy armor |
| Peresviet (1905) | `HarveyNickelSteel` | `KruppCemented1894` if modeling main/heavy armor |
| Inflexible | `CompoundHardSteelFacedWroughtIron` | `WroughtIron` |
| Centurion | `HarveyNickelSteel` | `CompoundHardSteelFacedWroughtIron` if modeling main/heavy armor |
| Orlando | `MildSteel` | `CompoundHardSteelFacedWroughtIron` |
| Royal Sovereign | `HarveyNickelSteel` | `CompoundHardSteelFacedWroughtIron` if modeling main/heavy armor |
| Descartes | `HarveyNickelSteel` | `MildSteel` |
| Kasuga | `KruppCemented1894` | `HarveyNickelSteel`, pending second-source confirmation |
| Nisshin | `KruppCemented1894` | `HarveyNickelSteel`, pending second-source confirmation |
| Blanco Encalada | `HighTensileSteel` | `MildSteel` |
| Capitan Prat | `HarveyMildSteel` | `MildSteel` |

## Caveats

Several ships used mixed materials by location. Because `ShipClasses.xml`
stores one `armorType` per ship class, the recommendation above generally
prioritizes main belt/heavy vertical armor for battleships and armored cruisers,
and protective deck material for protected cruisers. That policy should be
confirmed before applying XML edits.
