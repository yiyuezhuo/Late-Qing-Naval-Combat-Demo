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

## Chinese

| English term | Simplified Chinese | Traditional Chinese | Notes |
| --- | --- | --- | --- |
| Fire Control System | 火控系统 | 火控系統 | Natural short technical term for naval gunnery UI text. |
| Fire Control Table | 火控表 | 火控表 | Use for the SK5/game table. |
| Fire Control Code | 火控码 | 火控碼 | Use for codes such as `1Q1`. |
| latent variable model | 隐变量模型 | 隱變量模型 | Prefer this over literal mixed English phrases such as `latent model` or `latent-rounding model`. |

