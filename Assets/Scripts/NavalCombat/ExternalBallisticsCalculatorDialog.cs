using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using NavalCombatCore;
using YYZ;

public enum ExternalBallisticsCalculatorMode
{
    Single,
    Multiple
}

public enum ExternalBallisticsPlotAxis
{
    ElevationAngle,
    Range,
    TimeOfFlight,
    ImpactVelocity,
    AngleOfFall
}

sealed class ExternalBallisticsTableRow
{
    public ExternalBallisticsResult result;
    public string elevationAngle;
    public string range;
    public string timeOfFlight;
    public string impactVelocity;
    public string angleOfFall;
}

[UxmlElement]
public partial class ExternalBallisticsTrajectoryChart : VisualElement
{
    const float LeftPadding = 54f;
    const float RightPadding = 18f;
    const float TopPadding = 18f;
    const float BottomPadding = 34f;

    readonly VisualElement labelLayer = new();
    List<ExternalBallisticsResult> results = new();

    static readonly Color[] SeriesColors =
    {
        new(0.75f, 0.18f, 0.14f, 1f),
        new(0.14f, 0.46f, 0.78f, 1f),
        new(0.1f, 0.55f, 0.22f, 1f)
    };

    static readonly Color AxisColor = new(0.78f, 0.82f, 0.86f, 1f);
    static readonly Color GridColor = new(0.78f, 0.82f, 0.86f, 0.16f);

    public ExternalBallisticsTrajectoryChart()
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

    public void SetResults(IEnumerable<ExternalBallisticsResult> newResults)
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

        var maxRange = Mathf.Max(1f, results.Max(result => result.rangeMeters));
        var maxHeight = Mathf.Max(1f, results.SelectMany(result => result.trajectory).Max(point => point.yMeters));

        for (int i = 0; i < results.Count; i++)
        {
            DrawTrajectory(painter, chartRect, results[i], maxRange, maxHeight, SeriesColors[i % SeriesColors.Length]);
        }
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

