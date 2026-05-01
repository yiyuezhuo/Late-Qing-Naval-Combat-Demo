using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

using NavalCombatCore;
using YYZ;

public enum NaabLikeElevationMode
{
    Range,
    SearchFix,
    SearchSk5
}

public enum NaabLikePlotMode
{
    None,
    Trajectories,
    Penetration
}

sealed class NaabLikeArmorPreset
{
    public string name;
    public float quality;
    public float elongationPercent;
    public float bhn;
}

sealed class NaabLikeProjectilePreset
{
    public NaabLikeProjectile projectile;
}

sealed class NaabLikeResultRow
{
    public NaabLikeBallisticsResult result;
    public string elevation;
    public string range;
    public string horizontalPenetration;
    public string verticalPenetration;
    public string timeOfFlight;
    public string impactVelocity;
    public string angleOfFall;
}

[UxmlElement]
public partial class NaabLikeTrajectoryChart : VisualElement
{
    const float LeftPadding = 54f;
    const float RightPadding = 18f;
    const float TopPadding = 18f;
    const float BottomPadding = 34f;
    const float FeetPerYard = 3f;

    readonly VisualElement labelLayer = new();
    List<NaabLikeBallisticsResult> results = new();

    static readonly Color[] SeriesColors =
    {
        new(0.75f, 0.18f, 0.14f, 1f),
        new(0.14f, 0.46f, 0.78f, 1f),
        new(0.1f, 0.55f, 0.22f, 1f)
    };
    static readonly Color AxisColor = new(0.78f, 0.82f, 0.86f, 1f);
    static readonly Color GridColor = new(0.78f, 0.82f, 0.86f, 0.16f);

