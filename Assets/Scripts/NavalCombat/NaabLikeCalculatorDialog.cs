using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Unity.Properties;
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

public sealed class NaabLikeArmorPreset
{
    public string name;
    public float quality;
    public float elongationPercent;
    public float bhn;
}

public sealed class NaabLikeProjectilePreset
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

public sealed class NaabLikeCalculatorViewModel : INotifyBindablePropertyChanged
{
    readonly List<NaabLikeArmorPreset> armorPresets;
    readonly List<NaabLikeProjectilePreset> projectilePresets;

    int _armorPresetIndex = 2;
    int _projectilePresetIndex;
    int _elevationModeIndex = 2;
    int _plotModeIndex = 1;
    int _capTypeIndex;
    int _dragFunctionIndex = 2;
    string _statusText = "";
    bool _progressVisible;
    bool _calculateEnabled = true;
    float _progressValue;
    string _progressTitle = "0%";
    string _progressText = "";
    bool _chartVisible;
    bool _trajectoryChartVisible;
    bool _penetrationChartVisible;

    float _armorQuality = 0.95f;
    float _armorElongation = 22f;
    float _armorBhn = 235f;
    float _armorInclined;
    float _diameter = 5f;
    float _totalWeight = 50f;
    float _bodyWeight = 50f;
    float _windscreen;
    float _apCapWeight;
    float _windscreenNblAddendMultiplier = 0.75f;
    float _highObliquityWindscreenNblAddendMultiplier = 0.1f;
    float _highObliquityThreshold;
    float _muzzleVelocity = 3000f;
    float _maxRange = 22600f;
    float _ballisticCoefficient = 1.9307f;
    float _dragCoefficient = 14f;
    float _projectileMaxElevation = 20f;
    float _effectiveShellQuality = 0.575f;
    float _integrationStep = 3f;
    float _startElevation = 1f;
    float _endElevation = 20f;
    float _elevationStep = 1f;
    float _searchRangeStep = 1000f;

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    public NaabLikeCalculatorViewModel(List<NaabLikeArmorPreset> armorPresets, List<NaabLikeProjectilePreset> projectilePresets)
    {
        this.armorPresets = armorPresets;
        this.projectilePresets = projectilePresets;
        ApplyArmorPreset(armorPresets[Math.Clamp(_armorPresetIndex, 0, armorPresets.Count - 1)]);
        ApplyProjectilePreset(projectilePresets[Math.Clamp(_projectilePresetIndex, 0, projectilePresets.Count - 1)].projectile);
    }

    [CreateProperty]
    public int armorPresetIndex
    {
        get => _armorPresetIndex;
        set
        {
            if (!SetProperty(ref _armorPresetIndex, value, nameof(armorPresetIndex)))
                return;
            if (value >= 0 && value < armorPresets.Count)
                ApplyArmorPreset(armorPresets[value]);
        }
    }

    [CreateProperty]
    public int projectilePresetIndex
    {
        get => _projectilePresetIndex;
        set
        {
            if (!SetProperty(ref _projectilePresetIndex, value, nameof(projectilePresetIndex)))
                return;
            if (value >= 0 && value < projectilePresets.Count)
                ApplyProjectilePreset(projectilePresets[value].projectile);
        }
    }

    [CreateProperty]
    public int elevationModeIndex
    {
        get => _elevationModeIndex;
        set
        {
            if (!SetProperty(ref _elevationModeIndex, value, nameof(elevationModeIndex)))
                return;
            Notify(nameof(rangeElevationDisplay));
            Notify(nameof(searchFixDisplay));
        }
    }

    [CreateProperty] public int plotModeIndex { get => _plotModeIndex; set => SetProperty(ref _plotModeIndex, value, nameof(plotModeIndex)); }
    [CreateProperty] public int capTypeIndex { get => _capTypeIndex; set => SetProperty(ref _capTypeIndex, value, nameof(capTypeIndex)); }
    [CreateProperty] public int dragFunctionIndex { get => _dragFunctionIndex; set => SetProperty(ref _dragFunctionIndex, value, nameof(dragFunctionIndex)); }

