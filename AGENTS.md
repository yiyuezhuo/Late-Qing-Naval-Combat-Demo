# Repository Guidelines

## Project Structure & Module Organization
This is a Unity 6 project for tactical/strategic naval warfare scenarios.
- `Assets/Scripts/`: gameplay code, split by domain (`NavalCombat`, `StrategicCombat`, `CoreUtils`, etc.).
- `Assets/Scenes/`: playable scenes (`Main Menu`, `Naval Game`, `Strategic Game`) plus test scenes in `Assets/Scenes/test/`.
- `Assets/UIDocuments/` and `Assets/UI Toolkit/`: UI Toolkit UXML/USS assets and templates.
- `Assets/StreamingAssets/`: scenarios, scripts, pictures, and runtime data.
- `Assets/Editor/`: editor automation (build preprocessors and custom menu tools).

## Build, Test, and Development Commands
- Don't write test.
- `Tools/NaabLikeCheck` is a small C# console harness for manual NAAB-like ballistics parity checks.

## Coding Style & Naming Conventions
- C# uses 4-space indentation and standard Unity/.NET style.
- Use `PascalCase` for classes/methods/properties/enums; use `camelCase` for fields/locals.
- Keep scripts in the closest domain folder (for example, naval tactical code under `Assets/Scripts/NavalCombat/`).
- Prefer small, composable MonoBehaviours; avoid hardcoding asset paths outside `StreamingAssets` conventions.

## UI Binding & Model Adaptation
- For UI Toolkit binding-only properties marked with `[CreateProperty]`, put them in the relevant `*Adapt.cs` partial/adaptation file, not in the core model file.
- Keep gameplay/domain formulas and invariants in the core model layer; adaptation properties may call core helpers but should not own the rule.
- Prefer binding through a small property with a setter for derived side effects instead of registering UI callbacks in editor scripts.

## Unity UI-Toolkit styling related thing
Check UI_Toolkit_AI_Guide.md for making style-related modifications in UI Toolkit.
With the current Panel Settings (`Scale With Screen Size`, reference resolution 1200x800, match width), 16:9 builds have about 1200x675 UI units available, so keep centered dialog outer heights at or below 620px.

## Unity Localization related thing
Check Localization_AI_Guide.md for making localization modifications in UI Toolkit.
Use `python Tools/add_localization.py` to add Dynamic Table entries (handles YAML quoting and ID assignment automatically).
Use `python Tools/normalize_localization_ids.py --apply` to re-normalize IDs after bulk edits.

## Data analysis related thing
Check Data_Analysis_AI_Guide.md before running ad hoc analysis over project data files, especially scenario XML.

## Local command guardrails
- Only build this repo’s solution: `Late-Qing-Naval-Combat-Demo.slnx`.
- When validating a build from the command line, use `dotnet build "Late-Qing-Naval-Combat-Demo.slnx" -v minimal`.

## XML editing guardrails
- For bulk edits to scenario XML under `Assets/StreamingAssets/Scenarios/`, prefer Python scripts over PowerShell text replacement/write-back.
- Do not use PowerShell write-back commands such as `Set-Content` for multilingual scenario XML, because they may corrupt encoding or localized text.
- When renaming or batch-replacing XML tags/attributes, preserve the original file encoding and line endings, and verify the diff only changes the intended XML nodes.

## Subagent Authorization
- For tasks in this repository, the agent may autonomously use subagents when helpful for exploration, parallel analysis, focused implementation, or verification.
