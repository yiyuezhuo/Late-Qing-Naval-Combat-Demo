using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Properties;
using UnityEngine;
using NavalCombatCore;

public enum PlaceholderFunnelCountMode
{
    Auto,
    Zero,
    One,
    Two,
    Three
}

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

public struct ShipClassPlaceholderImageRenderSettings
{
    public int canvasWidth;
    public int canvasHeight;
    public int hullPadding;
    public int lineWidth;
    public int deckInsetAmount;
    public float superstructureHeightScale;
    public PlaceholderFunnelCountMode funnelCountMode;
    public float funnelSpacingBias;
    public float bowSharpness;
    public float sternFullness;
    public float weaponScale;
}

public static class ShipClassPlaceholderImageRenderer
{
    public enum RenderVariant
    {
        Preview,
        TopJpg,
        IconPng,
    }

    struct RenderPalette
    {
        public Color32 background;
        public Color32 hullFill;
        public Color32 hullOutline;
        public Color32 interiorLine;
        public Color32 detailFill;
        public Color32 mountFill;
        public Color32 rapidFire;
    }

    sealed class PixelCanvas
    {
        readonly Color32[] pixels;

        public int width { get; }
        public int height { get; }

        public PixelCanvas(int width, int height, Color32 background)
        {
            this.width = width;
            this.height = height;
            pixels = Enumerable.Repeat(background, width * height).ToArray();
        }

        public void SetPixel(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            pixels[y * width + x] = Blend(pixels[y * width + x], color);
        }

