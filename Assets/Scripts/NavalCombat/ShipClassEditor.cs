using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;

using NavalCombatCore;
using CoreUtils;
using System;
using YYZ;

public class BatteryFigurePoint
{
    public float distanceYards;
    public float verticalPenetrationInches;
    public float horizontalPenetrationInches;
    public float fireControlValue;
}

[UxmlElement]
public partial class BatteryPenetrationFireControlChart : VisualElement
{
    const float LeftPadding = 42f;
    const float RightPadding = 42f;
    const float TopPadding = 20f;
    const float BottomPadding = 34f;

    readonly VisualElement labelLayer = new();
    List<BatteryFigurePoint> points = new();
    float? rangeYards;
    float? mainBeltEffectiveInches;

    static readonly Color VerticalPenetrationColor = new(0.75f, 0.2f, 0.18f, 1f);
    static readonly Color HorizontalPenetrationColor = new(0.16f, 0.42f, 0.78f, 1f);
    static readonly Color FireControlColor = new(0.12f, 0.6f, 0.24f, 1f);
    static readonly Color GridColor = new(0f, 0f, 0f, 0.18f);
    static readonly Color RangeLineColor = new(0.85f, 0.85f, 0.85f, 0.9f);
    static readonly Color ImmuneZoneColor = new(0.0f, 0.72f, 0.72f, 1f);
    static readonly Color VulnerableZoneColor = new(0.9f, 0.6f, 0.12f, 1f);

    public BatteryPenetrationFireControlChart()
    {
        style.position = Position.Relative;

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

    public void SetPoints(IEnumerable<BatteryFigurePoint> newPoints)
    {
        points = newPoints?.Where(point => point != null).OrderBy(point => point.distanceYards).ToList() ?? new();
        RebuildLabels();
        MarkDirtyRepaint();
    }

    public void SetRangeYards(float? value)
    {
        rangeYards = value;
        RebuildLabels();
        MarkDirtyRepaint();
    }

    public void SetMainBeltEffectiveInches(float? value)
    {
        mainBeltEffectiveInches = value.HasValue && value.Value > 0f ? value.Value : null;
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var painter = context.painter2D;
        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;
        painter.strokeColor = Color.black;

        DrawAxes(painter, chartRect);

        if (points.Count == 0)
            return;

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        var (minDistance, maxDistance) = GetDistanceBounds();

        DrawRangeLine(painter, chartRect, minDistance, maxDistance);
        DrawMainBeltEffectiveLine(painter, chartRect, minDistance, maxDistance, leftMax);

        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.verticalPenetrationInches, minDistance, maxDistance, leftMax)),
            VerticalPenetrationColor);
        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.horizontalPenetrationInches, minDistance, maxDistance, leftMax)),
            HorizontalPenetrationColor);
        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.fireControlValue, minDistance, maxDistance, rightMax)),
            FireControlColor);
    }

    void DrawAxes(Painter2D painter, Rect chartRect)
    {
        painter.strokeColor = Color.black;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, chartRect.yMin));
        painter.LineTo(new Vector2(chartRect.xMin, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMin));
        painter.Stroke();

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        foreach (var ratio in GetTickRatios())
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, ratio);
            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin, y));
            painter.LineTo(new Vector2(chartRect.xMax, y));
            painter.Stroke();

            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin - 4f, y));
            painter.LineTo(new Vector2(chartRect.xMin, y));
            painter.MoveTo(new Vector2(chartRect.xMax, y));
            painter.LineTo(new Vector2(chartRect.xMax + 4f, y));
            painter.Stroke();
        }

        if (points.Count == 0)
            return;

        var (minDistance, maxDistance) = GetDistanceBounds();

        foreach (var tickDistance in GetDistanceLabelDistances())
        {
            var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, tickDistance));
            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, chartRect.yMax));
            painter.LineTo(new Vector2(x, chartRect.yMax + 4f));
            painter.Stroke();
        }
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        labelLayer.Add(BuildOverlayLabel(
            "(in)",
            0f,
            2f,
            LeftPadding - 6f,
            14f,
            TextAnchor.UpperRight));
        labelLayer.Add(BuildOverlayLabel(
            "(fc)",
            contentRect.width - RightPadding + 6f,
            2f,
            RightPadding - 6f,
            14f,
            TextAnchor.UpperLeft));
        if (rangeYards.HasValue)
        {
            var (rangeMinDistance, rangeMaxDistance) = GetDistanceBounds();
            var rangeX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(rangeMinDistance, rangeMaxDistance, rangeYards.Value));
            labelLayer.Add(BuildOverlayLabel(
                rangeYards.Value.ToString("0"),
                rangeX - 32f,
                2f,
                64f,
                14f,
                TextAnchor.UpperCenter,
                RangeLineColor));
        }

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        foreach (var ratio in GetTickRatios())
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, ratio) - 8f;
            labelLayer.Add(BuildOverlayLabel(
                Mathf.Lerp(0f, leftMax, ratio).ToString("0.0"),
                0f,
                y,
                LeftPadding - 6f,
                16f,
                TextAnchor.MiddleRight));
            labelLayer.Add(BuildOverlayLabel(
                Mathf.Lerp(0f, rightMax, ratio).ToString("0.0"),
                chartRect.xMax + 6f,
                y,
                RightPadding - 6f,
                16f,
                TextAnchor.MiddleLeft));
        }

        if (points.Count == 0)
            return;

        var (minDistance, maxDistance) = GetDistanceBounds();

        var distanceLabelWidth = GetDistanceLabelWidth(chartRect.width);
        foreach (var tickDistance in GetDistanceLabelDistances())
        {
            var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, tickDistance));
            labelLayer.Add(BuildOverlayLabel(
                tickDistance.ToString("0"),
                x - distanceLabelWidth * 0.5f,
                chartRect.yMax + 6f,
                distanceLabelWidth,
                18f,
                TextAnchor.UpperCenter));
        }
    }

    Rect GetChartRect()
    {
        var width = Mathf.Max(0f, contentRect.width - LeftPadding - RightPadding);
        var height = Mathf.Max(0f, contentRect.height - TopPadding - BottomPadding);
        return new Rect(LeftPadding, TopPadding, width, height);
    }

    float GetLeftAxisMax()
    {
        var maxValue = points.Count == 0
            ? 1f
            : Mathf.Max(
                points.Max(point => point.verticalPenetrationInches),
                points.Max(point => point.horizontalPenetrationInches));
        if (mainBeltEffectiveInches.HasValue)
            maxValue = Mathf.Max(maxValue, mainBeltEffectiveInches.Value);
        return Mathf.Max(1f, Mathf.Ceil(maxValue));
    }

    float GetRightAxisMax()
    {
        var maxValue = points.Count == 0 ? 1f : points.Max(point => point.fireControlValue);
        return Mathf.Max(1f, Mathf.Ceil(maxValue));
    }

    (float minDistance, float maxDistance) GetDistanceBounds()
    {
        if (points.Count == 0)
            return (0f, 1f);

        var minDistance = points.Min(point => point.distanceYards);
        var maxDistance = points.Max(point => point.distanceYards);
        if (rangeYards.HasValue)
        {
            minDistance = Mathf.Min(minDistance, rangeYards.Value);
            maxDistance = Mathf.Max(maxDistance, rangeYards.Value);
        }

        if (Mathf.Approximately(minDistance, maxDistance))
        {
            minDistance -= 1f;
            maxDistance += 1f;
        }

        return (minDistance, maxDistance);
    }

    static IEnumerable<float> GetTickRatios()
    {
        yield return 0f;
        yield return 0.25f;
        yield return 0.5f;
        yield return 0.75f;
        yield return 1f;
    }

    IEnumerable<float> GetDistanceLabelDistances()
    {
        return points
            .Select(point => point.distanceYards)
            .Distinct()
            .OrderBy(distance => distance)
            .ToList();
    }

    float GetDistanceLabelWidth(float chartWidth)
    {
        var count = Mathf.Max(1, GetDistanceLabelDistances().Count());
        return Mathf.Max(32f, chartWidth / count + 12f);
    }

    static Vector2 MapPoint(Rect chartRect, float distanceYards, float value, float minDistance, float maxDistance, float axisMax)
    {
        var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, distanceYards));
        var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(0f, Mathf.Max(1f, axisMax), value));
        return new Vector2(x, y);
    }

    static void DrawSeries(Painter2D painter, Rect chartRect, IEnumerable<Vector2> seriesPoints, Color color)
    {
        var pointList = seriesPoints.ToList();
        if (pointList.Count == 0)
            return;

        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = 2f;
        if (pointList.Count >= 2)
        {
            painter.BeginPath();
            painter.MoveTo(pointList[0]);
            for (int i = 1; i < pointList.Count; i++)
            {
                painter.LineTo(pointList[i]);
            }
            painter.Stroke();
        }

        foreach (var point in pointList)
        {
            if (!chartRect.Contains(point))
                continue;

            painter.BeginPath();
            painter.Arc(point, 2.5f, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
    }

    static void DrawRangeLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance, float rangeYards)
    {
        var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, rangeYards));
        if (x < chartRect.xMin || x > chartRect.xMax)
            return;

        painter.strokeColor = RangeLineColor;
        painter.lineWidth = 1f;
        const float dashLength = 6f;
        const float gapLength = 4f;
        var y = chartRect.yMin;
        while (y < chartRect.yMax)
        {
            var endY = Mathf.Min(y + dashLength, chartRect.yMax);
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x, endY));
            painter.Stroke();
            y += dashLength + gapLength;
        }
    }

    void DrawRangeLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance)
    {
        if (rangeYards.HasValue)
        {
            DrawRangeLine(painter, chartRect, minDistance, maxDistance, rangeYards.Value);
        }
    }

    void DrawMainBeltEffectiveLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance, float leftAxisMax)
    {
        if (!mainBeltEffectiveInches.HasValue || points.Count == 0)
            return;

        var armorValue = mainBeltEffectiveInches.Value;
        var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(0f, Mathf.Max(1f, leftAxisMax), armorValue));
        if (y < chartRect.yMin || y > chartRect.yMax)
            return;

        var splitDistances = new List<float> { minDistance, maxDistance };
        for (int i = 0; i < points.Count - 1; i++)
        {
            AddCrossingDistance(splitDistances, points[i].distanceYards, points[i + 1].distanceYards, points[i].verticalPenetrationInches, points[i + 1].verticalPenetrationInches, armorValue);
            AddCrossingDistance(splitDistances, points[i].distanceYards, points[i + 1].distanceYards, points[i].horizontalPenetrationInches, points[i + 1].horizontalPenetrationInches, armorValue);
        }

        splitDistances = splitDistances
            .Distinct()
            .Where(distance => distance >= minDistance && distance <= maxDistance)
            .OrderBy(distance => distance)
            .ToList();

        painter.lineWidth = 1f;
        const float dashLength = 6f;
        const float gapLength = 4f;

        for (int i = 0; i < splitDistances.Count - 1; i++)
        {
            var startDistance = splitDistances[i];
            var endDistance = splitDistances[i + 1];
            if (endDistance <= startDistance)
                continue;

            var midDistance = (startDistance + endDistance) * 0.5f;
            painter.strokeColor = IsImmuneAtDistance(midDistance, armorValue) ? ImmuneZoneColor : VulnerableZoneColor;

            var startX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, startDistance));
            var endX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, endDistance));

            var x = startX;
            while (x < endX)
            {
                var dashEnd = Mathf.Min(x + dashLength, endX);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(dashEnd, y));
                painter.Stroke();
                x += dashLength + gapLength;
            }
        }
    }

    bool IsImmuneAtDistance(float distanceYards, float armorValue)
    {
        var verticalPenetration = EvaluatePenetration(distanceYards, point => point.verticalPenetrationInches);
        var horizontalPenetration = EvaluatePenetration(distanceYards, point => point.horizontalPenetrationInches);
        return verticalPenetration < armorValue && horizontalPenetration < armorValue;
    }

    float EvaluatePenetration(float distanceYards, Func<BatteryFigurePoint, float> selector)
    {
        if (points.Count == 0)
            return 0f;
        if (points.Count == 1)
            return selector(points[0]);

        if (distanceYards <= points[0].distanceYards)
            return selector(points[0]);
        if (distanceYards >= points[^1].distanceYards)
            return selector(points[^1]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            if (distanceYards < start.distanceYards || distanceYards > end.distanceYards)
                continue;

            var t = Mathf.InverseLerp(start.distanceYards, end.distanceYards, distanceYards);
            return Mathf.Lerp(selector(start), selector(end), t);
        }

        return selector(points[^1]);
    }

    static void AddCrossingDistance(List<float> splitDistances, float startDistance, float endDistance, float startValue, float endValue, float threshold)
    {
        var startDelta = startValue - threshold;
        var endDelta = endValue - threshold;

        if (Mathf.Approximately(startDelta, 0f))
            splitDistances.Add(startDistance);
        if (Mathf.Approximately(endDelta, 0f))
            splitDistances.Add(endDistance);
        if (Mathf.Approximately(startDelta, 0f) || Mathf.Approximately(endDelta, 0f) || Mathf.Sign(startDelta) == Mathf.Sign(endDelta))
            return;

        var t = Mathf.InverseLerp(startValue, endValue, threshold);
        splitDistances.Add(Mathf.Lerp(startDistance, endDistance, t));
    }

    static Label BuildOverlayLabel(string text, float x, float y, float width, float height, TextAnchor textAnchor, Color? color = null)
    {
        var label = new Label(text);
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = x;
        label.style.top = y;
        label.style.width = width;
        label.style.height = height;
        label.style.fontSize = 10;
        label.style.unityTextAlign = textAnchor;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        if (color.HasValue)
        {
            label.style.color = color.Value;
        }
        return label;
    }
}


