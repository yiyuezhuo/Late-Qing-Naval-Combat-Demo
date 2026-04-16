using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using NavalCombatCore;
using YYZ;

public enum TerminalBallisticsCalculatorMode
{
    Single,
    Combined
}

sealed class TerminalBallisticsTableRow
{
    public TerminalBallisticsResult result;
    public float? rangeYardsValue;
    public float? timeOfFlightSecondsValue;
    public string range;
    public string timeOfFlight;
    public float elevationAngleDeg;
    public string impactVelocity;
    public string angleOfFall;
    public string horizontalPenetration;
    public string verticalPenetration;
}

sealed class TerminalBallisticsPenetrationTableRow
{
    public float rangeYards;
    public RangeBand rangeBand;
    public float angleOfFallDeg;
    public float timeOfFlightSeconds;
    public float horizontalPenetrationInches;
    public float verticalPenetrationInches;
}

[UxmlElement]
public partial class TerminalBallisticsPenetrationChart : VisualElement
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

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public TerminalBallisticsPenetrationChart()
    {
        style.flexGrow = 1;
        style.minHeight = 210;
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

    internal void SetRows(IEnumerable<TerminalBallisticsTableRow> rows)
    {
        horizontalPoints.Clear();
        verticalPoints.Clear();

        foreach (var row in rows ?? Enumerable.Empty<TerminalBallisticsTableRow>())
        {
            if (row?.rangeYardsValue == null || row.result?.success != true)
                continue;

            var rangeYards = row.rangeYardsValue.Value;
            horizontalPoints.Add(new Vector2(rangeYards, row.result.horizontalPenetrationInches));
            verticalPoints.Add(new Vector2(rangeYards, row.result.verticalPenetrationInches));
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
        painter.lineCap = LineCap.Butt;
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
        labelLayer.Add(BuildLabel(Localize("Pen (in)"), 2f, chartRect.yMin - 8f, LeftPadding - 8f, 24f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel(Localize("Range (yd)"), chartRect.xMax - 120f, chartRect.yMax + 14f, 138f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxX.ToString("0"), chartRect.xMax - 64f, chartRect.yMax + 2f, 64f, 18f, TextAnchor.UpperRight));
        labelLayer.Add(BuildLabel(bounds.maxY.ToString("0.##"), 2f, chartRect.yMin + 10f, LeftPadding - 8f, 18f, TextAnchor.MiddleRight));
        labelLayer.Add(BuildLabel(Localize("Horizontal Pen"), chartRect.xMin + 8f, chartRect.yMin + 4f, 120f, 18f, TextAnchor.UpperLeft, HorizontalColor));
        labelLayer.Add(BuildLabel(Localize("Vertical Pen"), chartRect.xMin + 132f, chartRect.yMin + 4f, 120f, 18f, TextAnchor.UpperLeft, VerticalColor));
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

public sealed partial class ExternalBallisticsCalculatorDialog
{
    DropdownField terminalModeField;
    FloatField terminalProjectileDiameterInchField;
    FloatField terminalProjectileMassKgField;
    FloatField terminalImpactVelocityField;
    FloatField terminalAngleOfFallField;
    FloatField terminalMuzzleVelocityField;
    FloatField terminalMinElevationField;
    FloatField terminalMaxElevationField;
    FloatField terminalElevationStepField;
    DropdownField terminalDragInputModeField;
    FloatField terminalBallisticCoefficientField;
    DropdownField terminalDragModelField;
    FloatField terminalConstantDragCoefficientField;
    FloatField terminalAirDensityField;
    FloatField terminalTimeStepField;
    DropdownField terminalFormulaPresetField;
    FloatField terminalFormulaConstantField;
    FloatField terminalProjectileDiameterExponentField;
    FloatField terminalEnergyDensityExponentField;
    FloatField terminalFormulaCoefficientField;
    FloatField terminalObliquityCosineExponentField;
    Label terminalProjectileDiameterMmLabel;
    Label terminalMassPoundsLabel;
    Label terminalFormulaHelpLabel;
    Label terminalStatusLabel;
    VisualElement terminalSingleRows;
    VisualElement terminalCombinedRows;
    VisualElement terminalGModelRows;
    VisualElement terminalPhysicalCdRows;
    VisualElement terminalPenetrationTableContainer;
    VisualElement terminalPenetrationChartContainer;
    MultiColumnListView terminalPenetrationTableListView;
    MultiColumnListView terminalResultListView;
    TerminalBallisticsPenetrationChart terminalPenetrationChart;

    readonly List<TerminalBallisticsResult> terminalResults = new();
    readonly List<TerminalBallisticsTableRow> terminalTableRows = new();
    readonly List<TerminalBallisticsPenetrationTableRow> terminalPenetrationTableRows = new();
    static readonly float[] TerminalPenetrationTableRangesYards =
    {
        2000f, 4000f, 6000f, 8000f, 10000f, 12000f, 15000f,
        18000f, 21000f, 24000f, 27000f, 30000f, 33000f, 36000f
    };

    VisualElement BuildTerminalBallisticsTab()
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
                flexBasis = 340,
                flexShrink = 0,
                marginRight = 8
            }
        };
        BuildTerminalInputPanel(inputScroll);

        var outputPanel = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        BuildTerminalOutputPanel(outputPanel);

        mainRow.Add(inputScroll);
        mainRow.Add(outputPanel);
        root.Add(mainRow);

        return root;
    }

    void BuildTerminalInputPanel(VisualElement root)
    {
        root.Add(BuildSectionLabel(Localize("Inputs")));

        terminalModeField = new DropdownField(Localize("Mode"), new List<string> { Localize("Single"), Localize("Combined") }, 0);
        root.Add(terminalModeField);

        terminalProjectileDiameterInchField = BuildFloatField(Localize("Projectile Diameter (inch)"), 12f);
        terminalProjectileDiameterMmLabel = new Label();
        terminalProjectileDiameterMmLabel.style.marginLeft = 3;
        terminalProjectileDiameterMmLabel.style.marginBottom = 4;
        terminalProjectileMassKgField = BuildFloatField(Localize("Projectile Mass (kg)"), 386f);
        terminalMassPoundsLabel = new Label();
        terminalMassPoundsLabel.style.marginLeft = 3;
        terminalMassPoundsLabel.style.marginBottom = 4;
        root.Add(terminalProjectileDiameterInchField);
        root.Add(terminalProjectileDiameterMmLabel);
        root.Add(terminalProjectileMassKgField);
        root.Add(terminalMassPoundsLabel);

        terminalSingleRows = new VisualElement();
        terminalImpactVelocityField = BuildFloatField(Localize("Impact Velocity (m/s)"), 500f);
        terminalAngleOfFallField = BuildFloatField(Localize("Angle of Fall (deg)"), 10f);
        terminalSingleRows.Add(terminalImpactVelocityField);
        terminalSingleRows.Add(terminalAngleOfFallField);
        root.Add(terminalSingleRows);

        terminalCombinedRows = new VisualElement();
        terminalCombinedRows.Add(BuildSectionLabel(Localize("External Ballistics Inputs")));
        terminalMuzzleVelocityField = BuildFloatField(Localize("Muzzle Velocity (m/s)"), 730f);
        terminalMinElevationField = BuildFloatField(Localize("Min Elevation (deg)"), 1f);
        terminalMaxElevationField = BuildFloatField(Localize("Max Elevation (deg)"), 20f);
        terminalElevationStepField = BuildFloatField(Localize("Elevation Step (deg)"), 1f);
        terminalCombinedRows.Add(terminalMuzzleVelocityField);
        terminalCombinedRows.Add(terminalMinElevationField);
        terminalCombinedRows.Add(terminalMaxElevationField);
        terminalCombinedRows.Add(terminalElevationStepField);

        terminalDragInputModeField = new DropdownField(Localize("Drag Input Mode"), new List<string> { Localize("G Model BC"), Localize("Physical Cd") }, 0);
        terminalCombinedRows.Add(terminalDragInputModeField);

        terminalGModelRows = new VisualElement();
        terminalBallisticCoefficientField = BuildFloatField(Localize("Ballistic Coefficient"), 0.5f);
        terminalDragModelField = new DropdownField(Localize("Drag Model"), new List<string> { "G1", "G7" }, 0);
        terminalGModelRows.Add(terminalBallisticCoefficientField);
        terminalGModelRows.Add(terminalDragModelField);
        terminalCombinedRows.Add(terminalGModelRows);

        terminalPhysicalCdRows = new VisualElement();
        terminalConstantDragCoefficientField = BuildFloatField(Localize("Constant Cd"), 0.3f);
        terminalPhysicalCdRows.Add(terminalConstantDragCoefficientField);
        terminalCombinedRows.Add(terminalPhysicalCdRows);

        terminalAirDensityField = BuildFloatField(Localize("Air Density (kg/m3)"), 1.225f);
        terminalTimeStepField = BuildFloatField(Localize("Time Step (s)"), 0.02f);
        terminalCombinedRows.Add(terminalAirDensityField);
        terminalCombinedRows.Add(terminalTimeStepField);
        root.Add(terminalCombinedRows);

        root.Add(BuildSectionLabel(Localize("Terminal Ballistics Formula")));

        terminalFormulaPresetField = new DropdownField(Localize("Formula Preset"), GetTerminalFormulaPresetLabels(), 0);
        root.Add(terminalFormulaPresetField);

        terminalFormulaConstantField = BuildFloatField(Localize("Formula Constant"), 0.00005021f);
        terminalProjectileDiameterExponentField = BuildFloatField(Localize("Projectile Diameter Exponent"), 0.07144f);
        terminalEnergyDensityExponentField = BuildFloatField(Localize("Energy Density Exponent"), 0.71429f);
        terminalFormulaCoefficientField = BuildFloatField(Localize("Formula Coefficient C"), 1f);
        terminalObliquityCosineExponentField = BuildFloatField(Localize("Obliquity Cosine Exponent"), 3f);
        root.Add(terminalFormulaConstantField);
        root.Add(terminalProjectileDiameterExponentField);
        root.Add(terminalEnergyDensityExponentField);
        root.Add(terminalFormulaCoefficientField);
        root.Add(terminalObliquityCosineExponentField);

        terminalFormulaHelpLabel = new Label();
        terminalFormulaHelpLabel.style.whiteSpace = WhiteSpace.Normal;
        terminalFormulaHelpLabel.style.marginTop = 4;
        terminalFormulaHelpLabel.style.marginBottom = 4;
        root.Add(terminalFormulaHelpLabel);

        var calculateButton = new Button(CalculateTerminalBallistics)
        {
            text = Localize("Calculate"),
            style =
            {
                marginTop = 8
            }
        };
        root.Add(calculateButton);

        terminalStatusLabel = new Label();
        terminalStatusLabel.style.whiteSpace = WhiteSpace.Normal;
        terminalStatusLabel.style.marginTop = 6;
        root.Add(terminalStatusLabel);

        RegisterTerminalInputCallbacks();
        UpdateTerminalModeVisibility();
        UpdateTerminalDragInputModeVisibility();
        UpdateTerminalHelpers();
        UpdateTerminalFormulaParameterState();
    }

    void BuildTerminalOutputPanel(VisualElement root)
    {
        terminalPenetrationTableContainer = new VisualElement
        {
            style =
            {
                flexShrink = 0,
                marginBottom = 6
            }
        };
        terminalPenetrationTableContainer.Add(new Label(Localize("Penetration"))
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 3
            }
        });
        terminalPenetrationTableListView = BuildTerminalPenetrationTableListView();
        terminalPenetrationTableContainer.Add(terminalPenetrationTableListView);
        root.Add(terminalPenetrationTableContainer);

        terminalResultListView = BuildTerminalResultListView();
        root.Add(terminalResultListView);

        terminalPenetrationChartContainer = new VisualElement
        {
            style =
            {
                flexShrink = 0,
                marginTop = 6
            }
        };
        terminalPenetrationChartContainer.Add(new Label(Localize("Penetration by Range"))
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 3
            }
        });
        terminalPenetrationChart = new TerminalBallisticsPenetrationChart
        {
            style =
            {
                height = 230
            }
        };
        terminalPenetrationChartContainer.Add(terminalPenetrationChart);
        root.Add(terminalPenetrationChartContainer);
    }

    MultiColumnListView BuildTerminalPenetrationTableListView()
    {
        var listView = new MultiColumnListView
        {
            name = "TerminalBallisticsPenetrationTableListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                height = 190,
                flexShrink = 0
            }
        };

        void AddColumn(string name, string title, int width, Func<TerminalBallisticsPenetrationTableRow, string> selector)
        {
            listView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 90),
                stretchable = true,
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
                    var row = index >= 0 && index < terminalPenetrationTableRows.Count ? terminalPenetrationTableRows[index] : null;
                    label.text = row == null ? "" : selector(row);
                }
            });
        }

        AddColumn("range", Localize("Range"), 120, row => $"{row.rangeYards:0} yd");
        AddColumn("timeOfFlight", Localize("Time of Flight"), 130, row => $"{row.timeOfFlightSeconds:0.00} s");
        AddColumn("rangeBand", Localize("Range Band"), 120, row => FormatTerminalRangeBand(row.rangeBand));
        AddColumn("horizontalPenetration", Localize("Horizontal Pen"), 150, row => FormatTerminalPenetration(row.horizontalPenetrationInches));
        AddColumn("verticalPenetration", Localize("Vertical Pen"), 150, row => FormatTerminalPenetration(row.verticalPenetrationInches));

        return listView;
    }

    MultiColumnListView BuildTerminalResultListView()
    {
        var listView = new MultiColumnListView
        {
            name = "TerminalBallisticsResultListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
                minHeight = 170
            }
        };

        RefreshTerminalResultColumns(listView);
        return listView;
    }

    void RefreshTerminalResultColumns(MultiColumnListView listView = null)
    {
        listView ??= terminalResultListView;
        if (listView == null)
            return;

        listView.columns.Clear();

        void AddColumn(string name, string title, int width, Func<TerminalBallisticsTableRow, string> selector)
        {
            listView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 90),
                stretchable = true,
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
                    var row = index >= 0 && index < terminalTableRows.Count ? terminalTableRows[index] : null;
                    label.text = row == null ? "" : selector(row);
                }
            });
        }

        if (GetTerminalMode() == TerminalBallisticsCalculatorMode.Combined)
            AddColumn("range", Localize("Range"), 120, row => row.range);
        if (GetTerminalMode() == TerminalBallisticsCalculatorMode.Combined)
            AddColumn("timeOfFlight", Localize("Time of Flight"), 130, row => row.timeOfFlight);
        AddColumn("impactVelocity", Localize("Impact Velocity"), 160, row => row.impactVelocity);
        AddColumn("angleOfFall", Localize("Angle of Fall"), 140, row => row.angleOfFall);
        AddColumn("horizontalPenetration", Localize("Horizontal Pen"), 160, row => row.horizontalPenetration);
        AddColumn("verticalPenetration", Localize("Vertical Pen"), 160, row => row.verticalPenetration);
    }

    void RegisterTerminalInputCallbacks()
    {
        terminalModeField.RegisterValueChangedCallback(_ =>
        {
            UpdateTerminalModeVisibility();
            CalculateTerminalBallistics();
        });

        foreach (var field in new[]
        {
            terminalProjectileDiameterInchField,
            terminalProjectileMassKgField,
            terminalImpactVelocityField,
            terminalAngleOfFallField,
            terminalMuzzleVelocityField,
            terminalMinElevationField,
            terminalMaxElevationField,
            terminalElevationStepField,
            terminalBallisticCoefficientField,
            terminalConstantDragCoefficientField,
            terminalAirDensityField,
            terminalTimeStepField,
            terminalFormulaConstantField,
            terminalProjectileDiameterExponentField,
            terminalEnergyDensityExponentField,
            terminalFormulaCoefficientField,
            terminalObliquityCosineExponentField
        })
        {
            field.RegisterValueChangedCallback(_ =>
            {
                UpdateTerminalHelpers();
                CalculateTerminalBallistics();
            });
        }

        terminalDragInputModeField.RegisterValueChangedCallback(_ =>
        {
            UpdateTerminalDragInputModeVisibility();
            CalculateTerminalBallistics();
        });
        terminalDragModelField.RegisterValueChangedCallback(_ => CalculateTerminalBallistics());
        terminalFormulaPresetField.RegisterValueChangedCallback(_ =>
        {
            ApplyTerminalFormulaPreset();
            UpdateTerminalFormulaParameterState();
            CalculateTerminalBallistics();
        });
    }

    void CalculateTerminalBallistics()
    {
        if (terminalResultListView == null)
            return;

        terminalResults.Clear();
        terminalTableRows.Clear();
        terminalPenetrationTableRows.Clear();

        var validationError = ValidateTerminalInputs(out var angles);
        if (validationError != null)
        {
            terminalStatusLabel.text = validationError;
            RefreshTerminalOutputs();
            return;
        }

        var externalFailures = 0;
        var terminalFailures = 0;
        if (GetTerminalMode() == TerminalBallisticsCalculatorMode.Single)
        {
            AddTerminalResult(
                BuildTerminalInput(terminalImpactVelocityField.value, terminalAngleOfFallField.value),
                null,
                null,
                0f,
                ref terminalFailures);
        }
        else
        {
            foreach (var angle in angles)
            {
                var externalResult = ExternalBallisticsSolver.Solve(BuildTerminalExternalBallisticsInput(angle));
                if (!externalResult.success)
                {
                    externalFailures++;
                    continue;
                }

                AddTerminalResult(
                    BuildTerminalInput(externalResult.impactVelocityMetersPerSecond, externalResult.angleOfFallDeg),
                    ExternalBallisticsSolver.MetersToYards(externalResult.rangeMeters),
                    externalResult.timeOfFlightSeconds,
                    externalResult.elevationAngleDeg,
                    ref terminalFailures);
            }
        }

        if (externalFailures > 0 || terminalFailures > 0)
            terminalStatusLabel.text = Localize("{0} result(s), {1} external ballistic failure(s), {2} terminal ballistic failure(s).", terminalTableRows.Count, externalFailures, terminalFailures);
        else
            terminalStatusLabel.text = Localize("{0} result(s).", terminalTableRows.Count);
        RefreshTerminalOutputs();
    }

    void AddTerminalResult(TerminalBallisticsInput input, float? rangeYards, float? timeOfFlightSeconds, float elevationAngleDeg, ref int terminalFailures)
    {
        var result = TerminalBallisticsSolver.Solve(input);
        terminalResults.Add(result);
        if (!result.success)
        {
            terminalFailures++;
            return;
        }

        terminalTableRows.Add(BuildTerminalRow(result, rangeYards, timeOfFlightSeconds, elevationAngleDeg));
    }

    string ValidateTerminalInputs(out List<float> angles)
    {
        angles = new List<float>();

        if (!TerminalIsFinitePositive(terminalProjectileDiameterInchField.value))
            return Localize("Projectile diameter must be greater than 0.");
        if (!TerminalIsFinitePositive(terminalProjectileMassKgField.value))
            return Localize("Projectile mass must be greater than 0.");
        if (!TerminalIsFinitePositive(terminalFormulaConstantField.value))
            return Localize("Formula constant must be greater than 0.");
        if (!float.IsFinite(terminalProjectileDiameterExponentField.value))
            return Localize("Projectile diameter exponent must be finite.");
        if (!TerminalIsFinitePositive(terminalEnergyDensityExponentField.value))
            return Localize("Energy density exponent must be greater than 0.");
        if (!TerminalIsFinitePositive(terminalFormulaCoefficientField.value))
            return Localize("Formula coefficient must be greater than 0.");
        if (!float.IsFinite(terminalObliquityCosineExponentField.value) || terminalObliquityCosineExponentField.value < 0f)
            return Localize("Obliquity cosine exponent must be 0 or greater.");

        if (GetTerminalMode() == TerminalBallisticsCalculatorMode.Single)
        {
            if (!TerminalIsFinitePositive(terminalImpactVelocityField.value))
                return Localize("Impact velocity must be greater than 0.");
            if (!IsTerminalAngleOfFall(terminalAngleOfFallField.value))
                return Localize("Angle of fall must be between 0 and 90 degrees.");
            return null;
        }

        if (!TerminalIsFinitePositive(terminalMuzzleVelocityField.value))
            return Localize("Muzzle velocity must be greater than 0.");
        if (GetTerminalDragInputMode() == ExternalBallisticsDragInputMode.GModelBallisticCoefficient && !TerminalIsFinitePositive(terminalBallisticCoefficientField.value))
            return Localize("Ballistic coefficient must be greater than 0.");
        if (GetTerminalDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd && !TerminalIsFinitePositive(terminalConstantDragCoefficientField.value))
            return Localize("Drag coefficient must be greater than 0.");
        if (!TerminalIsFinitePositive(terminalAirDensityField.value))
            return Localize("Air density must be greater than 0.");
        if (!TerminalIsFinitePositive(terminalTimeStepField.value))
            return Localize("Time step must be greater than 0.");

        var minAngle = terminalMinElevationField.value;
        var maxAngle = terminalMaxElevationField.value;
        var step = terminalElevationStepField.value;
        if (!IsPracticalElevation(minAngle) || !IsPracticalElevation(maxAngle))
            return Localize("Elevation angle must be greater than 0 and less than 90 degrees.");
        if (!TerminalIsFinitePositive(step))
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

    TerminalBallisticsInput BuildTerminalInput(float impactVelocityMetersPerSecond, float angleOfFallDeg)
    {
        return new TerminalBallisticsInput
        {
            projectileMassKg = terminalProjectileMassKgField.value,
            projectileDiameterInches = terminalProjectileDiameterInchField.value,
            impactVelocityMetersPerSecond = impactVelocityMetersPerSecond,
            angleOfFallDeg = angleOfFallDeg,
            formulaParameters = BuildTerminalFormulaParameters()
        };
    }

    ExternalBallisticsInput BuildTerminalExternalBallisticsInput(float angleDeg)
    {
        return new ExternalBallisticsInput
        {
            muzzleVelocityMetersPerSecond = terminalMuzzleVelocityField.value,
            elevationAngleDeg = angleDeg,
            dragInputMode = GetTerminalDragInputMode(),
            projectileDiameterMeters = ExternalBallisticsSolver.InchesToMeters(terminalProjectileDiameterInchField.value),
            projectileMassKg = terminalProjectileMassKgField.value,
            ballisticCoefficient = terminalBallisticCoefficientField.value,
            dragModel = terminalDragModelField.index == 1 ? ExternalBallisticsDragModel.G7 : ExternalBallisticsDragModel.G1,
            constantDragCoefficient = terminalConstantDragCoefficientField.value,
            airDensityKgPerCubicMeter = terminalAirDensityField.value,
            timeStepSeconds = terminalTimeStepField.value
        };
    }

    TerminalBallisticsFormulaParameters BuildTerminalFormulaParameters()
    {
        return new TerminalBallisticsFormulaParameters
        {
            preset = GetTerminalFormulaPreset(),
            name = terminalFormulaPresetField?.value ?? "",
            numericalConstant = terminalFormulaConstantField.value,
            projectileDiameterExponent = terminalProjectileDiameterExponentField.value,
            energyDensityExponent = terminalEnergyDensityExponentField.value,
            coefficient = terminalFormulaCoefficientField.value,
            obliquityCosineExponent = terminalObliquityCosineExponentField.value
        };
    }

    TerminalBallisticsTableRow BuildTerminalRow(TerminalBallisticsResult result, float? rangeYards, float? timeOfFlightSeconds, float elevationAngleDeg)
    {
        return new TerminalBallisticsTableRow
        {
            result = result,
            rangeYardsValue = rangeYards,
            timeOfFlightSecondsValue = timeOfFlightSeconds,
            elevationAngleDeg = elevationAngleDeg,
            range = rangeYards.HasValue ? $"{rangeYards.Value:0} yd" : "",
            timeOfFlight = timeOfFlightSeconds.HasValue ? $"{timeOfFlightSeconds.Value:0.00} s" : "",
            impactVelocity = $"{result.impactVelocityMetersPerSecond:0.0} m/s / {TerminalBallisticsSolver.MetersPerSecondToFeetPerSecond(result.impactVelocityMetersPerSecond):0} ft/s",
            angleOfFall = $"{result.angleOfFallDeg:0.00} deg",
            horizontalPenetration = FormatTerminalPenetration(result.horizontalPenetrationInches),
            verticalPenetration = FormatTerminalPenetration(result.verticalPenetrationInches)
        };
    }

    void RefreshTerminalOutputs()
    {
        RebuildTerminalPenetrationTableRows();
        terminalPenetrationTableContainer.style.display = GetTerminalMode() == TerminalBallisticsCalculatorMode.Combined
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        terminalPenetrationTableListView.itemsSource = terminalPenetrationTableRows;
        terminalPenetrationTableListView.Rebuild();

        RefreshTerminalResultColumns();
        terminalResultListView.itemsSource = terminalTableRows;
        terminalResultListView.Rebuild();
        terminalPenetrationChartContainer.style.display = GetTerminalMode() == TerminalBallisticsCalculatorMode.Combined
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        terminalPenetrationChart.SetRows(GetTerminalMode() == TerminalBallisticsCalculatorMode.Combined
            ? terminalTableRows
            : Enumerable.Empty<TerminalBallisticsTableRow>());
    }

    void RebuildTerminalPenetrationTableRows()
    {
        terminalPenetrationTableRows.Clear();
        if (GetTerminalMode() != TerminalBallisticsCalculatorMode.Combined)
            return;

        var sourceRows = GetLowTrajectoryTerminalRowsForInterpolation();
        if (sourceRows.Count < 2)
            return;

        var maxRange = sourceRows.Max(row => row.rangeYardsValue.Value);
        var maxTableRange = TerminalPenetrationTableRangesYards.FirstOrDefault(range => range >= maxRange);
        if (maxTableRange <= 0f)
            maxTableRange = TerminalPenetrationTableRangesYards[^1];

        foreach (var rangeYards in TerminalPenetrationTableRangesYards.Where(range => range <= maxTableRange + 0.001f))
        {
            var sample = InterpolateTerminalPenetrationTableSample(sourceRows, rangeYards);
            terminalPenetrationTableRows.Add(new TerminalBallisticsPenetrationTableRow
            {
                rangeYards = rangeYards,
                rangeBand = GetRangeBandByAngleOfFall(sample.angleOfFallDeg),
                angleOfFallDeg = sample.angleOfFallDeg,
                timeOfFlightSeconds = sample.timeOfFlightSeconds,
                horizontalPenetrationInches = sample.horizontalPenetrationInches,
                verticalPenetrationInches = sample.verticalPenetrationInches
            });
        }
    }

    List<TerminalBallisticsTableRow> GetLowTrajectoryTerminalRowsForInterpolation()
    {
        var rowsByElevation = terminalTableRows
            .Where(row => row.rangeYardsValue.HasValue && row.result?.success == true)
            .OrderBy(row => row.elevationAngleDeg)
            .ToList();
        if (rowsByElevation.Count <= 2)
            return rowsByElevation.OrderBy(row => row.rangeYardsValue.Value).ToList();

        var lowBranch = new List<TerminalBallisticsTableRow> { rowsByElevation[0] };
        var previousRange = rowsByElevation[0].rangeYardsValue.Value;
        for (int i = 1; i < rowsByElevation.Count; i++)
        {
            var range = rowsByElevation[i].rangeYardsValue.Value;
            if (range + 0.001f < previousRange)
                break;

            lowBranch.Add(rowsByElevation[i]);
            previousRange = range;
        }

        return lowBranch
            .GroupBy(row => Mathf.RoundToInt(row.rangeYardsValue.Value * 1000f))
            .Select(group => group.OrderBy(row => row.elevationAngleDeg).First())
            .OrderBy(row => row.rangeYardsValue.Value)
            .ToList();
    }

    static (float angleOfFallDeg, float timeOfFlightSeconds, float horizontalPenetrationInches, float verticalPenetrationInches) InterpolateTerminalPenetrationTableSample(
        List<TerminalBallisticsTableRow> sourceRows,
        float rangeYards)
    {
        var upperIndex = sourceRows.FindIndex(row => row.rangeYardsValue.Value >= rangeYards);
        if (upperIndex < 0)
            upperIndex = sourceRows.Count - 1;
        else if (upperIndex == 0)
            upperIndex = 1;

        TerminalBallisticsTableRow lower;
        TerminalBallisticsTableRow upper;
        lower = sourceRows[upperIndex - 1];
        upper = sourceRows[upperIndex];

        var lowerRange = lower.rangeYardsValue.Value;
        var upperRange = upper.rangeYardsValue.Value;
        var ratio = Mathf.Approximately(lowerRange, upperRange)
            ? 0f
            : (rangeYards - lowerRange) / (upperRange - lowerRange);

        return (
            LerpUnclamped(lower.result.angleOfFallDeg, upper.result.angleOfFallDeg, ratio),
            LerpUnclamped(lower.timeOfFlightSecondsValue ?? 0f, upper.timeOfFlightSecondsValue ?? 0f, ratio),
            LerpUnclamped(lower.result.horizontalPenetrationInches, upper.result.horizontalPenetrationInches, ratio),
            LerpUnclamped(lower.result.verticalPenetrationInches, upper.result.verticalPenetrationInches, ratio));
    }

    static RangeBand GetRangeBandByAngleOfFall(float angleOfFallDeg)
    {
        if (angleOfFallDeg < 7f)
            return RangeBand.Short;
        if (angleOfFallDeg < 20.5f)
            return RangeBand.Medium;
        if (angleOfFallDeg < 41f)
            return RangeBand.Long;
        return RangeBand.Extreme;
    }

    static float LerpUnclamped(float a, float b, float ratio) => a + (b - a) * ratio;

    static string FormatTerminalRangeBand(RangeBand rangeBand)
    {
        return rangeBand switch
        {
            RangeBand.Medium => Localize("RangeBand.Medium"),
            RangeBand.Long => Localize("RangeBand.Long"),
            RangeBand.Extreme => Localize("RangeBand.Extreme"),
            _ => Localize("RangeBand.Short")
        };
    }

    void UpdateTerminalModeVisibility()
    {
        if (terminalSingleRows == null || terminalCombinedRows == null)
            return;

        var isSingle = GetTerminalMode() == TerminalBallisticsCalculatorMode.Single;
        terminalSingleRows.style.display = isSingle ? DisplayStyle.Flex : DisplayStyle.None;
        terminalCombinedRows.style.display = isSingle ? DisplayStyle.None : DisplayStyle.Flex;
        RefreshTerminalResultColumns();
    }

    void UpdateTerminalDragInputModeVisibility()
    {
        if (terminalGModelRows == null || terminalPhysicalCdRows == null)
            return;

        var isPhysicalCd = GetTerminalDragInputMode() == ExternalBallisticsDragInputMode.PhysicalCd;
        terminalGModelRows.style.display = isPhysicalCd ? DisplayStyle.None : DisplayStyle.Flex;
        terminalPhysicalCdRows.style.display = isPhysicalCd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void UpdateTerminalFormulaParameterState()
    {
        var isCustom = GetTerminalFormulaPreset() == TerminalBallisticsFormulaPreset.Custom;
        terminalFormulaConstantField.SetEnabled(isCustom);
        terminalProjectileDiameterExponentField.SetEnabled(isCustom);
        terminalEnergyDensityExponentField.SetEnabled(isCustom);
        terminalObliquityCosineExponentField.SetEnabled(isCustom);
        terminalFormulaCoefficientField.SetEnabled(true);
        terminalFormulaHelpLabel.text = GetTerminalFormulaHelp(GetTerminalFormulaPreset());
    }

    void UpdateTerminalHelpers()
    {
        if (terminalProjectileDiameterMmLabel != null)
        {
            terminalProjectileDiameterMmLabel.text = Localize(
                "Approx. {0:0.#} mm",
                TerminalBallisticsSolver.InchesToMillimeters(terminalProjectileDiameterInchField.value));
        }

        if (terminalMassPoundsLabel != null)
        {
            terminalMassPoundsLabel.text = Localize(
                "Approx. {0:0} lb",
                TerminalBallisticsSolver.KilogramsToPounds(terminalProjectileMassKgField.value));
        }
    }

    void ApplyTerminalFormulaPreset()
    {
        var preset = GetTerminalFormulaPreset();
        if (preset == TerminalBallisticsFormulaPreset.Custom)
            return;

        var parameters = TerminalBallisticsFormulaParameters.ForPreset(preset);
        terminalFormulaConstantField.SetValueWithoutNotify(parameters.numericalConstant);
        terminalProjectileDiameterExponentField.SetValueWithoutNotify(parameters.projectileDiameterExponent);
        terminalEnergyDensityExponentField.SetValueWithoutNotify(parameters.energyDensityExponent);
        terminalFormulaCoefficientField.SetValueWithoutNotify(parameters.coefficient);
        terminalObliquityCosineExponentField.SetValueWithoutNotify(parameters.obliquityCosineExponent);
    }

    TerminalBallisticsCalculatorMode GetTerminalMode()
    {
        return terminalModeField != null && terminalModeField.index == 1
            ? TerminalBallisticsCalculatorMode.Combined
            : TerminalBallisticsCalculatorMode.Single;
    }

    ExternalBallisticsDragInputMode GetTerminalDragInputMode()
    {
        return terminalDragInputModeField != null && terminalDragInputModeField.index == 1
            ? ExternalBallisticsDragInputMode.PhysicalCd
            : ExternalBallisticsDragInputMode.GModelBallisticCoefficient;
    }

    TerminalBallisticsFormulaPreset GetTerminalFormulaPreset()
    {
        return terminalFormulaPresetField?.index switch
        {
            1 => TerminalBallisticsFormulaPreset.KruppAllPurpose,
            2 => TerminalBallisticsFormulaPreset.Custom,
            _ => TerminalBallisticsFormulaPreset.DeMarreNickelSteel
        };
    }

    static List<string> GetTerminalFormulaPresetLabels()
    {
        return new List<string>
        {
            Localize("De Marre Nickel-Steel"),
            Localize("Krupp All-Purpose"),
            Localize("Custom")
        };
    }

    static string GetTerminalFormulaHelp(TerminalBallisticsFormulaPreset preset)
    {
        return preset switch
        {
            TerminalBallisticsFormulaPreset.KruppAllPurpose => Localize("Krupp All-Purpose is a normal-obliquity-oriented Okun formula. Its preset uses no cosine obliquity term."),
            TerminalBallisticsFormulaPreset.Custom => Localize("Custom edits all power-law coefficients. Okun units are ft/s, inches, and pounds internally."),
            _ => Localize("De Marre Nickel-Steel uses Okun's cosine obliquity term. Vertical armor obliquity is angle of fall; horizontal armor obliquity is 90 minus angle of fall.")
        };
    }

    static string FormatTerminalPenetration(float inches)
    {
        return $"{inches:0.00} in";
    }

    static bool IsTerminalAngleOfFall(float angle) => float.IsFinite(angle) && angle >= 0f && angle <= 90f;
    static bool TerminalIsFinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
