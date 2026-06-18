# NavalCombatHeadless

This is a compile probe for a future headless naval combat command-line runner.
It intentionally does not implement CLI behavior yet.

Current probe scope:

- `Assets/Scripts/CoreUtils/**/*.cs`
- `Assets/Scripts/YYZLib/**/*.cs`
- `Assets/Scripts/NavalCombatCore/**/*.cs`

Current status:

- `dotnet build Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -v minimal` compiles the probe.
- Runtime CLI loading, scenario selection, ticking, and output formatting are intentionally not implemented yet.

Next likely step:

- Add a small runner entry point that loads a naval combat state/scenario, registers explicit headless service defaults, runs a fixed number of ticks, and writes a concise text or JSON summary.