public class ShipClassEditor : LeftObjectPickerRightEditor<ShipClassEditor, ShipClass>
{
    ListView batteryRecordsListView;
    VisualElement portraitTopPreview;
    VisualElement portraitIconPreview;
    VisualElement graphicTabContent;
    VisualElement sectorArcsTabContent;
    VisualElement batterySectorArcsContainer;
    VisualElement batteryFigureChartsContainer;
    Label torpedoSectorTitleLabel;
    Image defaultPlaceholderPreviewImage;
    Texture2D defaultPlaceholderPreviewTexture;
    string lastDefaultPlaceholderSignature;
    string lastDefaultPlaceholderShipObjectId;
    string lastSectorArcSignature;
    string lastSectorArcShipObjectId;

    SectorArcIndicatorBinder torpedoSectorArcIndicatorBinder = new();

    protected override string ObjectListViewElementName => "ShipClassListView";

    public ListView shipClassListView => objectListView;

    [CreateProperty]
    public ShipClass selectedShipClass => selectedObject;

    public ShipClass SelectedShipClassProvider()
    {
        return selectedObject;
    }

    // protected override void Awake()
    protected override void OnEnable()
    {
        base.OnEnable();

        torpedoSectorArcIndicatorBinder.BindUI(root.Q<VisualElement>("TorpedoSectorArcIndicator"));
        sectorArcsTabContent = root.Q<VisualElement>("SectorArcsTabContent");
        batterySectorArcsContainer = root.Q<VisualElement>("BatterySectorArcsContainer");
        batteryFigureChartsContainer = root.Q<VisualElement>("BatteryFigureChartsContainer");
        torpedoSectorTitleLabel = root.Q<Label>("TorpedoSectorTitleLabel");
        sectorArcsTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestSectorArcRefresh());

        shipClassListView.selectionChanged += (objs) =>
        {
            // Debug.Log($"selectionChanged: {objs}");
            var currentShipClass = objs.FirstOrDefault() as ShipClass;
            if (currentShipClass != null)
            {
                Debug.Log($"currentShipClass: {currentShipClass}");
            }

            RequestSectorArcRefresh(currentShipClass, true);
            RequestDefaultPlaceholderPreviewRefresh(currentShipClass, true);
        };

