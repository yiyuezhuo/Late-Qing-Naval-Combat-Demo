using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public ShipClass shipClass;

    [CreateProperty] public int canvasWidth { get; set; } = 1095;
    [CreateProperty] public int canvasHeight { get; set; } = 211;
    [CreateProperty] public int hullPadding { get; set; } = 18;
    [CreateProperty] public int lineWidth { get; set; } = 3;
    [CreateProperty] public int fillAlpha { get; set; } = 150;
    [CreateProperty] public int deckInsetAmount { get; set; } = 10;
    [CreateProperty] public float superstructureHeightScale { get; set; } = 1f;
    [CreateProperty] public int funnelCountModeValue { get; set; } = (int)PlaceholderFunnelCountMode.Auto;
    [CreateProperty] public float funnelSpacingBias { get; set; }
    [CreateProperty] public float bowSharpness { get; set; } = 1.15f;
    [CreateProperty] public float sternFullness { get; set; } = 1.05f;

    public Texture2D previewTexture { get; private set; }
    public Texture2D topTexture { get; private set; }
    public Texture2D iconTexture { get; private set; }

    public string statusText { get; private set; } = "Adjust parameters and click Generate.";
    public bool hasGenerated => previewTexture != null && topTexture != null && iconTexture != null;

    public bool TryGenerate()
    {
        if (shipClass == null)
        {
            statusText = "No ship class is selected.";
            DisposeTextures();
            return false;
        }

        if (shipClass.type == ShipType.LandBattery)
        {
            statusText = "Land Battery does not support placeholder ship images.";
            DisposeTextures();
            return false;
        }

        if (shipClass.lengthFoot <= 0 || shipClass.beamFoot <= 0)
        {
            statusText = "Ship Class lengthFoot and beamFoot must both be greater than 0.";
            DisposeTextures();
            return false;
        }

        DisposeTextures();

        var settings = BuildSettings();
        previewTexture = ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.Preview);
        topTexture = ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.TopJpg);
        iconTexture = ShipClassPlaceholderImageRenderer.Render(shipClass, settings, ShipClassPlaceholderImageRenderer.RenderVariant.IconPng);

        statusText = $"Generated placeholder silhouette for {shipClass.name.GetMergedName()} ({settings.canvasWidth}x{settings.canvasHeight}).";
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
            fillAlpha = Mathf.Clamp(fillAlpha, 30, 255),
            deckInsetAmount = Mathf.Clamp(deckInsetAmount, 2, 80),
            superstructureHeightScale = Mathf.Clamp(superstructureHeightScale, 0.4f, 2.5f),
            funnelCountMode = (PlaceholderFunnelCountMode)Mathf.Clamp(funnelCountModeValue, 0, (int)PlaceholderFunnelCountMode.Three),
            funnelSpacingBias = Mathf.Clamp(funnelSpacingBias, -0.35f, 0.35f),
            bowSharpness = Mathf.Clamp(bowSharpness, 0.45f, 2.5f),
            sternFullness = Mathf.Clamp(sternFullness, 0.45f, 2.5f),
        };
    }

    string GetDefaultFileName(string suffix)
    {
        var baseName = shipClass?.name?.GetMergedNamePure();
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "ShipClass";

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }

        return baseName + suffix;
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

