using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Properties;
using UnityEngine;
using NavalCombatCore;

public class ShipClassPlaceholderGeneratorDialogModel : IDisposable
{
    const int DefaultCanvasHeight = 250;

    public ShipClass shipClass;

    [CreateProperty] public int canvasWidth { get; set; } = 1200;
    [CreateProperty] public int canvasHeight { get; set; } = DefaultCanvasHeight;
    [CreateProperty] public int hullPadding { get; set; } = 18;
    [CreateProperty] public int lineWidth { get; set; } = 3;
    [CreateProperty] public int deckInsetAmount { get; set; } = 10;
    [CreateProperty] public float superstructureHeightScale { get; set; } = 1f;
    [CreateProperty] public int funnelCountModeValue { get; set; } = (int)PlaceholderFunnelCountMode.Auto;
    [CreateProperty] public float funnelSpacingBias { get; set; }
    [CreateProperty] public float bowSharpness { get; set; } = 1.15f;
    [CreateProperty] public float sternFullness { get; set; } = 1.05f;
    [CreateProperty] public float weaponScale { get; set; } = 1f;

    public Texture2D previewTexture { get; private set; }
    public Texture2D topTexture { get; private set; }
    public Texture2D iconTexture { get; private set; }

    public string statusText { get; private set; } = "Adjust parameters and click Generate.";
    public bool hasGenerated => previewTexture != null && topTexture != null && iconTexture != null;

    public void ApplyRecommendedCanvasSize()
    {
        canvasHeight = DefaultCanvasHeight;

        if (shipClass == null || shipClass.lengthFoot <= 0 || shipClass.beamFoot <= 0)
        {
            canvasWidth = Mathf.Max(canvasWidth, 320);
            return;
        }

        var settings = BuildSettings();
        settings.canvasHeight = DefaultCanvasHeight;
        var recommendedWidth = ShipClassPlaceholderImageRenderer.CalculateRecommendedCanvasWidth(shipClass, settings);
        canvasWidth = Mathf.Clamp(recommendedWidth, 320, 4096);
    }

    public bool TryGenerate()
    {
        if (!ShipClassPlaceholderImageGenerator.TryRenderFromModel(this, out var renderResult))
        {
            statusText = renderResult.message;
            DisposeTextures();
            return false;
        }

        DisposeTextures();
        previewTexture = renderResult.previewTexture;
        topTexture = renderResult.topTexture;
        iconTexture = renderResult.iconTexture;
        statusText = renderResult.message;
        return true;
    }

    public string SaveTopImage()
    {
        if (topTexture == null)
        {
            statusText = "Generate an image before saving.";
            return statusText;
        }

        var saved = IOManager.Instance.SaveBinaryFile(topTexture.EncodeToJPG(90), GetDefaultFileName("_Top"), "jpg");
        statusText = saved ? "Saved Top JPG." : "Top JPG save cancelled.";
        return statusText;
    }

    public string SaveIconImage()
    {
        if (iconTexture == null)
        {
            statusText = "Generate an image before saving.";
            return statusText;
        }

        var saved = IOManager.Instance.SaveBinaryFile(iconTexture.EncodeToPNG(), GetDefaultFileName("_Icon"), "png");
        statusText = saved ? "Saved Icon PNG." : "Icon PNG save cancelled.";
        return statusText;
    }

    ShipClassPlaceholderImageRenderSettings BuildSettings()
    {
        return new ShipClassPlaceholderImageRenderSettings
        {
            canvasWidth = Mathf.Clamp(canvasWidth, 320, 4096),
            canvasHeight = Mathf.Clamp(canvasHeight, 96, 2048),
            hullPadding = Mathf.Clamp(hullPadding, 4, 256),
            lineWidth = Mathf.Clamp(lineWidth, 1, 24),
            deckInsetAmount = Mathf.Clamp(deckInsetAmount, 2, 80),
            superstructureHeightScale = Mathf.Clamp(superstructureHeightScale, 0.4f, 2.5f),
            funnelCountMode = (PlaceholderFunnelCountMode)Mathf.Clamp(funnelCountModeValue, 0, (int)PlaceholderFunnelCountMode.Three),
            funnelSpacingBias = Mathf.Clamp(funnelSpacingBias, -0.35f, 0.35f),
            bowSharpness = Mathf.Clamp(bowSharpness, 0.45f, 2.5f),
            sternFullness = Mathf.Clamp(sternFullness, 0.45f, 2.5f),
            weaponScale = Mathf.Clamp(weaponScale, 0.4f, 3f),
        };
    }

