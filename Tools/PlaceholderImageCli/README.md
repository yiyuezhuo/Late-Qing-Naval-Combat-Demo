# Placeholder Image CLI

Renders ship placeholder images outside Unity for quick visual iteration.

Example:

```powershell
dotnet run --project Tools/PlaceholderImageCli/PlaceholderImageCli.csproj -- --name "Yoshino" --out ".codex-tmp/placeholder-preview"
```

The tool reads `Assets/StreamingAssets/Scenarios/ShipClasses.xml` by default and writes:

- `<ShipName>_Preview.png`
- `<ShipName>_Top.jpg`
- `<ShipName>_Icon.png`

Algorithm work should usually start from `Assets/Scripts/NavalCombatCore/ShipClassPlaceholderRendererCore.cs`. Unity adapts `ShipClass` models to that shared renderer in `Assets/Scripts/NavalCombat/ShipClassPlaceholderImageGenerator.cs`.

For algorithm tuning, compare generated placeholders with non-`isGraphicPlaceholder` ship classes in `Assets/StreamingAssets/Scenarios/ShipClasses.xml`. Their `portraitTopReference` and `portraitIconReference` entries usually point to historical override/top/icon images in `Assets/StreamingAssets/Pictures/Ships/`; those are useful qualitative targets for hull proportions, superstructure placement, and weapon layout.