public struct ShipClassPlaceholderImageRenderSettings
{
    public int canvasWidth;
    public int canvasHeight;
    public int hullPadding;
    public int lineWidth;
    public int fillAlpha;
    public int deckInsetAmount;
    public float superstructureHeightScale;
    public PlaceholderFunnelCountMode funnelCountMode;
    public float funnelSpacingBias;
    public float bowSharpness;
    public float sternFullness;
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
        { ShipType.ArmoredCruiser, new HullProfile { bowSection = 0.23f, sternSection = 0.70f, maxBeamScale = 0.95f, sternTipScale = 0.16f, bridgeCenter = 0.32f, superstructureLength = 0.17f, deckhouseCenter = 0.72f } },
        { ShipType.Cruiser, new HullProfile { bowSection = 0.26f, sternSection = 0.68f, maxBeamScale = 0.88f, sternTipScale = 0.12f, bridgeCenter = 0.33f, superstructureLength = 0.15f, deckhouseCenter = 0.70f } },
        { ShipType.LightCruiser, new HullProfile { bowSection = 0.27f, sternSection = 0.67f, maxBeamScale = 0.84f, sternTipScale = 0.10f, bridgeCenter = 0.34f, superstructureLength = 0.14f, deckhouseCenter = 0.70f } },
        { ShipType.Destroyer, new HullProfile { bowSection = 0.30f, sternSection = 0.63f, maxBeamScale = 0.70f, sternTipScale = 0.08f, bridgeCenter = 0.36f, superstructureLength = 0.10f, deckhouseCenter = 0.67f } },
        { ShipType.TorpedoBoat, new HullProfile { bowSection = 0.31f, sternSection = 0.62f, maxBeamScale = 0.62f, sternTipScale = 0.07f, bridgeCenter = 0.38f, superstructureLength = 0.08f, deckhouseCenter = 0.64f } },
        { ShipType.PatrolGunboat, new HullProfile { bowSection = 0.24f, sternSection = 0.74f, maxBeamScale = 0.92f, sternTipScale = 0.18f, bridgeCenter = 0.34f, superstructureLength = 0.13f, deckhouseCenter = 0.72f } },
        { ShipType.Transport, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1.08f, sternTipScale = 0.24f, bridgeCenter = 0.30f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { ShipType.Repair, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1.05f, sternTipScale = 0.24f, bridgeCenter = 0.32f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { ShipType.ArmedMerchantCruiser, new HullProfile { bowSection = 0.24f, sternSection = 0.76f, maxBeamScale = 1.02f, sternTipScale = 0.22f, bridgeCenter = 0.31f, superstructureLength = 0.15f, deckhouseCenter = 0.73f } },
        { ShipType.NotSpecified, new HullProfile { bowSection = 0.25f, sternSection = 0.72f, maxBeamScale = 0.90f, sternTipScale = 0.14f, bridgeCenter = 0.33f, superstructureLength = 0.14f, deckhouseCenter = 0.71f } },
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

    static Rect CalculateShipRect(ShipClass shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var availableWidth = settings.canvasWidth - settings.hullPadding * 2f;
        var availableHeight = settings.canvasHeight - settings.hullPadding * 2f;
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
            var t = Mathf.InverseLerp(shipRect.xMin, shipRect.xMax, ix);
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
        var bridge = BuildCenteredRect(shipRect, profile.bridgeCenter, mainLength, mainWidth, centerY);
        DrawDeckBlock(canvas, bridge, palette.detailFill, palette.hullOutline, settings.lineWidth);

        var deckhouse = BuildCenteredRect(shipRect, profile.deckhouseCenter, mainLength * 0.55f, mainWidth * 0.7f, centerY);
        DrawDeckBlock(canvas, deckhouse, palette.detailFill, palette.hullOutline, Mathf.Max(1, settings.lineWidth - 1));

        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return;

        var baseXMin = shipRect.xMin + shipRect.width * (0.40f + settings.funnelSpacingBias * 0.2f);
        var baseXMax = shipRect.xMin + shipRect.width * (0.62f + settings.funnelSpacingBias * 0.2f);
        if (funnelCount == 1)
        {
            baseXMin = baseXMax = shipRect.xMin + shipRect.width * (0.48f + settings.funnelSpacingBias * 0.2f);
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
        foreach (var battery in shipClass.batteryRecords ?? new List<BatteryRecord>())
        {
            foreach (var record in battery.mountLocationRecords ?? new List<MountLocationRecord>())
            {
                var positions = ResolveMountPositions(record.mountLocation, record.mounts, shipRect, centerY, topEdge, bottomEdge);
                var glyphSize = Mathf.Clamp(3.5f + battery.shellSizeInch * 0.42f, 4f, 15f);
                foreach (var pos in positions)
                {
                    DrawGunMount(canvas, pos, record.mountLocation, record.barrels, glyphSize, settings.lineWidth, palette.mountFill, palette.hullOutline);
                }
            }
        }
    }

    static void DrawTorpedoMounts(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        foreach (var record in shipClass.torpedoSector?.mountLocationRecords ?? new List<MountLocationRecord>())
        {
            var positions = ResolveMountPositions(record.mountLocation, record.mounts, shipRect, centerY, topEdge, bottomEdge);
            var size = Mathf.Clamp(5f + record.barrels * 0.7f, 5f, 11f);
            foreach (var pos in positions)
            {
                DrawTorpedoMount(canvas, pos, record.mountLocation, record.barrels, size, palette.mountFill, palette.hullOutline);
            }
        }
    }

    static void DrawRapidFireDetails(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var rapidFireRecords = shipClass.rapidFireBatteryRecords ?? new List<RapidFireBatteryRecord>();
        var portBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelPort ?? new List<int>()).FirstOrDefault());
        var starboardBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelStarboard ?? new List<int>()).FirstOrDefault());
        DrawRapidFireSide(canvas, shipRect, centerY, topEdge, bottomEdge, portBarrels, false, palette.rapidFire);
        DrawRapidFireSide(canvas, shipRect, centerY, topEdge, bottomEdge, starboardBarrels, true, palette.rapidFire);
    }

    static void DrawRapidFireSide(PixelCanvas canvas, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge, int count, bool starboard, Color32 color)
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
            canvas.DrawLine(new Vector2(x - 2f, y), new Vector2(x + 2f, y + direction * 3f), 1, color);
        }
    }

    static void DrawMasts(PixelCanvas canvas, ShipClass shipClass, Rect shipRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var mastXs = new List<float> { shipRect.xMin + shipRect.width * 0.22f };
        if (shipClass.type != ShipType.TorpedoBoat)
        {
            mastXs.Add(shipRect.xMin + shipRect.width * 0.82f);
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

    static List<Vector2> ResolveMountPositions(MountLocation location, int mountCount, Rect shipRect, float centerY, float[] topEdge, float[] bottomEdge)
    {
        var anchors = GetAnchor(location);
        var spread = mountCount <= 1 ? 0f : Mathf.Min(shipRect.width * 0.05f, 22f);
        var positions = new List<Vector2>();
        for (var i = 0; i < Mathf.Max(1, mountCount); i++)
        {
            var offsetT = mountCount <= 1 ? 0f : (i - (mountCount - 1) / 2f);
            var x = shipRect.xMin + shipRect.width * anchors.x + offsetT * spread;
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, Mathf.RoundToInt(x));
            var y = centerY + halfBreadth * anchors.y;
            positions.Add(new Vector2(x, y));
        }
        return positions;
    }

    static Vector2 GetAnchor(MountLocation location)
    {
        return location switch
        {
            MountLocation.PortForward => new Vector2(0.23f, -0.52f),
            MountLocation.Forward => new Vector2(0.18f, 0f),
            MountLocation.StarboardForward => new Vector2(0.23f, 0.52f),
            MountLocation.PortMidship => new Vector2(0.50f, -0.58f),
            MountLocation.Midship => new Vector2(0.50f, 0f),
            MountLocation.StarboardMidship => new Vector2(0.50f, 0.58f),
            MountLocation.PortAfter => new Vector2(0.77f, -0.52f),
            MountLocation.After => new Vector2(0.82f, 0f),
            MountLocation.StarboardAfter => new Vector2(0.77f, 0.52f),
            _ => new Vector2(0.50f, 0f)
        };
    }

    static void DrawGunMount(PixelCanvas canvas, Vector2 pos, MountLocation location, int barrels, float size, int lineWidth, Color32 fill, Color32 outline)
    {
        var radiusX = size;
        var radiusY = Mathf.Max(3f, size * 0.62f);
        canvas.FillEllipse(pos, radiusX, radiusY, fill);
        canvas.DrawRectOutline(new Rect(pos.x - radiusX, pos.y - radiusY, radiusX * 2f, radiusY * 2f), Mathf.Max(1, lineWidth - 1), outline);

        var direction = GetBarrelDirection(location);
        var perpendicular = new Vector2(-direction.y, direction.x);
        var lines = Mathf.Clamp(barrels, 1, 4);
        for (var i = 0; i < lines; i++)
        {
            var spread = lines == 1 ? 0f : Mathf.Lerp(-radiusY * 0.65f, radiusY * 0.65f, i / (float)(lines - 1));
            var from = pos + perpendicular * spread;
            var to = from + direction * (size + 4f);
            canvas.DrawLine(from, to, 1, outline);
        }
    }

    static void DrawTorpedoMount(PixelCanvas canvas, Vector2 pos, MountLocation location, int barrels, float size, Color32 fill, Color32 outline)
    {
        var along = GetBarrelDirection(location);
        var perp = new Vector2(-along.y, along.x);
        var halfLength = size;
        var halfWidth = Mathf.Max(2f, size * 0.28f);
        var rect = new Rect(pos.x - halfLength, pos.y - halfWidth, halfLength * 2f, halfWidth * 2f);
        canvas.FillRect(new RectInt(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)), fill);
        canvas.DrawRectOutline(rect, 1, outline);

        var tubes = Mathf.Clamp(barrels, 1, 4);
        for (var i = 0; i < tubes; i++)
        {
            var spread = tubes == 1 ? 0f : Mathf.Lerp(-halfWidth, halfWidth, i / (float)(tubes - 1));
            var start = pos + perp * spread - along * halfLength * 0.7f;
            var end = pos + perp * spread + along * halfLength * 0.7f;
            canvas.DrawLine(start, end, 1, outline);
        }
    }

    static Vector2 GetBarrelDirection(MountLocation location)
    {
        return location switch
        {
            MountLocation.PortForward => new Vector2(-0.8f, -0.55f).normalized,
            MountLocation.Forward => Vector2.left,
            MountLocation.StarboardForward => new Vector2(-0.8f, 0.55f).normalized,
            MountLocation.PortMidship => Vector2.up,
            MountLocation.Midship => Vector2.right,
            MountLocation.StarboardMidship => Vector2.down,
            MountLocation.PortAfter => new Vector2(0.8f, -0.55f).normalized,
            MountLocation.After => Vector2.right,
            MountLocation.StarboardAfter => new Vector2(0.8f, 0.55f).normalized,
            _ => Vector2.right
        };
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
        if (t < profile.bowSection)
        {
            var bowT = t / Mathf.Max(0.001f, profile.bowSection);
            var curve = Mathf.Pow(Mathf.Sin(bowT * Mathf.PI * 0.5f), settings.bowSharpness);
            return profile.maxBeamScale * curve;
        }

        if (t > profile.sternSection)
        {
            var sternT = (t - profile.sternSection) / Mathf.Max(0.001f, 1f - profile.sternSection);
            var curve = Mathf.Pow(1f - sternT, settings.sternFullness);
            return Mathf.Lerp(profile.sternTipScale, profile.maxBeamScale, curve);
        }

        var midT = Mathf.InverseLerp(profile.bowSection, profile.sternSection, t);
        var fullness = 1f - 0.03f * Mathf.Cos(midT * Mathf.PI);
        return profile.maxBeamScale * fullness;
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
        if (shipClass.type == ShipType.Cruiser || shipClass.type == ShipType.LightCruiser || shipClass.type == ShipType.ArmedMerchantCruiser)
            return shipClass.speedKnots >= 21 ? 2 : 1;
        return shipClass.displacementTons >= 6000 ? 2 : 1;
    }

    static RenderPalette GetPalette(ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        return variant switch
        {
            RenderVariant.TopJpg => new RenderPalette
            {
                background = new Color32(244, 241, 233, 255),
                hullFill = new Color32(145, 145, 145, 255),
                hullOutline = new Color32(28, 28, 28, 255),
                interiorLine = new Color32(72, 72, 72, 255),
                detailFill = new Color32(110, 110, 110, 255),
                mountFill = new Color32(36, 36, 36, 255),
                rapidFire = new Color32(55, 55, 55, 255),
            },
            RenderVariant.IconPng => new RenderPalette
            {
                background = new Color32(0, 0, 0, 0),
                hullFill = new Color32(48, 48, 48, 255),
                hullOutline = new Color32(0, 0, 0, 255),
                interiorLine = new Color32(80, 80, 80, 255),
                detailFill = new Color32(32, 32, 32, 255),
                mountFill = new Color32(0, 0, 0, 255),
                rapidFire = new Color32(24, 24, 24, 255),
            },
            _ => new RenderPalette
            {
                background = new Color32(0, 0, 0, 0),
                hullFill = new Color32(35, 35, 35, (byte)settings.fillAlpha),
                hullOutline = new Color32(0, 0, 0, 255),
                interiorLine = new Color32(90, 90, 90, 230),
                detailFill = new Color32(55, 55, 55, 220),
                mountFill = new Color32(0, 0, 0, 255),
                rapidFire = new Color32(40, 40, 40, 220),
            }
        };
    }
}
