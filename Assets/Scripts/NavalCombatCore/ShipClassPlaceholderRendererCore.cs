using System;
using System.Collections.Generic;
using System.Linq;

public enum PlaceholderFunnelCountMode
{
    Auto,
    Zero,
    One,
    Two,
    Three
}

public enum PlaceholderShipType
{
    NotSpecified,
    Battleship,
    Battlecruiser,
    LightCruiser,
    ArmoredCruiser,
    Destroyer,
    PatrolGunboat,
    TorpedoBoat,
    ArmedMerchantCruiser,
    Transport,
    Repair,
    LandBattery
}

public enum PlaceholderMountLocation
{
    NotSpecified,
    PortForward,
    Forward,
    StarboardForward,
    PortMidship,
    Midship,
    StarboardMidship,
    PortAfter,
    After,
    StarboardAfter,
}

public sealed class ShipClassPlaceholderRenderInput
{
    public string name;
    public PlaceholderShipType type;
    public float displacementTons;
    public float lengthFoot;
    public float beamFoot;
    public float speedKnots;
    public List<PlaceholderBatteryRenderRecord> batteryRecords = new();
    public List<PlaceholderMountRenderRecord> torpedoMountLocationRecords = new();
    public List<PlaceholderRapidFireRenderRecord> rapidFireBatteryRecords = new();
}

public sealed class PlaceholderBatteryRenderRecord
{
    public float shellSizeInch;
    public List<PlaceholderMountRenderRecord> mountLocationRecords = new();
}

public sealed class PlaceholderMountRenderRecord
{
    public PlaceholderMountLocation mountLocation;
    public int barrels;
    public int mounts;
    public List<PlaceholderMountArcRenderRecord> mountArcs = new();
    public bool trainable;
}

public sealed class PlaceholderMountArcRenderRecord
{
    public float startDeg;
    public float CoverageDeg;
    public bool isCrossDeckFire;
}

public sealed class PlaceholderRapidFireRenderRecord
{
    public List<int> barrelsLevelPort = new();
    public List<int> barrelsLevelStarboard = new();
}

public readonly struct RgbaImage
{
    public readonly int width;
    public readonly int height;
    public readonly byte[] pixels;

    public RgbaImage(int width, int height, byte[] pixels)
    {
        this.width = width;
        this.height = height;
        this.pixels = pixels ?? Array.Empty<byte>();
    }
}

public readonly struct RgbaColor
{
    public readonly byte r;
    public readonly byte g;
    public readonly byte b;
    public readonly byte a;

    public RgbaColor(byte r, byte g, byte b, byte a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }
}

readonly struct PixelVector2
{
    public readonly float x;
    public readonly float y;

    public PixelVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public float sqrMagnitude => x * x + y * y;

    public PixelVector2 normalized
    {
        get
        {
            var magnitude = MathF.Sqrt(sqrMagnitude);
            return magnitude <= 0.0001f ? zero : new PixelVector2(x / magnitude, y / magnitude);
        }
    }

    public static PixelVector2 zero => new(0f, 0f);
    public static PixelVector2 right => new(1f, 0f);
    public static PixelVector2 left => new(-1f, 0f);
    public static PixelVector2 up => new(0f, 1f);
    public static PixelVector2 down => new(0f, -1f);

    public static PixelVector2 operator +(PixelVector2 lhs, PixelVector2 rhs) => new(lhs.x + rhs.x, lhs.y + rhs.y);
    public static PixelVector2 operator -(PixelVector2 lhs, PixelVector2 rhs) => new(lhs.x - rhs.x, lhs.y - rhs.y);
    public static PixelVector2 operator *(PixelVector2 lhs, float rhs) => new(lhs.x * rhs, lhs.y * rhs);
    public static PixelVector2 operator *(float lhs, PixelVector2 rhs) => rhs * lhs;

    public static float Distance(PixelVector2 lhs, PixelVector2 rhs)
    {
        var delta = lhs - rhs;
        return MathF.Sqrt(delta.sqrMagnitude);
    }

    public static PixelVector2 Lerp(PixelVector2 lhs, PixelVector2 rhs, float t)
    {
        t = PixelMath.Clamp01(t);
        return new PixelVector2(PixelMath.Lerp(lhs.x, rhs.x, t), PixelMath.Lerp(lhs.y, rhs.y, t));
    }
}