    [CreateProperty] public float armorQuality { get => _armorQuality; set => SetProperty(ref _armorQuality, value, nameof(armorQuality)); }
    [CreateProperty] public float armorElongation { get => _armorElongation; set => SetProperty(ref _armorElongation, value, nameof(armorElongation)); }
    [CreateProperty] public float armorBhn { get => _armorBhn; set => SetProperty(ref _armorBhn, value, nameof(armorBhn)); }
    [CreateProperty] public float armorInclined { get => _armorInclined; set => SetProperty(ref _armorInclined, value, nameof(armorInclined)); }
    [CreateProperty] public float diameter { get => _diameter; set => SetProperty(ref _diameter, value, nameof(diameter)); }
    [CreateProperty] public float totalWeight { get => _totalWeight; set => SetProperty(ref _totalWeight, value, nameof(totalWeight)); }
    [CreateProperty] public float bodyWeight { get => _bodyWeight; set => SetProperty(ref _bodyWeight, value, nameof(bodyWeight)); }
    [CreateProperty] public float windscreen { get => _windscreen; set => SetProperty(ref _windscreen, value, nameof(windscreen)); }
    [CreateProperty] public float apCapWeight { get => _apCapWeight; set => SetProperty(ref _apCapWeight, value, nameof(apCapWeight)); }
    [CreateProperty] public float windscreenNblAddendMultiplier { get => _windscreenNblAddendMultiplier; set => SetProperty(ref _windscreenNblAddendMultiplier, value, nameof(windscreenNblAddendMultiplier)); }
    [CreateProperty] public float highObliquityWindscreenNblAddendMultiplier { get => _highObliquityWindscreenNblAddendMultiplier; set => SetProperty(ref _highObliquityWindscreenNblAddendMultiplier, value, nameof(highObliquityWindscreenNblAddendMultiplier)); }
    [CreateProperty] public float highObliquityThreshold { get => _highObliquityThreshold; set => SetProperty(ref _highObliquityThreshold, value, nameof(highObliquityThreshold)); }
    [CreateProperty] public float muzzleVelocity { get => _muzzleVelocity; set => SetProperty(ref _muzzleVelocity, value, nameof(muzzleVelocity)); }
    [CreateProperty] public float maxRange { get => _maxRange; set => SetProperty(ref _maxRange, value, nameof(maxRange)); }
    [CreateProperty] public float ballisticCoefficient { get => _ballisticCoefficient; set => SetProperty(ref _ballisticCoefficient, value, nameof(ballisticCoefficient)); }
    [CreateProperty] public float dragCoefficient { get => _dragCoefficient; set => SetProperty(ref _dragCoefficient, value, nameof(dragCoefficient)); }
    [CreateProperty] public float projectileMaxElevation { get => _projectileMaxElevation; set => SetProperty(ref _projectileMaxElevation, value, nameof(projectileMaxElevation)); }
    [CreateProperty] public float effectiveShellQuality { get => _effectiveShellQuality; set => SetProperty(ref _effectiveShellQuality, value, nameof(effectiveShellQuality)); }
    [CreateProperty] public float integrationStep { get => _integrationStep; set => SetProperty(ref _integrationStep, value, nameof(integrationStep)); }
    [CreateProperty] public float startElevation { get => _startElevation; set => SetProperty(ref _startElevation, value, nameof(startElevation)); }
    [CreateProperty] public float endElevation { get => _endElevation; set => SetProperty(ref _endElevation, value, nameof(endElevation)); }
    [CreateProperty] public float elevationStep { get => _elevationStep; set => SetProperty(ref _elevationStep, value, nameof(elevationStep)); }
    [CreateProperty] public float searchRangeStep { get => _searchRangeStep; set => SetProperty(ref _searchRangeStep, value, nameof(searchRangeStep)); }

    [CreateProperty] public string statusText { get => _statusText; set => SetProperty(ref _statusText, value, nameof(statusText)); }
    [CreateProperty] public bool calculateEnabled { get => _calculateEnabled; set => SetProperty(ref _calculateEnabled, value, nameof(calculateEnabled)); }
    [CreateProperty] public bool progressVisible { get => _progressVisible; set { if (SetProperty(ref _progressVisible, value, nameof(progressVisible))) Notify(nameof(progressOverlayDisplay)); } }
    [CreateProperty] public float progressValue { get => _progressValue; set => SetProperty(ref _progressValue, value, nameof(progressValue)); }
    [CreateProperty] public string progressTitle { get => _progressTitle; set => SetProperty(ref _progressTitle, value, nameof(progressTitle)); }
    [CreateProperty] public string progressText { get => _progressText; set => SetProperty(ref _progressText, value, nameof(progressText)); }
    [CreateProperty] public bool chartVisible { get => _chartVisible; set { if (SetProperty(ref _chartVisible, value, nameof(chartVisible))) Notify(nameof(chartDisplay)); } }
    [CreateProperty] public bool trajectoryChartVisible { get => _trajectoryChartVisible; set { if (SetProperty(ref _trajectoryChartVisible, value, nameof(trajectoryChartVisible))) Notify(nameof(trajectoryChartDisplay)); } }
    [CreateProperty] public bool penetrationChartVisible { get => _penetrationChartVisible; set { if (SetProperty(ref _penetrationChartVisible, value, nameof(penetrationChartVisible))) Notify(nameof(penetrationChartDisplay)); } }