        var speedIncreaseMultiColumnListView = root.Q<MultiColumnListView>("SpeedIncreaseMultiColumnListView");
        // speedIncreaseMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView);
        Utils.BindItemsAddedRemoved<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView, SelectedShipClassProvider);

        var inferSpeedIncreaseButton = root.Q<Button>("InferSpeedIncreaseButton");
        if (inferSpeedIncreaseButton != null)
        {
            inferSpeedIncreaseButton.clicked += () =>
            {
                var shipClass = selectedShipClass;
                if (shipClass == null)
                {
                    DialogRoot.Instance.PopupMessageDialog(Localize("No ship class is selected."));
                    return;
                }

                shipClass.InferSpeedIncreaseRecord();
                shipClass.InferTurnRate();
                shipClass.InferMachineryHitSpeedLimits();
            };
        }

        batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
        // batteryRecordsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<BatteryRecord>(batteryRecordsListView);
        Utils.BindItemsAddedRemoved<BatteryRecord>(batteryRecordsListView, SelectedShipClassProvider);
        batteryRecordsListView.makeItem = () =>
        {
            var el = batteryRecordsListView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(el);

            var fireControlTableMultiColumnListView = el.Q<MultiColumnListView>("FireControlTableMultiColumnListView");
            var penetrationTableMultiColumnListView = el.Q<MultiColumnListView>("PenetrationTableMultiColumnListView");
            var mountsListView = el.Q<ListView>("MountsListView");
            var fireControlModelComparisonButton = el.Q<Button>("FireControlModelComparisonButton");
            var batteryRecordMetaInfoButton = el.Q<Button>("BatteryRecordMetaInfoButton");
            if (batteryRecordMetaInfoButton != null)
            {
                batteryRecordMetaInfoButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(batteryRecordMetaInfoButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    DialogRoot.Instance.PopupBatteryRecordMetaInfoDialog(batteryRecord, () =>
                    {
                        penetrationTableMultiColumnListView?.RefreshItems();
                        RequestSectorArcRefresh(true);
                    });
                };
            }

            if (fireControlModelComparisonButton != null)
            {
                fireControlModelComparisonButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(fireControlModelComparisonButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    PopupFireControlModelComparisonDialog(selectedShipClass, batteryRecord);
                };
            }

            var resetFireControlTableButton = el.Q<Button>("ResetFireControlTableButton");
            if (resetFireControlTableButton != null)
            {
                resetFireControlTableButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(resetFireControlTableButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    UpdateFireControlTableFromCodeModel(selectedShipClass, batteryRecord);
                    fireControlTableMultiColumnListView?.RefreshItems();
                    DialogRoot.Instance.PopupMessageDialog(
                        Localize("Fire control table reset from code model."),
                        Localize("Reset Fire Control Table"));
                };
            }

            var resetPenetrationTableButton = el.Q<Button>("ResetPenetrationTableButton");
            if (resetPenetrationTableButton != null)
            {
                resetPenetrationTableButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(resetPenetrationTableButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    var rowCount = ResetPenetrationTableFromModel(batteryRecord);
                    penetrationTableMultiColumnListView?.RefreshItems();
                    RequestSectorArcRefresh(true);
                    DialogRoot.Instance.PopupMessageDialog(
                        Localize("Penetration table reset from model with {0} rows.", rowCount),
                        Localize("Reset Penetration Table"));
                };
            }

            // fireControlTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<FireControlTableRecord>(fireControlTableMultiColumnListView);
            // penetrationTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<PenetrationTableRecord>(penetrationTableMultiColumnListView);
            // mountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(mountsListView);
            Utils.BindItemsAddedRemoved<FireControlTableRecord>(fireControlTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<PenetrationTableRecord>(penetrationTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<MountLocationRecord>(mountsListView, SelectedShipClassProvider);

            mountsListView.makeItem = () =>
            {
                var el2 = mountsListView.itemTemplate.CloneTree();

                var mountsArcsMultiColumnsListView = el2.Q<MultiColumnListView>("MountArcsMultiColumnListView");
                // mountsArcsMultiColumnsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountsArcsMultiColumnsListView);
                Utils.BindItemsAddedRemoved<MountArcRecord>(mountsArcsMultiColumnsListView, SelectedShipClassProvider);

                Utils.BindItemsSourceRecursive(el2);

                return el2;
            };

            return el;
        };

        var torpedoSettingsMultiColumnListView = root.Q<MultiColumnListView>("TorpedoSettingsMultiColumnListView");
        // torpedoSettingsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<TorpedoSetting>(torpedoSettingsMultiColumnListView);
        Utils.BindItemsAddedRemoved<TorpedoSetting>(torpedoSettingsMultiColumnListView, SelectedShipClassProvider);

        var torpedoMountsListView = root.Q<ListView>("TorpedoMountsListView");
        // torpedoMountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(torpedoMountsListView);
        Utils.BindItemsAddedRemoved<MountLocationRecord>(torpedoMountsListView, SelectedShipClassProvider);
        torpedoMountsListView.makeItem = () =>
        {
            var el = torpedoMountsListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var mountArcsMultiColumnListView = el.Q<MultiColumnListView>("MountArcsMultiColumnListView");
            // mountArcsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountArcsMultiColumnListView);
            Utils.BindItemsAddedRemoved<MountArcRecord>(mountArcsMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var rapidFireBatteryListView = root.Q<ListView>("RapidFireBatteryListView");
        // rapidFireBatteryListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryRecord>(rapidFireBatteryListView);
        Utils.BindItemsAddedRemoved<RapidFireBatteryRecord>(rapidFireBatteryListView, SelectedShipClassProvider);

        rapidFireBatteryListView.makeItem = () =>
        {
            var el = rapidFireBatteryListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var fireControlLevelMultiColumnListView = el.Q<MultiColumnListView>("FireControlLevelMultiColumnListView");
            // fireControlLevelMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView);
            Utils.BindItemsAddedRemoved<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView, SelectedShipClassProvider);

            var metaInfoButton = el.Q<Button>("RapidFireBatteryMetaInfoSetButton");
            metaInfoButton.clicked += () =>
            {
                if (!Utils.TryResolveCurrentValueForBinding(metaInfoButton, out RapidFireBatteryRecord rapidFireBatteryRecord))
                    return;

                DialogRoot.Instance.PopupRapidFireBatteryRecordMetaInfoDialog(rapidFireBatteryRecord, null);
            };

            return el;
        };

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipClassesXMLLoaded;
            IOManager.Instance.LoadTextFile(OnShipClassesXMLLoaded, "xml");
        };

        var exportSelectedBatteryButton = root.Q<Button>("ExportSelectedBatteryButton");
        var importToSelectedBatteryButton = root.Q<Button>("ImportToSelectedBatteryButton");

        exportSelectedBatteryButton.clicked += () =>
        {
            var battryRecord = batteryRecordsListView.selectedItem as BatteryRecord;
            if (battryRecord != null)
            {
                var content = battryRecord.ToXML();
                IOManager.Instance.SaveTextFile(content, "battery", "xml");
            }
        };

        importToSelectedBatteryButton.clicked += () =>
        {
            var idx = batteryRecordsListView.selectedIndex;
            if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
            {
                // IOManager.Instance.textLoaded += OnBatteryXMLLoaded;
                IOManager.Instance.LoadTextFile(OnBatteryXMLLoaded, "xml");
            }
        };

        var setSelectedByBatterySelectorButton = root.Q<Button>("SetSelectedByBatterySelectorButton");
        setSelectedByBatterySelectorButton.clicked += () =>
        {
            // Debug.Log("setSelectedByBatterySelectorButton clicked");

            DialogRoot.Instance.PopupBatteryRecordSelectorDialog(_batteryRecord =>
            {
                var batteryRecord = XmlUtils.FromXML<BatteryRecord>(XmlUtils.ToXML(_batteryRecord));
                ((IObjectIdLabeled)batteryRecord).ResetObjectId();

                var idx = batteryRecordsListView.selectedIndex;
                if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    batteryRecordsListView.itemsSource[idx] = batteryRecord;
                }
                else
                {
                    batteryRecordsListView.itemsSource.Add(batteryRecord);
                }

                var gameState = SuperGameState.Instance.GetCurrentGameState();
                gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };

        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitTopReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitIconReferenceField"));
        portraitTopPreview = root.Q<VisualElement>("PortraitTopPreview");
        portraitIconPreview = root.Q<VisualElement>("PortraitIconPreview");
        graphicTabContent = root.Q<VisualElement>("GraphicTabContent");
        defaultPlaceholderPreviewImage = root.Q<Image>("DefaultPlaceholderPreviewImage");

        graphicTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestDefaultPlaceholderPreviewRefresh());

        root.Q<Button>("GeneratePlaceholderImageButton").clicked += () =>
        {
            if (selectedShipClass != null)
            {
                DialogRoot.Instance.PopupShipClassPlaceholderGeneratorDialog(selectedShipClass);
            }
        };

        root.Q<Button>("GeneratePlaceholderImageForAllPlaceholderButton").clicked += () =>
        {
            var placeholders = SuperGameState.Instance.GetCurrentGameState().shipClasses.Where(x => x.isGraphicPlaceholder).ToList();
            var count = placeholders.Count;
            if (count == 0)
            {
                DialogRoot.Instance.PopupMessageDialog("No ship class is marked as graphic placeholder.");
                return;
            }

            DialogRoot.Instance.PopupConfirmDialog(
                $"Generate placeholder images for {count} ship class? If confirm, {count} x 2 images would be generated in the game folder and binding would be reset to those image.\n\n Warning: This will modify files in the disk.",
                () =>
                {
                    var result = ShipClassPlaceholderImageGenerator.GenerateAndBindAllMarked(placeholders);
                    UnityWebRequestImageReader.Instance.Reset();
                    RefreshGraphicBindings();

                    var message = $"Generated placeholder images for {result.generatedShipClasses.Count} ship class.";
                    if (result.skippedMessages.Count > 0)
                    {
                        message += "\nSkipped:\n" + string.Join("\n", result.skippedMessages);
                    }
                    DialogRoot.Instance.PopupMessageDialog(message);
                });
        };

        var batteryArcIndicatorDialogButton = root.Q<Button>("BatteryArcIndicatorDialogButton");
        batteryArcIndicatorDialogButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(batteryArcIndicatorDialogButton, out ShipClass shipClass))
            {
                DialogRoot.Instance.PopupBatteryArcIndicatorDialog(shipClass);
            }
        };

        root.Q<Button>("SetSelectedByRapidFireBatterySelectorButton").clicked += () =>
        {
            Debug.Log("SetSelectedByRapidFireBatterySelectorButton clicked");

            DialogRoot.Instance.PopupRapidFireBatteryRecordSelectorDialog(_rapidFireBatteryRecord =>
            {
                var rapidFireBatteryRecord = XmlUtils.FromXML<RapidFireBatteryRecord>(XmlUtils.ToXML(_rapidFireBatteryRecord));
                // ((IObjectIdLabeled)rapidFireBatteryRecord).ResetObjectId();

                var idx = rapidFireBatteryListView.selectedIndex;
                if (idx >= 0 && idx < rapidFireBatteryListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    rapidFireBatteryListView.itemsSource[idx] = rapidFireBatteryRecord;
                }
                else
                {
                    rapidFireBatteryListView.itemsSource.Add(rapidFireBatteryRecord);
                }

                // var gameState = SuperGameState.Instance.GetCurrentGameState();
                // gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };
        
        var setByTorpedoSelectorButton = root.Q<Button>("SetByTorpedoSelectorButton");
        setByTorpedoSelectorButton.clicked += () =>
        {
            Debug.Log("SetByTorpedoSelectorButton clicked");

            DialogRoot.Instance.PopupTorpedoSectorSelectorDialog(_shipClass =>
            {
                var _torpedoSector = _shipClass.torpedoSector;
                var torpedoSector = XmlUtils.FromXML<TorpedoSector>(XmlUtils.ToXML(_torpedoSector));
                foreach (var mountLocationRecord in torpedoSector.mountLocationRecords)
                {
                    mountLocationRecord.objectId = null;
                }

                if(Utils.TryResolveCurrentValueForBinding<ShipClass>(setByTorpedoSelectorButton, out var shipClass))
                {
                    shipClass.torpedoSector = torpedoSector;
                    SuperGameState.Instance.GetCurrentGameState().ResetAndRegisterAll();
                }
            });
        };
    }

    void OnDisable()
    {
        ClearSectorArcState();
        DisposeDefaultPlaceholderPreviewTexture();
    }

    public EventHandler shown;
    public EventHandler hidden;

    protected override void OnShow()
    {
        RequestDefaultPlaceholderPreviewRefresh();
        shown?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnHidden()
    {
        ClearDefaultPlaceholderPreviewState();
        hidden?.Invoke(this, EventArgs.Empty);
    }

    public void OnBatteryXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnBatteryXMLLoaded;

        var idx = batteryRecordsListView.selectedIndex;
        if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
        {
            var battryRecord = BatteryRecord.FromXml(text);
            batteryRecordsListView.itemsSource[idx] = battryRecord;
        }

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ResetAndRegisterAll(); // re-duplicate object id // FIXME: Correctness is questionable though
    }

    public void OnShipClassesXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipClassesFromXML(text);
        gameState.ResetAndRegisterAll();
        GetFullObjects();
        RefreshFilter();
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, true);
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;

    protected override void GetFullObjects()
    {
        fullObjects = currentGameState.shipClasses;
    }

    protected override void ProcessRemovedOne(ShipClass removeObj)
    {
        EntityManager.Instance.Unregister(removeObj);
    }

    protected override void OnAddObjectButtonClicked()
    {
        var newObj = new ShipClass();
        EntityManager.Instance.Register(newObj, null);
        fullObjects.Add(newObj);

        ProcessAddedOne(newObj);

        RefreshFilter();
        SelectObject(newObj);
    }

    void RefreshGraphicBindings()
    {
        shipClassListView?.RefreshItems();
        if (selectedShipClass == null)
            return;

        RefreshPictureField(root.Q<VisualElement>("PortraitTopReferenceField"), selectedShipClass.portraitTopReference);
        RefreshPictureField(root.Q<VisualElement>("PortraitIconReferenceField"), selectedShipClass.portraitIconReference);

        if (portraitTopPreview != null)
            portraitTopPreview.style.backgroundImage = selectedShipClass.portraitTopReference.pictureStyleBackground;

        if (portraitIconPreview != null)
            portraitIconPreview.style.backgroundImage = selectedShipClass.portraitIconReference.pictureStyleBackground;

        RequestDefaultPlaceholderPreviewRefresh();
    }

    void RequestSectorArcRefresh(bool force = false)
    {
        RequestSectorArcRefresh(selectedShipClass, force);
    }

    void RequestSectorArcRefresh(ShipClass shipClass, bool force = false)
    {
        if (sectorArcsTabContent == null || batterySectorArcsContainer == null || batteryFigureChartsContainer == null || !IsElementActuallyVisible(sectorArcsTabContent))
            return;

        if (shipClass == null)
        {
            ClearSectorArcState();
            return;
        }

        if (lastSectorArcShipObjectId != shipClass.objectId)
        {
            ClearSectorArcState();
            lastSectorArcShipObjectId = shipClass.objectId;
        }

        var signature = BuildSectorArcSignature(shipClass);
        if (!force && signature == lastSectorArcSignature)
            return;

        RebuildBatterySectorArcCards(shipClass);
        RebuildBatteryFigureCharts(shipClass);
        torpedoSectorArcIndicatorBinder.BindTorpedoData(shipClass);
        if (torpedoSectorTitleLabel != null)
        {
            torpedoSectorTitleLabel.text = shipClass?.torpedoSector?.name?.GetShortName() ?? "";
        }
        lastSectorArcShipObjectId = shipClass.objectId;
        lastSectorArcSignature = signature;
    }

    void RequestDefaultPlaceholderPreviewRefresh(bool force = false)
    {
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, force);
    }

    void RequestDefaultPlaceholderPreviewRefresh(ShipClass shipClass, bool force = false)
    {
        if (graphicTabContent == null || defaultPlaceholderPreviewImage == null || !IsElementActuallyVisible(graphicTabContent))
            return;

        if (shipClass == null)
        {
            ClearDefaultPlaceholderPreviewState();
            return;
        }

        if (lastDefaultPlaceholderShipObjectId != shipClass.objectId)
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        }

        var signature = ShipClassPlaceholderImageGenerator.BuildDefaultPreviewSignature(shipClass);
        if (!force && signature == lastDefaultPlaceholderSignature && defaultPlaceholderPreviewTexture != null)
            return;

        if (!ShipClassPlaceholderImageGenerator.TryRenderDefaultPreview(shipClass, out var renderResult))
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
            lastDefaultPlaceholderSignature = signature;
            return;
        }

        DisposeDefaultPlaceholderPreviewTexture();
        defaultPlaceholderPreviewTexture = renderResult.previewTexture;
        defaultPlaceholderPreviewImage.image = defaultPlaceholderPreviewTexture;
        lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        lastDefaultPlaceholderSignature = signature;

        if (renderResult.topTexture != null)
            Destroy(renderResult.topTexture);
        if (renderResult.iconTexture != null)
            Destroy(renderResult.iconTexture);
    }

    void RebuildBatterySectorArcCards(ShipClass shipClass)
    {
        batterySectorArcsContainer.Clear();

        if (shipClass?.batteryRecords == null)
            return;

        for (int i = 0; i < shipClass.batteryRecords.Count; i++)
        {
            batterySectorArcsContainer.Add(BuildBatterySectorArcCard(shipClass.batteryRecords[i], i));
        }
    }

    void RebuildBatteryFigureCharts(ShipClass shipClass)
    {
        batteryFigureChartsContainer.Clear();

        if (shipClass?.batteryRecords == null)
            return;

        for (int i = 0; i < shipClass.batteryRecords.Count; i++)
        {
            batteryFigureChartsContainer.Add(BuildBatteryFigureChartCard(shipClass, shipClass.batteryRecords[i], i));
        }
    }

    VisualElement BuildBatterySectorArcCard(BatteryRecord batteryRecord, int batteryIndex)
    {
        var card = new VisualElement();
        card.style.width = 220;
        card.style.minWidth = 220;
        card.style.alignItems = Align.Center;
        card.style.marginRight = 8;
        card.style.marginBottom = 8;
        card.style.paddingTop = 6;
        card.style.paddingRight = 6;
        card.style.paddingBottom = 6;
        card.style.paddingLeft = 6;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Color.black;
        card.style.borderRightColor = Color.black;
        card.style.borderBottomColor = Color.black;
        card.style.borderLeftColor = Color.black;

        var titleLabel = new Label(GetBatterySectorArcTitle(batteryRecord, batteryIndex));
        titleLabel.style.width = Length.Percent(100);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.marginBottom = 6;
        card.Add(titleLabel);

        var indicatorRoot = CreateSectorArcIndicatorLayout();
        var binder = new SectorArcIndicatorBinder();
        binder.BindUI(indicatorRoot);
        binder.BindBatteryData(batteryRecord);
        card.Add(indicatorRoot);

        return card;
    }

    VisualElement BuildBatteryFigureChartCard(ShipClass shipClass, BatteryRecord batteryRecord, int batteryIndex)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Column;
        card.style.alignItems = Align.Stretch;
        card.style.marginBottom = 8;
        card.style.paddingTop = 6;
        card.style.paddingRight = 6;
        card.style.paddingBottom = 6;
        card.style.paddingLeft = 6;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Color.black;
        card.style.borderRightColor = Color.black;
        card.style.borderBottomColor = Color.black;
        card.style.borderLeftColor = Color.black;

        var titleLabel = new Label(GetBatterySectorArcTitle(batteryRecord, batteryIndex));
        titleLabel.style.width = Length.Percent(100);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.marginBottom = 6;
        card.Add(titleLabel);

        var chartRow = new VisualElement();
        chartRow.style.flexDirection = FlexDirection.Row;
        chartRow.style.alignItems = Align.FlexStart;
        var chart = new BatteryPenetrationFireControlChart();
        chart.style.flexGrow = 1;
        chart.style.minWidth = 560;
        chart.style.height = 220;
        chart.style.minHeight = 220;
        chart.SetPoints(BuildBatteryFigurePoints(batteryRecord));
        chart.SetRangeYards(batteryRecord?.rangeYards);
        chart.SetMainBeltEffectiveInches(shipClass?.armorRating?.mainBelt?.effectInch);
        chartRow.Add(chart);
        chartRow.Add(BuildBatteryFigureLegend());
        card.Add(chartRow);

        return card;
    }

    VisualElement CreateSectorArcIndicatorLayout()
    {
        var indicatorRoot = new VisualElement();
        indicatorRoot.style.flexGrow = 0;
        indicatorRoot.style.alignItems = Align.Center;
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortForward", "Forward", "StarboardForward"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortMidship", "Midship", "StarboardMidship"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortAfter", "After", "StarboardAfter"));
        return indicatorRoot;
    }

    VisualElement CreateSectorArcIndicatorRow(params string[] indicatorNames)
    {
        var row = new VisualElement();
        row.style.flexGrow = 0;
        row.style.flexDirection = FlexDirection.Row;

        foreach (var indicatorName in indicatorNames)
        {
            var indicator = new BatteryArcIndicator();
            indicator.name = indicatorName;
            indicator.style.justifyContent = Justify.Center;
            row.Add(indicator);
        }

        return row;
    }

    void ClearSectorArcState()
    {
        batterySectorArcsContainer?.Clear();
        batteryFigureChartsContainer?.Clear();
        torpedoSectorArcIndicatorBinder.BindTorpedoData((ShipClass)null);
        if (torpedoSectorTitleLabel != null)
        {
            torpedoSectorTitleLabel.text = string.Empty;
        }
        lastSectorArcSignature = null;
        lastSectorArcShipObjectId = null;
    }

    string GetBatterySectorArcTitle(BatteryRecord batteryRecord, int batteryIndex)
    {
        var shortName = batteryRecord?.name?.GetShortName();
        return string.IsNullOrWhiteSpace(shortName) ? Localize("Battery {0}", batteryIndex + 1) : shortName;
    }

    string BuildSectorArcSignature(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        var batterySignature = string.Join(";",
            (shipClass.batteryRecords ?? new List<BatteryRecord>())
                .Select(batteryRecord => string.Join("~", new[]
                {
                    batteryRecord?.name?.GetShortName() ?? "",
                    BuildMountLocationSignature(batteryRecord?.mountLocationRecords),
                    BuildPenetrationSignature(batteryRecord?.penetrationTableRecords),
                    BuildFireControlSignature(batteryRecord?.fireControlTableRecords)
                })));

        return string.Join("|", new[]
        {
            shipClass.objectId ?? "",
            $"{shipClass.armorRating?.mainBelt?.effectInch:0.###}",
            batterySignature,
            BuildMountLocationSignature(shipClass.torpedoSector?.mountLocationRecords)
        });
    }

    static string BuildMountLocationSignature(IEnumerable<MountLocationRecord> mountLocationRecords)
    {
        return string.Join(";",
            (mountLocationRecords ?? Enumerable.Empty<MountLocationRecord>())
                .Select(record => string.Join(":", new[]
                {
                    record.mountLocation.ToString(),
                    BuildMountArcSignature(record.mountArcs)
                })));
    }

    static string BuildMountArcSignature(IEnumerable<MountArcRecord> mountArcs)
    {
        return string.Join(",",
            (mountArcs ?? Enumerable.Empty<MountArcRecord>())
                .Select(arc => $"{arc.startDeg:0.###}-{arc.CoverageDeg:0.###}"));
    }

    static string BuildPenetrationSignature(IEnumerable<PenetrationTableRecord> penetrationTableRecords)
    {
        return string.Join(",",
            (penetrationTableRecords ?? Enumerable.Empty<PenetrationTableRecord>())
                .Select(record => $"{record.distanceYards:0.###}:{record.verticalPenetrationInchs:0.###}:{record.horizontalPenetrationInchs:0.###}:{record.rateOfFire:0.###}:{record.rangeBand}"));
    }

    static readonly FireControlComparisonColumn[] FireControlComparisonColumns =
    {
        new("S/B", RangeBand.Short, TargetAspect.Broad),
        new("S/N", RangeBand.Short, TargetAspect.Narrow),
        new("M/B", RangeBand.Medium, TargetAspect.Broad),
        new("M/N", RangeBand.Medium, TargetAspect.Narrow),
        new("L/B", RangeBand.Long, TargetAspect.Broad),
        new("L/N", RangeBand.Long, TargetAspect.Narrow),
        new("E/B", RangeBand.Extreme, TargetAspect.Broad),
        new("E/N", RangeBand.Extreme, TargetAspect.Narrow),
    };

    static readonly FireControlSpeedFactor[] FireControlSpeedFactors =
    {
        new(9f, 1f),
        new(18f, 0.6710f),
        new(27f, 0.5265f),
        new(36f, 0.4393f),
        new(45f, 0.3758f),
    };

    static readonly Dictionary<FCSCode, float> FireControlCodeLatentOffsets = new()
    {
        { FCSCode.Z, 0f },
        { FCSCode.Y, 2.1829f },
        { FCSCode.X, 4.2304f },
        { FCSCode.W, 4.1718f },
        { FCSCode.U, 2.4735f },
        { FCSCode.T, 4.0784f },
        { FCSCode.S, 5.2111f },
        { FCSCode.R, 6.0497f },
        { FCSCode.Q, 7.4472f },
    };

    const float FireControlCodeLatentIntercept = 4.5261f;
    const float FireControlCodeShellSizeCoef = 0.2334f;
    const float FireControlCodeDisplacement1000Coef = -0.0823f;

    void PopupFireControlModelComparisonDialog(ShipClass shipClass, BatteryRecord batteryRecord)
    {
        if (batteryRecord == null)
            return;

        if ((batteryRecord.fireControlTableRecords == null || batteryRecord.fireControlTableRecords.Count == 0) &&
            (batteryRecord.penetrationTableRecords == null || batteryRecord.penetrationTableRecords.Count == 0))
        {
            DialogRoot.Instance.PopupMessageDialog(Localize("Fire control and penetration tables are empty."), Localize("Model Comparison"));
            return;
        }

        DialogRoot.Instance.PopupModelComparisonDialog(
            Localize("Model Comparison"),
            () => BuildModelComparisonContent(shipClass, batteryRecord),
            Localize("Close")
        );
    }

    VisualElement BuildModelComparisonContent(ShipClass shipClass, BatteryRecord batteryRecord)
    {
        var tabView = new TabView
        {
            name = "ModelComparisonTabView",
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        tabView.Add(BuildModelComparisonTab(Localize("Fire Control"), BuildFireControlModelComparisonContent(shipClass, batteryRecord)));
        tabView.Add(BuildModelComparisonTab(Localize("Penetration"), BuildPenetrationModelComparisonContent(batteryRecord)));
        return tabView;
    }

    Tab BuildModelComparisonTab(string label, VisualElement content)
    {
        var tab = new Tab
        {
            label = label,
            style =
            {
                flexGrow = 1,
            }
        };
        tab.Add(content);
        return tab;
    }

    VisualElement BuildFireControlModelComparisonContent(ShipClass shipClass, BatteryRecord batteryRecord)
    {
        if (batteryRecord.fireControlTableRecords == null || batteryRecord.fireControlTableRecords.Count == 0)
        {
            var empty = new Label(Localize("Fire control table is empty."));
            empty.style.whiteSpace = WhiteSpace.Normal;
            return empty;
        }

        var records = batteryRecord.fireControlTableRecords
            .OrderBy(record => record.speedThresholdKnot)
            .ToList();
        var observedLeftTop = records[0].shortBroad;
        var codeLatent = PredictFireControlLatentFromCode(shipClass, batteryRecord, out var usedCodeCoefficient);

        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;

        var roundPredictionToggle = new Toggle(Localize("Round predicted values"));
        roundPredictionToggle.value = true;
        roundPredictionToggle.style.marginBottom = 8;
        scrollView.Add(roundPredictionToggle);

        var summary = new Label();
        summary.style.whiteSpace = WhiteSpace.Normal;
        summary.style.marginBottom = 10;
        scrollView.Add(summary);

        if (!usedCodeCoefficient)
        {
            var warning = new Label(Localize("No fitted Code coefficient is available for this fire-control code. The Code model uses the component fallback."));
            warning.style.whiteSpace = WhiteSpace.Normal;
            warning.style.marginBottom = 10;
            scrollView.Add(warning);
        }

        var tablesContainer = new VisualElement();
        scrollView.Add(tablesContainer);

        void RefreshComparison()
        {
            var roundPredictions = roundPredictionToggle.value;
            var proportionStats = CalculateFireControlComparisonStats(records, observedLeftTop, roundPredictions);
            var codeStats = CalculateFireControlComparisonStats(records, codeLatent, roundPredictions);

            summary.text =
                $"{Localize("Overall Error")}\n" +
                $"{Localize("Proportion table")} (left-top={observedLeftTop:0.###}): {FormatFireControlErrorStats(proportionStats)}\n" +
                $"{Localize("Code model table")} (latent={codeLatent:0.###}): {FormatFireControlErrorStats(codeStats)}";

            tablesContainer.Clear();
            tablesContainer.Add(BuildFireControlComparisonTable(
                Localize("Proportion Check"),
                Localize("Uses the current left-top value, then applies the fitted Broad/Narrow, range-band, and speed ratios."),
                records,
                observedLeftTop,
                roundPredictions
            ));
            tablesContainer.Add(BuildFireControlComparisonTable(
                Localize("Code, Shell Size, Displacement Model"),
                Localize("Uses the fitted latent left-top value from Code, shell size, and displacement, then applies the same ratios."),
                records,
                codeLatent,
                roundPredictions
            ));
        }

        roundPredictionToggle.RegisterValueChangedCallback(_ => RefreshComparison());
        RefreshComparison();

        return scrollView;
    }

    VisualElement BuildPenetrationModelComparisonContent(BatteryRecord batteryRecord)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;

        var records = (batteryRecord.penetrationTableRecords ?? new List<PenetrationTableRecord>())
            .GroupBy(record => record.distanceYards)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.distanceYards).First());
        var expectedDistances = GetExpectedPenetrationTableDistances(batteryRecord.rangeYards).ToList();

        var summary = new Label();
        summary.style.whiteSpace = WhiteSpace.Normal;
        summary.style.marginBottom = 10;
        scrollView.Add(summary);

        var stats = new PenetrationComparisonStats();
        foreach (var distanceYards in expectedDistances)
        {
            if (!records.TryGetValue(distanceYards, out var record))
            {
                stats.missingRows++;
                continue;
            }

            var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
            stats.AddRateOfFire(record.rateOfFire, prediction.rateOfFire);
            stats.AddVertical(record.verticalPenetrationInchs, prediction.verticalPenetrationInches);
            stats.AddHorizontal(record.horizontalPenetrationInchs, prediction.horizontalPenetrationInches);
            stats.AddRangeBand(record.rangeBand, prediction.rangeBand);
        }

        var extraRows = records.Keys.Count(distance => !expectedDistances.Contains(distance));
        summary.text =
            $"{Localize("Expected rows")}: {expectedDistances.Count}, {Localize("current rows")}: {records.Count}, {Localize("missing")}: {stats.missingRows}, {Localize("extra")}: {extraRows}\n" +
            $"{Localize("Rate of Fire")}: {FormatFireControlErrorStats(stats.rateOfFire)}\n" +
            $"{Localize("Vertical Penetration")}: {FormatFireControlErrorStats(stats.verticalPenetration)}\n" +
            $"{Localize("Horizontal Penetration")}: {FormatFireControlErrorStats(stats.horizontalPenetration)}\n" +
            $"{Localize("Range Band")}: exact {stats.rangeBandExact}/{stats.rangeBandCount} ({FormatPercent(stats.rangeBandCount == 0 ? 0f : (float)stats.rangeBandExact / stats.rangeBandCount)})";

        var description = new Label(Localize("Predictions use Battery-level fields and distance only. Expected penetration rows use fixed yard marks up to the first mark that covers the battery range."));
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.marginBottom = 8;
        scrollView.Add(description);

        scrollView.Add(BuildPenetrationComparisonTable(batteryRecord, expectedDistances, records));
        return scrollView;
    }

    VisualElement BuildPenetrationComparisonTable(BatteryRecord batteryRecord, List<float> expectedDistances, Dictionary<float, PenetrationTableRecord> records)
    {
        var section = new VisualElement();
        section.style.minWidth = 900;

        var table = new VisualElement();
        table.style.flexDirection = FlexDirection.Column;
        section.Add(table);

        var header = BuildFireControlComparisonTableRow();
        header.Add(BuildFireControlComparisonCell(Localize("Distance"), true, 82));
        header.Add(BuildFireControlComparisonCell(Localize("ROF"), true, 120));
        header.Add(BuildFireControlComparisonCell(Localize("Band"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Vert Pen"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Hor Pen"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Status"), true, 160));
        table.Add(header);

        foreach (var distanceYards in expectedDistances)
        {
            var row = BuildFireControlComparisonTableRow();
            var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
            row.Add(BuildFireControlComparisonCell($"{distanceYards:0}", true, 82));

            if (records.TryGetValue(distanceYards, out var record))
            {
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.rateOfFire, prediction.rateOfFire), false, 120));
                row.Add(BuildFireControlComparisonCell($"{record.rangeBand} / {prediction.rangeBand}\n{FormatRangeBandDiff(record.rangeBand, prediction.rangeBand)}", false, 128));
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.verticalPenetrationInchs, prediction.verticalPenetrationInches), false, 128));
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.horizontalPenetrationInchs, prediction.horizontalPenetrationInches), false, 128));
                row.Add(BuildFireControlComparisonCell(Localize("Current row"), false, 160));
            }
            else
            {
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.rateOfFire:0.0}", false, 120));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.rangeBand}", false, 128));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.verticalPenetrationInches:0.0}", false, 128));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.horizontalPenetrationInches:0.0}", false, 128));
                row.Add(BuildFireControlComparisonCell(Localize("Expected by range coverage"), false, 160));
            }

            table.Add(row);
        }

        var legend = new Label(Localize("Each cell is shown as current / model, then model-current delta. Missing rows show only model values."));
        legend.style.whiteSpace = WhiteSpace.Normal;
        legend.style.marginTop = 4;
        section.Add(legend);

        return section;
    }

    static readonly float[] PenetrationTableDistanceYards =
    {
        2000f, 4000f, 6000f, 8000f, 10000f, 12000f, 15000f,
        18000f, 21000f, 24000f, 27000f, 30000f, 33000f, 36000f
    };

    static IEnumerable<float> GetExpectedPenetrationTableDistances(float rangeYards)
    {
        if (rangeYards <= 0f)
        {
            yield return PenetrationTableDistanceYards[0];
            yield break;
        }

        foreach (var distance in PenetrationTableDistanceYards)
        {
            yield return distance;
            if (distance >= rangeYards)
                yield break;
        }
    }

    static PenetrationPrediction PredictPenetrationRecord(BatteryRecord batteryRecord, float distanceYards)
    {
        return new PenetrationPrediction
        {
            distanceYards = distanceYards,
            rateOfFire = PredictPenetrationRateOfFire(batteryRecord, distanceYards),
            rangeBand = PredictPenetrationRangeBand(batteryRecord, distanceYards),
            verticalPenetrationInches = PredictVerticalPenetrationInches(batteryRecord, distanceYards),
            horizontalPenetrationInches = PredictHorizontalPenetrationInches(batteryRecord, distanceYards),
        };
    }

    static List<PenetrationTableRecord> BuildModelPenetrationTableRecords(BatteryRecord batteryRecord)
    {
        return GetExpectedPenetrationTableDistances(batteryRecord?.rangeYards ?? 0f)
            .Select(distanceYards =>
            {
                var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
                return new PenetrationTableRecord
                {
                    distanceYards = prediction.distanceYards,
                    rateOfFire = prediction.rateOfFire,
                    rangeBand = prediction.rangeBand,
                    horizontalPenetrationInchs = prediction.horizontalPenetrationInches,
                    verticalPenetrationInchs = prediction.verticalPenetrationInches,
                };
            })
            .ToList();
    }

    static int ResetPenetrationTableFromModel(BatteryRecord batteryRecord)
    {
        if (batteryRecord == null)
            return 0;

        var modelRecords = BuildModelPenetrationTableRecords(batteryRecord);
        batteryRecord.penetrationTableRecords ??= new List<PenetrationTableRecord>();
        batteryRecord.penetrationTableRecords.Clear();
        batteryRecord.penetrationTableRecords.AddRange(modelRecords);
        return modelRecords.Count;
    }

    static float PredictPenetrationRateOfFire(BatteryRecord batteryRecord, float distanceYards)
    {
        const float fixedProcessSeconds = 9.090133f;
        const float equivalentVelocityYardsPerSecond = 371.07068f;
        var cap = 120f / (fixedProcessSeconds + distanceYards / equivalentVelocityYardsPerSecond);
        var inherent = Mathf.Max(0f, (batteryRecord?.maxRateOfFireShootPerMin ?? 0f) * 2f);
        return RoundTenth(Mathf.Min(inherent, cap));
    }

    static RangeBand PredictPenetrationRangeBand(BatteryRecord batteryRecord, float distanceYards)
    {
        var rangeYards = batteryRecord?.rangeYards ?? 0f;
        if (rangeYards <= 0f)
            return RangeBand.Short;

        var rel = distanceYards / rangeYards;
        var shellSize = batteryRecord?.shellSizeInch ?? 0f;

        var shortToMedium = 0.56f;
        var mediumToLong = 0.90f;
        var longToExtreme = 1.05f;

        if (rangeYards <= 5900f)
        {
            shortToMedium -= 0.08f;
            mediumToLong -= 0.10f;
        }

        if (shellSize >= 12f)
        {
            shortToMedium += 0.08f;
            mediumToLong += 0.08f;
            longToExtreme += 0.08f;
        }

        if (rel < shortToMedium)
            return RangeBand.Short;
        if (rel < mediumToLong)
            return RangeBand.Medium;
        if (rel < longToExtreme)
            return RangeBand.Long;
        return RangeBand.Extreme;
    }

    static float PredictVerticalPenetrationInches(BatteryRecord batteryRecord, float distanceYards)
    {
        var shellSize = Mathf.Max(0.1f, batteryRecord?.shellSizeInch ?? 0f);
        var shellWeight = Mathf.Max(0.1f, batteryRecord?.shellWeightPounds ?? 0f);
        var rangeYards = Mathf.Max(1f, batteryRecord?.rangeYards ?? 0f);
        var maxRof = batteryRecord?.maxRateOfFireShootPerMin ?? 0f;
        var distanceKyd = distanceYards / 1000f;
        var logShellSize = Mathf.Log(shellSize);
        var logRange = Mathf.Log(rangeYards);
        var logValue = -7.19567f
            - 0.596694f * logShellSize
            + 0.702142f * Mathf.Log(shellWeight)
            + 0.733421f * logRange
            + 0.0331102f * maxRof
            + 0.402345f * distanceKyd
            + 0.00675885f * distanceKyd * distanceKyd
            + 0.0367314f * logShellSize * distanceKyd
            - 0.0718001f * logRange * distanceKyd;

        return RoundTenth(Mathf.Exp(logValue));
    }

    static float PredictHorizontalPenetrationInches(BatteryRecord batteryRecord, float distanceYards)
    {
        var shellSize = Mathf.Max(0.1f, batteryRecord?.shellSizeInch ?? 0f);
        var shellWeight = Mathf.Max(0.1f, batteryRecord?.shellWeightPounds ?? 0f);
        var rangeYards = Mathf.Max(1f, batteryRecord?.rangeYards ?? 0f);
        var maxRof = batteryRecord?.maxRateOfFireShootPerMin ?? 0f;
        var rel = distanceYards / rangeYards;
        var logValue = -13.5807f
            - 0.404477f * Mathf.Log(shellSize)
            + 0.492548f * Mathf.Log(shellWeight)
            + 1.01641f * Mathf.Log(rangeYards)
            - 0.0211344f * maxRof
            + 3.84280f * rel
            - 1.27663f * rel * rel;

        return RoundTenth(Mathf.Exp(logValue));
    }

    static string FormatPenetrationActualPredicted(float actual, float predicted)
    {
        return $"{actual:0.0} / {predicted:0.0}\n{FormatFireControlDiff(predicted - actual, true)}";
    }

    static string FormatRangeBandDiff(RangeBand actual, RangeBand predicted)
    {
        return actual == predicted ? "0" : $"{(int)predicted - (int)actual:+0;-0;0}";
    }

    static string FormatPercent(float value)
    {
        return $"{100f * value:0.#}%";
    }

    VisualElement BuildFireControlComparisonTable(string title, string description, List<FireControlTableRecord> records, float leftTop, bool roundPredictions)
    {
        var section = new VisualElement();
        section.style.marginTop = 10;
        section.style.marginBottom = 12;

        var titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 2;
        section.Add(titleLabel);

        var descriptionLabel = new Label(description);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginBottom = 6;
        section.Add(descriptionLabel);

        var table = new VisualElement();
        table.style.flexDirection = FlexDirection.Column;
        table.style.minWidth = 920;
        section.Add(table);

        var header = BuildFireControlComparisonTableRow();
        header.Add(BuildFireControlComparisonCell(Localize("Tgt Spd"), true, 74));
        foreach (var column in FireControlComparisonColumns)
        {
            header.Add(BuildFireControlComparisonCell(column.label, true));
        }
        table.Add(header);

        foreach (var record in records)
        {
            var row = BuildFireControlComparisonTableRow();
            row.Add(BuildFireControlComparisonCell($"{record.speedThresholdKnot:0.#} kt", true, 74));
            foreach (var column in FireControlComparisonColumns)
            {
                var actual = record.GetValue(column.rangeBand, column.targetAspect);
                var predicted = PredictFireControlCell(leftTop, record.speedThresholdKnot, column.rangeBand, column.targetAspect, roundPredictions);
                var diff = predicted - actual;
                row.Add(BuildFireControlComparisonCell($"{actual:0.#} / {FormatFireControlPredictedValue(predicted, roundPredictions)}\n{FormatFireControlDiff(diff, roundPredictions)}", false));
            }
            table.Add(row);
        }

        var legend = new Label(Localize("Each cell is shown as current / model, then model-current delta."));
        legend.style.whiteSpace = WhiteSpace.Normal;
        legend.style.marginTop = 4;
        section.Add(legend);

        return section;
    }

    static VisualElement BuildFireControlComparisonTableRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexShrink = 0;
        return row;
    }

    static Label BuildFireControlComparisonCell(string text, bool isHeader, int width = 96)
    {
        var cell = new Label(text);
        cell.style.width = width;
        cell.style.minHeight = isHeader ? 24 : 40;
        cell.style.paddingLeft = 4;
        cell.style.paddingRight = 4;
        cell.style.paddingTop = 3;
        cell.style.paddingBottom = 3;
        cell.style.marginRight = 1;
        cell.style.marginBottom = 1;
        cell.style.unityTextAlign = TextAnchor.MiddleCenter;
        cell.style.whiteSpace = WhiteSpace.Normal;
        cell.style.backgroundColor = isHeader ? new Color(0.16f, 0.16f, 0.16f, 0.18f) : new Color(0.16f, 0.16f, 0.16f, 0.08f);
        if (isHeader)
            cell.style.unityFontStyleAndWeight = FontStyle.Bold;
        return cell;
    }

    static FireControlErrorStats CalculateFireControlComparisonStats(List<FireControlTableRecord> records, float leftTop, bool roundPredictions)
    {
        var stats = new FireControlErrorStats();
        foreach (var record in records)
        {
            foreach (var column in FireControlComparisonColumns)
            {
                var actual = record.GetValue(column.rangeBand, column.targetAspect);
                var predicted = PredictFireControlCell(leftTop, record.speedThresholdKnot, column.rangeBand, column.targetAspect, roundPredictions);
                stats.Add(actual, predicted);
            }
        }
        return stats;
    }

    static string FormatFireControlErrorStats(FireControlErrorStats stats)
    {
        if (stats.count == 0)
            return "n/a";

        return $"exact {stats.exact}/{stats.count} ({(100f * stats.exact / stats.count):0.#}%), MAE {stats.MAE:0.###}, RMSE {stats.RMSE:0.###}, max {stats.maxAbs:0.###}";
    }

    static float PredictFireControlLatentFromCode(ShipClass shipClass, BatteryRecord batteryRecord, out bool usedCodeCoefficient)
    {
        var fcs = batteryRecord?.fireControlType;
        if (fcs != null && FireControlCodeLatentOffsets.TryGetValue(fcs.code, out var codeOffset))
        {
            usedCodeCoefficient = true;
            return FireControlCodeLatentIntercept
                + codeOffset
                + FireControlCodeShellSizeCoef * (batteryRecord?.shellSizeInch ?? 0f)
                + FireControlCodeDisplacement1000Coef * ((shipClass?.displacementTons ?? 0f) / 1000f);
        }

        usedCodeCoefficient = false;
        return PredictFireControlLatentFromComponents(shipClass, batteryRecord);
    }

    static float PredictFireControlLatentFromComponents(ShipClass shipClass, BatteryRecord batteryRecord)
    {
        var fcs = batteryRecord?.fireControlType;
        var latent = 4.7316f
            + 0.2128f * (batteryRecord?.shellSizeInch ?? 0f)
            - 0.0893f * ((shipClass?.displacementTons ?? 0f) / 1000f);

        if (fcs == null)
            return latent;

        if (fcs.gunSight == GunSightType.Telescope)
            latent += 1.8941f;
        if (fcs.fireControlInstrument == FireControlInstrumentType.Basic)
            latent += 2.1650f;
        if (fcs.rangeFinder == RangeFinderType.Optical)
            latent += 1.5337f;
        if (fcs.directorControl == DirectorControlType.FollowThePointer)
            latent += 1.9988f;

        return latent;
    }

    static string FormatFireControlPredictedValue(float value, bool roundPredictions)
    {
        return roundPredictions ? $"{value:0.#}" : $"{value:0.00}";
    }

    static string FormatFireControlDiff(float value, bool roundPredictions)
    {
        return roundPredictions ? $"{value:+0.#;-0.#;0}" : $"{value:+0.00;-0.00;0.00}";
    }

    static void UpdateFireControlTableFromCodeModel(ShipClass shipClass, BatteryRecord batteryRecord)
    {
        if (batteryRecord == null)
            return;

        batteryRecord.fireControlTableRecords ??= new List<FireControlTableRecord>();
        if (batteryRecord.fireControlTableRecords.Count == 0)
        {
            foreach (var speedFactor in FireControlSpeedFactors)
            {
                batteryRecord.fireControlTableRecords.Add(new FireControlTableRecord
                {
                    speedThresholdKnot = speedFactor.speedThresholdKnot
                });
            }
        }

        var latent = PredictFireControlLatentFromCode(shipClass, batteryRecord, out _);
        foreach (var record in batteryRecord.fireControlTableRecords)
        {
            record.shortBroad = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Short, TargetAspect.Broad, true);
            record.shortNarrow = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Short, TargetAspect.Narrow, true);
            record.mediumBroad = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Medium, TargetAspect.Broad, true);
            record.mediumNarrow = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Medium, TargetAspect.Narrow, true);
            record.longBroad = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Long, TargetAspect.Broad, true);
            record.longNarrow = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Long, TargetAspect.Narrow, true);
            record.extremeBroad = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Extreme, TargetAspect.Broad, true);
            record.extremeNarrow = PredictFireControlCell(latent, record.speedThresholdKnot, RangeBand.Extreme, TargetAspect.Narrow, true);
        }
    }

    static float PredictFireControlCell(float leftTop, float speedThresholdKnot, RangeBand rangeBand, TargetAspect targetAspect, bool roundPrediction)
    {
        var predicted = leftTop * GetFireControlSpeedFactor(speedThresholdKnot) * GetFireControlRangeBandFactor(rangeBand) * GetFireControlAspectFactor(targetAspect);
        return roundPrediction ? RoundHalfUp(predicted) : predicted;
    }

    static float GetFireControlAspectFactor(TargetAspect targetAspect)
    {
        return targetAspect == TargetAspect.Narrow ? 0.6005f : 1f;
    }

    static float GetFireControlRangeBandFactor(RangeBand rangeBand)
    {
        return rangeBand switch
        {
            RangeBand.Medium => 0.6010f,
            RangeBand.Long => 0.4165f,
            RangeBand.Extreme => 0.3567f,
            _ => 1f
        };
    }

    static float GetFireControlSpeedFactor(float speedThresholdKnot)
    {
        const float tolerance = 0.01f;
        foreach (var speedFactor in FireControlSpeedFactors)
        {
            if (Mathf.Abs(speedThresholdKnot - speedFactor.speedThresholdKnot) <= tolerance)
                return speedFactor.factor;
        }

        if (speedThresholdKnot <= FireControlSpeedFactors[0].speedThresholdKnot)
            return FireControlSpeedFactors[0].factor;
        if (speedThresholdKnot >= FireControlSpeedFactors[^1].speedThresholdKnot)
            return FireControlSpeedFactors[^1].factor;

        for (var i = 1; i < FireControlSpeedFactors.Length; i++)
        {
            var previous = FireControlSpeedFactors[i - 1];
            var next = FireControlSpeedFactors[i];
            if (speedThresholdKnot <= next.speedThresholdKnot)
            {
                var t = Mathf.InverseLerp(previous.speedThresholdKnot, next.speedThresholdKnot, speedThresholdKnot);
                return Mathf.Lerp(previous.factor, next.factor, t);
            }
        }

        return 1f;
    }

    static float RoundHalfUp(float value)
    {
        return Mathf.Floor(value + 0.5f);
    }

    static float RoundTenth(float value)
    {
        return Mathf.Floor(value * 10f + 0.5f) / 10f;
    }

    readonly struct FireControlComparisonColumn
    {
        public readonly string label;
        public readonly RangeBand rangeBand;
        public readonly TargetAspect targetAspect;

        public FireControlComparisonColumn(string label, RangeBand rangeBand, TargetAspect targetAspect)
        {
            this.label = label;
            this.rangeBand = rangeBand;
            this.targetAspect = targetAspect;
        }
    }

    readonly struct FireControlSpeedFactor
    {
        public readonly float speedThresholdKnot;
        public readonly float factor;

        public FireControlSpeedFactor(float speedThresholdKnot, float factor)
        {
            this.speedThresholdKnot = speedThresholdKnot;
            this.factor = factor;
        }
    }

    class FireControlErrorStats
    {
        public int count;
        public int exact;
        public float sumAbs;
        public float sumSquared;
        public float maxAbs;

        public float MAE => count == 0 ? 0f : sumAbs / count;
        public float RMSE => count == 0 ? 0f : Mathf.Sqrt(sumSquared / count);

        public void Add(float actual, float predicted)
        {
            var abs = Mathf.Abs(predicted - actual);
            count++;
            if (abs <= 0.001f)
                exact++;
            sumAbs += abs;
            sumSquared += abs * abs;
            maxAbs = Mathf.Max(maxAbs, abs);
        }
    }

    class PenetrationComparisonStats
    {
        public readonly FireControlErrorStats rateOfFire = new();
        public readonly FireControlErrorStats verticalPenetration = new();
        public readonly FireControlErrorStats horizontalPenetration = new();
        public int rangeBandCount;
        public int rangeBandExact;
        public int missingRows;

        public void AddRateOfFire(float actual, float predicted) => rateOfFire.Add(actual, predicted);
        public void AddVertical(float actual, float predicted) => verticalPenetration.Add(actual, predicted);
        public void AddHorizontal(float actual, float predicted) => horizontalPenetration.Add(actual, predicted);

        public void AddRangeBand(RangeBand actual, RangeBand predicted)
        {
            rangeBandCount++;
            if (actual == predicted)
                rangeBandExact++;
        }
    }

    class PenetrationPrediction
    {
        public float distanceYards;
        public float rateOfFire;
        public RangeBand rangeBand;
        public float verticalPenetrationInches;
        public float horizontalPenetrationInches;
    }

    static string BuildFireControlSignature(IEnumerable<FireControlTableRecord> fireControlTableRecords)
    {
        return string.Join(",",
            (fireControlTableRecords ?? Enumerable.Empty<FireControlTableRecord>())
                .Select(record => $"{record.speedThresholdKnot:0.###}:{record.shortBroad:0.###}:{record.shortNarrow:0.###}:{record.mediumBroad:0.###}:{record.mediumNarrow:0.###}:{record.longBroad:0.###}:{record.longNarrow:0.###}:{record.extremeBroad:0.###}:{record.extremeNarrow:0.###}"));
    }

    VisualElement BuildBatteryFigureLegend()
    {
        var legendColumn = new VisualElement();
        legendColumn.style.width = 200;
        legendColumn.style.minWidth = 200;
        legendColumn.style.marginLeft = 10;
        legendColumn.style.paddingTop = 8;
        legendColumn.style.flexDirection = FlexDirection.Column;
        legendColumn.style.alignItems = Align.FlexStart;

        legendColumn.Add(BuildLegendItem(new Color(0.75f, 0.2f, 0.18f, 1f), Localize("Vertical Penetration (in)")));
        legendColumn.Add(BuildLegendItem(new Color(0.16f, 0.42f, 0.78f, 1f), Localize("Horizontal Penetration (in)")));
        legendColumn.Add(BuildLegendItem(new Color(0.12f, 0.6f, 0.24f, 1f), Localize("Fire Control (Lowest Speed Broad)")));

        return legendColumn;
    }

    VisualElement BuildLegendItem(Color color, string text)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.alignItems = Align.Center;
        item.style.marginBottom = 6;

        var swatch = new VisualElement();
        swatch.style.width = 10;
        swatch.style.height = 10;
        swatch.style.backgroundColor = color;
        swatch.style.marginRight = 4;
        item.Add(swatch);

        var label = new Label(text);
        label.style.fontSize = 10;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexGrow = 1;
        item.Add(label);

        return item;
    }

    static List<BatteryFigurePoint> BuildBatteryFigurePoints(BatteryRecord batteryRecord)
    {
        var lowestSpeedFireControlRow = (batteryRecord?.fireControlTableRecords ?? new List<FireControlTableRecord>())
            .OrderBy(record => record.speedThresholdKnot)
            .FirstOrDefault();

        return (batteryRecord?.penetrationTableRecords ?? new List<PenetrationTableRecord>())
            .OrderBy(record => record.distanceYards)
            .Select(record => new BatteryFigurePoint
            {
                distanceYards = record.distanceYards,
                verticalPenetrationInches = record.verticalPenetrationInchs,
                horizontalPenetrationInches = record.horizontalPenetrationInchs,
                fireControlValue = lowestSpeedFireControlRow?.GetValue(record.rangeBand, TargetAspect.Broad) ?? 0f
            })
            .ToList();
    }

    void ClearDefaultPlaceholderPreviewState()
    {
        DisposeDefaultPlaceholderPreviewTexture();
        lastDefaultPlaceholderSignature = null;
        lastDefaultPlaceholderShipObjectId = null;
    }

    void DisposeDefaultPlaceholderPreviewTexture()
    {
        if (defaultPlaceholderPreviewImage != null)
            defaultPlaceholderPreviewImage.image = null;

        if (defaultPlaceholderPreviewTexture != null)
        {
            Destroy(defaultPlaceholderPreviewTexture);
            defaultPlaceholderPreviewTexture = null;
        }
    }

    static bool IsElementActuallyVisible(VisualElement element)
    {
        return element != null
            && element.resolvedStyle.display != DisplayStyle.None
            && element.worldBound.width > 1f
            && element.worldBound.height > 1f;
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    static void RefreshPictureField(VisualElement fieldRoot, PictureReference pictureReference)
    {
        if (fieldRoot == null || pictureReference == null)
            return;

        var textField = fieldRoot.Q<TextField>();
        if (textField != null)
            textField.SetValueWithoutNotify(pictureReference.path);

        var toggle = fieldRoot.Q<Toggle>();
        if (toggle != null)
            toggle.SetValueWithoutNotify(pictureReference.isBuiltin);
    }
}
