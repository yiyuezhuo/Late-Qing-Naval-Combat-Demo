using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

public enum NaabLikeFitMode
{
    ExternalBallistic,
    TerminalBallistic
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
    public string rangeBandComparison;
}

public sealed class NaabLikeCalculatorLaunchContext
{
    public BatteryRecord sourceBatteryRecord;
    public bool applyProjectileToBallistic;
    public float rangeYards;
    public float maxRateOfFireShootPerMin;
    public float shellSizeInch;
    public float shellWeightPounds;
    public List<PenetrationTableRecord> penetrationTableRecords = new();
    public NaabLikeProjectile projectile;

    public static NaabLikeCalculatorLaunchContext FromBatteryRecord(BatteryRecord batteryRecord, bool applyProjectileToBallistic)
    {
        return new NaabLikeCalculatorLaunchContext
        {
            sourceBatteryRecord = batteryRecord,
            applyProjectileToBallistic = applyProjectileToBallistic,
            rangeYards = batteryRecord?.rangeYards ?? 0f,
            maxRateOfFireShootPerMin = batteryRecord?.maxRateOfFireShootPerMin ?? 0f,
            shellSizeInch = batteryRecord?.shellSizeInch ?? 0f,
            shellWeightPounds = batteryRecord?.shellWeightPounds ?? 0f,
            penetrationTableRecords = ClonePenetrationTableRecords(batteryRecord?.penetrationTableRecords),
            projectile = batteryRecord?.metaInfo?.naabLikeProjectile?.Clone()
        };
    }

    static List<PenetrationTableRecord> ClonePenetrationTableRecords(IEnumerable<PenetrationTableRecord> records)
    {
        return (records ?? Enumerable.Empty<PenetrationTableRecord>())
            .Select(record => new PenetrationTableRecord
            {
                distanceYards = record.distanceYards,
                rateOfFire = record.rateOfFire,
                rangeBand = record.rangeBand,
                horizontalPenetrationInchs = record.horizontalPenetrationInchs,
                verticalPenetrationInchs = record.verticalPenetrationInchs
            })
            .ToList();
    }
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
    bool _fitControlsVisible;

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
    float _sk5RangeYards;
    float _sk5MaxRateOfFireShootPerMin;
    float _sk5ShellSizeInch;
    float _sk5ShellWeightPounds;

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
    [CreateProperty] public List<PenetrationTableRecord> sk5PenetrationTableRecords { get; } = new();

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
    [CreateProperty] public float sk5RangeYards { get => _sk5RangeYards; set => SetProperty(ref _sk5RangeYards, value, nameof(sk5RangeYards)); }
    [CreateProperty] public float sk5MaxRateOfFireShootPerMin { get => _sk5MaxRateOfFireShootPerMin; set => SetProperty(ref _sk5MaxRateOfFireShootPerMin, value, nameof(sk5MaxRateOfFireShootPerMin)); }
    [CreateProperty] public float sk5ShellSizeInch { get => _sk5ShellSizeInch; set => SetProperty(ref _sk5ShellSizeInch, value, nameof(sk5ShellSizeInch)); }
    [CreateProperty] public float sk5ShellWeightPounds { get => _sk5ShellWeightPounds; set => SetProperty(ref _sk5ShellWeightPounds, value, nameof(sk5ShellWeightPounds)); }

    [CreateProperty] public string statusText { get => _statusText; set => SetProperty(ref _statusText, value, nameof(statusText)); }
    [CreateProperty] public bool calculateEnabled { get => _calculateEnabled; set => SetProperty(ref _calculateEnabled, value, nameof(calculateEnabled)); }
    [CreateProperty] public bool progressVisible { get => _progressVisible; set { if (SetProperty(ref _progressVisible, value, nameof(progressVisible))) Notify(nameof(progressOverlayDisplay)); } }
    [CreateProperty] public float progressValue { get => _progressValue; set => SetProperty(ref _progressValue, value, nameof(progressValue)); }
    [CreateProperty] public string progressTitle { get => _progressTitle; set => SetProperty(ref _progressTitle, value, nameof(progressTitle)); }
    [CreateProperty] public string progressText { get => _progressText; set => SetProperty(ref _progressText, value, nameof(progressText)); }
    [CreateProperty] public bool chartVisible { get => _chartVisible; set { if (SetProperty(ref _chartVisible, value, nameof(chartVisible))) Notify(nameof(chartDisplay)); } }
    [CreateProperty] public bool trajectoryChartVisible { get => _trajectoryChartVisible; set { if (SetProperty(ref _trajectoryChartVisible, value, nameof(trajectoryChartVisible))) Notify(nameof(trajectoryChartDisplay)); } }
    [CreateProperty] public bool penetrationChartVisible { get => _penetrationChartVisible; set { if (SetProperty(ref _penetrationChartVisible, value, nameof(penetrationChartVisible))) Notify(nameof(penetrationChartDisplay)); } }
    [CreateProperty] public bool fitControlsVisible { get => _fitControlsVisible; set { if (SetProperty(ref _fitControlsVisible, value, nameof(fitControlsVisible))) Notify(nameof(fitControlsDisplay)); } }

    [CreateProperty] public DisplayStyle rangeElevationDisplay => elevationModeIndex == (int)NaabLikeElevationMode.Range ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle searchFixDisplay => elevationModeIndex == (int)NaabLikeElevationMode.SearchFix ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle progressOverlayDisplay => progressVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle chartDisplay => chartVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle trajectoryChartDisplay => trajectoryChartVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle penetrationChartDisplay => penetrationChartVisible ? DisplayStyle.Flex : DisplayStyle.None;
    [CreateProperty] public DisplayStyle fitControlsDisplay => fitControlsVisible ? DisplayStyle.Flex : DisplayStyle.None;