    [CreateProperty] public DisplayStyle rangeElevationDisplay => elevationModeIndex == (int)NaabLikeElevationMode.Range ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle searchFixDisplay => elevationModeIndex == (int)NaabLikeElevationMode.SearchFix ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle progressOverlayDisplay => progressVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle chartDisplay => chartVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle trajectoryChartDisplay => trajectoryChartVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle penetrationChartDisplay => penetrationChartVisible ? DisplayStyle.Flex : DisplayStyle.None;

    void ApplyArmorPreset(NaabLikeArmorPreset preset)
    {
        if (preset == null)
            return;
        armorQuality = preset.quality;
        armorElongation = preset.elongationPercent;
        armorBhn = preset.bhn;
    }

    void ApplyProjectilePreset(NaabLikeProjectile preset)
    {
        if (preset == null)
            return;
        diameter = preset.diameterInches;
        totalWeight = preset.totalWeightPounds;
        bodyWeight = preset.bodyWeightPounds;
        windscreen = preset.windscreenWeightPounds;
        apCapWeight = preset.apCapWeightPounds;
        capTypeIndex = Math.Clamp(preset.hcwclcrCapType, 0, 4);
        windscreenNblAddendMultiplier = preset.windscreenNblAddendMultiplier;
        highObliquityWindscreenNblAddendMultiplier = preset.highObliquityWindscreenNblAddendMultiplier;
        highObliquityThreshold = preset.highObliquityThresholdDeg;
        muzzleVelocity = preset.muzzleVelocityFeetPerSecond;
        maxRange = preset.maxRangeYards;
        dragFunctionIndex = NaabLikeCalculatorDialog.GetDragFunctionIndex(preset.dragFunction);
        ballisticCoefficient = preset.ballisticCoefficient;
        dragCoefficient = preset.dragCoefficientAdjust;
        projectileMaxElevation = preset.maxElevationDeg;
        endElevation = preset.maxElevationDeg;
        effectiveShellQuality = preset.effectiveShellQuality;
    }