    void DrawTrajectory(Painter2D painter, Rect chartRect, ExternalBallisticsResult result, float maxRange, float maxHeight, Color color)
    {
        painter.strokeColor = color;
        painter.lineWidth = 2f;
        painter.BeginPath();

        var started = false;
        foreach (var point in result.trajectory)
        {
            var mapped = MapPoint(chartRect, point.xMeters, point.yMeters, maxRange, maxHeight);
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

        var maxRange = results.Count > 0 ? Mathf.Max(1f, results.Max(result => result.rangeMeters)) : 1f;
        var maxHeight = results.Count > 0 ? Mathf.Max(1f, results.SelectMany(result => result.trajectory).Max(point => point.yMeters)) : 1f;

        labelLayer.Add(BuildLabel("y m", 2f, chartRect.yMin - 6f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel("x yd", chartRect.xMax - 52f, chartRect.yMax + 8f, 70f, 18f, TextAnchor.UpperLeft));
        labelLayer.Add(BuildLabel(ExternalBallisticsSolver.MetersToYards(maxRange).ToString("0"), chartRect.xMax - 45f, chartRect.yMax + 8f, 50f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(maxHeight.ToString("0"), 2f, chartRect.yMin + 10f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));

        for (int i = 0; i < results.Count; i++)
        {
            labelLayer.Add(BuildLabel(
                $"{results[i].elevationAngleDeg:0.##} deg",
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

    static Vector2 MapPoint(Rect chartRect, float xMeters, float yMeters, float maxRange, float maxHeight)
    {
        return new Vector2(
            Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(0f, maxRange, xMeters)),
            Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(0f, maxHeight, yMeters)));
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
public partial class ExternalBallisticsScatterChart : VisualElement
{
    const float LeftPadding = 58f;
    const float RightPadding = 18f;
    const float TopPadding = 18f;
    const float BottomPadding = 40f;

    readonly VisualElement labelLayer = new();
    readonly List<Vector2> points = new();
    string xLabel = "";
    string yLabel = "";

    static readonly Color AxisColor = new(0.78f, 0.82f, 0.86f, 1f);
    static readonly Color GridColor = new(0.78f, 0.82f, 0.86f, 0.16f);
    static readonly Color SeriesColor = new(0.15f, 0.42f, 0.78f, 1f);

    public ExternalBallisticsScatterChart()
    {
        style.flexGrow = 1;
        style.minHeight = 180;
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

    public void SetPoints(IEnumerable<Vector2> newPoints, string newXLabel, string newYLabel)
    {
        points.Clear();
        points.AddRange(newPoints.Where(point => IsFinite(point.x) && IsFinite(point.y)));
        xLabel = newXLabel ?? "";
        yLabel = newYLabel ?? "";
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
        DrawAxes(painter, chartRect);

        if (points.Count == 0)
            return;

        var bounds = GetBounds();
        painter.strokeColor = SeriesColor;
        painter.fillColor = SeriesColor;
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
            var mapped = MapPoint(chartRect, point, bounds);
            painter.BeginPath();
            painter.Arc(mapped, 3f, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
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

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var bounds = GetBounds();
        labelLayer.Add(BuildLabel(yLabel, 2f, chartRect.yMin - 8f, LeftPadding - 8f, 24f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel(xLabel, chartRect.xMax - 120f, chartRect.yMax + 14f, 138f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxX.ToString("0.##"), chartRect.xMax - 64f, chartRect.yMax + 2f, 64f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxY.ToString("0.##"), 2f, chartRect.yMin + 10f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));
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
        if (points.Count == 0)
            return (0f, 1f, 0f, 1f);

        var minX = points.Min(point => point.x);
        var maxX = points.Max(point => point.x);
        var minY = points.Min(point => point.y);
        var maxY = points.Max(point => point.y);

        if (Mathf.Approximately(minX, maxX))
        {
            minX -= 1f;
            maxX += 1f;
        }
        if (Mathf.Approximately(minY, maxY))
        {
            minY -= 1f;
            maxY += 1f;
        }

        return (minX, maxX, minY, maxY);
    }

    static Vector2 MapPoint(Rect chartRect, Vector2 point, (float minX, float maxX, float minY, float maxY) bounds)
    {
        return new Vector2(
            Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(bounds.minX, bounds.maxX, point.x)),
            Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(bounds.minY, bounds.maxY, point.y)));
    }

    static Label BuildLabel(string text, float left, float top, float width, float height, TextAnchor align)
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
        label.style.color = AxisColor;
        return label;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class ExternalBallisticsCalculatorDialog
{
    const int MaxMultipleAngles = 121;

    DropdownField modeField;
    FloatField muzzleVelocityField;
    FloatField singleElevationField;
    FloatField minElevationField;
    FloatField maxElevationField;
    FloatField elevationStepField;
    FloatField diameterInchField;
    FloatField massKgField;
    DropdownField dragInputModeField;
    FloatField ballisticCoefficientField;
    DropdownField dragModelField;
    FloatField constantDragCoefficientField;
    FloatField airDensityField;
    FloatField timeStepField;
    DropdownField xAxisField;
    DropdownField yAxisField;
    Label massPoundsLabel;
    Label statusLabel;
    VisualElement singleAngleRow;
    VisualElement multipleAngleRows;
    VisualElement gModelRows;
    VisualElement physicalCdRows;
    VisualElement secondaryPlotContainer;
    MultiColumnListView resultListView;
    ExternalBallisticsTrajectoryChart trajectoryChart;
    ExternalBallisticsScatterChart scatterChart;

    readonly List<ExternalBallisticsResult> results = new();
    readonly List<ExternalBallisticsTableRow> tableRows = new();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public VisualElement BuildContent()
    {
        var tabView = new TabView
        {
            name = "CalculatorTabView",
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };

        var tab = new Tab
        {
            label = Localize("External Ballistics (外弹道学)"),
            style =
            {
                flexGrow = 1
            }
        };
        tab.Add(BuildExternalBallisticsTab());
        tabView.Add(tab);

        Calculate();
        return tabView;
    }

    VisualElement BuildExternalBallisticsTab()
    {
        var root = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };

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
                flexBasis = 310,
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
        root.Add(BuildSectionLabel(Localize("Inputs")));

        modeField = new DropdownField(Localize("Mode"), new List<string> { Localize("Single"), Localize("Multiple") }, 0);
        root.Add(modeField);

        muzzleVelocityField = BuildFloatField(Localize("Muzzle Velocity (m/s)"), 730f);
        root.Add(muzzleVelocityField);

        singleAngleRow = new VisualElement();
        singleElevationField = BuildFloatField(Localize("Elevation Angle (deg)"), 15f);
        singleAngleRow.Add(singleElevationField);
        root.Add(singleAngleRow);

        multipleAngleRows = new VisualElement();
        minElevationField = BuildFloatField(Localize("Min Elevation (deg)"), 5f);
        maxElevationField = BuildFloatField(Localize("Max Elevation (deg)"), 20f);
        elevationStepField = BuildFloatField(Localize("Elevation Step (deg)"), 5f);
        multipleAngleRows.Add(minElevationField);
        multipleAngleRows.Add(maxElevationField);
        multipleAngleRows.Add(elevationStepField);
        root.Add(multipleAngleRows);

        root.Add(BuildSectionLabel(Localize("Model Parameters")));

        diameterInchField = BuildFloatField(Localize("Projectile Diameter (inch)"), 12f);
        massKgField = BuildFloatField(Localize("Projectile Mass (kg)"), 386f);
        massPoundsLabel = new Label();
        massPoundsLabel.style.marginLeft = 3;
        massPoundsLabel.style.marginBottom = 4;
        root.Add(diameterInchField);
        root.Add(massKgField);
        root.Add(massPoundsLabel);

        dragInputModeField = new DropdownField(Localize("Drag Input Mode"), new List<string> { Localize("G Model BC"), Localize("Physical Cd") }, 0);
        root.Add(dragInputModeField);

        gModelRows = new VisualElement();
        ballisticCoefficientField = BuildFloatField(Localize("Ballistic Coefficient"), 0.5f);
        dragModelField = new DropdownField(Localize("Drag Model"), new List<string> { "G1", "G7" }, 0);
        gModelRows.Add(ballisticCoefficientField);
        gModelRows.Add(dragModelField);
        root.Add(gModelRows);

        physicalCdRows = new VisualElement();
        constantDragCoefficientField = BuildFloatField(Localize("Constant Cd"), 0.3f);
        physicalCdRows.Add(constantDragCoefficientField);
        root.Add(physicalCdRows);

        airDensityField = BuildFloatField(Localize("Air Density (kg/m3)"), 1.225f);
        root.Add(airDensityField);

        root.Add(BuildSectionLabel(Localize("Algorithm Parameters")));

        timeStepField = BuildFloatField(Localize("Time Step (s)"), 0.02f);
        root.Add(timeStepField);

        var calculateButton = new Button(Calculate)
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

        RegisterInputCallbacks();
        UpdateModeVisibility();
        UpdateDragInputModeVisibility();
        UpdateMassHelper();
    }

    void BuildOutputPanel(VisualElement root)
    {
        trajectoryChart = new ExternalBallisticsTrajectoryChart
        {
            style =
            {
                flexBasis = 230,
                flexShrink = 0,
                marginBottom = 6
            }
        };
        root.Add(trajectoryChart);

        resultListView = BuildResultListView();
        root.Add(resultListView);

        secondaryPlotContainer = new VisualElement
        {
            style =
            {
                flexShrink = 0,
                marginTop = 6
            }
        };

        var axisRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexShrink = 0
            }
        };
        xAxisField = new DropdownField(Localize("X Axis"), GetAxisLabels(), 0);
        yAxisField = new DropdownField(Localize("Y Axis"), GetAxisLabels(), 1);
        xAxisField.style.flexGrow = 1;
        yAxisField.style.flexGrow = 1;
        yAxisField.style.marginLeft = 6;
        axisRow.Add(xAxisField);
        axisRow.Add(yAxisField);
        secondaryPlotContainer.Add(axisRow);

        scatterChart = new ExternalBallisticsScatterChart
        {
            style =
            {
                height = 190,
                marginTop = 4
            }
        };
        secondaryPlotContainer.Add(scatterChart);
        root.Add(secondaryPlotContainer);

        xAxisField.RegisterValueChangedCallback(_ => RefreshSecondaryPlot());
        yAxisField.RegisterValueChangedCallback(_ => RefreshSecondaryPlot());
    }

    MultiColumnListView BuildResultListView()
    {
        var listView = new MultiColumnListView
        {
            name = "ExternalBallisticsResultListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
                minHeight = 150
            }
        };

        void AddColumn(string name, string title, int width, Func<ExternalBallisticsTableRow, string> selector)
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

        AddColumn("elevation", Localize("Elevation"), 90, row => row.elevationAngle);
        AddColumn("range", Localize("Range"), 150, row => row.range);
        AddColumn("time", Localize("Time of Flight"), 120, row => row.timeOfFlight);
        AddColumn("velocity", Localize("Impact Velocity"), 130, row => row.impactVelocity);
        AddColumn("fall", Localize("Angle of Fall"), 120, row => row.angleOfFall);

        return listView;
    }

    void RegisterInputCallbacks()
    {
        modeField.RegisterValueChangedCallback(_ =>
        {
            UpdateModeVisibility();
            Calculate();
        });

        foreach (var field in new[]
        {
            muzzleVelocityField,
            singleElevationField,
            minElevationField,
            maxElevationField,
            elevationStepField,
            diameterInchField,
            massKgField,
            ballisticCoefficientField,
            constantDragCoefficientField,
            airDensityField,
            timeStepField
        })
        {
            field.RegisterValueChangedCallback(_ =>
            {
                UpdateMassHelper();
                Calculate();
            });
        }

        dragInputModeField.RegisterValueChangedCallback(_ =>
        {
            UpdateDragInputModeVisibility();
            Calculate();
        });
        dragModelField.RegisterValueChangedCallback(_ => Calculate());
    }

    void Calculate()
    {
        if (resultListView == null)
            return;

        results.Clear();
        tableRows.Clear();

        var validationError = ValidateInputs(out var angles);
        if (validationError != null)
        {
            statusLabel.text = validationError;
            RefreshOutputs();
            return;
        }

        foreach (var angle in angles)
        {
            var result = ExternalBallisticsSolver.Solve(BuildSolverInput(angle));
            results.Add(result);
            if (!result.success)
            {
                statusLabel.text = Localize("Calculation failed: {0}", result.failureReason);
                continue;
            }

            tableRows.Add(BuildRow(result));
        }

        statusLabel.text = Localize("{0} result(s).", tableRows.Count);
        RefreshOutputs();
    }

    string ValidateInputs(out List<float> angles)
    {
        angles = new List<float>();

        if (muzzleVelocityField.value <= 0f)
            return Localize("Muzzle velocity must be greater than 0.");
        if (GetDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd && diameterInchField.value <= 0f)
            return Localize("Projectile diameter must be greater than 0.");
        if (GetDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd && massKgField.value <= 0f)
            return Localize("Projectile mass must be greater than 0.");
        if (GetDragInputMode() == ExternalBallisticsDragInputMode.GModelBallisticCoefficient && ballisticCoefficientField.value <= 0f)
            return Localize("Ballistic coefficient must be greater than 0.");
        if (GetDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd && constantDragCoefficientField.value <= 0f)
            return Localize("Drag coefficient must be greater than 0.");
        if (airDensityField.value <= 0f)
            return Localize("Air density must be greater than 0.");
        if (timeStepField.value <= 0f)
            return Localize("Time step must be greater than 0.");

        if (GetMode() == ExternalBallisticsCalculatorMode.Single)
        {
            if (!IsPracticalElevation(singleElevationField.value))
                return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
            angles.Add(singleElevationField.value);
            return null;
        }

        var minAngle = minElevationField.value;
        var maxAngle = maxElevationField.value;
        var step = elevationStepField.value;
        if (!IsPracticalElevation(minAngle) || !IsPracticalElevation(maxAngle))
            return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
        if (step <= 0f)
            return Localize("Elevation step must be greater than 0.");
        if (minAngle > maxAngle)
            return Localize("Min elevation must be less than or equal to max elevation.");

        var count = Mathf.FloorToInt((maxAngle - minAngle) / step) + 1;
        if (count > MaxMultipleAngles)
            return Localize("Too many elevation samples. Increase the step or narrow the range.");

        for (int i = 0; i < count; i++)
        {
            var angle = minAngle + step * i;
            if (angle <= maxAngle + 0.0001f)
                angles.Add(angle);
        }

        return angles.Count == 0 ? Localize("No elevation angle is available.") : null;
    }

    ExternalBallisticsInput BuildSolverInput(float angleDeg)
    {
        return new ExternalBallisticsInput
        {
            muzzleVelocityMetersPerSecond = muzzleVelocityField.value,
            elevationAngleDeg = angleDeg,
            dragInputMode = GetDragInputMode(),
            projectileDiameterMeters = ExternalBallisticsSolver.InchesToMeters(diameterInchField.value),
            projectileMassKg = massKgField.value,
            ballisticCoefficient = ballisticCoefficientField.value,
            dragModel = dragModelField.index == 1 ? ExternalBallisticsDragModel.G7 : ExternalBallisticsDragModel.G1,
            constantDragCoefficient = constantDragCoefficientField.value,
            airDensityKgPerCubicMeter = airDensityField.value,
            timeStepSeconds = timeStepField.value
        };
    }

    ExternalBallisticsTableRow BuildRow(ExternalBallisticsResult result)
    {
        return new ExternalBallisticsTableRow
        {
            result = result,
            elevationAngle = $"{result.elevationAngleDeg:0.###} deg",
            range = $"{ExternalBallisticsSolver.MetersToYards(result.rangeMeters):0} yd / {result.rangeMeters:0} m",
            timeOfFlight = $"{result.timeOfFlightSeconds:0.00} s",
            impactVelocity = $"{result.impactVelocityMetersPerSecond:0.0} m/s",
            angleOfFall = $"{result.angleOfFallDeg:0.00} deg"
        };
    }

    void RefreshOutputs()
    {
        var successfulResults = results.Where(result => result.success).ToList();
        resultListView.itemsSource = tableRows;
        resultListView.Rebuild();

        trajectoryChart.SetResults(GetTrajectoryResultsForCurrentMode(successfulResults));
        secondaryPlotContainer.style.display = GetMode() == ExternalBallisticsCalculatorMode.Multiple
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        RefreshSecondaryPlot();
    }

    IEnumerable<ExternalBallisticsResult> GetTrajectoryResultsForCurrentMode(List<ExternalBallisticsResult> successfulResults)
    {
        if (successfulResults.Count == 0)
            return Enumerable.Empty<ExternalBallisticsResult>();
        if (GetMode() == ExternalBallisticsCalculatorMode.Single || successfulResults.Count <= 3)
            return successfulResults;

        var min = successfulResults.First();
        var middle = successfulResults[successfulResults.Count / 2];
        var max = successfulResults.Last();
        return new[] { min, middle, max }.Distinct().ToList();
    }

    void RefreshSecondaryPlot()
    {
        if (scatterChart == null)
            return;

        if (GetMode() != ExternalBallisticsCalculatorMode.Multiple)
        {
            scatterChart.SetPoints(Enumerable.Empty<Vector2>(), "", "");
            return;
        }

        var xAxis = GetAxis(xAxisField.index);
        var yAxis = GetAxis(yAxisField.index);
        var points = results
            .Where(result => result.success)
            .Select(result => new Vector2(GetAxisValue(result, xAxis), GetAxisValue(result, yAxis)))
            .ToList();

        scatterChart.SetPoints(points, GetAxisLabel(xAxis), GetAxisLabel(yAxis));
    }

    void UpdateModeVisibility()
    {
        if (singleAngleRow == null || multipleAngleRows == null)
            return;

        var isSingle = GetMode() == ExternalBallisticsCalculatorMode.Single;
        singleAngleRow.style.display = isSingle ? DisplayStyle.Flex : DisplayStyle.None;
        multipleAngleRows.style.display = isSingle ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void UpdateDragInputModeVisibility()
    {
        if (gModelRows == null || physicalCdRows == null)
            return;

        var isPhysicalCd = GetDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd;
        gModelRows.style.display = isPhysicalCd ? DisplayStyle.None : DisplayStyle.Flex;
        physicalCdRows.style.display = isPhysicalCd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void UpdateMassHelper()
    {
        if (massPoundsLabel == null)
            return;

        massPoundsLabel.text = Localize("Approx. {0:0} lb", ExternalBallisticsSolver.KilogramsToPounds(massKgField.value));
    }

    ExternalBallisticsCalculatorMode GetMode()
    {
        return modeField != null && modeField.index == 1
            ? ExternalBallisticsCalculatorMode.Multiple
            : ExternalBallisticsCalculatorMode.Single;
    }

    ExternalBallisticsDragInputMode GetDragInputMode()
    {
        return dragInputModeField != null && dragInputModeField.index == 1
            ? ExternalBallisticsDragInputMode.PhysicalCd
            : ExternalBallisticsDragInputMode.GModelBallisticCoefficient;
    }

    static bool IsPracticalElevation(float angle) => angle > 0f && angle < 90f;

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

    static List<string> GetAxisLabels()
    {
        return new List<string>
        {
            GetAxisLabel(ExternalBallisticsPlotAxis.ElevationAngle),
            GetAxisLabel(ExternalBallisticsPlotAxis.Range),
            GetAxisLabel(ExternalBallisticsPlotAxis.TimeOfFlight),
            GetAxisLabel(ExternalBallisticsPlotAxis.ImpactVelocity),
            GetAxisLabel(ExternalBallisticsPlotAxis.AngleOfFall)
        };
    }

    static ExternalBallisticsPlotAxis GetAxis(int index)
    {
        return index switch
        {
            1 => ExternalBallisticsPlotAxis.Range,
            2 => ExternalBallisticsPlotAxis.TimeOfFlight,
            3 => ExternalBallisticsPlotAxis.ImpactVelocity,
            4 => ExternalBallisticsPlotAxis.AngleOfFall,
            _ => ExternalBallisticsPlotAxis.ElevationAngle
        };
    }

    static string GetAxisLabel(ExternalBallisticsPlotAxis axis)
    {
        return axis switch
        {
            ExternalBallisticsPlotAxis.Range => Localize("Range (yd)"),
            ExternalBallisticsPlotAxis.TimeOfFlight => Localize("Time of Flight (s)"),
            ExternalBallisticsPlotAxis.ImpactVelocity => Localize("Impact Velocity (m/s)"),
            ExternalBallisticsPlotAxis.AngleOfFall => Localize("Angle of Fall (deg)"),
            _ => Localize("Elevation Angle (deg)")
        };
    }

    static float GetAxisValue(ExternalBallisticsResult result, ExternalBallisticsPlotAxis axis)
    {
        return axis switch
        {
            ExternalBallisticsPlotAxis.Range => ExternalBallisticsSolver.MetersToYards(result.rangeMeters),
            ExternalBallisticsPlotAxis.TimeOfFlight => result.timeOfFlightSeconds,
            ExternalBallisticsPlotAxis.ImpactVelocity => result.impactVelocityMetersPerSecond,
            ExternalBallisticsPlotAxis.AngleOfFall => result.angleOfFallDeg,
            _ => result.elevationAngleDeg
        };
    }
}