    void ApplyArmorPreset(NaabLikeArmorPreset preset)
    {
        if (preset == null)
            return;
        armorQuality = preset.quality;
        armorElongation = preset.elongationPercent;
        armorBhn = preset.bhn;
    }

    public void ApplyProjectilePreset(NaabLikeProjectile preset)
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

    public void ApplySk5Data(NaabLikeCalculatorLaunchContext context)
    {
        if (context == null)
            return;
        sk5RangeYards = context.rangeYards;
        sk5MaxRateOfFireShootPerMin = context.maxRateOfFireShootPerMin;
        sk5ShellSizeInch = context.shellSizeInch;
        sk5ShellWeightPounds = context.shellWeightPounds;
        sk5PenetrationTableRecords.Clear();
        sk5PenetrationTableRecords.AddRange(context.penetrationTableRecords ?? new());
        if (context.applyProjectileToBallistic && context.projectile != null)
            ApplyProjectilePreset(context.projectile);
    }

    public NaabLikeProjectile BuildProjectile(NaabLikeProjectile preset, NaabLikeDragFunction dragFunction, int capType)
    {
        var projectile = (preset ?? new NaabLikeProjectile()).Clone();
        projectile.diameterInches = diameter;
        projectile.totalWeightPounds = totalWeight;
        projectile.bodyWeightPounds = bodyWeight;
        projectile.windscreenWeightPounds = windscreen;
        projectile.apCapWeightPounds = apCapWeight;
        projectile.hcwclcrCapType = capType;
        projectile.windscreenNblAddendMultiplier = windscreenNblAddendMultiplier;
        projectile.highObliquityWindscreenNblAddendMultiplier = highObliquityWindscreenNblAddendMultiplier;
        projectile.highObliquityThresholdDeg = highObliquityThreshold;
        projectile.muzzleVelocityFeetPerSecond = muzzleVelocity;
        projectile.maxRangeYards = maxRange;
        projectile.dragFunction = dragFunction;
        projectile.ballisticCoefficient = ballisticCoefficient;
        projectile.dragCoefficientAdjust = dragCoefficient;
        projectile.maxElevationDeg = projectileMaxElevation;
        projectile.effectiveShellQuality = effectiveShellQuality;
        return projectile;
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
        new NaabLikeArmorPreset { name = "Avg. U.S WWI-era class \"B\" armor", quality = 0.95f, elongationPercent = 22f, bhn = 235f },
        new NaabLikeArmorPreset { name = "Avg. U.S WWII-era class \"B\" armor", quality = 1f, elongationPercent = 25f, bhn = 235f },
    };