        public void FillRect(RectInt rect, Color32 color)
        {
            var xMin = Mathf.Max(0, rect.xMin);
            var xMax = Mathf.Min(width, rect.xMax);
            var yMin = Mathf.Max(0, rect.yMin);
            var yMax = Mathf.Min(height, rect.yMax);
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    pixels[y * width + x] = Blend(pixels[y * width + x], color);
                }
            }
        }

        public void FillEllipse(Vector2 center, float radiusX, float radiusY, Color32 color)
        {
            var xMin = Mathf.FloorToInt(center.x - radiusX);
            var xMax = Mathf.CeilToInt(center.x + radiusX);
            var yMin = Mathf.FloorToInt(center.y - radiusY);
            var yMax = Mathf.CeilToInt(center.y + radiusY);
            var invRx = radiusX <= 0 ? 0 : 1f / radiusX;
            var invRy = radiusY <= 0 ? 0 : 1f / radiusY;
            for (var y = yMin; y <= yMax; y++)
            {
                for (var x = xMin; x <= xMax; x++)
                {
                    var dx = (x - center.x) * invRx;
                    var dy = (y - center.y) * invRy;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        SetPixel(x, y, color);
                    }
                }
            }
        }

        public void DrawLine(Vector2 from, Vector2 to, int thickness, Color32 color)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 1.5f);
            steps = Mathf.Max(steps, 1);
            var radius = Mathf.Max(1, thickness) / 2f;
            for (var i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)steps);
                FillEllipse(p, radius, radius, color);
            }
        }

        public void DrawRectOutline(Rect rect, int thickness, Color32 color)
        {
            DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), thickness, color);
            DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), thickness, color);
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), thickness, color);
            DrawLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), thickness, color);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Color32 Blend(Color32 dst, Color32 src)
        {
            if (src.a == 255)
                return src;
            if (src.a == 0)
                return dst;

            var srcA = src.a / 255f;
            var dstA = dst.a / 255f;
            var outA = srcA + dstA * (1 - srcA);
            if (outA <= 0)
                return new Color32(0, 0, 0, 0);

            byte BlendChannel(byte s, byte d)
            {
                var result = (s * srcA + d * dstA * (1 - srcA)) / outA;
                return (byte)Mathf.Clamp(Mathf.RoundToInt(result), 0, 255);
            }

            return new Color32(
                BlendChannel(src.r, dst.r),
                BlendChannel(src.g, dst.g),
                BlendChannel(src.b, dst.b),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255)
            );
        }
    }

    sealed class HullProfile
    {
        public float bowSection;
        public float sternSection;
        public float maxBeamScale;
        public float sternTipScale;
        public float bridgeCenter;
        public float superstructureLength;
        public float deckhouseCenter;
    }

    static readonly Dictionary<ShipType, HullProfile> HullProfiles = new()
    {
        { ShipType.Battleship, new HullProfile { bowSection = 0.22f, sternSection = 0.70f, maxBeamScale = 1f, sternTipScale = 0.18f, bridgeCenter = 0.31f, superstructureLength = 0.19f, deckhouseCenter = 0.73f } },
        { ShipType.ArmoredCruiser, new HullProfile { bowSection = 0.23f, sternSection = 0.70f, maxBeamScale = 1f, sternTipScale = 0.16f, bridgeCenter = 0.32f, superstructureLength = 0.17f, deckhouseCenter = 0.72f } },
        // { ShipType.Cruiser, new HullProfile { bowSection = 0.26f, sternSection = 0.68f, maxBeamScale = 1f, sternTipScale = 0.12f, bridgeCenter = 0.33f, superstructureLength = 0.15f, deckhouseCenter = 0.70f } },
        { ShipType.LightCruiser, new HullProfile { bowSection = 0.27f, sternSection = 0.67f, maxBeamScale = 1f, sternTipScale = 0.10f, bridgeCenter = 0.34f, superstructureLength = 0.14f, deckhouseCenter = 0.70f } },
        { ShipType.Destroyer, new HullProfile { bowSection = 0.30f, sternSection = 0.63f, maxBeamScale = 1f, sternTipScale = 0.08f, bridgeCenter = 0.36f, superstructureLength = 0.10f, deckhouseCenter = 0.67f } },
        { ShipType.TorpedoBoat, new HullProfile { bowSection = 0.31f, sternSection = 0.62f, maxBeamScale = 1f, sternTipScale = 0.07f, bridgeCenter = 0.38f, superstructureLength = 0.08f, deckhouseCenter = 0.64f } },
        { ShipType.PatrolGunboat, new HullProfile { bowSection = 0.24f, sternSection = 0.74f, maxBeamScale = 1f, sternTipScale = 0.18f, bridgeCenter = 0.34f, superstructureLength = 0.13f, deckhouseCenter = 0.72f } },
        { ShipType.Transport, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.24f, bridgeCenter = 0.30f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { ShipType.Repair, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.24f, bridgeCenter = 0.32f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { ShipType.ArmedMerchantCruiser, new HullProfile { bowSection = 0.24f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.22f, bridgeCenter = 0.31f, superstructureLength = 0.15f, deckhouseCenter = 0.73f } },
        { ShipType.NotSpecified, new HullProfile { bowSection = 0.25f, sternSection = 0.72f, maxBeamScale = 1f, sternTipScale = 0.14f, bridgeCenter = 0.33f, superstructureLength = 0.14f, deckhouseCenter = 0.71f } },
    };

    public static Texture2D Render(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        var palette = GetPalette(settings, variant);
        var canvas = new PixelCanvas(settings.canvasWidth, settings.canvasHeight, palette.background);

        var shipRect = CalculateShipRect(shipClass, settings);
        var centerY = shipRect.center.y;
        var topEdge = new float[settings.canvasWidth];
        var bottomEdge = new float[settings.canvasWidth];
        for (var i = 0; i < topEdge.Length; i++)
        {
            topEdge[i] = float.NaN;
            bottomEdge[i] = float.NaN;
        }

        DrawHull(canvas, shipClass, shipRect, settings, palette, topEdge, bottomEdge);
        DrawDeckLines(canvas, shipRect, settings, palette, topEdge, bottomEdge);
        DrawSuperstructure(canvas, shipClass, shipRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawBatteryMounts(canvas, shipClass, shipRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawTorpedoMounts(canvas, shipClass, shipRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawRapidFireDetails(canvas, shipClass, shipRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawMasts(canvas, shipClass, shipRect, centerY, settings, palette, topEdge, bottomEdge);

        return canvas.ToTexture();
    }

    public static int CalculateRecommendedCanvasWidth(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var ratio = Mathf.Max(1.5f, shipClass.lengthFoot / Mathf.Max(1f, shipClass.beamFoot));
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableHeight = Mathf.Max(1f, settings.canvasHeight - visualPadding * 2f);
        var shipWidth = availableHeight * ratio;
        var canvasWidth = shipWidth + visualPadding * 2f;
        return Mathf.CeilToInt(canvasWidth);
    }

    static Rect CalculateShipRect(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableWidth = settings.canvasWidth - visualPadding * 2f;
        var availableHeight = settings.canvasHeight - visualPadding * 2f;
        var ratio = Mathf.Max(1.5f, shipClass.lengthFoot / Mathf.Max(1f, shipClass.beamFoot));

        var shipWidth = availableWidth;
        var shipHeight = shipWidth / ratio;
        if (shipHeight > availableHeight)
        {
            shipHeight = availableHeight;
            shipWidth = shipHeight * ratio;
        }

        var x = (settings.canvasWidth - shipWidth) / 2f;
        var y = (settings.canvasHeight - shipHeight) / 2f;
        return new Rect(x, y, shipWidth, shipHeight);
    }

    static void DrawHull(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        Vector2? prevTop = null;
        Vector2? prevBottom = null;

        for (var ix = Mathf.FloorToInt(shipRect.xMin); ix <= Mathf.CeilToInt(shipRect.xMax); ix++)
        {
            var t = 1f - Mathf.InverseLerp(shipRect.xMin, shipRect.xMax, ix);
            var halfBreadth = EvaluateHalfBreadth(t, profile, settings) * shipRect.height * 0.5f;
            var yTop = shipRect.center.y - halfBreadth;
            var yBottom = shipRect.center.y + halfBreadth;

            topEdge[Mathf.Clamp(ix, 0, topEdge.Length - 1)] = yTop;
            bottomEdge[Mathf.Clamp(ix, 0, bottomEdge.Length - 1)] = yBottom;

            for (var y = Mathf.CeilToInt(yTop); y <= Mathf.FloorToInt(yBottom); y++)
            {
                canvas.SetPixel(ix, y, palette.hullFill);
            }

            var top = new Vector2(ix, yTop);
            var bottom = new Vector2(ix, yBottom);
            if (prevTop.HasValue)
            {
                canvas.DrawLine(prevTop.Value, top, settings.lineWidth, palette.hullOutline);
                canvas.DrawLine(prevBottom.Value, bottom, settings.lineWidth, palette.hullOutline);
            }
            prevTop = top;
            prevBottom = bottom;
        }

        canvas.DrawLine(new Vector2(shipRect.xMin, shipRect.center.y), new Vector2(shipRect.xMax, shipRect.center.y), Mathf.Max(1, settings.lineWidth - 1), palette.interiorLine);
    }

    static void DrawDeckLines(PixelCanvas canvas, Rect shipRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        Vector2? prevInnerTop = null;
        Vector2? prevInnerBottom = null;
        var deckInset = settings.deckInsetAmount;
        for (var ix = Mathf.FloorToInt(shipRect.xMin); ix <= Mathf.CeilToInt(shipRect.xMax); ix++)
        {
            var clampedX = Mathf.Clamp(ix, 0, topEdge.Length - 1);
            if (float.IsNaN(topEdge[clampedX]))
                continue;

            var yTop = topEdge[clampedX] + deckInset;
            var yBottom = bottomEdge[clampedX] - deckInset;
            if (yBottom <= yTop)
                continue;

            var innerTop = new Vector2(ix, yTop);
            var innerBottom = new Vector2(ix, yBottom);
            if (prevInnerTop.HasValue)
            {
                canvas.DrawLine(prevInnerTop.Value, innerTop, 1, palette.interiorLine);
                canvas.DrawLine(prevInnerBottom.Value, innerBottom, 1, palette.interiorLine);
            }
            prevInnerTop = innerTop;
            prevInnerBottom = innerBottom;
        }
    }

    static void DrawSuperstructure(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipRect.height * 0.20f * settings.superstructureHeightScale;
        var bridge = BuildCenteredRect(shipRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY);
        DrawDeckBlock(canvas, bridge, palette.detailFill, palette.hullOutline, settings.lineWidth);

        var deckhouse = BuildCenteredRect(shipRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55f, mainWidth * 0.7f, centerY);
        DrawDeckBlock(canvas, deckhouse, palette.detailFill, palette.hullOutline, Mathf.Max(1, settings.lineWidth - 1));

        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return;

        var baseXMin = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.62f + settings.funnelSpacingBias * 0.2f);
        var baseXMax = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.40f + settings.funnelSpacingBias * 0.2f);
        if (funnelCount == 1)
        {
            baseXMin = baseXMax = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.48f + settings.funnelSpacingBias * 0.2f);
        }

        for (var i = 0; i < funnelCount; i++)
        {
            var t = funnelCount == 1 ? 0.5f : i / (float)(funnelCount - 1);
            var x = Mathf.Lerp(baseXMin, baseXMax, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var funnelHeight = Mathf.Max(5f, halfBreadth * 0.9f * settings.superstructureHeightScale);
            var funnelWidth = Mathf.Max(4f, shipRect.width * 0.015f);
            var rect = new Rect(x - funnelWidth / 2f, centerY - funnelHeight / 2f, funnelWidth, funnelHeight);
            DrawDeckBlock(canvas, rect, palette.hullOutline, palette.hullOutline, 1);
        }
    }

    static void DrawBatteryMounts(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var batteryVisualScale = EvaluateBatteryGlyphVisualScale(shipRect);
        var groupedRecords = (shipClass.batteryRecords ?? new List<BatteryRecord>())
            .SelectMany(battery => (battery.mountLocationRecords ?? new List<MountLocationRecord>())
                .Select(record => new
                {
                    battery,
                    record,
                    glyphSize = EvaluateBatteryGlyphSize(battery.shellSizeInch) * settings.weaponScale * 2.2f * batteryVisualScale,
                }))
            .GroupBy(entry => entry.record.mountLocation);

        foreach (var group in groupedRecords)
        {
            var totalMounts = group.Sum(entry => Mathf.Max(1, entry.record.mounts));
            var layoutGlyphSize = group.Max(entry => entry.glyphSize);
            var allPositions = ResolveMountPositions(group.Key, totalMounts, layoutGlyphSize, shipRect, centerY, topEdge, bottomEdge);
            var positionIndex = 0;
            var orderedEntries = group
                .OrderBy(entry => ResolveMountDirection(entry.record).x)
                .ThenBy(entry => ResolveMountDirection(entry.record).y)
                .ToList();

            foreach (var entry in orderedEntries)
            {
                var mountCount = Mathf.Max(1, entry.record.mounts);
                var positions = allPositions.Skip(positionIndex).Take(mountCount);
                var direction = ResolveMountDirection(entry.record);
                foreach (var pos in positions)
                {
                    DrawGunMount(canvas, pos, direction, entry.record.barrels, entry.glyphSize, settings.lineWidth, palette.mountFill, palette.hullOutline, palette.detailFill);
                }

                positionIndex += mountCount;
            }
        }
    }

    static void DrawTorpedoMounts(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var torpedoVisualScale = EvaluateTorpedoGlyphVisualScale(shipRect);
        var deckObstacles = BuildSuperstructureObstacles(shipClass, shipRect, centerY, settings, topEdge, bottomEdge);
        foreach (var record in shipClass.torpedoSector?.mountLocationRecords ?? new List<MountLocationRecord>())
        {
            var submerged = IsSubmergedTorpedoRecord(record);
            var baseSize = Mathf.Clamp(5f + record.barrels * 0.7f, 5f, 11f) * settings.weaponScale * 2f * torpedoVisualScale;
            var size = submerged ? baseSize : baseSize * 2f;
            var direction = submerged
                ? GetSubmergedMountOutwardDirection(record.mountLocation, ResolveMountDirection(record))
                : ResolveMountDirection(record);
            var positions = submerged
                ? ResolveSubmergedTorpedoPositions(record, size, shipRect, centerY, topEdge, bottomEdge, direction)
                : ResolveDeckTorpedoPositions(record.mountLocation, record.mounts, size, shipRect, centerY, topEdge, bottomEdge, deckObstacles);
            foreach (var pos in positions)
            {
                DrawTorpedoMount(canvas, pos, direction, record.barrels, size, submerged ? false : record.trainable, palette.mountFill, palette.hullOutline, palette.detailFill);
            }
        }
    }

    static void DrawRapidFireDetails(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var rapidFireRecords = shipClass.rapidFireBatteryRecords ?? new List<RapidFireBatteryRecord>();
        var portBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelPort ?? new List<int>()).FirstOrDefault());
        var starboardBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelStarboard ?? new List<int>()).FirstOrDefault());
        DrawRapidFireSide(canvas, shipRect, centerY, topEdge, bottomEdge, portBarrels, false, palette.rapidFire, settings.weaponScale);
        DrawRapidFireSide(canvas, shipRect, centerY, topEdge, bottomEdge, starboardBarrels, true, palette.rapidFire, settings.weaponScale);
    }

    static void DrawRapidFireSide(PixelCanvas canvas, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge, int count, bool starboard, Color32 color, float weaponScale)
    {
        if (count <= 0)
            return;

        var rendered = Mathf.Clamp(count, 1, 12);
        for (var i = 0; i < rendered; i++)
        {
            var t = rendered == 1 ? 0.5f : i / (float)(rendered - 1);
            var x = Mathf.Lerp(shipRect.xMin + shipRect.width * 0.24f, shipRect.xMin + shipRect.width * 0.76f, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var y = centerY + (starboard ? 1 : -1) * halfBreadth * 0.68f;
            var direction = starboard ? 1f : -1f;
            var halfSpan = 2f * weaponScale;
            var elevation = 3f * weaponScale;
            canvas.DrawLine(new Vector2(x - halfSpan, y), new Vector2(x + halfSpan, y + direction * elevation), 1, color);
        }
    }

    static void DrawMasts(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var mastXs = new List<float> { shipRect.xMin + shipRect.width * NormalizeForeAftX(0.22f) };
        if (shipClass.type != ShipType.TorpedoBoat)
        {
            mastXs.Add(shipRect.xMin + shipRect.width * NormalizeForeAftX(0.82f));
        }

        foreach (var x in mastXs)
        {
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var height = Mathf.Max(6f, halfBreadth * 0.95f);
            canvas.DrawLine(new Vector2(x, centerY - height / 2f), new Vector2(x, centerY + height / 2f), 1, palette.interiorLine);
        }
    }

    static void DrawDeckBlock(PixelCanvas canvas, Rect rect, Color32 fill, Color32 outline, int lineWidth)
    {
        canvas.FillRect(new RectInt(Mathf.RoundToInt(rect.xMin), Mathf.RoundToInt(rect.yMin), Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)), fill);
        canvas.DrawRectOutline(rect, Mathf.Max(1, lineWidth), outline);
    }

    static Rect BuildCenteredRect(Rect shipRect, float normalizedX, float length, float width, float centerY)
    {
        var centerX = shipRect.xMin + shipRect.width * normalizedX;
        return new Rect(centerX - length / 2f, centerY - width / 2f, length, width);
    }

    static List<Vector2> ResolveMountPositions(MountLocation location, int mountCount, float symbolSize, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge)
    {
        var anchors = GetAnchor(location);
        var isSideLocation =
            location == MountLocation.PortForward ||
            location == MountLocation.StarboardForward ||
            location == MountLocation.PortMidship ||
            location == MountLocation.StarboardMidship ||
            location == MountLocation.PortAfter ||
            location == MountLocation.StarboardAfter;

        var baseSpread = isSideLocation
            ? Mathf.Max(symbolSize * 2.4f, shipRect.width * 0.06f)
            : Mathf.Max(symbolSize * 1.35f, shipRect.width * 0.032f);
        var spread = mountCount <= 1 ? 0f : Mathf.Min(baseSpread, shipRect.width * (isSideLocation ? 0.13f : 0.07f));
        var positions = new List<Vector2>();
        for (var i = 0; i < Mathf.Max(1, mountCount); i++)
        {
            var offsetT = mountCount <= 1 ? 0f : (i - (mountCount - 1) / 2f);
            var x = shipRect.xMin + shipRect.width * anchors.x + offsetT * spread;
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var sideCompression = isSideLocation ? 0.76f : 1f;
            var y = centerY + halfBreadth * anchors.y * sideCompression;

            if (isSideLocation && mountCount > 1)
            {
                var stagger = Mathf.Abs(offsetT) < 0.1f ? 0f : Mathf.Sign(offsetT) * Mathf.Min(symbolSize * 0.18f, halfBreadth * 0.08f);
                y += Mathf.Sign(anchors.y) * stagger;
            }

            positions.Add(new Vector2(x, y));
        }
        return positions;
    }

    static List<Vector2> ResolveDeckTorpedoPositions(MountLocation location, int mountCount, float symbolSize, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge, List<Rect> obstacles)
    {
        var positions = ResolveMountPositions(location, mountCount, symbolSize * 2.8f, shipRect, centerY, topEdge, bottomEdge);
        if (obstacles == null || obstacles.Count == 0)
            return positions;

        var adjusted = new List<Vector2>(positions.Count);
        foreach (var pos in positions)
        {
            adjusted.Add(ResolveDeckMountAvoidance(pos, symbolSize, shipRect, obstacles));
        }

        return adjusted;
    }

    static bool IsSubmergedTorpedoRecord(MountLocationRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return false;

        return record.mountArcs.All(arc => arc.CoverageDeg <= 30f);
    }

    static List<Vector2> ResolveSubmergedTorpedoPositions(MountLocationRecord record, float symbolSize, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge, Vector2 direction)
    {
        var outward = GetSubmergedMountOutwardDirection(record.mountLocation, direction);
        var positions = new List<Vector2>();
        var count = Mathf.Max(1, record.mounts);
        var alongHull = new Vector2(-outward.y, outward.x);
        var spacing = symbolSize * 0.9f;
        var exposure = symbolSize * 0.58f;
        var anchor = GetAnchor(record.mountLocation);

        for (var i = 0; i < count; i++)
        {
            var offset = count <= 1 ? 0f : (i - (count - 1) / 2f) * spacing;
            float x;
            float y;

            if (Mathf.Abs(outward.x) > 0.5f)
            {
                x = outward.x > 0 ? shipRect.xMax + exposure : shipRect.xMin - exposure;
                y = centerY;
            }
            else
            {
                x = shipRect.xMin + shipRect.width * anchor.x;
                var hullY = outward.y < 0
                    ? topEdge[Mathf.Clamp(Mathf.RoundToInt(x), 0, topEdge.Length - 1)]
                    : bottomEdge[Mathf.Clamp(Mathf.RoundToInt(x), 0, bottomEdge.Length - 1)];
                y = hullY + outward.y * exposure;
            }

            var pos = new Vector2(x, y) + alongHull * offset;
            positions.Add(pos);
        }

        return positions;
    }

    static Vector2 ResolveDeckMountAvoidance(Vector2 originalPos, float symbolSize, Rect shipRect, List<Rect> obstacles)
    {
        var candidate = originalPos;
        if (!IntersectsObstacle(candidate, symbolSize, obstacles))
            return candidate;

        var halfWidth = GetDeckMountBounds(symbolSize).width * 0.5f;
        var margin = Mathf.Max(4f, symbolSize * 0.35f);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bounds = GetDeckMountBounds(candidate, symbolSize);
            var overlapping = obstacles.Where(obstacle => bounds.Overlaps(obstacle)).ToList();
            if (overlapping.Count == 0)
                return candidate;

            var bestCandidate = candidate;
            var bestDistance = float.MaxValue;
            foreach (var obstacle in overlapping)
            {
                var aftX = Mathf.Max(shipRect.xMin + halfWidth, obstacle.xMin - halfWidth - margin);
                var aftCandidate = new Vector2(aftX, originalPos.y);
                if (!IntersectsObstacle(aftCandidate, symbolSize, obstacles))
                {
                    var aftDistance = Mathf.Abs(aftCandidate.x - originalPos.x);
                    if (aftDistance < bestDistance)
                    {
                        bestDistance = aftDistance;
                        bestCandidate = aftCandidate;
                    }
                }

                var foreX = Mathf.Min(shipRect.xMax - halfWidth, obstacle.xMax + halfWidth + margin);
                var foreCandidate = new Vector2(foreX, originalPos.y);
                if (!IntersectsObstacle(foreCandidate, symbolSize, obstacles))
                {
                    var foreDistance = Mathf.Abs(foreCandidate.x - originalPos.x);
                    if (foreDistance < bestDistance)
                    {
                        bestDistance = foreDistance;
                        bestCandidate = foreCandidate;
                    }
                }
            }

            if (bestDistance < float.MaxValue)
                return bestCandidate;

            candidate = new Vector2(Mathf.Clamp(candidate.x - margin, shipRect.xMin + halfWidth, shipRect.xMax - halfWidth), candidate.y);
        }

        return candidate;
    }

    static bool IntersectsObstacle(Vector2 pos, float symbolSize, List<Rect> obstacles)
    {
        var bounds = GetDeckMountBounds(pos, symbolSize);
        foreach (var obstacle in obstacles)
        {
            if (bounds.Overlaps(obstacle))
                return true;
        }

        return false;
    }

    static Rect GetDeckMountBounds(float symbolSize)
    {
        return new Rect(0f, 0f, symbolSize * 2.6f, symbolSize * 1.9f);
    }

    static Rect GetDeckMountBounds(Vector2 pos, float symbolSize)
    {
        var size = GetDeckMountBounds(symbolSize).size;
        return new Rect(pos.x - size.x / 2f, pos.y - size.y / 2f, size.x, size.y);
    }

    static float EvaluateBatteryGlyphSize(float shellSizeInch)
    {
        if (shellSizeInch <= 0)
            return 4.5f;

        if (shellSizeInch <= 4.7f)
            return Mathf.Clamp(3.1f + shellSizeInch * 0.48f, 4f, 6.2f);

        var largeGunBoost = Mathf.Pow(shellSizeInch - 4.7f, 0.98f) * 1.55f;
        return Mathf.Clamp(5.8f + largeGunBoost, 5.8f, 23f);
    }

    static float EvaluateBatteryGlyphVisualScale(Rect shipRect)
    {
        var visualBeam = Mathf.Max(1f, shipRect.height);
        var scale = Mathf.Pow(72f / visualBeam, 0.55f);
        return Mathf.Clamp(scale, 0.82f, 1.45f);
    }

    static float EvaluateTorpedoGlyphVisualScale(Rect shipRect)
    {
        var visualBeam = Mathf.Max(1f, shipRect.height);
        var scale = Mathf.Pow(82f / visualBeam, 0.72f);
        return Mathf.Clamp(scale, 0.95f, 1.85f);
    }

    static Vector2 GetAnchor(MountLocation location)
    {
        return location switch
        {
            MountLocation.PortForward => new Vector2(NormalizeForeAftX(0.23f), -0.52f),
            MountLocation.Forward => new Vector2(NormalizeForeAftX(0.18f), 0f),
            MountLocation.StarboardForward => new Vector2(NormalizeForeAftX(0.23f), 0.52f),
            MountLocation.PortMidship => new Vector2(0.50f, -0.58f),
            MountLocation.Midship => new Vector2(0.50f, 0f),
            MountLocation.StarboardMidship => new Vector2(0.50f, 0.58f),
            MountLocation.PortAfter => new Vector2(NormalizeForeAftX(0.77f), -0.52f),
            MountLocation.After => new Vector2(NormalizeForeAftX(0.82f), 0f),
            MountLocation.StarboardAfter => new Vector2(NormalizeForeAftX(0.77f), 0.52f),
            _ => new Vector2(0.50f, 0f)
        };
    }

    static List<Rect> BuildSuperstructureObstacles(ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, float[] topEdge, float[] bottomEdge)
    {
        var obstacles = new List<Rect>();
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipRect.height * 0.20f * settings.superstructureHeightScale;
        obstacles.Add(ExpandRect(BuildCenteredRect(shipRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY), 3f));
        obstacles.Add(ExpandRect(BuildCenteredRect(shipRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55f, mainWidth * 0.7f, centerY), 3f));

        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return obstacles;

        var baseXMin = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.62f + settings.funnelSpacingBias * 0.2f);
        var baseXMax = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.40f + settings.funnelSpacingBias * 0.2f);
        if (funnelCount == 1)
            baseXMin = baseXMax = shipRect.xMin + shipRect.width * NormalizeForeAftX(0.48f + settings.funnelSpacingBias * 0.2f);

        for (var i = 0; i < funnelCount; i++)
        {
            var t = funnelCount == 1 ? 0.5f : i / (float)(funnelCount - 1);
            var x = Mathf.Lerp(baseXMin, baseXMax, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var funnelHeight = Mathf.Max(5f, halfBreadth * 0.9f * settings.superstructureHeightScale);
            var funnelWidth = Mathf.Max(4f, shipRect.width * 0.015f);
            obstacles.Add(ExpandRect(new Rect(x - funnelWidth / 2f, centerY - funnelHeight / 2f, funnelWidth, funnelHeight), 4f));
        }

        return obstacles;
    }

    static Rect ExpandRect(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    static Vector2 GetSubmergedMountOutwardDirection(MountLocation location, Vector2 fallbackDirection)
    {
        return location switch
        {
            MountLocation.Forward => Vector2.right,
            MountLocation.After => Vector2.left,
            MountLocation.PortForward => Vector2.up,
            MountLocation.PortMidship => Vector2.up,
            MountLocation.PortAfter => Vector2.up,
            MountLocation.StarboardForward => Vector2.down,
            MountLocation.StarboardMidship => Vector2.down,
            MountLocation.StarboardAfter => Vector2.down,
            _ => Mathf.Abs(fallbackDirection.x) >= Mathf.Abs(fallbackDirection.y)
                ? new Vector2(Mathf.Sign(fallbackDirection.x), 0f)
                : new Vector2(0f, Mathf.Sign(fallbackDirection.y))
        };
    }

    static void DrawGunMount(PixelCanvas canvas, Vector2 pos, Vector2 direction, int barrels, float size, int lineWidth, Color32 fill, Color32 outline, Color32 detailFill)
    {
        var lines = Mathf.Clamp(barrels, 1, 4);
        var footprintSize = size * (1f + 0.3f * (lines - 1));
        var bodyLength = footprintSize * 1.25f;
        var bodyWidth = Mathf.Max(4f, footprintSize * 0.85f);
        var perpendicular = new Vector2(-direction.y, direction.x);
        var front = pos + direction * (bodyLength * 0.42f);
        var rear = pos - direction * (bodyLength * 0.36f);

        canvas.FillEllipse(pos, bodyLength * 0.58f, bodyWidth * 0.72f, detailFill);
        canvas.DrawRectOutline(new Rect(pos.x - bodyLength * 0.58f, pos.y - bodyWidth * 0.72f, bodyLength * 1.16f, bodyWidth * 1.44f), 1, outline);
        canvas.FillEllipse(front, bodyWidth * 0.48f, bodyWidth * 0.48f, fill);
        canvas.FillEllipse(rear, bodyWidth * 0.38f, bodyWidth * 0.32f, detailFill);

        var barrelSpacing = Mathf.Min(bodyWidth * 0.28f, footprintSize * 0.24f + 0.45f);
        var barrelThickness = Mathf.Max(Mathf.Max(2, lineWidth + 1), Mathf.RoundToInt(size * 0.18f));
        for (var i = 0; i < lines; i++)
        {
            var spread = lines == 1 ? 0f : (i - (lines - 1) / 2f) * barrelSpacing;
            var from = front + perpendicular * spread;
            var to = from + direction * (size + 6f);
            canvas.DrawLine(from, to, barrelThickness, outline);
        }

        canvas.DrawLine(rear, front, 1, outline);
    }

    static void DrawTorpedoMount(PixelCanvas canvas, Vector2 pos, Vector2 direction, int barrels, float size, bool trainable, Color32 fill, Color32 outline, Color32 detailFill)
    {
        var along = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
        var perp = new Vector2(-along.y, along.x);
        var halfLength = size;
        var halfWidth = Mathf.Max(2f, size * (trainable ? 0.34f : 0.25f));
        var bodyStart = pos - along * halfLength;
        var bodyEnd = pos + along * halfLength;
        var outlineThickness = Mathf.Max(2, Mathf.RoundToInt(halfWidth * 2f + 2f));
        var fillThickness = Mathf.Max(1, Mathf.RoundToInt(halfWidth * 2f - 1f));

        if (trainable)
        {
            canvas.FillEllipse(pos, halfLength * 0.55f, halfLength * 0.55f, detailFill);
            canvas.DrawLine(pos, pos, Mathf.Max(1, Mathf.RoundToInt(halfLength * 1.1f)), outline);
            canvas.FillEllipse(pos, halfLength * 0.36f, halfLength * 0.36f, detailFill);
        }

        canvas.DrawLine(bodyStart, bodyEnd, outlineThickness, outline);
        canvas.DrawLine(bodyStart, bodyEnd, fillThickness, fill);

        var tubes = Mathf.Clamp(barrels, 1, 4);
        for (var i = 0; i < tubes; i++)
        {
            var spread = tubes == 1 ? 0f : Mathf.Lerp(-halfWidth, halfWidth, i / (float)(tubes - 1));
            var start = pos + perp * spread - along * halfLength * 0.7f;
            var end = pos + perp * spread + along * halfLength * 0.7f;
            canvas.DrawLine(start, end, 1, outline);
        }

        if (trainable)
        {
            canvas.FillEllipse(pos, halfWidth * 1.3f, halfWidth * 1.3f, detailFill);
            canvas.DrawLine(pos, pos + along * (halfLength + 3f), 1, outline);
        }
    }

    static Vector2 GetBarrelDirection(MountLocation location)
    {
        return location switch
        {
            MountLocation.PortForward => new Vector2(0.8f, -0.55f).normalized,
            MountLocation.Forward => Vector2.right,
            MountLocation.StarboardForward => new Vector2(0.8f, 0.55f).normalized,
            MountLocation.PortMidship => Vector2.up,
            MountLocation.Midship => Vector2.right,
            MountLocation.StarboardMidship => Vector2.down,
            MountLocation.PortAfter => new Vector2(-0.8f, -0.55f).normalized,
            MountLocation.After => Vector2.left,
            MountLocation.StarboardAfter => new Vector2(-0.8f, 0.55f).normalized,
            _ => Vector2.right
        };
    }

    static Vector2 ResolveMountDirection(MountLocationRecord record)
    {
        var avgAngle = ResolvePreferredArcAngle(record);
        if (avgAngle.HasValue)
        {
            return AngleDegToScreenVector(avgAngle.Value);
        }

        return GetBarrelDirection(record.mountLocation);
    }

    static float? ResolvePreferredArcAngle(MountLocationRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return null;

        var vectors = new List<Vector2>();
        foreach (var arc in record.mountArcs)
        {
            var midDeg = MeasureUtils.NormalizeAngle(arc.startDeg + arc.CoverageDeg / 2f);
            vectors.Add(AngleDegToScreenVector(midDeg));
        }

        if (vectors.Count == 0)
            return null;

        var sum = Vector2.zero;
        foreach (var v in vectors)
        {
            sum += v;
        }

        if (sum.sqrMagnitude < 0.0001f)
            return null;

        return ScreenVectorToAngleDeg(sum.normalized);
    }

    static Vector2 AngleDegToScreenVector(float angleDeg)
    {
        var rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    static float ScreenVectorToAngleDeg(Vector2 vector)
    {
        var rad = Mathf.Atan2(vector.y, vector.x);
        return MeasureUtils.NormalizeAngle(rad * Mathf.Rad2Deg);
    }

    static float NormalizeForeAftX(float normalizedX) => 1f - normalizedX;

    static float EstimateVisualPadding(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var maxBatteryGlyph = (shipClass.batteryRecords ?? new List<BatteryRecord>())
            .Select(b => EvaluateBatteryGlyphSize(b.shellSizeInch))
            .DefaultIfEmpty(6f)
            .Max() * settings.weaponScale;
        var maxTorpedoGlyph = (shipClass.torpedoSector?.mountLocationRecords ?? new List<MountLocationRecord>())
            .Select(r => Mathf.Clamp(5f + r.barrels * 0.7f, 5f, 11f))
            .DefaultIfEmpty(5f)
            .Max() * settings.weaponScale;

        var weaponPadding = Mathf.Max(maxBatteryGlyph * 1.2f, maxTorpedoGlyph * 1.15f);
        return Mathf.Max(settings.hullPadding + weaponPadding, settings.hullPadding + settings.lineWidth * 2f + 6f);
    }

    static float SampleHullHalfBreadth(float[] topEdge, float[] bottomEdge, int x)
    {
        x = Mathf.Clamp(x, 0, topEdge.Length - 1);
        if (float.IsNaN(topEdge[x]))
            return 0;
        return (bottomEdge[x] - topEdge[x]) / 2f;
    }

    static float EvaluateHalfBreadth(float t, HullProfile profile, ShipClassPlaceholderImageRenderSettings settings)
    {
        t = Mathf.Clamp01(t);
        // Use a simple 30/40/30 placeholder hull: sinusoidal bow entry, parallel midbody,
        // sinusoidal stern run. This keeps zero width at both ends and a flat max beam amidships.
        const float endSection = 0.3f;
        const float middleEnd = 0.7f;

        if (t < endSection)
        {
            var localT = t / endSection;
            return profile.maxBeamScale * Mathf.Sin(localT * Mathf.PI * 0.5f);
        }

        if (t <= middleEnd)
            return profile.maxBeamScale;

        var sternLocalT = (1f - t) / endSection;
        return profile.maxBeamScale * Mathf.Sin(sternLocalT * Mathf.PI * 0.5f);
    }

    static float SmootherStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static HullProfile GetHullProfile(ShipType shipType)
    {
        if (HullProfiles.TryGetValue(shipType, out var profile))
            return profile;
        return HullProfiles[ShipType.NotSpecified];
    }

    static int ResolveFunnelCount(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        return settings.funnelCountMode switch
        {
            PlaceholderFunnelCountMode.Zero => 0,
            PlaceholderFunnelCountMode.One => 1,
            PlaceholderFunnelCountMode.Two => 2,
            PlaceholderFunnelCountMode.Three => 3,
            _ => InferFunnelCount(shipClass)
        };
    }

    static int InferFunnelCount(ShipClass shipClass)
    {
        if (shipClass.type == ShipType.Transport || shipClass.type == ShipType.Repair)
            return shipClass.displacementTons > 7000 ? 2 : 1;
        if (shipClass.type == ShipType.TorpedoBoat)
            return shipClass.speedKnots >= 24 ? 2 : 1;
        if (shipClass.type == ShipType.Destroyer)
            return shipClass.speedKnots >= 27 ? 3 : 2;
        if (shipClass.type == ShipType.Battleship || shipClass.type == ShipType.ArmoredCruiser)
            return shipClass.displacementTons >= 9000 ? 2 : 1;
        // if (shipClass.type == ShipType.Cruiser || shipClass.type == ShipType.LightCruiser || shipClass.type == ShipType.ArmedMerchantCruiser)
        if (shipClass.type == ShipType.LightCruiser || shipClass.type == ShipType.ArmedMerchantCruiser)
            return shipClass.speedKnots >= 21 ? 2 : 1;
        return shipClass.displacementTons >= 6000 ? 2 : 1;
    }

    static RenderPalette GetPalette(ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        return variant switch
        {
            RenderVariant.TopJpg => new RenderPalette
            {
                background = new Color32(255, 255, 255, 255),
                hullFill = new Color32(255, 255, 255, 255),
                hullOutline = new Color32(28, 28, 28, 255),
                interiorLine = new Color32(0, 0, 0, 255),
                detailFill = new Color32(255, 255, 255, 255),
                mountFill = new Color32(36, 36, 36, 255),
                rapidFire = new Color32(0, 0, 0, 255),
            },
            RenderVariant.IconPng => new RenderPalette
            {
                background = new Color32(0, 0, 0, 0),
                hullFill = new Color32(255, 255, 255, 255),
                hullOutline = new Color32(0, 0, 0, 255),
                interiorLine = new Color32(0, 0, 0, 255),
                detailFill = new Color32(255, 255, 255, 255),
                mountFill = new Color32(0, 0, 0, 255),
                rapidFire = new Color32(0, 0, 0, 255),
            },
            _ => new RenderPalette
            {
                background = new Color32(0, 0, 0, 0),
                hullFill = new Color32(255, 255, 255, 255),
                hullOutline = new Color32(0, 0, 0, 255),
                interiorLine = new Color32(0, 0, 0, 230),
                detailFill = new Color32(255, 255, 255, 220),
                mountFill = new Color32(0, 0, 0, 255),
                rapidFire = new Color32(0, 0, 0, 220),
            }
        };
    }
}