    public NaabLikeTrajectoryChart()
    {
        style.flexGrow = 1;
        style.minHeight = 220;
        labelLayer.style.position = Position.Absolute;
        labelLayer.style.left = 0;
        labelLayer.style.top = 0;
        labelLayer.style.right = 0;
        labelLayer.style.bottom = 0;
        labelLayer.pickingMode = PickingMode.Ignore;
        Add(labelLayer);
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());
    }

    public void SetResults(IEnumerable<NaabLikeBallisticsResult> newResults)
    {
        results = newResults?
            .Where(result => result?.success == true && result.trajectory.Count >= 2)
            .ToList() ?? new();
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var painter = context.painter2D;
        painter.lineCap = LineCap.Butt;
        painter.lineWidth = 1f;
        DrawAxes(painter, chartRect);
        if (results.Count == 0)
            return;

        var scale = CalculateScale(chartRect);
        for (int i = 0; i < results.Count; i++)
            DrawTrajectory(painter, chartRect, results[i], scale, SeriesColors[i % SeriesColors.Length]);
    }

    void DrawAxes(Painter2D painter, Rect chartRect)
    {
        painter.strokeColor = AxisColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, chartRect.yMin));
        painter.LineTo(new Vector2(chartRect.xMin, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMax));
        painter.Stroke();

        for (int i = 1; i < 4; i++)
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, i / 4f);
            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin, y));
            painter.LineTo(new Vector2(chartRect.xMax, y));
            painter.Stroke();
        }
    }

    void DrawTrajectory(Painter2D painter, Rect chartRect, NaabLikeBallisticsResult result, ChartScale scale, Color color)
    {
        painter.strokeColor = color;
        painter.lineWidth = 2f;
        painter.BeginPath();
        var started = false;
        foreach (var point in result.trajectory)
        {
            var mapped = MapPoint(chartRect, point.rangeYards, point.heightFeet, scale);
            if (!started)
            {
                painter.MoveTo(mapped);
                started = true;
            }
            else
            {
                painter.LineTo(mapped);
            }
        }
        painter.Stroke();
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var scale = CalculateScale(chartRect);

        labelLayer.Add(BuildLabel("y ft", 2f, chartRect.yMin - 6f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel("x yd", chartRect.xMax - 52f, chartRect.yMax + 8f, 70f, 18f, TextAnchor.UpperLeft));
        labelLayer.Add(BuildLabel(scale.xAxisMaxYards.ToString("0"), chartRect.xMax - 45f, chartRect.yMax + 8f, 50f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(scale.yAxisMaxFeet.ToString("0"), 2f, chartRect.yMin + 10f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));

        for (int i = 0; i < results.Count; i++)
        {
            labelLayer.Add(BuildLabel(
                $"{results[i].elevationDeg:0.##} deg",
                chartRect.xMin + 8f + i * 78f,
                chartRect.yMin + 4f,
                76f,
                18f,
                TextAnchor.UpperLeft,
                SeriesColors[i % SeriesColors.Length]));
        }
    }

    Rect GetChartRect()
    {
        return new Rect(
            LeftPadding,
            TopPadding,
            Mathf.Max(0f, contentRect.width - LeftPadding - RightPadding),
            Mathf.Max(0f, contentRect.height - TopPadding - BottomPadding));
    }

    ChartScale CalculateScale(Rect chartRect)
    {
        if (results.Count == 0)
            return ChartScale.Create(chartRect, FeetPerYard, 1f);

        var maxRangeFeet = Mathf.Max(FeetPerYard, results.SelectMany(result => result.trajectory).Max(point => point.rangeYards) * FeetPerYard);
        var maxHeightFeet = Mathf.Max(1f, results.SelectMany(result => result.trajectory).Max(point => point.heightFeet));
        return ChartScale.Create(chartRect, maxRangeFeet, maxHeightFeet);
    }

    static Vector2 MapPoint(Rect chartRect, float rangeYards, float heightFeet, ChartScale scale)
    {
        return new Vector2(
            chartRect.xMin + rangeYards * FeetPerYard / scale.feetPerPixel,
            chartRect.yMax - heightFeet / scale.feetPerPixel);
    }

    readonly struct ChartScale
    {
        public readonly float feetPerPixel;
        public readonly float xAxisMaxYards;
        public readonly float yAxisMaxFeet;

        ChartScale(float feetPerPixel, float xAxisMaxYards, float yAxisMaxFeet)
        {
            this.feetPerPixel = feetPerPixel;
            this.xAxisMaxYards = xAxisMaxYards;
            this.yAxisMaxFeet = yAxisMaxFeet;
        }

        public static ChartScale Create(Rect chartRect, float maxRangeFeet, float maxHeightFeet)
        {
            var feetPerPixel = Mathf.Max(
                maxRangeFeet / Mathf.Max(1f, chartRect.width),
                maxHeightFeet / Mathf.Max(1f, chartRect.height));
            return new ChartScale(
                feetPerPixel,
                chartRect.width * feetPerPixel / FeetPerYard,
                chartRect.height * feetPerPixel);
        }
    }

    static Label BuildLabel(string text, float left, float top, float width, float height, TextAnchor align, Color? color = null)
    {
        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.left = left;
        label.style.top = top;
        label.style.width = width;
        label.style.height = height;
        label.style.unityTextAlign = align;
        label.style.fontSize = 11;
        label.style.color = color ?? AxisColor;
        return label;
    }
}

[UxmlElement]
public partial class NaabLikePenetrationChart : VisualElement
{
    const float LeftPadding = 58f;
    const float RightPadding = 18f;
    const float TopPadding = 24f;
    const float BottomPadding = 40f;

    readonly VisualElement labelLayer = new();
    readonly List<Vector2> horizontalPoints = new();
    readonly List<Vector2> verticalPoints = new();

    static readonly Color AxisColor = new(0.78f, 0.82f, 0.86f, 1f);
    static readonly Color GridColor = new(0.78f, 0.82f, 0.86f, 0.16f);
    static readonly Color HorizontalColor = new(0.12f, 0.53f, 0.78f, 1f);
    static readonly Color VerticalColor = new(0.78f, 0.28f, 0.14f, 1f);

    public NaabLikePenetrationChart()
    {
        style.flexGrow = 1;
        style.minHeight = 220;
        labelLayer.style.position = Position.Absolute;
        labelLayer.style.left = 0;
        labelLayer.style.top = 0;
        labelLayer.style.right = 0;
        labelLayer.style.bottom = 0;
        labelLayer.pickingMode = PickingMode.Ignore;
        Add(labelLayer);
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());
    }

    public void SetRows(IEnumerable<NaabLikeBallisticsResult> results)
    {
        horizontalPoints.Clear();
        verticalPoints.Clear();
        foreach (var result in results ?? Enumerable.Empty<NaabLikeBallisticsResult>())
        {
            if (result?.success != true)
                continue;
            horizontalPoints.Add(new Vector2(result.rangeYards, result.horizontalPenetrationInches));
            verticalPoints.Add(new Vector2(result.rangeYards, result.verticalPenetrationInches));
        }
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var painter = context.painter2D;
        DrawAxes(painter, chartRect);
        if (horizontalPoints.Count == 0 && verticalPoints.Count == 0)
            return;

        var bounds = GetBounds();
        DrawSeries(painter, chartRect, horizontalPoints, bounds, HorizontalColor);
        DrawSeries(painter, chartRect, verticalPoints, bounds, VerticalColor);
    }

    void DrawAxes(Painter2D painter, Rect chartRect)
    {
        painter.strokeColor = AxisColor;
        painter.lineWidth = 1f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, chartRect.yMin));
        painter.LineTo(new Vector2(chartRect.xMin, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMax));
        painter.Stroke();

        for (int i = 1; i < 4; i++)
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, i / 4f);
            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin, y));
            painter.LineTo(new Vector2(chartRect.xMax, y));
            painter.Stroke();
        }
    }

    void DrawSeries(Painter2D painter, Rect chartRect, List<Vector2> points, (float minX, float maxX, float minY, float maxY) bounds, Color color)
    {
        if (points.Count == 0)
            return;

        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = 2f;
        if (points.Count >= 2)
        {
            painter.BeginPath();
            painter.MoveTo(MapPoint(chartRect, points[0], bounds));
            for (int i = 1; i < points.Count; i++)
                painter.LineTo(MapPoint(chartRect, points[i], bounds));
            painter.Stroke();
        }

        foreach (var point in points)
        {
            painter.BeginPath();
            painter.Arc(MapPoint(chartRect, point, bounds), 3f, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var bounds = GetBounds();
        labelLayer.Add(BuildLabel("Pen (in)", 2f, chartRect.yMin - 8f, LeftPadding - 8f, 24f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel("Range (yd)", chartRect.xMax - 120f, chartRect.yMax + 14f, 138f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxX.ToString("0"), chartRect.xMax - 64f, chartRect.yMax + 2f, 64f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxY.ToString("0.##"), 2f, chartRect.yMin + 10f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel("Horizontal Pen", chartRect.xMin + 8f, chartRect.yMin + 4f, 120f, 18f, TextAnchor.UpperLeft, HorizontalColor));
        labelLayer.Add(BuildLabel("Vertical Pen", chartRect.xMin + 132f, chartRect.yMin + 4f, 120f, 18f, TextAnchor.UpperLeft, VerticalColor));
    }

    Rect GetChartRect()
    {
        return new Rect(
            LeftPadding,
            TopPadding,
            Mathf.Max(0f, contentRect.width - LeftPadding - RightPadding),
            Mathf.Max(0f, contentRect.height - TopPadding - BottomPadding));
    }

    (float minX, float maxX, float minY, float maxY) GetBounds()
    {
        var allPoints = horizontalPoints.Concat(verticalPoints).ToList();
        if (allPoints.Count == 0)
            return (0f, 1f, 0f, 1f);

        var minX = allPoints.Min(point => point.x);
        var maxX = allPoints.Max(point => point.x);
        var maxY = Mathf.Max(1f, allPoints.Max(point => point.y));
        if (Mathf.Approximately(minX, maxX))
        {
            minX -= 1f;
            maxX += 1f;
        }
        return (Mathf.Max(0f, minX), maxX, 0f, maxY);
    }

    static Vector2 MapPoint(Rect chartRect, Vector2 point, (float minX, float maxX, float minY, float maxY) bounds)
    {
        return new Vector2(
            Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(bounds.minX, bounds.maxX, point.x)),
            Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(bounds.minY, bounds.maxY, point.y)));
    }

    static Label BuildLabel(string text, float left, float top, float width, float height, TextAnchor align, Color? color = null)
    {
        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.left = left;
        label.style.top = top;
        label.style.width = width;
        label.style.height = height;
        label.style.unityTextAlign = align;
        label.style.fontSize = 11;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.color = color ?? AxisColor;
        return label;
    }
}

public sealed class NaabLikeCalculatorDialog
{
    const int MaxElevationSamples = 121;
    static readonly float[] Sk5RangesYards =
    {
        2000f, 4000f, 6000f, 8000f, 10000f, 12000f, 14000f, 16000f,
        18000f, 20000f, 22000f, 24000f, 26000f, 28000f, 30000f, 32000f,
        34000f, 36000f
    };

    static readonly List<NaabLikeArmorPreset> ArmorPresets = new()
    {
        new NaabLikeArmorPreset { name = "Avg. wrought iron construction and armor material", quality = 0.6f, elongationPercent = 22f, bhn = 235f },
        new NaabLikeArmorPreset { name = "Avg. \"mild/medium\" construction steel. (1890-on)", quality = 0.75f, elongationPercent = 24f, bhn = 235f },
        new NaabLikeArmorPreset { name = "Avg. U.S WWI-era class \"B\" armor", quality = 0.95f, elongationPercent = 22f, bhn = 235f }
    };

    static readonly List<NaabLikeProjectilePreset> ProjectilePresets = new()
    {
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "5''/50 Mk. 5 CM",
                diameterInches = 5f,
                totalWeightPounds = 50f,
                bodyWeightPounds = 50f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 3000f,
                maxRangeYards = 22600f,
                dragFunction = NaabLikeDragFunction.G5,
                ballisticCoefficient = 1.9307f,
                dragCoefficientAdjust = 14f,
                maxElevationDeg = 20f,
                effectiveShellQuality = 0.575f
            }
        },
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "38cm/47 SK C/34 APC (Bismarck)",
                diameterInches = 14.96f,
                totalWeightPounds = 1763.70f,
                bodyWeightPounds = 1552.05f,
                windscreenWeightPounds = 52.91f,
                apCapWeightPounds = 158.74f,
                hcwclcrCapType = 1,
                windscreenNblAddendMultiplier = 0.33f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 2690f,
                maxRangeYards = 38870f,
                dragFunction = NaabLikeDragFunction.G7,
                ballisticCoefficient = 7.7734f,
                dragCoefficientAdjust = 0f,
                maxElevationDeg = 30f,
                effectiveShellQuality = 0.985f
            }
        }
    };

    sealed class NaabLikeCalculationJob
    {
        public NaabLikeProjectile projectile;
        public NaabLikeArmorInput armor;
        public NaabLikeExteriorBallisticsSolver exterior;
        public NaabLikeTerminalBallisticsSolver terminal;
        public NaabLikeElevationMode elevationMode;
        public List<float> elevationSamples;
        public List<float> targetRanges;
        public int completedRows;
        public int totalRows;
        public int externalFailures;
        public float? angleHint;
    }

    static NaabLikeBallisticsData cachedData;
    static string cachedDataError;

    DropdownField armorPresetField;
    FloatField armorQualityField;
    FloatField armorElongationField;
    FloatField armorBhnField;
    FloatField armorInclinedField;
    DropdownField projectilePresetField;
    FloatField diameterField;
    FloatField totalWeightField;
    FloatField bodyWeightField;
    FloatField windscreenField;
    FloatField apCapWeightField;
    DropdownField capTypeField;
    FloatField windscreenNblAddendMultiplierField;
    FloatField highObliquityWindscreenNblAddendMultiplierField;
    FloatField highObliquityThresholdField;
    FloatField muzzleVelocityField;
    FloatField maxRangeField;
    DropdownField dragFunctionField;
    FloatField ballisticCoefficientField;
    FloatField dragCoefficientField;
    FloatField projectileMaxElevationField;
    FloatField effectiveShellQualityField;
    FloatField integrationStepField;
    DropdownField elevationModeField;
    FloatField startElevationField;
    FloatField endElevationField;
    FloatField elevationStepField;
    FloatField searchRangeStepField;
    DropdownField plotModeField;
    VisualElement rangeElevationRows;
    VisualElement searchFixRows;
    VisualElement contentRoot;
    VisualElement progressOverlay;
    ProgressBar progressBar;
    Label progressLabel;
    Button calculateButton;
    Label statusLabel;
    VisualElement chartContainer;
    NaabLikeTrajectoryChart trajectoryChart;
    NaabLikePenetrationChart penetrationChart;
    MultiColumnListView resultListView;
    IVisualElementScheduledItem calculationSchedule;
    NaabLikeCalculationJob currentJob;

    readonly List<NaabLikeBallisticsResult> results = new();
    readonly List<NaabLikeResultRow> tableRows = new();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public VisualElement BuildContent()
    {
        var root = new VisualElement
        {
            style =
            {
                position = Position.Relative,
                flexGrow = 1,
                flexShrink = 1
            }
        };
        contentRoot = root;

        var mainRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                flexShrink = 1
            }
        };

        var inputScroll = new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexBasis = 380,
                flexShrink = 0,
                marginRight = 8
            }
        };
        BuildInputPanel(inputScroll);

        var outputPanel = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        BuildOutputPanel(outputPanel);

        mainRow.Add(inputScroll);
        mainRow.Add(outputPanel);
        root.Add(mainRow);

        return root;
    }

    void BuildInputPanel(VisualElement root)
    {
        root.Add(BuildSectionLabel(Localize("Plot Parameters")));
        elevationModeField = new DropdownField(Localize("Elevation Mode"), new List<string> { Localize("Range"), Localize("Search Fix"), Localize("Search SK5") }, 2);
        plotModeField = new DropdownField(Localize("Plot Mode"), new List<string> { Localize("None"), Localize("Trajectories"), Localize("Penetration") }, 1);
        root.Add(elevationModeField);

        rangeElevationRows = new VisualElement();
        startElevationField = BuildFloatField(Localize("Start Elevation (deg)"), 1f);
        endElevationField = BuildFloatField(Localize("End Elevation (deg)"), 20f);
        elevationStepField = BuildFloatField(Localize("Elevation Step (deg)"), 1f);
        rangeElevationRows.Add(startElevationField);
        rangeElevationRows.Add(endElevationField);
        rangeElevationRows.Add(elevationStepField);
        root.Add(rangeElevationRows);

        searchFixRows = new VisualElement();
        searchRangeStepField = BuildFloatField(Localize("Range Step (yards)"), 1000f);
        searchFixRows.Add(searchRangeStepField);
        root.Add(searchFixRows);

        root.Add(plotModeField);
        calculateButton = new Button(Calculate)
        {
            text = Localize("Calculate"),
            style =
            {
                marginTop = 8
            }
        };
        root.Add(calculateButton);

        statusLabel = new Label();
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.marginTop = 6;
        root.Add(statusLabel);

        root.Add(BuildSectionLabel(Localize("Armor Parameters")));
        armorPresetField = new DropdownField(Localize("Armor Preset"), ArmorPresets.Select(preset => Localize(preset.name)).ToList(), 2);
        armorQualityField = BuildFloatField(Localize("Quality"), 0.95f);
        armorElongationField = BuildFloatField(Localize("Elongation (%)"), 22f);
        armorBhnField = BuildFloatField(Localize("BHN"), 235f);
        armorInclinedField = BuildFloatField(Localize("Inclined (deg)"), 0f);
        root.Add(armorPresetField);
        root.Add(armorQualityField);
        root.Add(armorElongationField);
        root.Add(armorBhnField);
        root.Add(armorInclinedField);

        root.Add(BuildSectionLabel(Localize("Projectile Parameters")));
        projectilePresetField = new DropdownField(Localize("Projectile Preset"), ProjectilePresets.Select(preset => preset.projectile.name).ToList(), 0);
        diameterField = BuildFloatField(Localize("Diameter (inch)"), 5f);
        totalWeightField = BuildFloatField(Localize("Total Weight (lb)"), 50f);
        bodyWeightField = BuildFloatField(Localize("Body Weight (lb)"), 50f);
        windscreenField = BuildFloatField(Localize("Windscreen (lb)"), 0f);
        apCapWeightField = BuildFloatField(Localize("AP Cap Weight (lb)"), 0f);
        capTypeField = new DropdownField(Localize("Cap Type"), GetCapTypeLabels(), 0);
        windscreenNblAddendMultiplierField = BuildFloatField(Localize("Windscreen NBL Addend Multiplier"), 0.75f);
        highObliquityWindscreenNblAddendMultiplierField = BuildFloatField(Localize("Hi-Obl Windscreen NBL Addend Mult."), 0.1f);
        highObliquityThresholdField = BuildFloatField(Localize("High-Obliquity Threshold (deg)"), 0f);
        muzzleVelocityField = BuildFloatField(Localize("Muzzle Velocity (fps)"), 3000f);
        maxRangeField = BuildFloatField(Localize("Max Range (yards)"), 22600f);
        dragFunctionField = new DropdownField(Localize("Drag Function"), GetDragFunctionLabels(), 2);
        ballisticCoefficientField = BuildFloatField(Localize("Ballistic Coefficient"), 1.9307f);
        dragCoefficientField = BuildFloatField(Localize("Drag Coefficient"), 14f);
        projectileMaxElevationField = BuildFloatField(Localize("Elevation Deg"), 20f);
        effectiveShellQualityField = BuildFloatField(Localize("Effective Shell Quality"), 0.575f);
        integrationStepField = BuildFloatField(Localize("Integration Step (ft)"), 3f);
        foreach (var element in new VisualElement[]
        {
            projectilePresetField, diameterField, totalWeightField, bodyWeightField, windscreenField, apCapWeightField,
            capTypeField, windscreenNblAddendMultiplierField, highObliquityWindscreenNblAddendMultiplierField,
            highObliquityThresholdField, muzzleVelocityField, maxRangeField, dragFunctionField, ballisticCoefficientField,
            dragCoefficientField, projectileMaxElevationField, effectiveShellQualityField, integrationStepField
        })
        {
            root.Add(element);
        }

        RegisterInputCallbacks();
        ApplyArmorPreset(ArmorPresets[2]);
        ApplyProjectilePreset(ProjectilePresets[0].projectile);
        UpdateElevationModeVisibility();
    }

    void BuildOutputPanel(VisualElement root)
    {
        chartContainer = new VisualElement
        {
            style =
            {
                flexBasis = 240,
                flexShrink = 0,
                marginBottom = 6,
                display = DisplayStyle.None
            }
        };
        trajectoryChart = new NaabLikeTrajectoryChart
        {
            style =
            {
                height = 230
            }
        };
        penetrationChart = new NaabLikePenetrationChart
        {
            style =
            {
                height = 230
            }
        };
        chartContainer.Add(trajectoryChart);
        chartContainer.Add(penetrationChart);
        root.Add(chartContainer);

        resultListView = BuildResultListView();
        root.Add(resultListView);
    }

    MultiColumnListView BuildResultListView()
    {
        var listView = new MultiColumnListView
        {
            name = "NaabLikeCalculatorResultListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
                minHeight = 170
            }
        };

        void AddColumn(string name, string title, int width, Func<NaabLikeResultRow, string> selector)
        {
            listView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 80),
                stretchable = false,
                makeCell = () => new Label
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.Normal
                    }
                },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;
                    var row = index >= 0 && index < tableRows.Count ? tableRows[index] : null;
                    label.text = row == null ? "" : selector(row);
                }
            });
        }

        AddColumn("range", Localize("Range"), 110, row => row.range);
        AddColumn("horizontalPenetration", Localize("Horizontal Pen"), 140, row => row.horizontalPenetration);
        AddColumn("verticalPenetration", Localize("Vertical Pen"), 130, row => row.verticalPenetration);
        AddColumn("velocity", Localize("Impact Velocity"), 130, row => row.impactVelocity);
        AddColumn("fall", Localize("Angle of Fall"), 120, row => row.angleOfFall);
        AddColumn("time", Localize("Time of Flight"), 120, row => row.timeOfFlight);
        AddColumn("elevation", Localize("Elevation"), 90, row => row.elevation);

        return listView;
    }

    void RegisterInputCallbacks()
    {
        armorPresetField.RegisterValueChangedCallback(evt =>
        {
            var index = armorPresetField.index;
            if (index >= 0 && index < ArmorPresets.Count)
                ApplyArmorPreset(ArmorPresets[index]);
        });

        projectilePresetField.RegisterValueChangedCallback(evt =>
        {
            var index = projectilePresetField.index;
            if (index >= 0 && index < ProjectilePresets.Count)
                ApplyProjectilePreset(ProjectilePresets[index].projectile);
        });

        elevationModeField.RegisterValueChangedCallback(_ =>
        {
            UpdateElevationModeVisibility();
        });
        plotModeField.RegisterValueChangedCallback(_ => RefreshOutputs());
    }

    void ApplyArmorPreset(NaabLikeArmorPreset preset)
    {
        if (preset == null)
            return;
        armorQualityField?.SetValueWithoutNotify(preset.quality);
        armorElongationField?.SetValueWithoutNotify(preset.elongationPercent);
        armorBhnField?.SetValueWithoutNotify(preset.bhn);
    }

    void ApplyProjectilePreset(NaabLikeProjectile preset)
    {
        if (preset == null)
            return;
        diameterField?.SetValueWithoutNotify(preset.diameterInches);
        totalWeightField?.SetValueWithoutNotify(preset.totalWeightPounds);
        bodyWeightField?.SetValueWithoutNotify(preset.bodyWeightPounds);
        windscreenField?.SetValueWithoutNotify(preset.windscreenWeightPounds);
        apCapWeightField?.SetValueWithoutNotify(preset.apCapWeightPounds);
        capTypeField?.SetValueWithoutNotify(GetCapTypeLabel(preset.hcwclcrCapType));
        windscreenNblAddendMultiplierField?.SetValueWithoutNotify(preset.windscreenNblAddendMultiplier);
        highObliquityWindscreenNblAddendMultiplierField?.SetValueWithoutNotify(preset.highObliquityWindscreenNblAddendMultiplier);
        highObliquityThresholdField?.SetValueWithoutNotify(preset.highObliquityThresholdDeg);
        muzzleVelocityField?.SetValueWithoutNotify(preset.muzzleVelocityFeetPerSecond);
        maxRangeField?.SetValueWithoutNotify(preset.maxRangeYards);
        dragFunctionField?.SetValueWithoutNotify(preset.dragFunction.ToString());
        ballisticCoefficientField?.SetValueWithoutNotify(preset.ballisticCoefficient);
        dragCoefficientField?.SetValueWithoutNotify(preset.dragCoefficientAdjust);
        projectileMaxElevationField?.SetValueWithoutNotify(preset.maxElevationDeg);
        endElevationField?.SetValueWithoutNotify(preset.maxElevationDeg);
        effectiveShellQualityField?.SetValueWithoutNotify(preset.effectiveShellQuality);
    }

    void Calculate()
    {
        if (resultListView == null || currentJob != null)
            return;

        results.Clear();
        tableRows.Clear();

        var data = LoadData();
        if (data == null)
        {
            statusLabel.text = cachedDataError;
            RefreshOutputs();
            return;
        }

        var validationError = ValidateInputs(out var projectile, out var armor);
        if (validationError != null)
        {
            statusLabel.text = validationError;
            RefreshOutputs();
            return;
        }

        var dragTable = data.dragTables[projectile.dragFunction];
        var dxFeet = integrationStepField.value;
        var exterior = new NaabLikeExteriorBallisticsSolver(dragTable, projectile, dxFeet);
        var terminal = new NaabLikeTerminalBallisticsSolver(data.terminalTables, projectile, armor);
        var elevationMode = GetElevationMode();
        var elevationSamples = elevationMode == NaabLikeElevationMode.Range ? GetElevationSamples() : null;
        var targetRanges = elevationMode == NaabLikeElevationMode.Range ? null : GetTargetRanges(projectile);
        var totalRows = elevationMode == NaabLikeElevationMode.Range ? elevationSamples.Count : targetRanges.Count;

        if (totalRows <= 0)
        {
            statusLabel.text = Localize("{0} result(s).", 0);
            RefreshOutputs();
            return;
        }

        currentJob = new NaabLikeCalculationJob
        {
            projectile = projectile,
            armor = armor,
            exterior = exterior,
            terminal = terminal,
            elevationMode = elevationMode,
            elevationSamples = elevationSamples,
            targetRanges = targetRanges,
            totalRows = totalRows
        };
        ShowProgressDialog(0, totalRows);
        calculationSchedule = contentRoot.schedule.Execute(ProcessCalculationStep).Every(1);
    }

    void ProcessCalculationStep()
    {
        if (currentJob == null)
        {
            calculationSchedule?.Pause();
            calculationSchedule = null;
            return;
        }

        var job = currentJob;
        if (job.completedRows >= job.totalRows)
        {
            calculationSchedule?.Pause();
            calculationSchedule = null;
            currentJob = null;
            HideProgressDialog();
            statusLabel.text = job.externalFailures > 0
                ? Localize("{0} result(s), {1} external ballistic failure(s).", tableRows.Count, job.externalFailures)
                : Localize("{0} result(s).", tableRows.Count);
            RefreshOutputs();
            return;
        }

        NaabLikeBallisticsResult result;
        if (job.elevationMode == NaabLikeElevationMode.Range)
        {
            var angle = job.elevationSamples[job.completedRows];
            result = job.exterior.SolveToGround(angle, job.projectile.maxRangeYards, MathF.Max(job.projectile.maxRangeYards / 120f, 100f));
        }
        else
        {
            var targetRange = job.targetRanges[job.completedRows];
            result = job.exterior.SolveForTargetRange(targetRange, MathF.Max(job.projectile.maxElevationDeg, 45f), job.angleHint);
            if (result.success)
                job.angleHint = result.elevationDeg;
        }

        AddResult(result, job.terminal, job.armor, ref job.externalFailures);
        job.completedRows++;
        UpdateProgressDialog(job.completedRows, job.totalRows);
    }

    void ShowProgressDialog(int completedRows, int totalRows)
    {
        progressOverlay?.RemoveFromHierarchy();
        progressBar = new ProgressBar
        {
            lowValue = 0f,
            highValue = 1f
        };
        progressLabel = new Label
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 6
            }
        };

        var panel = new VisualElement
        {
            style =
            {
                width = 320,
                height = 132,
                flexGrow = 0,
                flexShrink = 0,
                paddingLeft = 12,
                paddingRight = 12,
                paddingTop = 10,
                paddingBottom = 10
            }
        };
        panel.AddToClassList("panel");
        panel.Add(new Label(Localize("Calculating..."))
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 8
            }
        });
        panel.Add(progressBar);
        panel.Add(progressLabel);

        progressOverlay = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = 0,
                right = 0,
                top = 0,
                bottom = 0,
                alignItems = Align.Center,
                justifyContent = Justify.Center,
                backgroundColor = new Color(0f, 0f, 0f, 0.35f)
            }
        };
        progressOverlay.Add(panel);
        contentRoot.Add(progressOverlay);
        calculateButton?.SetEnabled(false);
        UpdateProgressDialog(completedRows, totalRows);
    }

    void UpdateProgressDialog(int completedRows, int totalRows)
    {
        var progress = totalRows <= 0 ? 1f : Mathf.Clamp01((float)completedRows / totalRows);
        if (progressBar != null)
        {
            progressBar.value = progress;
            progressBar.title = $"{progress:P0}";
        }
        if (progressLabel != null)
            progressLabel.text = Localize("{0}/{1} rows", completedRows, totalRows);
    }

    void HideProgressDialog()
    {
        progressOverlay?.RemoveFromHierarchy();
        progressOverlay = null;
        progressBar = null;
        progressLabel = null;
        calculateButton?.SetEnabled(true);
    }

    void AddResult(NaabLikeBallisticsResult result, NaabLikeTerminalBallisticsSolver terminal, NaabLikeArmorInput armor, ref int externalFailures)
    {
        results.Add(result);
        if (result?.success != true)
        {
            externalFailures++;
            return;
        }

        var sideObliquityDeg = armor.inclinedDeg + result.angleOfFallDeg;
        var deckObliquityDeg = armor.inclinedDeg + MathF.Max(90f - result.angleOfFallDeg, 0f);
        result.verticalPenetrationInches = terminal.CompletePenetrationInches(result.impactVelocityFeetPerSecond, sideObliquityDeg);
        result.horizontalPenetrationInches = terminal.CompletePenetrationInches(result.impactVelocityFeetPerSecond, deckObliquityDeg);
        tableRows.Add(BuildRow(result));
    }

    string ValidateInputs(out NaabLikeProjectile projectile, out NaabLikeArmorInput armor)
    {
        projectile = null;
        armor = null;

        if (armorQualityField.value <= 0f)
            return Localize("Quality must be greater than 0.");
        if (armorElongationField.value <= 0f)
            return Localize("Elongation must be greater than 0.");
        if (armorBhnField.value <= 0f)
            return Localize("BHN must be greater than 0.");
        if (diameterField.value <= 0f)
            return Localize("Projectile diameter must be greater than 0.");
        if (totalWeightField.value <= 0f)
            return Localize("Projectile mass must be greater than 0.");
        if (bodyWeightField.value < 0f || windscreenField.value < 0f || apCapWeightField.value < 0f)
            return Localize("Projectile component weights must be 0 or greater.");
        if (bodyWeightField.value + windscreenField.value + apCapWeightField.value > totalWeightField.value + 0.001f)
            return Localize("Projectile component weights must not exceed total weight.");
        if (windscreenNblAddendMultiplierField.value < 0f || highObliquityWindscreenNblAddendMultiplierField.value < 0f)
            return Localize("Windscreen NBL addend multipliers must be 0 or greater.");
        if (highObliquityThresholdField.value < 0f)
            return Localize("High-obliquity threshold must be 0 or greater.");
        if (muzzleVelocityField.value <= 0f)
            return Localize("Muzzle velocity must be greater than 0.");
        if (maxRangeField.value <= 0f)
            return Localize("Max range must be greater than 0.");
        if (ballisticCoefficientField.value <= 0f)
            return Localize("Ballistic coefficient must be greater than 0.");
        if (projectileMaxElevationField.value <= 0f)
            return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
        if (effectiveShellQualityField.value <= 0f)
            return Localize("Effective shell quality must be greater than 0.");
        if (integrationStepField.value <= 0f)
            return Localize("Integration step must be greater than 0.");

        if (GetElevationMode() == NaabLikeElevationMode.Range)
        {
            if (startElevationField.value <= 0f || startElevationField.value >= 90f || endElevationField.value <= 0f || endElevationField.value >= 90f)
                return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
            if (elevationStepField.value <= 0f)
                return Localize("Elevation step must be greater than 0.");
            if (startElevationField.value > endElevationField.value)
                return Localize("Start elevation must be less than or equal to end elevation.");
            if (GetElevationSamples().Count > MaxElevationSamples)
                return Localize("Too many elevation samples. Increase the step or narrow the range.");
        }
        else if (GetElevationMode() == NaabLikeElevationMode.SearchFix && searchRangeStepField.value <= 0f)
        {
            return Localize("Range step must be greater than 0.");
        }

        var preset = ProjectilePresets[Math.Clamp(projectilePresetField.index, 0, ProjectilePresets.Count - 1)].projectile;
        projectile = preset.Clone();
        projectile.diameterInches = diameterField.value;
        projectile.totalWeightPounds = totalWeightField.value;
        projectile.bodyWeightPounds = bodyWeightField.value;
        projectile.windscreenWeightPounds = windscreenField.value;
        projectile.apCapWeightPounds = apCapWeightField.value;
        projectile.hcwclcrCapType = GetHcwclcrCapType();
        projectile.windscreenNblAddendMultiplier = windscreenNblAddendMultiplierField.value;
        projectile.highObliquityWindscreenNblAddendMultiplier = highObliquityWindscreenNblAddendMultiplierField.value;
        projectile.highObliquityThresholdDeg = highObliquityThresholdField.value;
        projectile.muzzleVelocityFeetPerSecond = muzzleVelocityField.value;
        projectile.maxRangeYards = maxRangeField.value;
        projectile.dragFunction = GetDragFunction();
        projectile.ballisticCoefficient = ballisticCoefficientField.value;
        projectile.dragCoefficientAdjust = dragCoefficientField.value;
        projectile.maxElevationDeg = projectileMaxElevationField.value;
        projectile.effectiveShellQuality = effectiveShellQualityField.value;

        armor = new NaabLikeArmorInput
        {
            quality = armorQualityField.value,
            elongationPercent = armorElongationField.value,
            bhn = armorBhnField.value,
            inclinedDeg = armorInclinedField.value
        };
        return null;
    }

    List<float> GetElevationSamples()
    {
        var samples = new List<float>();
        var start = startElevationField.value;
        var end = endElevationField.value;
        var step = elevationStepField.value;
        if (step <= 0f)
            return samples;
        var count = Mathf.FloorToInt((end - start) / step) + 1;
        for (int i = 0; i < count; i++)
        {
            var angle = start + step * i;
            if (angle <= end + 0.0001f)
                samples.Add(angle);
        }
        return samples;
    }

    List<float> GetTargetRanges(NaabLikeProjectile projectile)
    {
        if (GetElevationMode() == NaabLikeElevationMode.SearchSk5)
            return Sk5RangesYards.Where(range => range <= projectile.maxRangeYards + 0.001f).ToList();

        var ranges = new List<float>();
        var step = searchRangeStepField.value;
        for (var range = step; range <= projectile.maxRangeYards + 0.001f; range += step)
            ranges.Add(range);
        return ranges;
    }

    NaabLikeResultRow BuildRow(NaabLikeBallisticsResult result)
    {
        return new NaabLikeResultRow
        {
            result = result,
            elevation = $"{result.elevationDeg:0.###} deg",
            range = $"{result.rangeYards:0} yd",
            horizontalPenetration = FormatPenetration(result.horizontalPenetrationInches),
            verticalPenetration = FormatPenetration(result.verticalPenetrationInches),
            timeOfFlight = $"{result.timeOfFlightSeconds:0.00} s",
            impactVelocity = $"{result.impactVelocityFeetPerSecond:0} ft/s",
            angleOfFall = $"{result.angleOfFallDeg:0.00} deg"
        };
    }

    void RefreshOutputs()
    {
        if (resultListView == null)
            return;

        resultListView.itemsSource = tableRows;
        resultListView.Rebuild();

        var successful = results.Where(result => result.success).ToList();
        var plotMode = GetPlotMode();
        var hasChartData = successful.Count > 0 && plotMode != NaabLikePlotMode.None;
        chartContainer.style.display = hasChartData ? DisplayStyle.Flex : DisplayStyle.None;
        trajectoryChart.style.display = hasChartData && plotMode == NaabLikePlotMode.Trajectories ? DisplayStyle.Flex : DisplayStyle.None;
        penetrationChart.style.display = hasChartData && plotMode == NaabLikePlotMode.Penetration ? DisplayStyle.Flex : DisplayStyle.None;
        trajectoryChart.SetResults(hasChartData && plotMode == NaabLikePlotMode.Trajectories ? SelectTrajectoryResults(successful) : Enumerable.Empty<NaabLikeBallisticsResult>());
        penetrationChart.SetRows(hasChartData && plotMode == NaabLikePlotMode.Penetration ? successful : Enumerable.Empty<NaabLikeBallisticsResult>());
    }

    IEnumerable<NaabLikeBallisticsResult> SelectTrajectoryResults(List<NaabLikeBallisticsResult> successful)
    {
        if (successful.Count <= 3)
            return successful;
        return new[] { successful.First(), successful[successful.Count / 2], successful.Last() }.Distinct().ToList();
    }

    void UpdateElevationModeVisibility()
    {
        var mode = GetElevationMode();
        rangeElevationRows.style.display = mode == NaabLikeElevationMode.Range ? DisplayStyle.Flex : DisplayStyle.None;
        searchFixRows.style.display = mode == NaabLikeElevationMode.SearchFix ? DisplayStyle.Flex : DisplayStyle.None;
    }

    static NaabLikeBallisticsData LoadData()
    {
        if (cachedData != null || cachedDataError != null)
            return cachedData;
        try
        {
            cachedData = NaabLikeBallisticsData.LoadEmbedded();
        }
        catch (Exception ex)
        {
            cachedDataError = Localize("Failed to load NAAB-like data: {0}", ex.Message);
        }
        return cachedData;
    }

    NaabLikeElevationMode GetElevationMode()
    {
        return elevationModeField.index switch
        {
            0 => NaabLikeElevationMode.Range,
            1 => NaabLikeElevationMode.SearchFix,
            _ => NaabLikeElevationMode.SearchSk5
        };
    }

    NaabLikePlotMode GetPlotMode()
    {
        return plotModeField.index switch
        {
            0 => NaabLikePlotMode.None,
            2 => NaabLikePlotMode.Penetration,
            _ => NaabLikePlotMode.Trajectories
        };
    }

    int GetHcwclcrCapType()
    {
        return Math.Clamp(capTypeField.index, 0, 4);
    }

    static List<string> GetCapTypeLabels()
    {
        return new List<string> { Localize("None"), Localize("Hard Cap"), Localize("Medium Cap"), Localize("Soft Cap"), Localize("Hood") };
    }

    static string GetCapTypeLabel(int hcwclcrCapType)
    {
        return hcwclcrCapType switch
        {
            1 => Localize("Hard Cap"),
            2 => Localize("Medium Cap"),
            3 => Localize("Soft Cap"),
            4 => Localize("Hood"),
            _ => Localize("None")
        };
    }

    NaabLikeDragFunction GetDragFunction()
    {
        return dragFunctionField.value switch
        {
            "G1" => NaabLikeDragFunction.G1,
            "G2" => NaabLikeDragFunction.G2,
            "G6" => NaabLikeDragFunction.G6,
            "G7" => NaabLikeDragFunction.G7,
            "G8" => NaabLikeDragFunction.G8,
            "G9" => NaabLikeDragFunction.G9,
            "GS" => NaabLikeDragFunction.GS,
            "GL" => NaabLikeDragFunction.GL,
            _ => NaabLikeDragFunction.G5
        };
    }

    static List<string> GetDragFunctionLabels()
    {
        return new List<string> { "G1", "G2", "G5", "G6", "G7", "G8", "G9", "GS", "GL" };
    }

    static string FormatPenetration(float inches)
    {
        return inches > 0f ? $"{inches:0.00} in" : "n/a";
    }

    static Label BuildSectionLabel(string text)
    {
        return new Label(text)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 6,
                marginBottom = 3
            }
        };
    }

    static FloatField BuildFloatField(string label, float value)
    {
        var field = new FloatField(label);
        field.SetValueWithoutNotify(value);
        field.style.marginBottom = 2;
        return field;
    }
}