    static readonly List<NaabLikeProjectilePreset> ProjectilePresets = new()
    {
        // new NaabLikeProjectilePreset
        // {
        //     projectile = new NaabLikeProjectile
        //     {
        //         name = "Chilled Cast Iron Shot (US) 5''",
        //         diameterInches = 5f,
        //         totalWeightPounds = 50f,
        //         bodyWeightPounds = 50f,
        //         windscreenWeightPounds = 0f,
        //         apCapWeightPounds = 0f,
        //         hcwclcrCapType = 0,
        //         windscreenNblAddendMultiplier = 0.75f,
        //         highObliquityWindscreenNblAddendMultiplier = 0.1f,
        //         highObliquityThresholdDeg = 0f,
        //         muzzleVelocityFeetPerSecond = 3000f,
        //         maxRangeYards = 22600f,
        //         dragFunction = NaabLikeDragFunction.G5,
        //         ballisticCoefficient = 1.9307f,
        //         dragCoefficientAdjust = 14f,
        //         maxElevationDeg = 20f,
        //         effectiveShellQuality = 0.575f
        //     },
        // },
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "Palliser Chilled Cast Iron Shot (GB) 6''",
                diameterInches = 6f,
                totalWeightPounds = 100f,
                bodyWeightPounds = 100f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 2230f,
                maxRangeYards = 14600f,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 2.9727f,
                dragCoefficientAdjust = 0f,
                maxElevationDeg = 20f,
                effectiveShellQuality = 0.575f
            },
        },
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "Palliser Chilled Cast Iron Shot (GB) 10''",
                diameterInches = 10f,
                totalWeightPounds = 500f,
                bodyWeightPounds = 500f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 2040f,
                maxRangeYards = 11000f,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 5.1846f,
                dragCoefficientAdjust = 0f,
                maxElevationDeg = 12.1f,
                effectiveShellQuality = 0.575f
            },
        },
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "Steel AP Shot/Shell (GB) 7.5''",
                diameterInches = 7.5f,
                totalWeightPounds = 200f,
                bodyWeightPounds = 200f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 2827f,
                maxRangeYards = 14328f,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 2.489f,
                dragCoefficientAdjust = 0f,
                maxElevationDeg = 15f,
                effectiveShellQuality = 0.679f
            },
        },
        new NaabLikeProjectilePreset
        {
            projectile = new NaabLikeProjectile
            {
                name = "Steel AP Shot/Shell (GB) 12''",
                diameterInches = 12f,
                totalWeightPounds = 714f,
                bodyWeightPounds = 714f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 1914f,
                maxRangeYards = 9450f,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 5.0293f,
                dragCoefficientAdjust = 0f,
                maxElevationDeg = 12.5f,
                effectiveShellQuality = 0.679f
            },
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
        public Task<List<NaabLikeBallisticsResult>> parallelTargetTask;
        public List<NaabLikeBallisticsResult> parallelTargetResults;
        public int parallelCompletedRows;
    }

    sealed class NaabLikeFitCandidate
    {
        public float ballisticCoefficient;
        public float dragCoefficient;
        public float shellQuality;
    }

    sealed class NaabLikeFitJob
    {
        public NaabLikeFitMode mode;
        public NaabLikeBallisticsData data;
        public NaabLikeProjectile projectile;
        public NaabLikeArmorInput armor;
        public List<PenetrationTableRecord> records = new();
        public readonly List<(PenetrationTableRecord record, NaabLikeBallisticsResult result)> impacts = new();
        public readonly List<NaabLikeFitCandidate> candidates = new();

        public int pass;
        public int candidateIndex;
        public int processedCandidates;
        public int totalCandidates;
        public int impactIndex;
        public float? impactAngleHint;
        public bool buildingImpacts;
        public bool pauseRequested;
        public bool cancelRequested;

        public float originalBallisticCoefficient;
        public float originalDragCoefficient;
        public float originalMaxRange;
        public float originalShellQuality;

        public float bestBallisticCoefficient;
        public float bestDragCoefficient;
        public float bestShellQuality;
        public float bestScore = float.PositiveInfinity;
        public float currentScore = float.PositiveInfinity;
        public float currentBallisticCoefficient;
        public float currentDragCoefficient;
        public float currentShellQuality;
        public string currentDetail = "";

        public float bcSpan = 0.75f;
        public float dragSpan = 20f;
        public float qualitySpan = 0.5f;
    }

    readonly struct ExteriorScore
    {
        public readonly float score;
        public readonly int rangeBandMismatchCount;
        public readonly float maxRangeErrorYards;

        public ExteriorScore(float score, int rangeBandMismatchCount, float maxRangeErrorYards)
        {
            this.score = score;
            this.rangeBandMismatchCount = rangeBandMismatchCount;
            this.maxRangeErrorYards = maxRangeErrorYards;
        }
    }

    static NaabLikeBallisticsData cachedData;
    static string cachedDataError;

    VisualElement rangeElevationRows;
    VisualElement searchFixRows;
    VisualElement contentRoot;
    VisualElement progressOverlay;
    VisualElement fitProgressControls;
    ProgressBar progressBar;
    Label progressLabel;
    Label progressTitleLabel;
    Button calculateButton;
    Button fitExternalButton;
    Button fitTerminalButton;
    Button fitPauseButton;
    Button fitCancelButton;
    Button syncBackButton;
    Label statusLabel;
    VisualElement chartContainer;
    NaabLikeTrajectoryChart trajectoryChart;
    NaabLikePenetrationChart penetrationChart;
    MultiColumnListView resultListView;
    MultiColumnListView sk5DataListView;
    IVisualElementScheduledItem calculationSchedule;
    IVisualElementScheduledItem fitSchedule;
    NaabLikeCalculationJob currentJob;
    NaabLikeFitJob currentFitJob;
    NaabLikeCalculatorViewModel viewModel;
    NaabLikeCalculatorLaunchContext launchContext;

    readonly List<NaabLikeBallisticsResult> results = new();
    readonly List<NaabLikeResultRow> tableRows = new();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public NaabLikeCalculatorDialog(NaabLikeCalculatorLaunchContext launchContext = null)
    {
        this.launchContext = launchContext;
    }

    public VisualElement BuildContent(VisualTreeAsset template = null)
    {
        viewModel = new NaabLikeCalculatorViewModel(ArmorPresets, ProjectilePresets);
        viewModel.ApplySk5Data(launchContext);
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
        ConfigureDropdown(root.Q<DropdownField>("ArmorPresetField"), ArmorPresets.Select(preset => preset.name).ToList(), viewModel.armorPresetIndex);
        ConfigureDropdown(root.Q<DropdownField>("ProjectilePresetField"), ProjectilePresets.Select(preset => preset.projectile.name).ToList(), viewModel.projectilePresetIndex);
        ConfigureDropdown(root.Q<DropdownField>("CapTypeField"), GetCapTypeLabels(), viewModel.capTypeIndex);
        ConfigureDropdown(root.Q<DropdownField>("DragFunctionField"), GetDragFunctionLabels(), viewModel.dragFunctionIndex);

        rangeElevationRows = root.Q<VisualElement>("RangeElevationRows");
        searchFixRows = root.Q<VisualElement>("SearchFixRows");
        progressOverlay = root.Q<VisualElement>("ProgressOverlay");
        fitProgressControls = root.Q<VisualElement>("FitProgressControls");
        progressBar = root.Q<ProgressBar>("ProgressBar");
        progressLabel = root.Q<Label>("ProgressLabel");
        progressTitleLabel = root.Q<Label>("ProgressTitleLabel");
        calculateButton = root.Q<Button>("CalculateButton");
        fitExternalButton = root.Q<Button>("Sk5FitExternalBallisticButton");
        fitTerminalButton = root.Q<Button>("Sk5FitTerminalBallisticButton");
        fitPauseButton = root.Q<Button>("FitPauseButton");
        fitCancelButton = root.Q<Button>("FitCancelButton");
        syncBackButton = root.Q<Button>("Sk5SyncBackButton");
        statusLabel = root.Q<Label>("StatusLabel");
        chartContainer = root.Q<VisualElement>("ChartContainer");
        trajectoryChart = root.Q<NaabLikeTrajectoryChart>("TrajectoryChart");
        penetrationChart = root.Q<NaabLikePenetrationChart>("PenetrationChart");
        resultListView = root.Q<MultiColumnListView>("NaabLikeCalculatorResultListView");
        sk5DataListView = root.Q<MultiColumnListView>("Sk5PenetrationTableListView");

        if (calculateButton != null)
            calculateButton.clicked += Calculate;
        if (fitExternalButton != null)
            fitExternalButton.clicked += StartExternalFit;
        if (fitTerminalButton != null)
            fitTerminalButton.clicked += StartTerminalFit;
        if (fitPauseButton != null)
            fitPauseButton.clicked += PauseCurrentFit;
        if (fitCancelButton != null)
            fitCancelButton.clicked += CancelCurrentFit;
        if (syncBackButton != null)
            syncBackButton.clicked += SyncBackSk5Data;
        ConfigureResultListView();
        ConfigureSk5DataListView();
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
        AddColumn("rangeBand", Localize("Range Band"), 130, row => row.rangeBandComparison);
        AddColumn("velocity", Localize("Impact Velocity"), 130, row => row.impactVelocity);
        AddColumn("fall", Localize("Angle of Fall"), 120, row => row.angleOfFall);
        AddColumn("time", Localize("Time of Flight"), 120, row => row.timeOfFlight);
        AddColumn("elevation", Localize("Elevation"), 90, row => row.elevation);
    }

    void ConfigureSk5DataListView()
    {
        if (sk5DataListView == null)
            return;

        sk5DataListView.itemsSource = viewModel.sk5PenetrationTableRecords;
        sk5DataListView.selectionType = SelectionType.Single;
        sk5DataListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
        sk5DataListView.showAddRemoveFooter = true;
        sk5DataListView.columns.Clear();

        void AddFloatColumn(string name, string title, int width, Func<PenetrationTableRecord, float> getter, Action<PenetrationTableRecord, float> setter)
        {
            sk5DataListView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 80),
                stretchable = false,
                makeCell = () =>
                {
                    var field = new FloatField();
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (field.userData is PenetrationTableRecord record)
                            setter(record, evt.newValue);
                    });
                    return field;
                },
                bindCell = (element, index) =>
                {
                    if (element is not FloatField field)
                        return;
                    var row = GetSk5Row(index);
                    field.userData = row;
                    field.SetValueWithoutNotify(row == null ? 0f : getter(row));
                }
            });
        }

        sk5DataListView.columns.Add(new Column
        {
            name = "rangeBand",
            title = Localize("Range Band"),
            width = 120,
            minWidth = 90,
            stretchable = false,
            makeCell = () =>
            {
                var field = new EnumField(RangeBand.Short);
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData is PenetrationTableRecord record && evt.newValue is RangeBand rangeBand)
                        record.rangeBand = rangeBand;
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                if (element is not EnumField field)
                    return;
                var row = GetSk5Row(index);
                field.userData = row;
                field.SetValueWithoutNotify(row?.rangeBand ?? RangeBand.Short);
            }
        });

        AddFloatColumn("distanceYards", Localize("Distance Yards"), 130, row => row.distanceYards, (row, value) => row.distanceYards = value);
        AddFloatColumn("rateOfFire", Localize("Rate of Fire"), 120, row => row.rateOfFire, (row, value) => row.rateOfFire = value);
        AddFloatColumn("horizontalPenetration", Localize("Hor Pen"), 110, row => row.horizontalPenetrationInchs, (row, value) => row.horizontalPenetrationInchs = value);
        AddFloatColumn("verticalPenetration", Localize("Vert Pen"), 110, row => row.verticalPenetrationInchs, (row, value) => row.verticalPenetrationInchs = value);

        sk5DataListView.itemsAdded += indexes =>
        {
            foreach (var index in indexes)
            {
                if (index >= 0 && index < viewModel.sk5PenetrationTableRecords.Count && viewModel.sk5PenetrationTableRecords[index] == null)
                    viewModel.sk5PenetrationTableRecords[index] = new PenetrationTableRecord();
            }
            sk5DataListView.Rebuild();
        };

        sk5DataListView.itemsRemoved += _ => sk5DataListView.Rebuild();
        sk5DataListView.Rebuild();
    }

    PenetrationTableRecord GetSk5Row(int index)
    {
        return index >= 0 && index < viewModel.sk5PenetrationTableRecords.Count
            ? viewModel.sk5PenetrationTableRecords[index]
            : null;
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
            calculateButton.SetEnabled(viewModel.calculateEnabled && currentFitJob == null);
        if (fitExternalButton != null)
            fitExternalButton.SetEnabled(currentJob == null && currentFitJob == null);
        if (fitTerminalButton != null)
            fitTerminalButton.SetEnabled(currentJob == null && currentFitJob == null);
        if (syncBackButton != null)
            syncBackButton.SetEnabled(launchContext?.sourceBatteryRecord != null && currentJob == null && currentFitJob == null);
        if (fitProgressControls != null)
            fitProgressControls.style.display = viewModel.fitControlsDisplay;
        if (progressTitleLabel != null)
            progressTitleLabel.text = viewModel.fitControlsVisible ? "Fitting..." : Localize("Calculating...");
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
        if (resultListView == null || currentJob != null || currentFitJob != null)
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
        if (elevationMode != NaabLikeElevationMode.Range)
        {
            currentJob.parallelTargetTask = Task.Run(() => exterior.SolveForTargetRangesParallel(
                targetRanges,
                MathF.Max(projectile.maxElevationDeg, 45f),
                8,
                (completed, _) => Volatile.Write(ref currentJob.parallelCompletedRows, completed)));
        }
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
        if (job.elevationMode != NaabLikeElevationMode.Range)
        {
            ProcessParallelTargetCalculationStep(job);
            return;
        }

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

    void ProcessParallelTargetCalculationStep(NaabLikeCalculationJob job)
    {
        if (job.parallelTargetResults == null)
        {
            if (job.parallelTargetTask == null)
            {
                FinishCalculationJob();
                return;
            }

            if (!job.parallelTargetTask.IsCompleted)
            {
                UpdateProgressDialog(Volatile.Read(ref job.parallelCompletedRows), job.totalRows);
                return;
            }

            if (job.parallelTargetTask.IsFaulted)
            {
                calculationSchedule?.Pause();
                calculationSchedule = null;
                currentJob = null;
                HideProgressDialog();
                viewModel.statusText = job.parallelTargetTask.Exception?.GetBaseException().Message ?? "Parallel target range calculation failed.";
                RefreshOutputs();
                return;
            }

            job.parallelTargetResults = job.parallelTargetTask.Result;
            for (int i = 0; i < job.parallelTargetResults.Count; i++)
                AddResult(job.parallelTargetResults[i], job.terminal, job.armor, ref job.externalFailures);
            job.completedRows = job.totalRows;
            UpdateProgressDialog(job.completedRows, job.totalRows);
            FinishCalculationJob();
            return;
        }
    }

    void FinishCalculationJob()
    {
        var job = currentJob;
        calculationSchedule?.Pause();
        calculationSchedule = null;
        currentJob = null;
        HideProgressDialog();
        viewModel.statusText = job != null && job.externalFailures > 0
            ? Localize("{0} result(s), {1} external ballistic failure(s).", tableRows.Count, job.externalFailures)
            : Localize("{0} result(s).", tableRows.Count);
        RefreshOutputs();
    }

    void ShowProgressDialog(int completedRows, int totalRows)
    {
        viewModel.progressVisible = true;
        viewModel.fitControlsVisible = false;
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
        viewModel.fitControlsVisible = false;
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
        projectile = viewModel.BuildProjectile(preset, GetDragFunction(), GetHcwclcrCapType());

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
        {
            var sk5Ranges = viewModel.sk5PenetrationTableRecords
                .Where(record => record.distanceYards > 0f && record.distanceYards <= projectile.maxRangeYards + 0.001f)
                .Select(record => record.distanceYards)
                .Distinct()
                .OrderBy(range => range)
                .ToList();
            return sk5Ranges.Count > 0
                ? sk5Ranges
                : Sk5RangesYards.Where(range => range <= projectile.maxRangeYards + 0.001f).ToList();
        }

        var ranges = new List<float>();
        var step = viewModel.searchRangeStep;
        for (var range = step; range <= projectile.maxRangeYards + 0.001f; range += step)
            ranges.Add(range);
        return ranges;
    }

    NaabLikeResultRow BuildRow(NaabLikeBallisticsResult result)
    {
        var sk5Record = FindSk5Record(result.rangeYards);
        var simulatedBand = Sk5RangeBandRules.FromAngleOfFallDeg(result.angleOfFallDeg);
        return new NaabLikeResultRow
        {
            result = result,
            elevation = $"{result.elevationDeg:0.###} deg",
            range = $"{result.rangeYards:0} yd",
            horizontalPenetration = FormatPenetrationComparison(result.horizontalPenetrationInches, sk5Record?.horizontalPenetrationInchs),
            verticalPenetration = FormatPenetrationComparison(result.verticalPenetrationInches, sk5Record?.verticalPenetrationInchs),
            timeOfFlight = $"{result.timeOfFlightSeconds:0.00} s",
            impactVelocity = $"{result.impactVelocityFeetPerSecond:0} ft/s",
            angleOfFall = $"{result.angleOfFallDeg:0.00} deg",
            rangeBandComparison = sk5Record == null ? simulatedBand.ToString() : $"{simulatedBand}/{sk5Record.rangeBand}"
        };
    }

    PenetrationTableRecord FindSk5Record(float rangeYards)
    {
        if (GetElevationMode() != NaabLikeElevationMode.SearchSk5 || viewModel.sk5PenetrationTableRecords.Count == 0)
            return null;
        return viewModel.sk5PenetrationTableRecords
            .Where(record => record.distanceYards > 0f)
            .OrderBy(record => MathF.Abs(record.distanceYards - rangeYards))
            .FirstOrDefault(record => MathF.Abs(record.distanceYards - rangeYards) <= 0.5f);
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

    void SyncBackSk5Data()
    {
        var batteryRecord = launchContext?.sourceBatteryRecord;
        if (batteryRecord == null)
        {
            viewModel.statusText = Localize("No source Battery Record is attached.");
            return;
        }

        batteryRecord.rangeYards = viewModel.sk5RangeYards;
        batteryRecord.maxRateOfFireShootPerMin = viewModel.sk5MaxRateOfFireShootPerMin;
        batteryRecord.shellSizeInch = viewModel.sk5ShellSizeInch;
        batteryRecord.shellWeightPounds = viewModel.sk5ShellWeightPounds;
        batteryRecord.penetrationTableRecords ??= new List<PenetrationTableRecord>();
        batteryRecord.penetrationTableRecords.Clear();
        batteryRecord.penetrationTableRecords.AddRange(ClonePenetrationRecords(viewModel.sk5PenetrationTableRecords));
        batteryRecord.metaInfo ??= new BatteryRecordMetaInfo();
        batteryRecord.metaInfo.naabLikeProjectile = BuildProjectileFromViewModel();
        viewModel.statusText = Localize("Synced SK5 data back to Battery Record.");
    }

    void StartExternalFit()
    {
        StartFit(NaabLikeFitMode.ExternalBallistic);
    }

    void StartTerminalFit()
    {
        StartFit(NaabLikeFitMode.TerminalBallistic);
    }

    void StartFit(NaabLikeFitMode mode)
    {
        if (currentJob != null || currentFitJob != null)
            return;

        var setupError = CreateFitJob(mode, out var job);
        if (setupError != null)
        {
            viewModel.statusText = setupError;
            return;
        }

        currentFitJob = job;
        viewModel.progressVisible = true;
        viewModel.fitControlsVisible = true;
        viewModel.calculateEnabled = false;
        PrepareFitPass(job);
        UpdateFitProgress(job, mode == NaabLikeFitMode.ExternalBallistic
            ? "External ballistic fit started."
            : "Terminal ballistic fit started.");
        fitSchedule = contentRoot.schedule.Execute(ProcessFitStep).Every(1);
    }

    string CreateFitJob(NaabLikeFitMode mode, out NaabLikeFitJob job)
    {
        job = null;
        if (viewModel.sk5PenetrationTableRecords.Count == 0)
            return "No valid SK5 rows.";

        var data = LoadData();
        if (data == null)
            return cachedDataError;

        var validationError = ValidateInputs(out var projectile, out var armor);
        if (validationError != null)
            return validationError;

        var fitRecords = viewModel.sk5PenetrationTableRecords
            .Where(record => record.distanceYards > 0f)
            .OrderBy(record => record.distanceYards)
            .ToList();
        if (fitRecords.Count == 0 || viewModel.sk5RangeYards <= 0f)
            return "No valid SK5 rows.";

        projectile.maxRangeYards = mode == NaabLikeFitMode.ExternalBallistic
            ? MathF.Max(viewModel.sk5RangeYards, fitRecords.Max(record => record.distanceYards))
            : viewModel.maxRange;

        job = new NaabLikeFitJob
        {
            mode = mode,
            data = data,
            projectile = projectile,
            armor = armor,
            records = fitRecords,
            originalBallisticCoefficient = viewModel.ballisticCoefficient,
            originalDragCoefficient = viewModel.dragCoefficient,
            originalMaxRange = viewModel.maxRange,
            originalShellQuality = viewModel.effectiveShellQuality,
            bestBallisticCoefficient = MathF.Max(projectile.ballisticCoefficient, 0.01f),
            bestDragCoefficient = projectile.dragCoefficientAdjust,
            bestShellQuality = Math.Clamp(projectile.effectiveShellQuality, 0.2f, 1.2f),
            currentBallisticCoefficient = projectile.ballisticCoefficient,
            currentDragCoefficient = projectile.dragCoefficientAdjust,
            currentShellQuality = projectile.effectiveShellQuality,
            totalCandidates = mode == NaabLikeFitMode.ExternalBallistic
                ? 1 + 4 * 9 * 9
                : fitRecords.Count + 1 + 4 * 13,
            buildingImpacts = mode == NaabLikeFitMode.TerminalBallistic
        };

        return null;
    }

    void ProcessFitStep()
    {
        var job = currentFitJob;
        if (job == null)
        {
            fitSchedule?.Pause();
            fitSchedule = null;
            return;
        }

        if (job.cancelRequested)
        {
            FinishFitJob(false, "Cancelled by user");
            return;
        }

        if (job.pauseRequested)
        {
            FinishFitJob(true, "Paused by user");
            return;
        }

        if (job.mode == NaabLikeFitMode.ExternalBallistic)
            ProcessExternalFitStep(job);
        else
            ProcessTerminalFitStep(job);
    }

    void ProcessExternalFitStep(NaabLikeFitJob job)
    {
        if (job.pass >= 4 && job.candidateIndex >= job.candidates.Count)
        {
            FinishFitJob(true, "Completed");
            return;
        }

        if (job.candidateIndex >= job.candidates.Count)
        {
            job.pass++;
            job.bcSpan *= 0.42f;
            job.dragSpan *= 0.42f;
            if (job.pass >= 4)
            {
                FinishFitJob(true, "Completed");
                return;
            }
            PrepareFitPass(job);
        }

        var candidate = job.candidates[job.candidateIndex++];
        job.currentBallisticCoefficient = candidate.ballisticCoefficient;
        job.currentDragCoefficient = candidate.dragCoefficient;
        var detail = ScoreExterior(job.data, job.projectile, candidate.ballisticCoefficient, candidate.dragCoefficient, job.records);
        job.currentScore = detail.score;
        job.currentDetail = $"mismatch {detail.rangeBandMismatchCount}, max range error {detail.maxRangeErrorYards:0} yd";
        job.processedCandidates++;
        if (detail.score < job.bestScore)
        {
            job.bestScore = detail.score;
            job.bestBallisticCoefficient = candidate.ballisticCoefficient;
            job.bestDragCoefficient = candidate.dragCoefficient;
        }
        UpdateFitProgress(job, BuildExternalDiagnostic(job));
    }

    void ProcessTerminalFitStep(NaabLikeFitJob job)
    {
        if (job.buildingImpacts)
        {
            if (job.impactIndex < job.records.Count)
            {
                var exterior = new NaabLikeExteriorBallisticsSolver(job.data.dragTables[job.projectile.dragFunction], job.projectile, viewModel.integrationStep);
                var record = job.records[job.impactIndex];
                var result = exterior.SolveForTargetRange(record.distanceYards, MathF.Max(job.projectile.maxElevationDeg, 45f), job.impactAngleHint);
                if (result.success)
                {
                    job.impactAngleHint = result.elevationDeg;
                    job.impacts.Add((record, result));
                }
                job.impactIndex++;
                job.processedCandidates++;
                UpdateFitProgress(job, $"Building impact rows: {job.impactIndex}/{job.records.Count}, valid {job.impacts.Count}");
                return;
            }

            if (job.impacts.Count == 0)
            {
                FinishFitJob(false, "No valid impact rows");
                return;
            }

            job.buildingImpacts = false;
            job.bestScore = ScoreShellQuality(job.data, job.projectile, job.armor, job.impacts, job.bestShellQuality);
            PrepareFitPass(job);
            UpdateFitProgress(job, $"Impact rows ready: {job.impacts.Count}");
            return;
        }

        if (job.pass >= 4 && job.candidateIndex >= job.candidates.Count)
        {
            FinishFitJob(true, "Completed");
            return;
        }

        if (job.candidateIndex >= job.candidates.Count)
        {
            job.pass++;
            job.qualitySpan *= 0.35f;
            if (job.pass >= 4)
            {
                FinishFitJob(true, "Completed");
                return;
            }
            PrepareFitPass(job);
        }

        var candidate = job.candidates[job.candidateIndex++];
        job.currentShellQuality = candidate.shellQuality;
        var score = ScoreShellQuality(job.data, job.projectile, job.armor, job.impacts, candidate.shellQuality);
        job.currentScore = score;
        job.currentDetail = $"valid impacts {job.impacts.Count}";
        job.processedCandidates++;
        if (score < job.bestScore)
        {
            job.bestScore = score;
            job.bestShellQuality = candidate.shellQuality;
        }
        UpdateFitProgress(job, BuildTerminalDiagnostic(job));
    }

    void PrepareFitPass(NaabLikeFitJob job)
    {
        job.candidates.Clear();
        job.candidateIndex = 0;
        if (job.mode == NaabLikeFitMode.ExternalBallistic)
        {
            for (int bi = -4; bi <= 4; bi++)
            {
                var bc = MathF.Max(0.01f, job.bestBallisticCoefficient * MathF.Pow(1f + job.bcSpan, bi / 4f));
                for (int di = -4; di <= 4; di++)
                {
                    job.candidates.Add(new NaabLikeFitCandidate
                    {
                        ballisticCoefficient = bc,
                        dragCoefficient = job.bestDragCoefficient + job.dragSpan * di / 4f
                    });
                }
            }
        }
        else if (!job.buildingImpacts)
        {
            for (int i = -6; i <= 6; i++)
            {
                job.candidates.Add(new NaabLikeFitCandidate
                {
                    shellQuality = Math.Clamp(job.bestShellQuality + job.qualitySpan * i / 6f, 0.2f, 1.2f)
                });
            }
        }
    }

    void PauseCurrentFit()
    {
        if (currentFitJob != null)
            currentFitJob.pauseRequested = true;
    }

    void CancelCurrentFit()
    {
        if (currentFitJob != null)
            currentFitJob.cancelRequested = true;
    }

    void FinishFitJob(bool applyBest, string reason)
    {
        var job = currentFitJob;
        fitSchedule?.Pause();
        fitSchedule = null;
        currentFitJob = null;

        if (job != null)
        {
            if (applyBest)
            {
                if (job.mode == NaabLikeFitMode.ExternalBallistic)
                {
                    viewModel.ballisticCoefficient = job.bestBallisticCoefficient;
                    viewModel.dragCoefficient = job.bestDragCoefficient;
                    viewModel.maxRange = viewModel.sk5RangeYards;
                }
                else
                {
                    viewModel.effectiveShellQuality = job.bestShellQuality;
                }
            }
            else
            {
                viewModel.ballisticCoefficient = job.originalBallisticCoefficient;
                viewModel.dragCoefficient = job.originalDragCoefficient;
                viewModel.maxRange = job.originalMaxRange;
                viewModel.effectiveShellQuality = job.originalShellQuality;
            }
        }

        viewModel.progressText = reason;
        viewModel.statusText = reason;
        HideProgressDialog();
        ApplyViewState();
        if (applyBest && reason != "No valid impact rows")
            Calculate();
    }

    void UpdateFitProgress(NaabLikeFitJob job, string diagnostic)
    {
        viewModel.progressVisible = true;
        viewModel.fitControlsVisible = true;
        viewModel.calculateEnabled = false;
        var progress = job.totalCandidates <= 0 ? 0f : Mathf.Clamp01((float)job.processedCandidates / job.totalCandidates);
        viewModel.progressValue = progress;
        viewModel.progressTitle = $"{progress:P0}";
        viewModel.progressText = diagnostic;
    }

    string BuildExternalDiagnostic(NaabLikeFitJob job)
    {
        return string.Join("\n", new[]
        {
            "External ballistic fit",
            $"pass {job.pass + 1}/4, candidate {job.candidateIndex}/{job.candidates.Count}",
            $"current BC {job.currentBallisticCoefficient:0.####}, Drag {job.currentDragCoefficient:0.####}, score {job.currentScore:0.####}",
            $"best BC {job.bestBallisticCoefficient:0.####}, Drag {job.bestDragCoefficient:0.####}, score {job.bestScore:0.####}",
            job.currentDetail
        });
    }

    string BuildTerminalDiagnostic(NaabLikeFitJob job)
    {
        return string.Join("\n", new[]
        {
            "Terminal ballistic fit",
            $"pass {job.pass + 1}/4, candidate {job.candidateIndex}/{job.candidates.Count}",
            $"current shell quality {job.currentShellQuality:0.####}, score {job.currentScore:0.####}",
            $"best shell quality {job.bestShellQuality:0.####}, score {job.bestScore:0.####}",
            job.currentDetail
        });
    }

    ExteriorScore ScoreExterior(NaabLikeBallisticsData data, NaabLikeProjectile seed, float ballisticCoefficient, float dragCoefficient, List<PenetrationTableRecord> records)
    {
        var projectile = seed.Clone();
        projectile.ballisticCoefficient = ballisticCoefficient;
        projectile.dragCoefficientAdjust = dragCoefficient;
        var exterior = new NaabLikeExteriorBallisticsSolver(data.dragTables[projectile.dragFunction], projectile, viewModel.integrationStep);
        var score = 0f;
        var mismatchCount = 0;
        var targetRanges = new List<float>(records.Count);
        for (int i = 0; i < records.Count; i++)
            targetRanges.Add(i == records.Count - 1 ? viewModel.sk5RangeYards : records[i].distanceYards);
        var rangeResults = exterior.SolveForTargetRangesParallel(
            targetRanges,
            MathF.Max(projectile.maxElevationDeg, 45f),
            8);

        for (int i = 0; i < records.Count; i++)
        {
            var result = i >= 0 && i < rangeResults.Count ? rangeResults[i] : null;
            if (result?.success == true)
            {
                var predicted = Sk5RangeBandRules.FromAngleOfFallDeg(result.angleOfFallDeg);
                var bandDelta = Math.Abs((int)predicted - (int)records[i].rangeBand);
                if (bandDelta > 0)
                    mismatchCount++;
                score += bandDelta * bandDelta * 10f;
            }
            else
            {
                mismatchCount++;
                score += 100f;
            }
        }

        var maxRangeErrorYards = 0f;
        var maxRangeResult = exterior.SolveToGround(projectile.maxElevationDeg, MathF.Max(viewModel.sk5RangeYards * 2f, viewModel.sk5RangeYards + 1000f), MathF.Max(viewModel.sk5RangeYards / 120f, 100f));
        if (maxRangeResult.success)
        {
            maxRangeErrorYards = maxRangeResult.rangeYards - viewModel.sk5RangeYards;
            var rangeError = maxRangeErrorYards / MathF.Max(viewModel.sk5RangeYards, 1f);
            score += rangeError * rangeError * 250f;
        }
        else
        {
            maxRangeErrorYards = float.PositiveInfinity;
            score += 250f;
        }

        return new ExteriorScore(score, mismatchCount, maxRangeErrorYards);
    }

    float ScoreShellQuality(
        NaabLikeBallisticsData data,
        NaabLikeProjectile seed,
        NaabLikeArmorInput armor,
        List<(PenetrationTableRecord record, NaabLikeBallisticsResult result)> impacts,
        float shellQuality)
    {
        var projectile = seed.Clone();
        projectile.effectiveShellQuality = shellQuality;
        var terminal = new NaabLikeTerminalBallisticsSolver(data.terminalTables, projectile, armor);
        var score = 0f;
        foreach (var impact in impacts)
        {
            var sideObliquityDeg = armor.inclinedDeg + impact.result.angleOfFallDeg;
            var deckObliquityDeg = armor.inclinedDeg + MathF.Max(90f - impact.result.angleOfFallDeg, 0f);
            var vertical = terminal.CompletePenetrationInches(impact.result.impactVelocityFeetPerSecond, sideObliquityDeg);
            var horizontal = terminal.CompletePenetrationInches(impact.result.impactVelocityFeetPerSecond, deckObliquityDeg);
            score += SquaredRelativeError(vertical, impact.record.verticalPenetrationInchs);
            score += SquaredRelativeError(horizontal, impact.record.horizontalPenetrationInchs);
        }
        return score;
    }

    static float SquaredRelativeError(float actual, float expected)
    {
        if (expected <= 0f)
            return actual <= 0f ? 0f : 1f;
        var error = (actual - expected) / expected;
        return error * error;
    }

    NaabLikeProjectile BuildProjectileFromViewModel()
    {
        var preset = ProjectilePresets[Math.Clamp(viewModel.projectilePresetIndex, 0, ProjectilePresets.Count - 1)].projectile;
        return viewModel.BuildProjectile(preset, GetDragFunction(), GetHcwclcrCapType());
    }

    static List<PenetrationTableRecord> ClonePenetrationRecords(IEnumerable<PenetrationTableRecord> records)
    {
        return (records ?? Enumerable.Empty<PenetrationTableRecord>())
            .Select(record => new PenetrationTableRecord
            {
                distanceYards = record.distanceYards,
                rateOfFire = record.rateOfFire,
                rangeBand = record.rangeBand,
                horizontalPenetrationInchs = record.horizontalPenetrationInchs,
                verticalPenetrationInchs = record.verticalPenetrationInchs
            })
            .ToList();
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

    static string FormatPenetrationComparison(float simulatedInches, float? sk5Inches)
    {
        if (!sk5Inches.HasValue)
            return FormatPenetration(simulatedInches);
        var simulated = simulatedInches > 0f ? $"{simulatedInches:0.00}" : "n/a";
        var sk5 = sk5Inches.Value > 0f ? $"{sk5Inches.Value:0.00}" : "n/a";
        return $"{simulated}/{sk5} in";
    }

}