    string GetDefaultFileName(string suffix)
    {
        return ShipClassPlaceholderImageGenerator.GetDefaultFileName(shipClass, suffix);
    }

    void DisposeTextures()
    {
        DestroyTexture(previewTexture);
        DestroyTexture(topTexture);
        DestroyTexture(iconTexture);
        previewTexture = null;
        topTexture = null;
        iconTexture = null;
    }

    static void DestroyTexture(Texture2D tex)
    {
        if (tex != null)
        {
            UnityEngine.Object.Destroy(tex);
        }
    }

    public void Dispose()
    {
        DisposeTextures();
    }
}

public static class ShipClassPlaceholderImageGenerator
{
    const int DefaultPreviewCanvasWidthFallback = 1200;

    public readonly struct RenderResult
    {
        public readonly bool success;
        public readonly string message;
        public readonly Texture2D previewTexture;
        public readonly Texture2D topTexture;
        public readonly Texture2D iconTexture;

        public RenderResult(bool success, string message, Texture2D previewTexture = null, Texture2D topTexture = null, Texture2D iconTexture = null)
        {
            this.success = success;
            this.message = message;
            this.previewTexture = previewTexture;
            this.topTexture = topTexture;
            this.iconTexture = iconTexture;
        }
    }

    public readonly struct BatchGenerateResult
    {
        public readonly List<ShipClass> generatedShipClasses;
        public readonly List<string> skippedMessages;

        public BatchGenerateResult(List<ShipClass> generatedShipClasses, List<string> skippedMessages)
        {
            this.generatedShipClasses = generatedShipClasses;
            this.skippedMessages = skippedMessages;
        }
    }

    public static ShipClassPlaceholderGeneratorDialogModel CreateDefaultDialogModel(ShipClass shipClass)
    {
        var model = new ShipClassPlaceholderGeneratorDialogModel
        {
            shipClass = shipClass
        };
        model.ApplyRecommendedCanvasSize();
        return model;
    }

