# Repository Guidelines

## Project Structure & Module Organization
This is a Unity 6 project (`6000.3.2f1`) for tactical/strategic naval warfare scenarios.
- `Assets/Scripts/`: gameplay code, split by domain (`NavalCombat`, `StrategicCombat`, `CoreUtils`, etc.).
- `Assets/Scenes/`: playable scenes (`Main Menu`, `Naval Game`, `Strategic Game`) plus test scenes in `Assets/Scenes/test/`.
- `Assets/UIDocuments/` and `Assets/UI Toolkit/`: UI Toolkit UXML/USS assets and templates.
- `Assets/StreamingAssets/`: scenarios, scripts, pictures, and runtime data.
- `Assets/Editor/`: editor automation (build preprocessors and custom menu tools).

## Build, Test, and Development Commands
- Open project in Unity Hub using editor `6000.3.2f1`.
- In Unity Editor, run `Custom/Build Manifest for platform without File System` before platform builds when streaming content changes.
- Build from Unity Build Profiles (desktop is the primary target; WebGL/mobile are secondary).
- Don't write test.

## Coding Style & Naming Conventions
- C# uses 4-space indentation and standard Unity/.NET style.
- Use `PascalCase` for classes/methods/properties/enums; use `camelCase` for fields/locals.
- Keep scripts in the closest domain folder (for example, naval tactical code under `Assets/Scripts/NavalCombat/`).
- Prefer small, composable MonoBehaviours; avoid hardcoding asset paths outside `StreamingAssets` conventions.

## Commit & Pull Request Guidelines
- Follow current history style: short, imperative commit subjects (for example, `Fix mount target setting soft close bugs`).
- Keep commits focused by feature/fix area; avoid mixing refactors with gameplay changes.
- PRs should include: purpose, key gameplay/UI impact, test evidence, and screenshots/GIFs for UI changes.
- Link related issues or TODO entries when applicable.

## Asset & Configuration Notes
- Keep Unity-generated folders (`Library`, `Temp`, `Logs`, `obj`) out of version control.
- `Assets/StreamingAssets/Manuals/` is intentionally excluded; update manuals separately.

## Unity UI-Toolkit styling related thing
Check UI_Toolkit_AI_Guide.md for making style-related modifications in UI Toolkit.

## Unity Localization related thing
Check Localization_AI_Guide.md for making localization modifications in UI Toolkit.
Use `python Tools/add_localization.py` to add Dynamic Table entries (handles YAML quoting and ID assignment automatically).
Use `python Tools/normalize_localization_ids.py --apply` to re-normalize IDs after bulk edits.

## Local command guardrails
- Only build this repo’s solution: `Late-Qing-Naval-Combat-Demo.slnx`.
- When validating a build from the command line, use `dotnet build "Late-Qing-Naval-Combat-Demo.slnx" -v minimal`.

## XML editing guardrails
- For bulk edits to scenario XML under `Assets/StreamingAssets/Scenarios/`, prefer Python scripts over PowerShell text replacement/write-back.
- Do not use PowerShell write-back commands such as `Set-Content` for multilingual scenario XML, because they may corrupt encoding or localized text.
- When renaming or batch-replacing XML tags/attributes, preserve the original file encoding and line endings, and verify the diff only changes the intended XML nodes.

## Subagent Authorization
- For tasks in this repository, the agent may autonomously use subagents when helpful for exploration, parallel analysis, focused implementation, or verification.
