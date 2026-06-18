# NavalCombatHeadless

This is a compile probe for a future headless naval combat command-line runner.
It intentionally does not implement CLI behavior yet.

Current probe scope:

- `Assets/Scripts/CoreUtils/**/*.cs`
- `Assets/Scripts/YYZLib/**/*.cs`
- `Assets/Scripts/NavalCombatCore/**/*.cs`

Known omissions exposed by `dotnet build Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -v minimal`:

- Several Core partial members are currently implemented in Unity-side adaptation files, such as `ShipLog.MarkNonPhysicalPoseChanged`, `MountStatusRecord.mountLocation`, `GlobalString.mergedName`, and log housekeeping helpers.
- Core currently calls the Unity-side `Utils` helper for list synchronization.
- `VictoryStatus` depends on `InfluenceMapUtility`, which currently lives in the Unity-side naval combat folder.