    public static bool TryRenderDefaultPreview(ShipClass shipClass, out RenderResult result)
    {
        if (shipClass == null)
        {
            result = new(false, "No ship class is selected.");
            return false;
        }

        if (shipClass.type == ShipType.LandBattery)
        {
            result = new(false, "Land Battery does not support placeholder ship images.");
            return false;
        }

        if (shipClass.lengthFoot <= 0 || shipClass.beamFoot <= 0)
        {
            result = new(false, "Ship Class lengthFoot and beamFoot must both be greater than 0.");
            return false;
        }

        var settings = BuildDefaultSettings(shipClass);
        result = new(
            true,
            $"Generated placeholder silhouette for {shipClass.name.GetMergedName()} ({settings.canvasWidth}x{settings.canvasHeight}).",
            ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.Preview),
            ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.TopJpg),
            ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.IconPng));
        return true;
    }

    public static string BuildDefaultPreviewSignature(ShipClass shipClass)
    {
        if (shipClass == null)
            return "null";

        var settings = BuildDefaultSettings(shipClass);
        var sb = new StringBuilder(512);
        sb.Append(shipClass.objectId).Append('|');
        sb.Append(shipClass.type).Append('|');
        sb.Append(shipClass.lengthFoot).Append('|');
        sb.Append(shipClass.beamFoot).Append('|');
        sb.Append(shipClass.displacementTons).Append('|');
        sb.Append(shipClass.speedKnots).Append('|');
        sb.Append(settings.canvasWidth).Append('|');
        sb.Append(settings.canvasHeight).Append('|');
        sb.Append(settings.hullPadding).Append('|');
        sb.Append(settings.lineWidth).Append('|');
        sb.Append(settings.deckInsetAmount).Append('|');
        sb.Append(settings.superstructureHeightScale).Append('|');
        sb.Append((int)settings.funnelCountMode).Append('|');
        sb.Append(settings.funnelSpacingBias).Append('|');
        sb.Append(settings.bowSharpness).Append('|');
        sb.Append(settings.sternFullness).Append('|');
        sb.Append(settings.weaponScale).Append('|');

        foreach (var battery in shipClass.batteryRecords ?? new List<BatteryRecord>())
        {
            sb.Append("B:").Append(battery.shellSizeInch).Append(';');
            foreach (var record in battery.mountLocationRecords ?? new List<MountLocationRecord>())
            {
                AppendMountRecordSignature(sb, record);
            }
        }

        foreach (var record in shipClass.torpedoSector?.mountLocationRecords ?? new List<MountLocationRecord>())
        {
            sb.Append("T:").Append(record.trainable).Append(';');
            AppendMountRecordSignature(sb, record);
        }

        foreach (var record in shipClass.rapidFireBatteryRecords ?? new List<RapidFireBatteryRecord>())
        {
            sb.Append("R:");
            AppendIntSequence(sb, record.barrelsLevelPort);
            sb.Append('/');
            AppendIntSequence(sb, record.barrelsLevelStarboard);
            sb.Append(';');
        }

        return sb.ToString();
    }

    public static bool TryRenderFromModel(ShipClassPlaceholderGeneratorDialogModel model, out RenderResult result)
    {
        if (model.shipClass == null)
        {
            result = new(false, "No ship class is selected.");
            return false;
        }

        if (model.shipClass.type == ShipType.LandBattery)
        {
            result = new(false, "Land Battery does not support placeholder ship images.");
            return false;
        }

        if (model.shipClass.lengthFoot <= 0 || model.shipClass.beamFoot <= 0)
        {
            result = new(false, "Ship Class lengthFoot and beamFoot must both be greater than 0.");
            return false;
        }

        var settings = BuildSettings(model);
        result = new(
            true,
            $"Generated placeholder silhouette for {model.shipClass.name.GetMergedName()} ({settings.canvasWidth}x{settings.canvasHeight}).",
            ShipClassPlaceholderImageRenderer.Render(model.shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.Preview),
            ShipClassPlaceholderImageRenderer.Render(model.shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.TopJpg),
            ShipClassPlaceholderImageRenderer.Render(model.shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.IconPng));
        return true;
    }

    public static BatchGenerateResult GenerateAndBindAllMarked(IEnumerable<ShipClass> shipClasses)
    {
        var generatedShipClasses = new List<ShipClass>();
        var skippedMessages = new List<string>();
        var shipsDirectoryPath = GetShipsDirectoryPath();
        Directory.CreateDirectory(shipsDirectoryPath);

        foreach (var shipClass in shipClasses)
        {
            var model = CreateDefaultDialogModel(shipClass);

            if (!TryRenderFromModel(model, out var renderResult))
            {
                skippedMessages.Add($"{shipClass?.name?.english ?? "ShipClass"}: {renderResult.message}");
                model.Dispose();
                continue;
            }

            try
            {
                try
                {
                    var topFileName = GetDefaultFileName(shipClass, "_Top");
                    var iconFileName = GetDefaultFileName(shipClass, "_Icon");

                    File.WriteAllBytes(Path.Combine(shipsDirectoryPath, $"{topFileName}.jpg"), renderResult.topTexture.EncodeToJPG(90));
                    File.WriteAllBytes(Path.Combine(shipsDirectoryPath, $"{iconFileName}.png"), renderResult.iconTexture.EncodeToPNG());

                    shipClass.portraitTopReference.path = $"Pictures/Ships/{topFileName}.jpg";
                    shipClass.portraitTopReference.isBuiltin = true;
                    shipClass.portraitIconReference.path = $"Pictures/Ships/{iconFileName}.png";
                    shipClass.portraitIconReference.isBuiltin = true;
                    generatedShipClasses.Add(shipClass);
                }
                catch (Exception ex)
                {
                    skippedMessages.Add($"{shipClass?.name?.english ?? "ShipClass"}: {ex.Message}");
                }
            }
            finally
            {
                if (renderResult.previewTexture != null)
                    UnityEngine.Object.Destroy(renderResult.previewTexture);
                if (renderResult.topTexture != null)
                    UnityEngine.Object.Destroy(renderResult.topTexture);
                if (renderResult.iconTexture != null)
                    UnityEngine.Object.Destroy(renderResult.iconTexture);
                model.Dispose();
            }
        }

        return new(generatedShipClasses, skippedMessages);
    }

    static void AppendMountRecordSignature(StringBuilder sb, MountLocationRecord record)
    {
        if (record == null)
        {
            sb.Append("null;");
            return;
        }

        sb.Append(record.mountLocation).Append(',');
        sb.Append(record.mounts).Append(',');
        sb.Append(record.barrels).Append(',');
        foreach (var arc in record.mountArcs ?? new List<MountArcRecord>())
        {
            sb.Append(arc.startDeg).Append(':').Append(arc.CoverageDeg).Append(',');
        }
        sb.Append(';');
    }

    static void AppendIntSequence(StringBuilder sb, IEnumerable<int> values)
    {
        foreach (var value in values ?? Enumerable.Empty<int>())
        {
            sb.Append(value).Append(',');
        }
    }

    static ShipClassPlaceholderImageRenderSettings BuildDefaultSettings(ShipClass shipClass)
    {
        var model = new ShipClassPlaceholderGeneratorDialogModel
        {
            shipClass = shipClass,
            canvasWidth = DefaultPreviewCanvasWidthFallback,
        };
        model.ApplyRecommendedCanvasSize();
        return BuildSettings(model);
    }

    public static string GetDefaultFileName(ShipClass shipClass, string suffix)
    {
        var baseName = shipClass?.name?.english;
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "ShipClass";

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }

        return baseName.Trim() + suffix;
    }

    public static string GetShipsDirectoryPath()
    {
        return Path.Combine(Application.streamingAssetsPath, "Pictures", "Ships");
    }

    static ShipClassPlaceholderImageRenderSettings BuildSettings(ShipClassPlaceholderGeneratorDialogModel model)
    {
        return new ShipClassPlaceholderImageRenderSettings
        {
            canvasWidth = Mathf.Clamp(model.canvasWidth, 320, 4096),
            canvasHeight = Mathf.Clamp(model.canvasHeight, 96, 2048),
            hullPadding = Mathf.Clamp(model.hullPadding, 4, 256),
            lineWidth = Mathf.Clamp(model.lineWidth, 1, 24),
            deckInsetAmount = Mathf.Clamp(model.deckInsetAmount, 2, 80),
            superstructureHeightScale = Mathf.Clamp(model.superstructureHeightScale, 0.4f, 2.5f),
            funnelCountMode = (PlaceholderFunnelCountMode)Mathf.Clamp(model.funnelCountModeValue, 0, (int)PlaceholderFunnelCountMode.Three),
            funnelSpacingBias = Mathf.Clamp(model.funnelSpacingBias, -0.35f, 0.35f),
            bowSharpness = Mathf.Clamp(model.bowSharpness, 0.45f, 2.5f),
            sternFullness = Mathf.Clamp(model.sternFullness, 0.45f, 2.5f),
            weaponScale = Mathf.Clamp(model.weaponScale, 0.4f, 3f),
        };
    }
}

