# NavalCombatHeadless

This is an early headless naval combat command-line runner.

Current probe scope:

- `Assets/Scripts/CoreUtils/**/*.cs`
- `Assets/Scripts/YYZLib/**/*.cs`
- `Assets/Scripts/NavalCombatCore/**/*.cs`

Current status:

- `dotnet build Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -v minimal` compiles the probe.
- `dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj` runs a minimal Core-only smoke check.
- The smoke check constructs a small ship/class in memory, registers it through `NavalGameState`/`EntityManager`, resets damage/expenditure state, evaluates battery and rapid-fire scores, checks `RandomUtils.SampleIndex`, and verifies the mask fallback.
- Passing `--scenario` runs a scenario headlessly. The runner resolves referenced `Leaders.xml`, `ShipClasses.xml`, and `NamedShips.xml` from the scenario folder when the scenario leaves those lists external.
- Top-level ship groups are set to automatic maneuver, fire, and searchlight control before running.
- Warnings are suppressed by default; use `--warnings` to show them. Errors are always printed.

Examples:

```powershell
dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -- smoke
dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -- --scenario "SJS - Manual Single Ship Duel.scen.xml" --duration-minutes 5
dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -- --scenario "Assets/StreamingAssets/Scenarios/SJS - Manual Single Ship Duel.scen.xml" --output Tools/Temp_Reports/headless-final.scen.xml
```

Runner options:

- `--scenario`, `-s`: scenario XML path, or a file name under `Assets/StreamingAssets/Scenarios`.
- `--duration-minutes`, `-m`: maximum simulated minutes. Defaults to the scenario end time, or 60 minutes if none exists.
- `--duration-seconds`: maximum simulated seconds.
- `--step-seconds`: simulation step size. Default: `1`.
- `--output`, `-o`: save the final headless `FullState` XML.
- `--warnings`: show Core warnings.

Next likely step:

- Load and run a representative full-length scenario, then decide which remaining Unity-backed services need real headless implementations rather than current fallbacks.
