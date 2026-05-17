# Localization Terminology

This document records domain-specific translation choices for localization work.
When adding or revising localized text, check this file before translating
naval, tactical, SK5, or UI terms by literal word matching. Add new entries here
when a term needs special handling.

## Japanese

| English term | Preferred Japanese | Notes |
| --- | --- | --- |
| Fire Control System | 射撃指揮システム | In the surface-ship naval gunnery context, prefer `射撃指揮システム`. The broader technical term is `射撃統制システム`, but Japanese naval usage commonly uses `射撃指揮`. Avoid `火器管制システム` here; that term is more associated with aircraft fire-control systems or weapon-control contexts. |
| Fire Control Table | 射撃指揮表 | Prefer this when referring to ship gunnery fire-control tables. Existing text may still contain `射撃管制表`; avoid introducing more variants unless matching a legacy key exactly is required. |
| Fire Control Code | 射撃指揮コード | Use for SK5 fire-control codes such as `1Q1`. |
| Fire Control Radar | 射撃管制レーダー | This expression is common and may be kept. |
| FCS | FCS | Keep the abbreviation when the English source uses the short label, especially in compact damage-effect text or UI columns. |
| Doctrine | ドクトリン | Use for gameplay/UI doctrine settings. Avoid Chinese-style literal translations such as `教義` or `条令` in Japanese UI and tutorial text. |
| Ship Class | 艦級 | Use for ship class/type records and UI labels. Avoid `艦型` unless translating a literal hull-form/type nuance outside the game data concept. |
| Quick-firing gun / rapid-firing gun | 速射砲 | Use for the historical naval weapon type. Avoid literal `連射砲`. |
| Battery (shipboard battery UI) | 砲兵装 | Use for shipboard battery labels, records, firepower, and selectors. Use `砲台` only when the source literally means a fixed coast/land battery, fort battery, or historical place name. |
| Rapid Fire Battery / Rapid Firing Battery | 速射砲兵装 | Use for the shipboard rapid-fire battery record/group in UI labels. Use bare `速射砲` for individual weapon names, and `速射砲群` only when the text explicitly emphasizes a group of guns. Avoid `砲台` and `砲側` for battery/record/selector labels. |
| Resolve (combat/result processing) | 処理 | Use `処理` for combat/result processing UI actions, e.g. `海戦処理` and `ターン処理`. Avoid literal `解決` except for mathematical/computation solutions such as a torpedo firing solution. |
| Mount | 砲架 | Use for gun/weapon mounts in UI and damage text. Use `魚雷発射管` or `魚雷発射機` for torpedo-specific mounts when the component is explicitly a torpedo tube/launcher. Avoid generic `マウント` and avoid confusing mount with `搭載`. |
| Encounter Rock | 過岩 | Scenario location name. Keep `過岩`; do not replace with a katakana transliteration. |

## Chinese

| English term | Simplified Chinese | Traditional Chinese | Notes |
| --- | --- | --- | --- |
| Fire Control System | 火控系统 | 火控系統 | Natural short technical term for naval gunnery UI text. |
| Fire Control Radar | 火控雷达 | 火控雷達 | Use the same `火控` wording as Fire Control System in Chinese UI text. |
| Fire Control Table | 火控表 | 火控表 | Use for the SK5/game table. |
| Fire Control Code | 火控码 | 火控碼 | Use for codes such as `1Q1`. |
| Role (SK5 fire-control context) | 地位 | 地位 | Use for the SK5 fire-control role/category field. Keep this wording even when `role` would normally be translated as `角色`. |
| EA / Early Access | EA | EA | Keep the abbreviation `EA` unchanged when it appears in UI or scenario prose. |
| latent variable model | 隐变量模型 | 隱變量模型 | Prefer this over literal mixed English phrases such as `latent model` or `latent-rounding model`. |
| Quick-firing gun / rapid-firing gun | 速射炮 | 速射砲 | Use for the historical naval weapon type. Avoid literal `连射炮` / `連射砲`. |
| Rapid Fire Battery / Rapid Firing Battery | 速射炮组 | 速射砲組 | Use for the shipboard rapid-fire battery record/group in UI labels. Avoid `炮台` / `砲台` unless the source literally means a fixed artillery battery. |
| Resolve (combat/result processing) | 结算 | 結算 | Use for combat/result processing UI actions. Avoid `解决` / `解決` for this gameplay operation. |
| Mount | 炮架 | 砲架 | Generic gun/weapon mount. Use `鱼雷发射管` / `魚雷發射管` for torpedo-specific mounts when appropriate. Avoid translating this as generic loading/carrying. |