public static class ShipClassPlaceholderImageRenderer
{
    public enum RenderVariant
    {
        Preview,
        TopJpg,
        IconPng,
    }

    public static Texture2D Render(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        var image = ShipClassPlaceholderImageRendererCore.Render(CreateRenderInput(shipClass), settings, ToCoreVariant(variant));
        return ToTexture(image);
    }

    public static int CalculateRecommendedCanvasWidth(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        return ShipClassPlaceholderImageRendererCore.CalculateRecommendedCanvasWidth(CreateRenderInput(shipClass), settings);
    }

    public static ShipClassPlaceholderRenderInput CreateRenderInput(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        return new ShipClassPlaceholderRenderInput
        {
            name = shipClass.name?.GetMergedName() ?? shipClass.name?.english ?? "ShipClass",
            type = ToPlaceholderShipType(shipClass.type),
            displacementTons = shipClass.displacementTons,
            lengthFoot = shipClass.lengthFoot,
            beamFoot = shipClass.beamFoot,
            speedKnots = shipClass.speedKnots,
            batteryRecords = (shipClass.batteryRecords ?? new List<BatteryRecord>())
                .Select(battery => new PlaceholderBatteryRenderRecord
                {
                    shellSizeInch = battery.shellSizeInch,
                    mountLocationRecords = (battery.mountLocationRecords ?? new List<MountLocationRecord>())
                        .Select(CreateMountRecord)
                        .ToList()
                })
                .ToList(),
            torpedoMountLocationRecords = (shipClass.torpedoSector?.mountLocationRecords ?? new List<MountLocationRecord>())
                .Select(CreateMountRecord)
                .ToList(),
            rapidFireBatteryRecords = (shipClass.rapidFireBatteryRecords ?? new List<RapidFireBatteryRecord>())
                .Select(record => new PlaceholderRapidFireRenderRecord
                {
                    barrelsLevelPort = new List<int>(record.barrelsLevelPort ?? new List<int>()),
                    barrelsLevelStarboard = new List<int>(record.barrelsLevelStarboard ?? new List<int>())
                })
                .ToList()
        };
    }