    bool SetProperty<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Notify(propertyName);
        return true;
    }

    void Notify(string propertyName)
    {
        var bindingId = new BindingId(propertyName);
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(in bindingId));
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
    NaabLikeCalculatorViewModel viewModel;

    readonly List<NaabLikeBallisticsResult> results = new();
    readonly List<NaabLikeResultRow> tableRows = new();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public VisualElement BuildContent(VisualTreeAsset template = null)
    {
        viewModel = new NaabLikeCalculatorViewModel(ArmorPresets, ProjectilePresets);
#if UNITY_EDITOR
        template ??= UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UIDocuments/NavalCombat/NaabLikeCalculatorDialog.uxml");
#endif
        var root = template != null ? template.CloneTree() : BuildMissingTemplateContent();
        root.dataSource = viewModel;
        var calculatorRoot = root.Q<VisualElement>("NaabLikeCalculatorRoot");
        if (calculatorRoot != null)
            calculatorRoot.dataSource = viewModel;
        root.style.position = Position.Relative;
        root.style.flexGrow = 1;
        root.style.flexShrink = 1;
        contentRoot = root;

        ConfigureDropdown(root.Q<DropdownField>("ElevationModeField"), new List<string> { Localize("Range"), Localize("Search Fix"), Localize("Search SK5") }, viewModel.elevationModeIndex);
        ConfigureDropdown(root.Q<DropdownField>("PlotModeField"), new List<string> { Localize("None"), Localize("Trajectories"), Localize("Penetration") }, viewModel.plotModeIndex);
        ConfigureDropdown(root.Q<DropdownField>("ArmorPresetField"), ArmorPresets.Select(preset => Localize(preset.name)).ToList(), viewModel.armorPresetIndex);
        ConfigureDropdown(root.Q<DropdownField>("ProjectilePresetField"), ProjectilePresets.Select(preset => preset.projectile.name).ToList(), viewModel.projectilePresetIndex);
        ConfigureDropdown(root.Q<DropdownField>("CapTypeField"), GetCapTypeLabels(), viewModel.capTypeIndex);
        ConfigureDropdown(root.Q<DropdownField>("DragFunctionField"), GetDragFunctionLabels(), viewModel.dragFunctionIndex);

        rangeElevationRows = root.Q<VisualElement>("RangeElevationRows");
        searchFixRows = root.Q<VisualElement>("SearchFixRows");
        progressOverlay = root.Q<VisualElement>("ProgressOverlay");
        progressBar = root.Q<ProgressBar>("ProgressBar");
        progressLabel = root.Q<Label>("ProgressLabel");
        calculateButton = root.Q<Button>("CalculateButton");
        statusLabel = root.Q<Label>("StatusLabel");
        chartContainer = root.Q<VisualElement>("ChartContainer");
        trajectoryChart = root.Q<NaabLikeTrajectoryChart>("TrajectoryChart");
        penetrationChart = root.Q<NaabLikePenetrationChart>("PenetrationChart");
        resultListView = root.Q<MultiColumnListView>("NaabLikeCalculatorResultListView");

        if (calculateButton != null)
            calculateButton.clicked += Calculate;
        ConfigureResultListView();
        viewModel.propertyChanged += OnViewModelPropertyChanged;
        ApplyViewState();
        RefreshOutputs();
        return root;
    }

    static VisualElement BuildMissingTemplateContent()
    {
        var root = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
                minHeight = 160
            }
        };
        root.Add(new Label(Localize("NAAB-like Calculator UXML is not configured.")));
        return root;
    }

    static void ConfigureDropdown(DropdownField field, List<string> choices, int index)
    {
        if (field == null)
            return;
        field.choices = choices;
        field.index = Math.Clamp(index, choices.Count > 0 ? 0 : -1, choices.Count - 1);
    }

    void ConfigureResultListView()
    {
        if (resultListView == null)
            return;

        resultListView.selectionType = SelectionType.None;
        resultListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
        resultListView.columns.Clear();

        void AddColumn(string name, string title, int width, Func<NaabLikeResultRow, string> selector)
        {
            resultListView.columns.Add(new Column
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
    }

    void ApplyViewState()
    {
        if (viewModel == null)
            return;
        if (rangeElevationRows != null)
            rangeElevationRows.style.display = viewModel.rangeElevationDisplay;
        if (searchFixRows != null)
            searchFixRows.style.display = viewModel.searchFixDisplay;
        if (progressOverlay != null)
            progressOverlay.style.display = viewModel.progressOverlayDisplay;
        if (chartContainer != null)
            chartContainer.style.display = viewModel.chartDisplay;
        if (trajectoryChart != null)
            trajectoryChart.style.display = viewModel.trajectoryChartDisplay;
        if (penetrationChart != null)
            penetrationChart.style.display = viewModel.penetrationChartDisplay;
        if (calculateButton != null)
            calculateButton.SetEnabled(viewModel.calculateEnabled);
        if (statusLabel != null)
            statusLabel.text = viewModel.statusText;
        if (progressBar != null)
        {
            progressBar.value = viewModel.progressValue;
            progressBar.title = viewModel.progressTitle;
        }
        if (progressLabel != null)
            progressLabel.text = viewModel.progressText;
    }

    void OnViewModelPropertyChanged(object sender, BindablePropertyChangedEventArgs args)
    {
        ApplyViewState();
        if (args.propertyName.ToString() == nameof(NaabLikeCalculatorViewModel.plotModeIndex))
            RefreshOutputs();
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
            viewModel.statusText = cachedDataError;
            RefreshOutputs();
            return;
        }

        var validationError = ValidateInputs(out var projectile, out var armor);
        if (validationError != null)
        {
            viewModel.statusText = validationError;
            RefreshOutputs();
            return;
        }

        var dragTable = data.dragTables[projectile.dragFunction];
        var dxFeet = viewModel.integrationStep;
        var exterior = new NaabLikeExteriorBallisticsSolver(dragTable, projectile, dxFeet);
        var terminal = new NaabLikeTerminalBallisticsSolver(data.terminalTables, projectile, armor);
        var elevationMode = GetElevationMode();
        var elevationSamples = elevationMode == NaabLikeElevationMode.Range ? GetElevationSamples() : null;
        var targetRanges = elevationMode == NaabLikeElevationMode.Range ? null : GetTargetRanges(projectile);
        var totalRows = elevationMode == NaabLikeElevationMode.Range ? elevationSamples.Count : targetRanges.Count;

        if (totalRows <= 0)
        {
            viewModel.statusText = Localize("{0} result(s).", 0);
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
            viewModel.statusText = job.externalFailures > 0
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
        viewModel.progressVisible = true;
        viewModel.calculateEnabled = false;
        UpdateProgressDialog(completedRows, totalRows);
    }

    void UpdateProgressDialog(int completedRows, int totalRows)
    {
        var progress = totalRows <= 0 ? 1f : Mathf.Clamp01((float)completedRows / totalRows);
        viewModel.progressValue = progress;
        viewModel.progressTitle = $"{progress:P0}";
        viewModel.progressText = Localize("{0}/{1} rows", completedRows, totalRows);
    }

    void HideProgressDialog()
    {
        viewModel.progressVisible = false;
        viewModel.calculateEnabled = true;
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

        if (viewModel.armorQuality <= 0f)
            return Localize("Quality must be greater than 0.");
        if (viewModel.armorElongation <= 0f)
            return Localize("Elongation must be greater than 0.");
        if (viewModel.armorBhn <= 0f)
            return Localize("BHN must be greater than 0.");
        if (viewModel.diameter <= 0f)
            return Localize("Projectile diameter must be greater than 0.");
        if (viewModel.totalWeight <= 0f)
            return Localize("Projectile mass must be greater than 0.");
        if (viewModel.bodyWeight < 0f || viewModel.windscreen < 0f || viewModel.apCapWeight < 0f)
            return Localize("Projectile component weights must be 0 or greater.");
        if (viewModel.bodyWeight + viewModel.windscreen + viewModel.apCapWeight > viewModel.totalWeight + 0.001f)
            return Localize("Projectile component weights must not exceed total weight.");
        if (viewModel.windscreenNblAddendMultiplier < 0f || viewModel.highObliquityWindscreenNblAddendMultiplier < 0f)
            return Localize("Windscreen NBL addend multipliers must be 0 or greater.");
        if (viewModel.highObliquityThreshold < 0f)
            return Localize("High-obliquity threshold must be 0 or greater.");
        if (viewModel.muzzleVelocity <= 0f)
            return Localize("Muzzle velocity must be greater than 0.");
        if (viewModel.maxRange <= 0f)
            return Localize("Max range must be greater than 0.");
        if (viewModel.ballisticCoefficient <= 0f)
            return Localize("Ballistic coefficient must be greater than 0.");
        if (viewModel.projectileMaxElevation <= 0f)
            return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
        if (viewModel.effectiveShellQuality <= 0f)
            return Localize("Effective shell quality must be greater than 0.");
        if (viewModel.integrationStep <= 0f)
            return Localize("Integration step must be greater than 0.");

        if (GetElevationMode() == NaabLikeElevationMode.Range)
        {
            if (viewModel.startElevation <= 0f || viewModel.startElevation >= 90f || viewModel.endElevation <= 0f || viewModel.endElevation >= 90f)
                return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
            if (viewModel.elevationStep <= 0f)
                return Localize("Elevation step must be greater than 0.");
            if (viewModel.startElevation > viewModel.endElevation)
                return Localize("Start elevation must be less than or equal to end elevation.");
            if (GetElevationSamples().Count > MaxElevationSamples)
                return Localize("Too many elevation samples. Increase the step or narrow the range.");
        }
        else if (GetElevationMode() == NaabLikeElevationMode.SearchFix && viewModel.searchRangeStep <= 0f)
        {
            return Localize("Range step must be greater than 0.");
        }

        var preset = ProjectilePresets[Math.Clamp(viewModel.projectilePresetIndex, 0, ProjectilePresets.Count - 1)].projectile;
        projectile = preset.Clone();
        projectile.diameterInches = viewModel.diameter;
        projectile.totalWeightPounds = viewModel.totalWeight;
        projectile.bodyWeightPounds = viewModel.bodyWeight;
        projectile.windscreenWeightPounds = viewModel.windscreen;
        projectile.apCapWeightPounds = viewModel.apCapWeight;
        projectile.hcwclcrCapType = GetHcwclcrCapType();
        projectile.windscreenNblAddendMultiplier = viewModel.windscreenNblAddendMultiplier;
        projectile.highObliquityWindscreenNblAddendMultiplier = viewModel.highObliquityWindscreenNblAddendMultiplier;
        projectile.highObliquityThresholdDeg = viewModel.highObliquityThreshold;
        projectile.muzzleVelocityFeetPerSecond = viewModel.muzzleVelocity;
        projectile.maxRangeYards = viewModel.maxRange;
        projectile.dragFunction = GetDragFunction();
        projectile.ballisticCoefficient = viewModel.ballisticCoefficient;
        projectile.dragCoefficientAdjust = viewModel.dragCoefficient;
        projectile.maxElevationDeg = viewModel.projectileMaxElevation;
        projectile.effectiveShellQuality = viewModel.effectiveShellQuality;

        armor = new NaabLikeArmorInput
        {
            quality = viewModel.armorQuality,
            elongationPercent = viewModel.armorElongation,
            bhn = viewModel.armorBhn,
            inclinedDeg = viewModel.armorInclined
        };
        return null;
    }

    List<float> GetElevationSamples()
    {
        var samples = new List<float>();
        var start = viewModel.startElevation;
        var end = viewModel.endElevation;
        var step = viewModel.elevationStep;
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
        var step = viewModel.searchRangeStep;
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
        viewModel.chartVisible = hasChartData;
        viewModel.trajectoryChartVisible = hasChartData && plotMode == NaabLikePlotMode.Trajectories;
        viewModel.penetrationChartVisible = hasChartData && plotMode == NaabLikePlotMode.Penetration;
        trajectoryChart?.SetResults(hasChartData && plotMode == NaabLikePlotMode.Trajectories ? SelectTrajectoryResults(successful) : Enumerable.Empty<NaabLikeBallisticsResult>());
        penetrationChart?.SetRows(hasChartData && plotMode == NaabLikePlotMode.Penetration ? successful : Enumerable.Empty<NaabLikeBallisticsResult>());
    }

    IEnumerable<NaabLikeBallisticsResult> SelectTrajectoryResults(List<NaabLikeBallisticsResult> successful)
    {
        if (successful.Count <= 3)
            return successful;
        return new[] { successful.First(), successful[successful.Count / 2], successful.Last() }.Distinct().ToList();
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
        return viewModel.elevationModeIndex switch
        {
            0 => NaabLikeElevationMode.Range,
            1 => NaabLikeElevationMode.SearchFix,
            _ => NaabLikeElevationMode.SearchSk5
        };
    }

    NaabLikePlotMode GetPlotMode()
    {
        return viewModel.plotModeIndex switch
        {
            0 => NaabLikePlotMode.None,
            2 => NaabLikePlotMode.Penetration,
            _ => NaabLikePlotMode.Trajectories
        };
    }

    int GetHcwclcrCapType()
    {
        return Math.Clamp(viewModel.capTypeIndex, 0, 4);
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
        return viewModel.dragFunctionIndex switch
        {
            0 => NaabLikeDragFunction.G1,
            1 => NaabLikeDragFunction.G2,
            3 => NaabLikeDragFunction.G6,
            4 => NaabLikeDragFunction.G7,
            5 => NaabLikeDragFunction.G8,
            6 => NaabLikeDragFunction.G9,
            7 => NaabLikeDragFunction.GS,
            8 => NaabLikeDragFunction.GL,
            _ => NaabLikeDragFunction.G5
        };
    }

    public static int GetDragFunctionIndex(NaabLikeDragFunction dragFunction)
    {
        return dragFunction switch
        {
            NaabLikeDragFunction.G1 => 0,
            NaabLikeDragFunction.G2 => 1,
            NaabLikeDragFunction.G6 => 3,
            NaabLikeDragFunction.G7 => 4,
            NaabLikeDragFunction.G8 => 5,
            NaabLikeDragFunction.G9 => 6,
            NaabLikeDragFunction.GS => 7,
            NaabLikeDragFunction.GL => 8,
            _ => 2,
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

}