readonly struct PixelRect
{
    readonly float x;
    readonly float y;

    public readonly float width;
    public readonly float height;

    public PixelRect(float x, float y, float width, float height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public float xMin => x;
    public float xMax => x + width;
    public float yMin => y;
    public float yMax => y + height;
    public PixelVector2 center => new(x + width / 2f, y + height / 2f);
    public PixelVector2 size => new(width, height);

    public bool Overlaps(PixelRect other)
    {
        return other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;
    }
}

readonly struct PixelRectInt
{
    readonly int x;
    readonly int y;
    readonly int width;
    readonly int height;

    public PixelRectInt(int x, int y, int width, int height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public int xMin => x;
    public int xMax => x + width;
    public int yMin => y;
    public int yMax => y + height;
}

static class PixelMath
{
    public const float PI = MathF.PI;
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    public static float Clamp(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);
    public static float Clamp01(float value) => Clamp(value, 0f, 1f);
    public static int Max(int lhs, int rhs) => Math.Max(lhs, rhs);
    public static float Max(float lhs, float rhs) => MathF.Max(lhs, rhs);
    public static int Min(int lhs, int rhs) => Math.Min(lhs, rhs);
    public static float Min(float lhs, float rhs) => MathF.Min(lhs, rhs);
    public static int FloorToInt(float value) => (int)MathF.Floor(value);
    public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
    public static int RoundToInt(float value) => (int)MathF.Round(value);
    public static float Sin(float value) => MathF.Sin(value);
    public static float Cos(float value) => MathF.Cos(value);
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);
    public static float Pow(float value, float power) => MathF.Pow(value, power);
    public static float Abs(float value) => MathF.Abs(value);
    public static float Sign(float value) => value >= 0f ? 1f : -1f;

    public static float Lerp(float lhs, float rhs, float t)
    {
        t = Clamp01(t);
        return lhs + (rhs - lhs) * t;
    }

    public static float InverseLerp(float lhs, float rhs, float value)
    {
        if (MathF.Abs(rhs - lhs) <= 0.000001f)
            return 0f;
        return Clamp01((value - lhs) / (rhs - lhs));
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

public static class ShipClassPlaceholderImageRendererCore
{
    public enum RenderVariant
    {
        Preview,
        TopJpg,
        IconPng,
    }

    struct RenderPalette
    {
        public RgbaColor background;
        public RgbaColor hullFill;
        public RgbaColor hullOutline;
        public RgbaColor interiorLine;
        public RgbaColor detailFill;
        public RgbaColor mountFill;
        public RgbaColor rapidFire;
    }

    sealed class PixelCanvas
    {
        readonly RgbaColor[] pixels;

        public int width { get; }
        public int height { get; }

        public PixelCanvas(int width, int height, RgbaColor background)
        {
            this.width = width;
            this.height = height;
            pixels = Enumerable.Repeat(background, width * height).ToArray();
        }

        public void SetPixel(int x, int y, RgbaColor color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            pixels[y * width + x] = Blend(pixels[y * width + x], color);
        }

        public void FillPixelRect(PixelRectInt rect, RgbaColor color)
        {
            var xMin = PixelMath.Max(0, rect.xMin);
            var xMax = PixelMath.Min(width, rect.xMax);
            var yMin = PixelMath.Max(0, rect.yMin);
            var yMax = PixelMath.Min(height, rect.yMax);
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    pixels[y * width + x] = Blend(pixels[y * width + x], color);
                }
            }
        }

        public void FillEllipse(PixelVector2 center, float radiusX, float radiusY, RgbaColor color)
        {
            var xMin = PixelMath.FloorToInt(center.x - radiusX);
            var xMax = PixelMath.CeilToInt(center.x + radiusX);
            var yMin = PixelMath.FloorToInt(center.y - radiusY);
            var yMax = PixelMath.CeilToInt(center.y + radiusY);
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

        public void DrawLine(PixelVector2 from, PixelVector2 to, int thickness, RgbaColor color)
        {
            var steps = PixelMath.CeilToInt(PixelVector2.Distance(from, to) * 1.5f);
            steps = PixelMath.Max(steps, 1);
            var radius = PixelMath.Max(1, thickness) / 2f;
            for (var i = 0; i <= steps; i++)
            {
                var p = PixelVector2.Lerp(from, to, i / (float)steps);
                FillEllipse(p, radius, radius, color);
            }
        }

        public void DrawPixelRectOutline(PixelRect rect, int thickness, RgbaColor color)
        {
            DrawLine(new PixelVector2(rect.xMin, rect.yMin), new PixelVector2(rect.xMax, rect.yMin), thickness, color);
            DrawLine(new PixelVector2(rect.xMax, rect.yMin), new PixelVector2(rect.xMax, rect.yMax), thickness, color);
            DrawLine(new PixelVector2(rect.xMax, rect.yMax), new PixelVector2(rect.xMin, rect.yMax), thickness, color);
            DrawLine(new PixelVector2(rect.xMin, rect.yMax), new PixelVector2(rect.xMin, rect.yMin), thickness, color);
        }

        public RgbaImage ToImage()
        {
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                bytes[offset] = pixels[i].r;
                bytes[offset + 1] = pixels[i].g;
                bytes[offset + 2] = pixels[i].b;
                bytes[offset + 3] = pixels[i].a;
            }

            return new RgbaImage(width, height, bytes);
        }

        static RgbaColor Blend(RgbaColor dst, RgbaColor src)
        {
            if (src.a == 255)
                return src;
            if (src.a == 0)
                return dst;

            var srcA = src.a / 255f;
            var dstA = dst.a / 255f;
            var outA = srcA + dstA * (1 - srcA);
            if (outA <= 0)
                return new RgbaColor(0, 0, 0, 0);

            byte BlendChannel(byte s, byte d)
            {
                var result = (s * srcA + d * dstA * (1 - srcA)) / outA;
                return (byte)PixelMath.Clamp(PixelMath.RoundToInt(result), 0, 255);
            }

            return new RgbaColor(
                BlendChannel(src.r, dst.r),
                BlendChannel(src.g, dst.g),
                BlendChannel(src.b, dst.b),
                (byte)PixelMath.Clamp(PixelMath.RoundToInt(outA * 255f), 0, 255)
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

    static readonly Dictionary<PlaceholderShipType, HullProfile> HullProfiles = new()
    {
        { PlaceholderShipType.Battleship, new HullProfile { bowSection = 0.22f, sternSection = 0.70f, maxBeamScale = 1f, sternTipScale = 0.18f, bridgeCenter = 0.31f, superstructureLength = 0.19f, deckhouseCenter = 0.73f } },
        { PlaceholderShipType.Battlecruiser, new HullProfile { bowSection = 0.22f, sternSection = 0.70f, maxBeamScale = 1f, sternTipScale = 0.18f, bridgeCenter = 0.31f, superstructureLength = 0.19f, deckhouseCenter = 0.73f } }, // TODO: It's copied from battleship now, use a more specific value. 
        { PlaceholderShipType.ArmoredCruiser, new HullProfile { bowSection = 0.23f, sternSection = 0.70f, maxBeamScale = 1f, sternTipScale = 0.16f, bridgeCenter = 0.32f, superstructureLength = 0.17f, deckhouseCenter = 0.72f } },
        // { PlaceholderShipType.Cruiser, new HullProfile { bowSection = 0.26f, sternSection = 0.68f, maxBeamScale = 1f, sternTipScale = 0.12f, bridgeCenter = 0.33f, superstructureLength = 0.15f, deckhouseCenter = 0.70f } },
        { PlaceholderShipType.LightCruiser, new HullProfile { bowSection = 0.27f, sternSection = 0.67f, maxBeamScale = 1f, sternTipScale = 0.10f, bridgeCenter = 0.34f, superstructureLength = 0.14f, deckhouseCenter = 0.70f } },
        { PlaceholderShipType.Destroyer, new HullProfile { bowSection = 0.30f, sternSection = 0.63f, maxBeamScale = 1f, sternTipScale = 0.08f, bridgeCenter = 0.36f, superstructureLength = 0.10f, deckhouseCenter = 0.67f } },
        { PlaceholderShipType.TorpedoBoat, new HullProfile { bowSection = 0.31f, sternSection = 0.62f, maxBeamScale = 1f, sternTipScale = 0.07f, bridgeCenter = 0.38f, superstructureLength = 0.08f, deckhouseCenter = 0.64f } },
        { PlaceholderShipType.PatrolGunboat, new HullProfile { bowSection = 0.24f, sternSection = 0.74f, maxBeamScale = 1f, sternTipScale = 0.18f, bridgeCenter = 0.34f, superstructureLength = 0.13f, deckhouseCenter = 0.72f } },
        { PlaceholderShipType.Transport, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.24f, bridgeCenter = 0.30f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { PlaceholderShipType.Repair, new HullProfile { bowSection = 0.22f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.24f, bridgeCenter = 0.32f, superstructureLength = 0.16f, deckhouseCenter = 0.74f } },
        { PlaceholderShipType.ArmedMerchantCruiser, new HullProfile { bowSection = 0.24f, sternSection = 0.76f, maxBeamScale = 1f, sternTipScale = 0.22f, bridgeCenter = 0.31f, superstructureLength = 0.15f, deckhouseCenter = 0.73f } },
        { PlaceholderShipType.NotSpecified, new HullProfile { bowSection = 0.25f, sternSection = 0.72f, maxBeamScale = 1f, sternTipScale = 0.14f, bridgeCenter = 0.33f, superstructureLength = 0.14f, deckhouseCenter = 0.71f } },
    };

    public static RgbaImage Render(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        var palette = GetPalette(settings, variant);
        var canvas = new PixelCanvas(settings.canvasWidth, settings.canvasHeight, palette.background);

        var shipPixelRect = CalculateShipPixelRect(shipClass, settings);
        var centerY = shipPixelRect.center.y;
        var topEdge = new float[settings.canvasWidth];
        var bottomEdge = new float[settings.canvasWidth];
        for (var i = 0; i < topEdge.Length; i++)
        {
            topEdge[i] = float.NaN;
            bottomEdge[i] = float.NaN;
        }

        DrawHull(canvas, shipClass, shipPixelRect, settings, palette, topEdge, bottomEdge);
        DrawDeckLines(canvas, shipPixelRect, settings, palette, topEdge, bottomEdge);
        DrawSuperstructure(canvas, shipClass, shipPixelRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawBatteryMounts(canvas, shipClass, shipPixelRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawTorpedoMounts(canvas, shipClass, shipPixelRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawRapidFireDetails(canvas, shipClass, shipPixelRect, centerY, settings, palette, topEdge, bottomEdge);
        DrawMasts(canvas, shipClass, shipPixelRect, centerY, settings, palette, topEdge, bottomEdge);

        return canvas.ToImage();
    }

    public static int CalculateRecommendedCanvasWidth(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var ratio = PixelMath.Max(1.5f, shipClass.lengthFoot / PixelMath.Max(1f, shipClass.beamFoot));
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableHeight = PixelMath.Max(1f, settings.canvasHeight - visualPadding * 2f);
        var shipWidth = availableHeight * ratio;
        var canvasWidth = shipWidth + visualPadding * 2f;
        return PixelMath.CeilToInt(canvasWidth);
    }

    static PixelRect CalculateShipPixelRect(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var visualPadding = EstimateVisualPadding(shipClass, settings);
        var availableWidth = settings.canvasWidth - visualPadding * 2f;
        var availableHeight = settings.canvasHeight - visualPadding * 2f;
        var ratio = PixelMath.Max(1.5f, shipClass.lengthFoot / PixelMath.Max(1f, shipClass.beamFoot));

        var shipWidth = availableWidth;
        var shipHeight = shipWidth / ratio;
        if (shipHeight > availableHeight)
        {
            shipHeight = availableHeight;
            shipWidth = shipHeight * ratio;
        }

        var x = (settings.canvasWidth - shipWidth) / 2f;
        var y = (settings.canvasHeight - shipHeight) / 2f;
        return new PixelRect(x, y, shipWidth, shipHeight);
    }

    static void DrawHull(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        PixelVector2? prevTop = null;
        PixelVector2? prevBottom = null;

        for (var ix = PixelMath.FloorToInt(shipPixelRect.xMin); ix <= PixelMath.CeilToInt(shipPixelRect.xMax); ix++)
        {
            var t = 1f - PixelMath.InverseLerp(shipPixelRect.xMin, shipPixelRect.xMax, ix);
            var halfBreadth = EvaluateHalfBreadth(t, profile, settings) * shipPixelRect.height * 0.5f;
            var yTop = shipPixelRect.center.y - halfBreadth;
            var yBottom = shipPixelRect.center.y + halfBreadth;

            topEdge[PixelMath.Clamp(ix, 0, topEdge.Length - 1)] = yTop;
            bottomEdge[PixelMath.Clamp(ix, 0, bottomEdge.Length - 1)] = yBottom;

            for (var y = PixelMath.CeilToInt(yTop); y <= PixelMath.FloorToInt(yBottom); y++)
            {
                canvas.SetPixel(ix, y, palette.hullFill);
            }

            var top = new PixelVector2(ix, yTop);
            var bottom = new PixelVector2(ix, yBottom);
            if (prevTop.HasValue)
            {
                canvas.DrawLine(prevTop.Value, top, settings.lineWidth, palette.hullOutline);
                canvas.DrawLine(prevBottom.Value, bottom, settings.lineWidth, palette.hullOutline);
            }
            prevTop = top;
            prevBottom = bottom;
        }

        canvas.DrawLine(new PixelVector2(shipPixelRect.xMin, shipPixelRect.center.y), new PixelVector2(shipPixelRect.xMax, shipPixelRect.center.y), PixelMath.Max(1, settings.lineWidth - 1), palette.interiorLine);
    }

    static void DrawDeckLines(PixelCanvas canvas, PixelRect shipPixelRect, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        PixelVector2? prevInnerTop = null;
        PixelVector2? prevInnerBottom = null;
        var deckInset = settings.deckInsetAmount;
        for (var ix = PixelMath.FloorToInt(shipPixelRect.xMin); ix <= PixelMath.CeilToInt(shipPixelRect.xMax); ix++)
        {
            var clampedX = PixelMath.Clamp(ix, 0, topEdge.Length - 1);
            if (float.IsNaN(topEdge[clampedX]))
                continue;

            var yTop = topEdge[clampedX] + deckInset;
            var yBottom = bottomEdge[clampedX] - deckInset;
            if (yBottom <= yTop)
                continue;

            var innerTop = new PixelVector2(ix, yTop);
            var innerBottom = new PixelVector2(ix, yBottom);
            if (prevInnerTop.HasValue)
            {
                canvas.DrawLine(prevInnerTop.Value, innerTop, 1, palette.interiorLine);
                canvas.DrawLine(prevInnerBottom.Value, innerBottom, 1, palette.interiorLine);
            }
            prevInnerTop = innerTop;
            prevInnerBottom = innerBottom;
        }
    }

    static void DrawSuperstructure(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipPixelRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipPixelRect.height * 0.20f * settings.superstructureHeightScale;
        var bridge = BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY);
        DrawDeckBlock(canvas, bridge, palette.detailFill, palette.hullOutline, settings.lineWidth);

        var deckhouse = BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55f, mainWidth * 0.7f, centerY);
        DrawDeckBlock(canvas, deckhouse, palette.detailFill, palette.hullOutline, PixelMath.Max(1, settings.lineWidth - 1));

        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return;

        var baseXMin = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.62f + settings.funnelSpacingBias * 0.2f);
        var baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.40f + settings.funnelSpacingBias * 0.2f);
        if (funnelCount == 1)
        {
            baseXMin = baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.48f + settings.funnelSpacingBias * 0.2f);
        }

        for (var i = 0; i < funnelCount; i++)
        {
            var t = funnelCount == 1 ? 0.5f : i / (float)(funnelCount - 1);
            var x = PixelMath.Lerp(baseXMin, baseXMax, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var funnelHeight = PixelMath.Max(5f, halfBreadth * 0.9f * settings.superstructureHeightScale);
            var funnelWidth = PixelMath.Max(4f, shipPixelRect.width * 0.015f);
            var rect = new PixelRect(x - funnelWidth / 2f, centerY - funnelHeight / 2f, funnelWidth, funnelHeight);
            DrawDeckBlock(canvas, rect, palette.hullOutline, palette.hullOutline, 1);
        }
    }

    static void DrawBatteryMounts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var batteryVisualScale = EvaluateBatteryGlyphVisualScale(shipPixelRect);
        var groupedRecords = (shipClass.batteryRecords ?? new List<PlaceholderBatteryRenderRecord>())
            .SelectMany(battery => (battery.mountLocationRecords ?? new List<PlaceholderMountRenderRecord>())
                .Select(record => new
                {
                    battery,
                    record,
                    glyphSize = EvaluateBatteryGlyphSize(battery.shellSizeInch) * settings.weaponScale * 2.2f * batteryVisualScale,
                }))
            .GroupBy(entry => entry.record.mountLocation);

        foreach (var group in groupedRecords)
        {
            var totalMounts = group.Sum(entry => PixelMath.Max(1, entry.record.mounts));
            var layoutGlyphSize = group.Max(entry => entry.glyphSize);
            var allPositions = ResolveMountPositions(group.Key, totalMounts, layoutGlyphSize, shipPixelRect, centerY, topEdge, bottomEdge);
            var positionIndex = 0;
            var orderedEntries = group
                .OrderBy(entry => ResolveMountDirection(entry.record).x)
                .ThenBy(entry => ResolveMountDirection(entry.record).y)
                .ToList();

            foreach (var entry in orderedEntries)
            {
                var mountCount = PixelMath.Max(1, entry.record.mounts);
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

    static void DrawTorpedoMounts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var torpedoVisualScale = EvaluateTorpedoGlyphVisualScale(shipPixelRect);
        var deckObstacles = BuildSuperstructureObstacles(shipClass, shipPixelRect, centerY, settings, topEdge, bottomEdge);
        foreach (var record in shipClass.torpedoMountLocationRecords ?? new List<PlaceholderMountRenderRecord>())
        {
            var submerged = IsSubmergedTorpedoRecord(record);
            var baseSize = PixelMath.Clamp(5f + record.barrels * 0.7f, 5f, 11f) * settings.weaponScale * 2f * torpedoVisualScale;
            var size = submerged ? baseSize : baseSize * 2f;
            var direction = submerged
                ? GetSubmergedMountOutwardDirection(record.mountLocation, ResolveMountDirection(record))
                : ResolveMountDirection(record);
            var positions = submerged
                ? ResolveSubmergedTorpedoPositions(record, size, shipPixelRect, centerY, topEdge, bottomEdge, direction)
                : ResolveDeckTorpedoPositions(record.mountLocation, record.mounts, size, shipPixelRect, centerY, topEdge, bottomEdge, deckObstacles);
            foreach (var pos in positions)
            {
                DrawTorpedoMount(canvas, pos, direction, record.barrels, size, submerged ? false : record.trainable, palette.mountFill, palette.hullOutline, palette.detailFill);
            }
        }
    }

    static void DrawRapidFireDetails(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var rapidFireRecords = shipClass.rapidFireBatteryRecords ?? new List<PlaceholderRapidFireRenderRecord>();
        var portBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelPort ?? new List<int>()).FirstOrDefault());
        var starboardBarrels = rapidFireRecords.Sum(r => (r.barrelsLevelStarboard ?? new List<int>()).FirstOrDefault());
        DrawRapidFireSide(canvas, shipPixelRect, centerY, topEdge, bottomEdge, portBarrels, false, palette.rapidFire, settings.weaponScale);
        DrawRapidFireSide(canvas, shipPixelRect, centerY, topEdge, bottomEdge, starboardBarrels, true, palette.rapidFire, settings.weaponScale);
    }

    static void DrawRapidFireSide(PixelCanvas canvas, PixelRect shipPixelRect, float centerY, float[] topEdge, float[] bottomEdge, int count, bool starboard, RgbaColor color, float weaponScale)
    {
        if (count <= 0)
            return;

        var rendered = PixelMath.Clamp(count, 1, 12);
        for (var i = 0; i < rendered; i++)
        {
            var t = rendered == 1 ? 0.5f : i / (float)(rendered - 1);
            var x = PixelMath.Lerp(shipPixelRect.xMin + shipPixelRect.width * 0.24f, shipPixelRect.xMin + shipPixelRect.width * 0.76f, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var y = centerY + (starboard ? 1 : -1) * halfBreadth * 0.68f;
            var direction = starboard ? 1f : -1f;
            var halfSpan = 2f * weaponScale;
            var elevation = 3f * weaponScale;
            canvas.DrawLine(new PixelVector2(x - halfSpan, y), new PixelVector2(x + halfSpan, y + direction * elevation), 1, color);
        }
    }

    static void DrawMasts(PixelCanvas canvas, ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, RenderPalette palette, float[] topEdge, float[] bottomEdge)
    {
        var mastXs = new List<float> { shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.22f) };
        if (shipClass.type != PlaceholderShipType.TorpedoBoat)
        {
            mastXs.Add(shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.82f));
        }

        foreach (var x in mastXs)
        {
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var height = PixelMath.Max(6f, halfBreadth * 0.95f);
            canvas.DrawLine(new PixelVector2(x, centerY - height / 2f), new PixelVector2(x, centerY + height / 2f), 1, palette.interiorLine);
        }
    }

    static void DrawDeckBlock(PixelCanvas canvas, PixelRect rect, RgbaColor fill, RgbaColor outline, int lineWidth)
    {
        canvas.FillPixelRect(new PixelRectInt(PixelMath.RoundToInt(rect.xMin), PixelMath.RoundToInt(rect.yMin), PixelMath.RoundToInt(rect.width), PixelMath.RoundToInt(rect.height)), fill);
        canvas.DrawPixelRectOutline(rect, PixelMath.Max(1, lineWidth), outline);
    }

    static PixelRect BuildCenteredPixelRect(PixelRect shipPixelRect, float normalizedX, float length, float width, float centerY)
    {
        var centerX = shipPixelRect.xMin + shipPixelRect.width * normalizedX;
        return new PixelRect(centerX - length / 2f, centerY - width / 2f, length, width);
    }

    static List<PixelVector2> ResolveMountPositions(PlaceholderMountLocation location, int mountCount, float symbolSize, PixelRect shipPixelRect, float centerY, float[] topEdge, float[] bottomEdge)
    {
        var anchors = GetAnchor(location);
        var isSideLocation =
            location == PlaceholderMountLocation.PortForward ||
            location == PlaceholderMountLocation.StarboardForward ||
            location == PlaceholderMountLocation.PortMidship ||
            location == PlaceholderMountLocation.StarboardMidship ||
            location == PlaceholderMountLocation.PortAfter ||
            location == PlaceholderMountLocation.StarboardAfter;

        var baseSpread = isSideLocation
            ? PixelMath.Max(symbolSize * 2.4f, shipPixelRect.width * 0.06f)
            : PixelMath.Max(symbolSize * 1.35f, shipPixelRect.width * 0.032f);
        var spread = mountCount <= 1 ? 0f : PixelMath.Min(baseSpread, shipPixelRect.width * (isSideLocation ? 0.13f : 0.07f));
        var positions = new List<PixelVector2>();
        for (var i = 0; i < PixelMath.Max(1, mountCount); i++)
        {
            var offsetT = mountCount <= 1 ? 0f : (i - (mountCount - 1) / 2f);
            var x = shipPixelRect.xMin + shipPixelRect.width * anchors.x + offsetT * spread;
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var sideCompression = isSideLocation ? 0.76f : 1f;
            var y = centerY + halfBreadth * anchors.y * sideCompression;

            if (isSideLocation && mountCount > 1)
            {
                var stagger = PixelMath.Abs(offsetT) < 0.1f ? 0f : PixelMath.Sign(offsetT) * PixelMath.Min(symbolSize * 0.18f, halfBreadth * 0.08f);
                y += PixelMath.Sign(anchors.y) * stagger;
            }

            positions.Add(new PixelVector2(x, y));
        }
        return positions;
    }

    static List<PixelVector2> ResolveDeckTorpedoPositions(PlaceholderMountLocation location, int mountCount, float symbolSize, PixelRect shipPixelRect, float centerY, float[] topEdge, float[] bottomEdge, List<PixelRect> obstacles)
    {
        var positions = ResolveMountPositions(location, mountCount, symbolSize * 2.8f, shipPixelRect, centerY, topEdge, bottomEdge);
        if (obstacles == null || obstacles.Count == 0)
            return positions;

        var adjusted = new List<PixelVector2>(positions.Count);
        foreach (var pos in positions)
        {
            adjusted.Add(ResolveDeckMountAvoidance(pos, symbolSize, shipPixelRect, obstacles));
        }

        return adjusted;
    }

    static bool IsSubmergedTorpedoRecord(PlaceholderMountRenderRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return false;

        return record.mountArcs.All(arc => arc.CoverageDeg <= 30f);
    }

    static List<PixelVector2> ResolveSubmergedTorpedoPositions(PlaceholderMountRenderRecord record, float symbolSize, PixelRect shipPixelRect, float centerY, float[] topEdge, float[] bottomEdge, PixelVector2 direction)
    {
        var outward = GetSubmergedMountOutwardDirection(record.mountLocation, direction);
        var positions = new List<PixelVector2>();
        var count = PixelMath.Max(1, record.mounts);
        var alongHull = new PixelVector2(-outward.y, outward.x);
        var spacing = symbolSize * 0.9f;
        var exposure = symbolSize * 0.58f;
        var anchor = GetAnchor(record.mountLocation);

        for (var i = 0; i < count; i++)
        {
            var offset = count <= 1 ? 0f : (i - (count - 1) / 2f) * spacing;
            float x;
            float y;

            if (PixelMath.Abs(outward.x) > 0.5f)
            {
                x = outward.x > 0 ? shipPixelRect.xMax + exposure : shipPixelRect.xMin - exposure;
                y = centerY;
            }
            else
            {
                x = shipPixelRect.xMin + shipPixelRect.width * anchor.x;
                var hullY = outward.y < 0
                    ? topEdge[PixelMath.Clamp(PixelMath.RoundToInt(x), 0, topEdge.Length - 1)]
                    : bottomEdge[PixelMath.Clamp(PixelMath.RoundToInt(x), 0, bottomEdge.Length - 1)];
                y = hullY + outward.y * exposure;
            }

            var pos = new PixelVector2(x, y) + alongHull * offset;
            positions.Add(pos);
        }

        return positions;
    }

    static PixelVector2 ResolveDeckMountAvoidance(PixelVector2 originalPos, float symbolSize, PixelRect shipPixelRect, List<PixelRect> obstacles)
    {
        var candidate = originalPos;
        if (!IntersectsObstacle(candidate, symbolSize, obstacles))
            return candidate;

        var halfWidth = GetDeckMountBounds(symbolSize).width * 0.5f;
        var margin = PixelMath.Max(4f, symbolSize * 0.35f);
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
                var aftX = PixelMath.Max(shipPixelRect.xMin + halfWidth, obstacle.xMin - halfWidth - margin);
                var aftCandidate = new PixelVector2(aftX, originalPos.y);
                if (!IntersectsObstacle(aftCandidate, symbolSize, obstacles))
                {
                    var aftDistance = PixelMath.Abs(aftCandidate.x - originalPos.x);
                    if (aftDistance < bestDistance)
                    {
                        bestDistance = aftDistance;
                        bestCandidate = aftCandidate;
                    }
                }

                var foreX = PixelMath.Min(shipPixelRect.xMax - halfWidth, obstacle.xMax + halfWidth + margin);
                var foreCandidate = new PixelVector2(foreX, originalPos.y);
                if (!IntersectsObstacle(foreCandidate, symbolSize, obstacles))
                {
                    var foreDistance = PixelMath.Abs(foreCandidate.x - originalPos.x);
                    if (foreDistance < bestDistance)
                    {
                        bestDistance = foreDistance;
                        bestCandidate = foreCandidate;
                    }
                }
            }

            if (bestDistance < float.MaxValue)
                return bestCandidate;

            candidate = new PixelVector2(PixelMath.Clamp(candidate.x - margin, shipPixelRect.xMin + halfWidth, shipPixelRect.xMax - halfWidth), candidate.y);
        }

        return candidate;
    }

    static bool IntersectsObstacle(PixelVector2 pos, float symbolSize, List<PixelRect> obstacles)
    {
        var bounds = GetDeckMountBounds(pos, symbolSize);
        foreach (var obstacle in obstacles)
        {
            if (bounds.Overlaps(obstacle))
                return true;
        }

        return false;
    }

    static PixelRect GetDeckMountBounds(float symbolSize)
    {
        return new PixelRect(0f, 0f, symbolSize * 2.6f, symbolSize * 1.9f);
    }

    static PixelRect GetDeckMountBounds(PixelVector2 pos, float symbolSize)
    {
        var size = GetDeckMountBounds(symbolSize).size;
        return new PixelRect(pos.x - size.x / 2f, pos.y - size.y / 2f, size.x, size.y);
    }

    static float EvaluateBatteryGlyphSize(float shellSizeInch)
    {
        if (shellSizeInch <= 0)
            return 4.5f;

        if (shellSizeInch <= 4.7f)
            return PixelMath.Clamp(3.1f + shellSizeInch * 0.48f, 4f, 6.2f);

        var largeGunBoost = PixelMath.Pow(shellSizeInch - 4.7f, 0.98f) * 1.55f;
        return PixelMath.Clamp(5.8f + largeGunBoost, 5.8f, 23f);
    }

    static float EvaluateBatteryGlyphVisualScale(PixelRect shipPixelRect)
    {
        var visualBeam = PixelMath.Max(1f, shipPixelRect.height);
        var scale = PixelMath.Pow(72f / visualBeam, 0.55f);
        return PixelMath.Clamp(scale, 0.82f, 1.45f);
    }

    static float EvaluateTorpedoGlyphVisualScale(PixelRect shipPixelRect)
    {
        var visualBeam = PixelMath.Max(1f, shipPixelRect.height);
        var scale = PixelMath.Pow(82f / visualBeam, 0.72f);
        return PixelMath.Clamp(scale, 0.95f, 1.85f);
    }

    static PixelVector2 GetAnchor(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.PortForward => new PixelVector2(NormalizeForeAftX(0.23f), -0.52f),
            PlaceholderMountLocation.Forward => new PixelVector2(NormalizeForeAftX(0.18f), 0f),
            PlaceholderMountLocation.StarboardForward => new PixelVector2(NormalizeForeAftX(0.23f), 0.52f),
            PlaceholderMountLocation.PortMidship => new PixelVector2(0.50f, -0.58f),
            PlaceholderMountLocation.Midship => new PixelVector2(0.50f, 0f),
            PlaceholderMountLocation.StarboardMidship => new PixelVector2(0.50f, 0.58f),
            PlaceholderMountLocation.PortAfter => new PixelVector2(NormalizeForeAftX(0.77f), -0.52f),
            PlaceholderMountLocation.After => new PixelVector2(NormalizeForeAftX(0.82f), 0f),
            PlaceholderMountLocation.StarboardAfter => new PixelVector2(NormalizeForeAftX(0.77f), 0.52f),
            _ => new PixelVector2(0.50f, 0f)
        };
    }

    static List<PixelRect> BuildSuperstructureObstacles(ShipClassPlaceholderRenderInput shipClass, PixelRect shipPixelRect, float centerY, ShipClassPlaceholderImageRenderSettings settings, float[] topEdge, float[] bottomEdge)
    {
        var obstacles = new List<PixelRect>();
        var profile = GetHullProfile(shipClass.type);
        var mainLength = shipPixelRect.width * profile.superstructureLength * settings.superstructureHeightScale;
        var mainWidth = shipPixelRect.height * 0.20f * settings.superstructureHeightScale;
        obstacles.Add(ExpandPixelRect(BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.bridgeCenter), mainLength, mainWidth, centerY), 3f));
        obstacles.Add(ExpandPixelRect(BuildCenteredPixelRect(shipPixelRect, NormalizeForeAftX(profile.deckhouseCenter), mainLength * 0.55f, mainWidth * 0.7f, centerY), 3f));

        var funnelCount = ResolveFunnelCount(shipClass, settings);
        if (funnelCount <= 0)
            return obstacles;

        var baseXMin = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.62f + settings.funnelSpacingBias * 0.2f);
        var baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.40f + settings.funnelSpacingBias * 0.2f);
        if (funnelCount == 1)
            baseXMin = baseXMax = shipPixelRect.xMin + shipPixelRect.width * NormalizeForeAftX(0.48f + settings.funnelSpacingBias * 0.2f);

        for (var i = 0; i < funnelCount; i++)
        {
            var t = funnelCount == 1 ? 0.5f : i / (float)(funnelCount - 1);
            var x = PixelMath.Lerp(baseXMin, baseXMax, t);
            var halfBreadth = SampleHullHalfBreadth(topEdge, bottomEdge, PixelMath.RoundToInt(x));
            var funnelHeight = PixelMath.Max(5f, halfBreadth * 0.9f * settings.superstructureHeightScale);
            var funnelWidth = PixelMath.Max(4f, shipPixelRect.width * 0.015f);
            obstacles.Add(ExpandPixelRect(new PixelRect(x - funnelWidth / 2f, centerY - funnelHeight / 2f, funnelWidth, funnelHeight), 4f));
        }

        return obstacles;
    }

    static PixelRect ExpandPixelRect(PixelRect rect, float amount)
    {
        return new PixelRect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    static PixelVector2 GetSubmergedMountOutwardDirection(PlaceholderMountLocation location, PixelVector2 fallbackDirection)
    {
        return location switch
        {
            PlaceholderMountLocation.Forward => PixelVector2.right,
            PlaceholderMountLocation.After => PixelVector2.left,
            PlaceholderMountLocation.PortForward => PixelVector2.up,
            PlaceholderMountLocation.PortMidship => PixelVector2.up,
            PlaceholderMountLocation.PortAfter => PixelVector2.up,
            PlaceholderMountLocation.StarboardForward => PixelVector2.down,
            PlaceholderMountLocation.StarboardMidship => PixelVector2.down,
            PlaceholderMountLocation.StarboardAfter => PixelVector2.down,
            _ => PixelMath.Abs(fallbackDirection.x) >= PixelMath.Abs(fallbackDirection.y)
                ? new PixelVector2(PixelMath.Sign(fallbackDirection.x), 0f)
                : new PixelVector2(0f, PixelMath.Sign(fallbackDirection.y))
        };
    }

    static void DrawGunMount(PixelCanvas canvas, PixelVector2 pos, PixelVector2 direction, int barrels, float size, int lineWidth, RgbaColor fill, RgbaColor outline, RgbaColor detailFill)
    {
        var lines = PixelMath.Clamp(barrels, 1, 4);
        var footprintSize = size * (1f + 0.3f * (lines - 1));
        var bodyLength = footprintSize * 1.25f;
        var bodyWidth = PixelMath.Max(4f, footprintSize * 0.85f);
        var perpendicular = new PixelVector2(-direction.y, direction.x);
        var front = pos + direction * (bodyLength * 0.42f);
        var rear = pos - direction * (bodyLength * 0.36f);

        canvas.FillEllipse(pos, bodyLength * 0.58f, bodyWidth * 0.72f, detailFill);
        canvas.DrawPixelRectOutline(new PixelRect(pos.x - bodyLength * 0.58f, pos.y - bodyWidth * 0.72f, bodyLength * 1.16f, bodyWidth * 1.44f), 1, outline);
        canvas.FillEllipse(front, bodyWidth * 0.48f, bodyWidth * 0.48f, fill);
        canvas.FillEllipse(rear, bodyWidth * 0.38f, bodyWidth * 0.32f, detailFill);

        var barrelSpacing = PixelMath.Min(bodyWidth * 0.28f, footprintSize * 0.24f + 0.45f);
        var barrelThickness = PixelMath.Max(PixelMath.Max(2, lineWidth + 1), PixelMath.RoundToInt(size * 0.18f));
        for (var i = 0; i < lines; i++)
        {
            var spread = lines == 1 ? 0f : (i - (lines - 1) / 2f) * barrelSpacing;
            var from = front + perpendicular * spread;
            var to = from + direction * (size + 6f);
            canvas.DrawLine(from, to, barrelThickness, outline);
        }

        canvas.DrawLine(rear, front, 1, outline);
    }

    static void DrawTorpedoMount(PixelCanvas canvas, PixelVector2 pos, PixelVector2 direction, int barrels, float size, bool trainable, RgbaColor fill, RgbaColor outline, RgbaColor detailFill)
    {
        var along = direction.sqrMagnitude < 0.0001f ? PixelVector2.right : direction.normalized;
        var perp = new PixelVector2(-along.y, along.x);
        var halfLength = size;
        var halfWidth = PixelMath.Max(2f, size * (trainable ? 0.34f : 0.25f));
        var bodyStart = pos - along * halfLength;
        var bodyEnd = pos + along * halfLength;
        var outlineThickness = PixelMath.Max(2, PixelMath.RoundToInt(halfWidth * 2f + 2f));
        var fillThickness = PixelMath.Max(1, PixelMath.RoundToInt(halfWidth * 2f - 1f));

        if (trainable)
        {
            canvas.FillEllipse(pos, halfLength * 0.55f, halfLength * 0.55f, detailFill);
            canvas.DrawLine(pos, pos, PixelMath.Max(1, PixelMath.RoundToInt(halfLength * 1.1f)), outline);
            canvas.FillEllipse(pos, halfLength * 0.36f, halfLength * 0.36f, detailFill);
        }

        canvas.DrawLine(bodyStart, bodyEnd, outlineThickness, outline);
        canvas.DrawLine(bodyStart, bodyEnd, fillThickness, fill);

        var tubes = PixelMath.Clamp(barrels, 1, 4);
        for (var i = 0; i < tubes; i++)
        {
            var spread = tubes == 1 ? 0f : PixelMath.Lerp(-halfWidth, halfWidth, i / (float)(tubes - 1));
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

    static PixelVector2 GetBarrelDirection(PlaceholderMountLocation location)
    {
        return location switch
        {
            PlaceholderMountLocation.PortForward => new PixelVector2(0.8f, -0.55f).normalized,
            PlaceholderMountLocation.Forward => PixelVector2.right,
            PlaceholderMountLocation.StarboardForward => new PixelVector2(0.8f, 0.55f).normalized,
            PlaceholderMountLocation.PortMidship => PixelVector2.up,
            PlaceholderMountLocation.Midship => PixelVector2.right,
            PlaceholderMountLocation.StarboardMidship => PixelVector2.down,
            PlaceholderMountLocation.PortAfter => new PixelVector2(-0.8f, -0.55f).normalized,
            PlaceholderMountLocation.After => PixelVector2.left,
            PlaceholderMountLocation.StarboardAfter => new PixelVector2(-0.8f, 0.55f).normalized,
            _ => PixelVector2.right
        };
    }

    static PixelVector2 ResolveMountDirection(PlaceholderMountRenderRecord record)
    {
        var avgAngle = ResolvePreferredArcAngle(record);
        if (avgAngle.HasValue)
        {
            return AngleDegToScreenVector(avgAngle.Value);
        }

        return GetBarrelDirection(record.mountLocation);
    }

    static float? ResolvePreferredArcAngle(PlaceholderMountRenderRecord record)
    {
        if (record.mountArcs == null || record.mountArcs.Count == 0)
            return null;

        var vectors = new List<PixelVector2>();
        foreach (var arc in record.mountArcs)
        {
            var midDeg = NormalizeAngle(arc.startDeg + arc.CoverageDeg / 2f);
            vectors.Add(AngleDegToScreenVector(midDeg));
        }

        if (vectors.Count == 0)
            return null;

        var sum = PixelVector2.zero;
        foreach (var v in vectors)
        {
            sum += v;
        }

        if (sum.sqrMagnitude < 0.0001f)
            return null;

        return ScreenVectorToAngleDeg(sum.normalized);
    }

    static PixelVector2 AngleDegToScreenVector(float angleDeg)
    {
        var rad = angleDeg * PixelMath.Deg2Rad;
        return new PixelVector2(PixelMath.Cos(rad), PixelMath.Sin(rad)).normalized;
    }

    static float ScreenVectorToAngleDeg(PixelVector2 vector)
    {
        var rad = PixelMath.Atan2(vector.y, vector.x);
        return NormalizeAngle(rad * PixelMath.Rad2Deg);
    }

    static float NormalizeAngle(float angleDeg)
    {
        var normalized = angleDeg % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }

    static float NormalizeForeAftX(float normalizedX) => 1f - normalizedX;

    static float EstimateVisualPadding(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
    {
        var maxBatteryGlyph = (shipClass.batteryRecords ?? new List<PlaceholderBatteryRenderRecord>())
            .Select(b => EvaluateBatteryGlyphSize(b.shellSizeInch))
            .DefaultIfEmpty(6f)
            .Max() * settings.weaponScale;
        var maxTorpedoGlyph = (shipClass.torpedoMountLocationRecords ?? new List<PlaceholderMountRenderRecord>())
            .Select(r => PixelMath.Clamp(5f + r.barrels * 0.7f, 5f, 11f))
            .DefaultIfEmpty(5f)
            .Max() * settings.weaponScale;

        var weaponPadding = PixelMath.Max(maxBatteryGlyph * 1.2f, maxTorpedoGlyph * 1.15f);
        return PixelMath.Max(settings.hullPadding + weaponPadding, settings.hullPadding + settings.lineWidth * 2f + 6f);
    }

    static float SampleHullHalfBreadth(float[] topEdge, float[] bottomEdge, int x)
    {
        x = PixelMath.Clamp(x, 0, topEdge.Length - 1);
        if (float.IsNaN(topEdge[x]))
            return 0;
        return (bottomEdge[x] - topEdge[x]) / 2f;
    }

    static float EvaluateHalfBreadth(float t, HullProfile profile, ShipClassPlaceholderImageRenderSettings settings)
    {
        t = PixelMath.Clamp01(t);
        // Use a simple 30/40/30 placeholder hull: sinusoidal bow entry, parallel midbody,
        // sinusoidal stern run. This keeps zero width at both ends and a flat max beam amidships.
        const float endSection = 0.3f;
        const float middleEnd = 0.7f;

        if (t < endSection)
        {
            var localT = t / endSection;
            return profile.maxBeamScale * PixelMath.Sin(localT * PixelMath.PI * 0.5f);
        }

        if (t <= middleEnd)
            return profile.maxBeamScale;

        var sternLocalT = (1f - t) / endSection;
        return profile.maxBeamScale * PixelMath.Sin(sternLocalT * PixelMath.PI * 0.5f);
    }

    static float SmootherStep01(float t)
    {
        t = PixelMath.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static HullProfile GetHullProfile(PlaceholderShipType shipType)
    {
        if (HullProfiles.TryGetValue(shipType, out var profile))
            return profile;
        return HullProfiles[PlaceholderShipType.NotSpecified];
    }

    static int ResolveFunnelCount(ShipClassPlaceholderRenderInput shipClass, ShipClassPlaceholderImageRenderSettings settings)
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

    static int InferFunnelCount(ShipClassPlaceholderRenderInput shipClass)
    {
        if (shipClass.type == PlaceholderShipType.Transport || shipClass.type == PlaceholderShipType.Repair)
            return shipClass.displacementTons > 7000 ? 2 : 1;
        if (shipClass.type == PlaceholderShipType.TorpedoBoat)
            return shipClass.speedKnots >= 24 ? 2 : 1;
        if (shipClass.type == PlaceholderShipType.Destroyer)
            return shipClass.speedKnots >= 27 ? 3 : 2;
        if (shipClass.type == PlaceholderShipType.Battleship || shipClass.type == PlaceholderShipType.ArmoredCruiser)
            return shipClass.displacementTons >= 9000 ? 2 : 1;
        // if (shipClass.type == PlaceholderShipType.Cruiser || shipClass.type == PlaceholderShipType.LightCruiser || shipClass.type == PlaceholderShipType.ArmedMerchantCruiser)
        if (shipClass.type == PlaceholderShipType.LightCruiser || shipClass.type == PlaceholderShipType.ArmedMerchantCruiser)
            return shipClass.speedKnots >= 21 ? 2 : 1;
        return shipClass.displacementTons >= 6000 ? 2 : 1;
    }

    static RenderPalette GetPalette(ShipClassPlaceholderImageRenderSettings settings, RenderVariant variant)
    {
        return variant switch
        {
            RenderVariant.TopJpg => new RenderPalette
            {
                background = new RgbaColor(255, 255, 255, 255),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(28, 28, 28, 255),
                interiorLine = new RgbaColor(0, 0, 0, 255),
                detailFill = new RgbaColor(255, 255, 255, 255),
                mountFill = new RgbaColor(36, 36, 36, 255),
                rapidFire = new RgbaColor(0, 0, 0, 255),
            },
            RenderVariant.IconPng => new RenderPalette
            {
                background = new RgbaColor(0, 0, 0, 0),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(0, 0, 0, 255),
                interiorLine = new RgbaColor(0, 0, 0, 255),
                detailFill = new RgbaColor(255, 255, 255, 255),
                mountFill = new RgbaColor(0, 0, 0, 255),
                rapidFire = new RgbaColor(0, 0, 0, 255),
            },
            _ => new RenderPalette
            {
                background = new RgbaColor(0, 0, 0, 0),
                hullFill = new RgbaColor(255, 255, 255, 255),
                hullOutline = new RgbaColor(0, 0, 0, 255),
                interiorLine = new RgbaColor(0, 0, 0, 230),
                detailFill = new RgbaColor(255, 255, 255, 220),
                mountFill = new RgbaColor(0, 0, 0, 255),
                rapidFire = new RgbaColor(0, 0, 0, 220),
            }
        };
    }
}