    static PlaceholderMountRenderRecord CreateMountRecord(MountLocationRecord record)
    {
        if (record == null)
            return new PlaceholderMountRenderRecord();

        return new PlaceholderMountRenderRecord
        {
            mountLocation = ToPlaceholderMountLocation(record.mountLocation),
            barrels = record.barrels,
            mounts = record.mounts,
            trainable = record.trainable,
            mountArcs = (record.mountArcs ?? new List<MountArcRecord>())
                .Select(arc => new PlaceholderMountArcRenderRecord
                {
                    startDeg = arc.startDeg,
                    CoverageDeg = arc.CoverageDeg,
                    isCrossDeckFire = arc.isCrossDeckFire
                })
                .ToList()
        };
    }

    static PlaceholderShipType ToPlaceholderShipType(ShipType shipType)
    {
        return Enum.TryParse(shipType.ToString(), out PlaceholderShipType placeholderType)
            ? placeholderType
            : PlaceholderShipType.NotSpecified;
    }

    static PlaceholderMountLocation ToPlaceholderMountLocation(MountLocation mountLocation)
    {
        return Enum.TryParse(mountLocation.ToString(), out PlaceholderMountLocation placeholderLocation)
            ? placeholderLocation
            : PlaceholderMountLocation.NotSpecified;
    }

    static ShipClassPlaceholderImageRendererCore.RenderVariant ToCoreVariant(RenderVariant variant)
    {
        return variant switch
        {
            RenderVariant.TopJpg => ShipClassPlaceholderImageRendererCore.RenderVariant.TopJpg,
            RenderVariant.IconPng => ShipClassPlaceholderImageRendererCore.RenderVariant.IconPng,
            _ => ShipClassPlaceholderImageRendererCore.RenderVariant.Preview
        };
    }

    static Texture2D ToTexture(RgbaImage image)
    {
        var tex = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(image.pixels);
        tex.Apply();
        return tex;
    }
}
