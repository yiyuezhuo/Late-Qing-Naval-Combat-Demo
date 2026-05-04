using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;

using NavalCombatCore;
using CoreUtils;
using YYZ;

public class HistoryPieSlice
{
    public string label;
    public float value;
    public Color color;
    public int hitCount;
    public List<float> hitValues = new();
}

class HistoryBarSegment
{
    public string label;
    public int count;
    public Color color;
}

class HistoryBarItem
{
    public string label;
    public List<HistoryBarSegment> segments = new();
    public int TotalCount => segments.Sum(segment => Mathf.Max(0, segment.count));
}

class HistoryLegendItem
{
    public string label;
    public int count;
    public Color color;
}

class MountFiringLogDetailRow
{
    public string mountLabel;
    public MountFiringRecord log;
}

class RapidFiringLogDetailRow
{
    public RapidFiringLog log;
}

[UxmlElement]
public partial class HistoryPieChart : VisualElement
{
    List<HistoryPieSlice> slices = new();

    public HistoryPieChart()
    {
        style.flexGrow = 1;
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetSlices(IEnumerable<HistoryPieSlice> newSlices)
    {
        slices = newSlices?.Where(s => s != null && s.value > 0).ToList() ?? new();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        var width = contentRect.width;
        var height = contentRect.height;
        var radius = Mathf.Max(0, Mathf.Min(width, height) * 0.5f - 6f);
        var center = new Vector2(width * 0.5f, height * 0.5f);

        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;

        var total = slices.Sum(s => Mathf.Max(0, s.value));
        if (radius <= 0)
            return;

        if (total <= 0.0001f)
        {
            DrawFullCircle(painter, center, radius, new Color(0.85f, 0.85f, 0.85f, 1f));
            return;
        }

        if (slices.Count == 1)
        {
            DrawFullCircle(painter, center, radius, slices[0].color);
            return;
        }

        var startAngle = -90f;
        foreach (var slice in slices)
        {
            var sweep = 360f * slice.value / total;
            if (sweep <= 0.01f)
                continue;

            if (sweep >= 359.99f)
            {
                DrawFullCircle(painter, center, radius, slice.color);
                return;
            }

            painter.fillColor = slice.color;
            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(center);
            painter.Arc(center, radius, startAngle, startAngle + sweep);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            startAngle += sweep;
        }
    }

    void DrawFullCircle(Painter2D painter, Vector2 center, float radius, Color fillColor)
    {
        painter.fillColor = fillColor;
        painter.strokeColor = Color.black;
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }
}

[UxmlElement]
public partial class HistoryStackedBarChart : VisualElement
{
    const float TopPadding = 8f;
    const float SidePadding = 8f;
    const float BottomPadding = 66f;
    const float BarGap = 18f;
    const float SegmentLabelPreferredHeight = 16f;

    readonly VisualElement labelLayer = new();
    List<HistoryBarItem> bars = new();

    public HistoryStackedBarChart()
    {
        style.flexGrow = 1;
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

    internal void SetBars(IEnumerable<HistoryBarItem> newBars)
    {
        bars = newBars?.Where(bar => bar != null && bar.TotalCount > 0).ToList() ?? new();
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0 || bars.Count == 0)
            return;

        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;
        painter.strokeColor = Color.black;

        var maxTotal = Mathf.Max(1, bars.Max(bar => bar.TotalCount));
        var barRects = GetBarRects(chartRect);
        var baselineY = chartRect.yMax;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, baselineY));
        painter.LineTo(new Vector2(chartRect.xMax, baselineY));
        painter.Stroke();

        for (int barIdx = 0; barIdx < bars.Count; barIdx++)
        {
            var bar = bars[barIdx];
            var barRect = barRects[barIdx];
            var currentTop = chartRect.yMax;

            foreach (var segment in bar.segments.Where(segment => segment.count > 0))
            {
                var segmentHeight = chartRect.height * segment.count / maxTotal;
                currentTop -= segmentHeight;
                var segmentRect = new Rect(barRect.x, currentTop, barRect.width, segmentHeight);
                DrawRect(painter, segmentRect, segment.color);
            }
        }
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0 || bars.Count == 0)
            return;

        var maxTotal = Mathf.Max(1, bars.Max(bar => bar.TotalCount));
        var barRects = GetBarRects(chartRect);

        for (int barIdx = 0; barIdx < bars.Count; barIdx++)
        {
            var bar = bars[barIdx];
            var barRect = barRects[barIdx];
            var currentTop = chartRect.yMax;

            foreach (var segment in bar.segments.Where(segment => segment.count > 0))
            {
                var segmentHeight = chartRect.height * segment.count / maxTotal;
                currentTop -= segmentHeight;
                var labelHeight = Mathf.Max(segmentHeight, SegmentLabelPreferredHeight);
                var labelTop = currentTop + segmentHeight * 0.5f - labelHeight * 0.5f;

                var label = BuildOverlayLabel(
                    BuildSegmentText(segment.count, bar.TotalCount, barRect.width),
                    barRect.x,
                    labelTop,
                    barRect.width,
                    labelHeight);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.fontSize = 11;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.color = ResolveSegmentLabelTextColor(segment.color);
                labelLayer.Add(label);
            }

            var xLabelWidth = Mathf.Max(barRect.width + 18f, 68f);
            var xLabel = BuildOverlayLabel(
                bar.label,
                barRect.x + barRect.width * 0.5f - xLabelWidth * 0.5f,
                chartRect.yMax + 6f,
                xLabelWidth,
                BottomPadding - 8f);
            xLabel.style.whiteSpace = WhiteSpace.Normal;
            xLabel.style.fontSize = 11;
            xLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            xLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            labelLayer.Add(xLabel);
        }
    }

    Rect GetChartRect()
    {
        var width = Mathf.Max(0, contentRect.width - SidePadding * 2f);
        var height = Mathf.Max(0, contentRect.height - TopPadding - BottomPadding);
        return new Rect(SidePadding, TopPadding, width, height);
    }

    List<Rect> GetBarRects(Rect chartRect)
    {
        if (bars.Count == 0)
            return new();

        var totalGap = BarGap * Mathf.Max(0, bars.Count - 1);
        var rawBarWidth = (chartRect.width - totalGap) / Mathf.Max(1, bars.Count);
        var barWidth = Mathf.Max(14f, rawBarWidth);
        if (barWidth * bars.Count + totalGap > chartRect.width)
        {
            barWidth = Mathf.Max(10f, (chartRect.width - totalGap) / Mathf.Max(1, bars.Count));
        }

        var totalBarsWidth = barWidth * bars.Count + totalGap;
        var startX = chartRect.xMin + Mathf.Max(0, (chartRect.width - totalBarsWidth) * 0.5f);
        var rects = new List<Rect>(bars.Count);
        for (int idx = 0; idx < bars.Count; idx++)
        {
            rects.Add(new Rect(startX + idx * (barWidth + BarGap), chartRect.yMin, barWidth, chartRect.height));
        }
        return rects;
    }

    static Label BuildOverlayLabel(string text, float x, float y, float width, float height, Color? color = null)
    {
        var label = new Label(text);
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = x;
        label.style.top = y;
        label.style.width = width;
        label.style.height = height;
        if (color.HasValue)
        {
            label.style.color = color.Value;
        }
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.fontSize = 11;
        label.style.overflow = Overflow.Visible;
        return label;
    }

    static void DrawRect(Painter2D painter, Rect rect, Color fillColor)
    {
        if (rect.width <= 0 || rect.height <= 0)
            return;

        painter.fillColor = fillColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMax));
        painter.LineTo(new Vector2(rect.xMin, rect.yMax));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }

    static string BuildSegmentText(int count, int totalCount, float availableWidth)
    {
        var ratio = totalCount <= 0 ? 0f : (float)count / totalCount;
        var ratioText = $"({Mathf.RoundToInt(ratio * 100f)}%)";
        var singleLineText = $"{count} {ratioText}";
        return availableWidth >= EstimateSingleLineWidth(singleLineText)
            ? singleLineText
            : $"{count}\n{ratioText}";
    }

    static float EstimateSingleLineWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        return text.Length * 6.6f + 10f;
    }

    static Color ResolveSegmentLabelTextColor(Color background)
    {
        return new Color(0.95f, 0.91f, 0.82f, 1f);
    }
}

public class ShipLogView
{
    public VisualElement root;

    VisualElement shipLogView;
    VisualElement historyTabContent;
    VisualElement currentDpLossLegend;
    VisualElement allHitsLegend;
    VisualElement outgoingDpByTargetLegend;
    VisualElement outgoingWeaponTargetLegend;
    VisualElement batteryArmorLocationLegend;
    VisualElement outgoingWeaponFireLegend;
    HistoryPieChart currentDpLossChart;
    HistoryPieChart allHitsChart;
    HistoryPieChart outgoingDpByTargetChart;
    HistoryPieChart outgoingWeaponTargetChart;
    HistoryStackedBarChart batteryArmorLocationChart;
    HistoryStackedBarChart outgoingWeaponFireChart;
    string lastHistorySignature;
    ListView waypointListView;
    readonly List<LatLon> emptyWaypointList = new();

    readonly Color32[] historyChartPalette =
    {
        new(51, 102, 153, 255),
        new(191, 87, 0, 255),
        new(46, 125, 50, 255),
        new(123, 31, 162, 255),
        new(194, 24, 91, 255),
        new(0, 121, 107, 255),
        new(97, 97, 97, 255),
        new(255, 179, 0, 255),
    };

    readonly Color32 penetrateDetonateColor = new(154, 68, 30, 255);
    readonly Color32 passThroughColor = new(142, 112, 28, 255);
    readonly Color32 noPenetrationColor = new(63, 88, 118, 255);
    readonly Color32 hitColor = new(50, 118, 64, 255);
    readonly Color32 missColor = new(112, 112, 112, 255);

    ShipLog GetSelectedShipLog()
    {
        if(Utils.TryResolveCurrentValueForBinding<ShipLog>(shipLogView, out var shipLog))
        {
            return shipLog;
        }
        return null;
    }

    public void Bind()
    {
        shipLogView = root.Q<VisualElement>("ShipLogView"); // selectedShipLog Provider
        // ShipLog GetSelectedShipLog()
        // {
        //     return _GetSelectedShipLog(shipLogView);
        // }

        var batteryStatusListView = root.Q<ListView>("BatteryStatusListView");
        Utils.BindItemsAddedRemoved<NavalCombatCore.BatteryStatus>(batteryStatusListView, () => GetSelectedShipLog());
        // MountStatusMultiColumnListView
        batteryStatusListView.makeItem = () =>
        {
            var batteryStatusElement = batteryStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(batteryStatusElement);

            var mountStatusMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("MountStatusMultiColumnListView");
            Utils.BindItemsAddedRemoved<MountStatusRecord>(mountStatusMultiColumnListView, () =>
            {
                var ctx = batteryStatusElement.GetHierarchicalDataSourceContext(); // 
                var isSucc = PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out NavalCombatCore.BatteryStatus bs);

                return bs;
            }); // TODO: Not always valid?

            var firingTargetColumn = mountStatusMultiColumnListView.columns["firingTarget"];
            firingTargetColumn.makeCell = () =>
            {
                var el = firingTargetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (SuperGameState.Instance.IsInNavalGame())
                    {
                        var ctx = setButton.GetHierarchicalDataSourceContext();
                        if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                        {
                            GameManager.Instance.selectedMountStatusRecordObjectId = mountStatus.objectId;
                            GameManager.Instance.state = GameManager.State.SelectingFiringTarget;
                            // SoftHide();
                            SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                        }
                    }
                };

                return el;
            };

            var detailColumn = mountStatusMultiColumnListView.columns["detail"];
            detailColumn.makeCell = () =>
            {
                var el = detailColumn.cellTemplate.CloneTree();

                var detailButton = el.Q<Button>("DetailButton");
                detailButton.clicked += () =>
                {
                    var ctx = detailButton.GetHierarchicalDataSourceContext();
                    // TODO: Transfer to Utils.TryResolveCurrentValueForBinding
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                    {
                        PopupMountDetailDialog(mountStatus);
                    }
                };

                return el;
            };

            var fireControlSystemMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("FireControlSystemMultiColumnListView");
            Utils.BindItemsAddedRemoved<FireControlSystemStatusRecord>(
                fireControlSystemMultiColumnListView,
                Utils.MakeDynamicResolveProvider<NavalCombatCore.BatteryStatus>(batteryStatusElement)
            );

            var targetColumn = fireControlSystemMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();
                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (SuperGameState.Instance.IsInNavalGame())
                    {
                        if (Utils.TryResolveCurrentValueForBinding(el, out FireControlSystemStatusRecord r))
                        {
                            GameManager.Instance.selectedFireControlSystemStatusRecordObjectId = r.objectId;
                            GameManager.Instance.state = GameManager.State.SelectingFireControlSystemTarget;
                            // SoftHide();
                            SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                        }
                    }
                };
                return el;
            };

            var batteryDetailButton = batteryStatusElement.Q<Button>("BatteryDetailButton");
            batteryDetailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(batteryDetailButton, out NavalCombatCore.BatteryStatus batteryStatus))
                {
                    PopupBatteryDetailDialog(batteryStatus);
                }
            };

            return batteryStatusElement;
        };

        var torpedoMountStatusMultiColumnListView = root.Q<MultiColumnListView>("TorpedoMountStatusMultiColumnListView");
        Utils.BindItemsAddedRemoved<MountStatusRecord>(torpedoMountStatusMultiColumnListView, () =>
        {
            return GetSelectedShipLog();
        });
        var torpedoMountStatusFiringTargetColumn = torpedoMountStatusMultiColumnListView.columns["firingTarget"];
        torpedoMountStatusFiringTargetColumn.makeCell = () =>
        {
            var el = torpedoMountStatusFiringTargetColumn.cellTemplate.CloneTree();

            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (SuperGameState.Instance.IsInNavalGame())
                {
                    var ctx = setButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out TorpedoMountStatusRecord torpedoMountStatusRecord))
                    {
                        // Debug.Log(torpedoMountStatusRecord);
                        GameManager.Instance.selectedTorpedoMountStatusRecord = torpedoMountStatusRecord;
                        GameManager.Instance.state = GameManager.State.SelectingTorpedoFiringTarget;
                        // SoftHide();
                        SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                    }
                }
            };

            return el;
        };

        var rapidFiringStatusListView = root.Q<ListView>("RapidFiringStatusListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusListView, () => GetSelectedShipLog());
        rapidFiringStatusListView.makeItem = () =>
        {
            var el = rapidFiringStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);

            var detailButton = el.Q<Button>("DetailButton");
            detailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringStatus r))
                {
                    PopupRapidFiringDetailDialog(r);
                }
            };

            var rapidFiringTargettingStatusMultiColumnListView = el.Q<MultiColumnListView>("RapidFiringTargettingStatusMultiColumnListView");

            Utils.BindItemsAddedRemoved<RapidFiringTargettingStatus>(
                rapidFiringTargettingStatusMultiColumnListView,
                Utils.MakeDynamicResolveProvider<RapidFiringStatus>(el)
            );

            var targetColumn = rapidFiringTargettingStatusMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringTargettingStatus r))
                    {
                        GameManager.Instance.selectedRapidFiringTargettingStatus = r;
                        GameManager.Instance.state = GameManager.State.SelectingRapidFiringTarget;
                        // SoftHide();
                        SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                    }
                };

                return el;
            };

            return el;
        };

        var resetDamageExpenditureStateButton = root.Q<Button>("ResetDamageExpenditureStateButton");
        resetDamageExpenditureStateButton.clicked += () =>
        {
            var selectedShipLog = GetSelectedShipLog();
            if (selectedShipLog == null)
                return;
            selectedShipLog.ResetDamageExpenditureState(new());
        };

        var generatePreScenarioDamageButton = root.Q<Button>("GeneratePreScenarioDamageButton");
        if (generatePreScenarioDamageButton != null)
        {
            generatePreScenarioDamageButton.clicked += () =>
            {
                var selectedShipLog = GetSelectedShipLog();
                if (selectedShipLog == null)
                    return;

                var maxDamagePoint = Math.Max(0, selectedShipLog.shipClass?.damagePoint ?? 0);
                var initialRatioPercent = maxDamagePoint > 0
                    ? Math.Clamp(100f * selectedShipLog.damagePoint / maxDamagePoint, 0, 100)
                    : 0;
                DialogRoot.Instance.PopupPreScenarioDamageDialog(
                    initialRatioPercent,
                    targetRatioPercent =>
                    {
                        var clearedLogsPreview = selectedShipLog.GeneratePreScenarioDamageByRatio(targetRatioPercent);
                        DialogRoot.Instance.PopupMessageDialog(clearedLogsPreview, "Pre-scenario Damage Roll");
                    }
                );
            };
        }

        var resetPreScenarioDamageButton = root.Q<Button>("ResetPreScenarioDamageButton");
        if (resetPreScenarioDamageButton != null)
        {
            resetPreScenarioDamageButton.clicked += () =>
            {
                var selectedShipLog = GetSelectedShipLog();
                if (selectedShipLog == null)
                    return;
                selectedShipLog.ResetDamageExpenditureState(new(), true);
            };
        }

        var setNamedShipButton = root.Q<Button>("SetNamedShipButton");
        setNamedShipButton.clicked += DialogRoot.Instance.PopupNamedShipSelctorDialogForShipLog;

        var gotoNamedShipButton = root.Q<Button>("GotoNamedShipButton");
        gotoNamedShipButton.clicked += () =>
        {
            var selectedShipLog = GetSelectedShipLog();
            var namedShip = selectedShipLog?.namedShip;
            SwitchCenter.Instance.SwitchToNamedShipView(namedShip);
        };

        var shipLogDetailButton = root.Q<Button>("ShipLogDetailButton");
        shipLogDetailButton.clicked += () =>
        {
            var ctx = shipLogDetailButton.GetHierarchicalDataSourceContext();
            // if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ShipLog shipLog))
            // {
            //     DialogRoot.Instance.PopupMessageDialog(shipLog.DescribeDetail(), Localize("ShipLog Detail"));
            // }
            if(Utils.TryResolveCurrentValueForBinding(shipLogDetailButton, out ShipLog shipLog))
            {
                DialogRoot.Instance.PopupMessageDialog(shipLog.DescribeDetail(), Localize("ShipLog Detail"));
            }
        };

        var plotTrajectoryOnMapButton = root.Q<Button>("PlotTrajectoryOnMapButton");
        plotTrajectoryOnMapButton.clicked += () =>
        {
            if (SuperGameState.Instance.currentGameMode == GameMode.Naval)
            {
                Debug.Log("plot trajectory on map");

                if (Utils.TryResolveCurrentValueForBinding(plotTrajectoryOnMapButton, out ShipLog shipLog))
                {
                    DialogRoot.Instance.PopupPlotTrajectoryDialog(shipLog);
                }
            }
        };

        var showTimeLocTableButton = root.Q<Button>("ShowTimeLocTableButton");
        if (showTimeLocTableButton != null)
        {
            showTimeLocTableButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(showTimeLocTableButton, out ShipLog shipLog))
                {
                    DialogRoot.Instance.PopupShipTimeLocDialog(shipLog);
                }
            };
        }

        waypointListView = root.Q<ListView>("WaypointListView");
        if (waypointListView != null)
        {
            waypointListView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var indexLabel = new Label { name = "WaypointIndexLabel" };
                indexLabel.style.width = 56;
                row.Add(indexLabel);

                var latitudeLabel = new Label { name = "WaypointLatitudeLabel" };
                latitudeLabel.style.flexGrow = 1;
                row.Add(latitudeLabel);

                var longitudeLabel = new Label { name = "WaypointLongitudeLabel" };
                longitudeLabel.style.flexGrow = 1;
                row.Add(longitudeLabel);

                return row;
            };
            waypointListView.bindItem = (element, index) =>
            {
                var shipLog = GetSelectedShipLog();
                var waypoint = shipLog?.manualRoute != null && index >= 0 && index < shipLog.manualRoute.Count
                    ? shipLog.manualRoute[index]
                    : null;

                element.Q<Label>("WaypointIndexLabel").text = (index + 1).ToString();
                element.Q<Label>("WaypointLatitudeLabel").text = waypoint != null ? waypoint.LatDeg.ToString("0.0000") : string.Empty;
                element.Q<Label>("WaypointLongitudeLabel").text = waypoint != null ? waypoint.LonDeg.ToString("0.0000") : string.Empty;
            };
            root.schedule.Execute(RefreshWaypointListView).Every(250);
            RefreshWaypointListView();
        }

        InitializeHistoryTab();

        // Utils.BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root);

        var loadedGroupListView = root.Q<ListView>("LoadedGroupListView");
        loadedGroupListView.makeItem = () =>
        {
            var el = loadedGroupListView.itemTemplate.CloneTree();
            // Utils.BindGotoButton(el, this);
            Utils.BindGotoButton(el);
            return el;
        };
    }

    void PopupBatteryDetailDialog(NavalCombatCore.BatteryStatus batteryStatus)
    {
        if (batteryStatus == null)
            return;

        DialogRoot.Instance.PopupCustomMessageContentDialog(
            Localize("Battery Detail"),
            () => BuildGunneryDetailContent(
                batteryStatus.DescribeFireControlDetail(),
                BuildBatteryFiringLogRows(batteryStatus)
            )
        );
    }

    void PopupMountDetailDialog(MountStatusRecord mountStatus)
    {
        if (mountStatus == null)
            return;

        var mountLabel = GetMountLabel(mountStatus);
        DialogRoot.Instance.PopupCustomMessageContentDialog(
            Localize("Mount Detail"),
            () => BuildGunneryDetailContent(
                mountStatus.DescribeFireControlDetail(),
                mountStatus.logs
                    .OrderBy(log => log.firingTime)
                    .Select(log => new MountFiringLogDetailRow
                    {
                        mountLabel = mountLabel,
                        log = log
                    })
                    .ToList()
            )
        );
    }

    VisualElement BuildGunneryDetailContent(string fireControlText, List<MountFiringLogDetailRow> allRows)
    {
        return BuildGunneryDetailContent(fireControlText, BuildBatteryFiringLogDetailTab(allRows));
    }

    VisualElement BuildGunneryDetailContent(string fireControlText, VisualElement shotResultsTabContent)
    {
        var tabView = new TabView
        {
            name = "GunneryDetailTabView",
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        tabView.Add(BuildDetailTab(Localize("Fire Control"), BuildFireControlDetailTab(fireControlText)));
        tabView.Add(BuildDetailTab(Localize("Shot Results"), shotResultsTabContent));
        return tabView;
    }

    void PopupRapidFiringDetailDialog(RapidFiringStatus rapidFiringStatus)
    {
        if (rapidFiringStatus == null)
            return;

        DialogRoot.Instance.PopupCustomMessageContentDialog(
            Localize("Rapid Firing Detail"),
            () => BuildGunneryDetailContent(
                rapidFiringStatus.DescribeFireControlDetail(),
                BuildRapidFiringLogDetailTab(
                    rapidFiringStatus.logs
                        .OrderBy(log => log.firingTime)
                        .Select(log => new RapidFiringLogDetailRow
                        {
                            log = log
                        })
                        .ToList()
                )
            )
        );
    }

    Tab BuildDetailTab(string label, VisualElement content)
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

    VisualElement BuildFireControlDetailTab(string fireControlText)
    {
        var textField = new TextField
        {
            multiline = true,
            isReadOnly = true,
            verticalScrollerVisibility = ScrollerVisibility.Auto,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
                whiteSpace = WhiteSpace.Normal,
            }
        };
        textField.SetValueWithoutNotify(fireControlText ?? "");
        return textField;
    }

    VisualElement BuildBatteryFiringLogDetailTab(List<MountFiringLogDetailRow> allRows)
    {
        allRows ??= new();
        var displayedRows = new List<MountFiringLogDetailRow>();

        var root = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        var toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexShrink = 0,
            }
        };

        var hitsOnlyToggle = new Toggle(Localize("Hits only"));
        var countLabel = new Label
        {
            style =
            {
                marginLeft = 12,
                unityTextAlign = TextAnchor.MiddleLeft,
            }
        };
        toolbar.Add(hitsOnlyToggle);
        toolbar.Add(countLabel);
        root.Add(toolbar);

        var listView = new MultiColumnListView
        {
            name = "FiringLogMultiColumnListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        void AddColumn(string name, string title, int width, Func<MountFiringLogDetailRow, string> valueSelector)
        {
            listView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 90),
                stretchable = false,
                makeCell = () => new Label
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.Normal,
                    }
                },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;

                    var row = index >= 0 && index < displayedRows.Count ? displayedRows[index] : null;
                    label.text = row != null ? valueSelector(row) : "";
                }
            });
        }

        AddColumn("time", Localize("Time"), 150, row => FormatFiringTime(row.log));
        AddColumn("mount", Localize("Mount"), 190, row => row.mountLabel);
        AddColumn("ammunition", Localize("Ammo"), 70, row => FormatAmmunition(row.log));
        AddColumn("target", Localize("Target"), 140, row => FormatTarget(row.log));
        AddColumn("distance", Localize("Distance"), 90, row => FormatYards(row.log?.distanceYards ?? 0));
        AddColumn("hitProbability", Localize("Hit Prob"), 90, row => FormatPercent(row.log?.hitProb ?? 0));
        AddColumn("result", Localize("Result"), 210, row => FormatFiringResult(row.log));
        AddColumn("damage", Localize("Damage"), 80, row => FormatDamage(row.log));
        AddColumn("effect", Localize("Effect"), 150, row => row.log?.DamageEffectId ?? "");

        root.Add(listView);

        void RefreshRows()
        {
            var hitsOnly = hitsOnlyToggle.value;
            displayedRows = allRows
                .Where(row => row?.log != null && (!hitsOnly || row.log.hit))
                .OrderBy(row => row.log.firingTime)
                .ToList();

            listView.itemsSource = displayedRows;
            listView.Rebuild();
            countLabel.text = Localize(
                hitsOnly ? "{0} hits / {1} shots" : "{1} shots, {0} hits",
                allRows.Count(row => row?.log != null && row.log.hit),
                allRows.Count(row => row?.log != null)
            );
        }

        hitsOnlyToggle.RegisterValueChangedCallback(_ => RefreshRows());
        RefreshRows();
        return root;
    }

    VisualElement BuildRapidFiringLogDetailTab(List<RapidFiringLogDetailRow> allRows)
    {
        allRows ??= new();
        var displayedRows = new List<RapidFiringLogDetailRow>();

        var root = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        var toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexShrink = 0,
            }
        };

        var hitsOnlyToggle = new Toggle(Localize("Hits only"));
        var countLabel = new Label
        {
            style =
            {
                marginLeft = 12,
                unityTextAlign = TextAnchor.MiddleLeft,
            }
        };
        toolbar.Add(hitsOnlyToggle);
        toolbar.Add(countLabel);
        root.Add(toolbar);

        var listView = new MultiColumnListView
        {
            name = "RapidFiringLogMultiColumnListView",
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        void AddColumn(string name, string title, int width, Func<RapidFiringLogDetailRow, string> valueSelector)
        {
            listView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 90),
                stretchable = false,
                makeCell = () => new Label
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.Normal,
                    }
                },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;

                    var row = index >= 0 && index < displayedRows.Count ? displayedRows[index] : null;
                    label.text = row != null ? valueSelector(row) : "";
                }
            });
        }

        AddColumn("time", Localize("Time"), 150, row => FormatFiringTime(row.log));
        AddColumn("target", Localize("Target"), 160, row => FormatTarget(row.log));
        AddColumn("distance", Localize("Distance"), 100, row => FormatYards(row.log?.distanceYards ?? 0));
        AddColumn("hitProbability", Localize("Hit Prob"), 100, row => FormatPercent(row.log?.hitProb ?? 0));
        AddColumn("result", Localize("Result"), 110, row => FormatRapidFiringResult(row.log));
        AddColumn("damage", Localize("Damage"), 90, row => FormatDamage(row.log));

        root.Add(listView);

        void RefreshRows()
        {
            var hitsOnly = hitsOnlyToggle.value;
            displayedRows = allRows
                .Where(row => row?.log != null && (!hitsOnly || row.log.hit))
                .OrderBy(row => row.log.firingTime)
                .ToList();

            listView.itemsSource = displayedRows;
            listView.Rebuild();
            countLabel.text = Localize(
                hitsOnly ? "{0} hits / {1} shots" : "{1} shots, {0} hits",
                allRows.Count(row => row?.log != null && row.log.hit),
                allRows.Count(row => row?.log != null)
            );
        }

        hitsOnlyToggle.RegisterValueChangedCallback(_ => RefreshRows());
        RefreshRows();
        return root;
    }

    List<MountFiringLogDetailRow> BuildBatteryFiringLogRows(NavalCombatCore.BatteryStatus batteryStatus)
    {
        var rows = new List<MountFiringLogDetailRow>();
        foreach (var mountStatus in batteryStatus.mountStatus)
        {
            var mountLabel = GetMountLabel(mountStatus);
            rows.AddRange(mountStatus.logs.Select(log => new MountFiringLogDetailRow
            {
                mountLabel = mountLabel,
                log = log
            }));
        }

        return rows
            .OrderBy(row => row.log.firingTime)
            .ToList();
    }

    static string GetMountLabel(MountStatusRecord mountStatus)
    {
        return mountStatus?.GetMountLocationRecordInfo()?.Summary()
            ?? mountStatus?.objectId
            ?? "";
    }

    static string FormatFiringTime(MountFiringRecord log)
    {
        return log == null ? "" : CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(log.firingTime);
    }

    static string FormatFiringTime(RapidFiringLog log)
    {
        return log == null ? "" : CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(log.firingTime);
    }

    static string FormatAmmunition(MountFiringRecord log)
    {
        if (log == null)
            return "";

        return BatteryAmmunitionRecord.ammunitionTypeAcronymMap.TryGetValue(log.ammunitionType, out var acronym)
            ? acronym
            : log.ammunitionType.ToString();
    }

    string FormatTarget(MountFiringRecord log)
    {
        if (log == null)
            return "";

        return log.GetFiringTarget()?.namedShip?.name?.GetShortName()
            ?? log.GetFiringTarget()?.namedShip?.name?.GetMergedName()
            ?? log.firingTargetObjectId
            ?? Localize("Unknown");
    }

    string FormatTarget(RapidFiringLog log)
    {
        if (log == null)
            return "";

        return log.GetFiringTarget()?.namedShip?.name?.GetShortName()
            ?? log.GetFiringTarget()?.namedShip?.name?.GetMergedName()
            ?? log.firingTargetObjectId
            ?? Localize("Unknown");
    }

    static string FormatYards(float yards) => $"{yards:0} yd";

    static string FormatPercent(float ratio) => $"{ratio * 100f:0.#}%";

    string FormatFiringResult(MountFiringRecord log)
    {
        if (log == null)
            return "";

        if (!log.hit)
            return Localize("miss");

        var location = log.DamageSchema switch
        {
            DamageSchema.Warship => LocalizeEnum(log.ArmorLocation),
            DamageSchema.LandBattery => LocalizeEnum(log.ArmorLocation),
            DamageSchema.MerchantVessal => LocalizeEnum(log.HitLocationMerchantVessel),
            _ => ""
        };

        var penetration = LocalizeEnum(log.HitPenDetType);
        return string.IsNullOrWhiteSpace(location)
            ? $"{Localize("Hit")} / {penetration}"
            : $"{Localize("Hit")} / {location} / {penetration}";
    }

    static string FormatDamage(MountFiringRecord log)
    {
        return log != null && log.hit
            ? $"{log.ShellDamageResult.damagePoint:0.##}"
            : "";
    }

    string FormatRapidFiringResult(RapidFiringLog log)
    {
        if (log == null)
            return "";

        return log.hit ? Localize("Hit") : Localize("miss");
    }

    static string FormatDamage(RapidFiringLog log)
    {
        return log != null && log.hit
            ? $"{log.damagePoint:0.##}"
            : "";
    }

    void InitializeHistoryTab()
    {
        historyTabContent = root.Q<VisualElement>("HistoryTabContent");
        if (historyTabContent == null)
            return;

        var currentDpLossChartHost = root.Q<VisualElement>("CurrentDpLossChartHost");
        var allHitsChartHost = root.Q<VisualElement>("AllHitsChartHost");
        var outgoingDpByTargetChartHost = root.Q<VisualElement>("OutgoingDpByTargetChartHost");
        var outgoingWeaponTargetChartHost = root.Q<VisualElement>("OutgoingWeaponTargetChartHost");
        var batteryArmorLocationChartHost = root.Q<VisualElement>("BatteryArmorLocationChartHost");
        var outgoingWeaponFireChartHost = root.Q<VisualElement>("OutgoingWeaponFireChartHost");
        currentDpLossLegend = root.Q<VisualElement>("CurrentDpLossLegend");
        allHitsLegend = root.Q<VisualElement>("AllHitsLegend");
        outgoingDpByTargetLegend = root.Q<VisualElement>("OutgoingDpByTargetLegend");
        outgoingWeaponTargetLegend = root.Q<VisualElement>("OutgoingWeaponTargetLegend");
        batteryArmorLocationLegend = root.Q<VisualElement>("BatteryArmorLocationLegend");
        outgoingWeaponFireLegend = root.Q<VisualElement>("OutgoingWeaponFireLegend");

        currentDpLossChart = new HistoryPieChart();
        allHitsChart = new HistoryPieChart();
        outgoingDpByTargetChart = new HistoryPieChart();
        outgoingWeaponTargetChart = new HistoryPieChart();
        batteryArmorLocationChart = new HistoryStackedBarChart();
        outgoingWeaponFireChart = new HistoryStackedBarChart();
        currentDpLossChartHost?.Add(currentDpLossChart);
        allHitsChartHost?.Add(allHitsChart);
        outgoingDpByTargetChartHost?.Add(outgoingDpByTargetChart);
        outgoingWeaponTargetChartHost?.Add(outgoingWeaponTargetChart);
        batteryArmorLocationChartHost?.Add(batteryArmorLocationChart);
        outgoingWeaponFireChartHost?.Add(outgoingWeaponFireChart);

        historyTabContent.RegisterCallback<GeometryChangedEvent>(_ => RequestHistoryRefresh());
        historyTabContent.schedule.Execute(() => RequestHistoryRefresh()).Every(500);
        RequestHistoryRefresh(true);
    }

    void RefreshWaypointListView()
    {
        if (waypointListView == null)
            return;

        var shipLog = GetSelectedShipLog();
        waypointListView.itemsSource = shipLog?.manualRoute ?? emptyWaypointList;
        waypointListView.Rebuild();
    }

    public void RequestHistoryRefresh(bool force = false)
    {
        // Debug.LogWarning("RequestHistoryRefresh");

        if (historyTabContent == null || !IsElementActuallyVisible(historyTabContent))
            return;

        var shipLog = GetSelectedShipLog();
        if (shipLog == null)
            return;

        var signature = BuildHistorySignature(shipLog);
        if (!force && signature == lastHistorySignature)
            return;

        var currentDamageSlices = BuildCurrentDamageSlices(shipLog);
        var incomingWeaponSlices = BuildIncomingWeaponDamageSlices(shipLog);
        var outgoingTargetSlices = BuildOutgoingDamageByTargetSlices(shipLog);
        var outgoingWeaponTargetSlices = BuildOutgoingWeaponTargetDamageSlices(shipLog);
        var incomingBatteryArmorBars = BuildIncomingBatteryArmorLocationBars(shipLog);
        var outgoingWeaponFireBars = BuildOutgoingWeaponFireBars(shipLog);

        currentDpLossChart?.SetSlices(currentDamageSlices);
        allHitsChart?.SetSlices(incomingWeaponSlices);
        outgoingDpByTargetChart?.SetSlices(outgoingTargetSlices);
        outgoingWeaponTargetChart?.SetSlices(outgoingWeaponTargetSlices);
        batteryArmorLocationChart?.SetBars(incomingBatteryArmorBars);
        outgoingWeaponFireChart?.SetBars(outgoingWeaponFireBars);
        RebuildLegend(currentDpLossLegend, currentDamageSlices, Localize("No current DP loss."));
        RebuildDetailedLegend(allHitsLegend, incomingWeaponSlices, Localize("No incoming DP records."));
        RebuildLegend(outgoingDpByTargetLegend, outgoingTargetSlices, Localize("No outgoing DP."));
        RebuildDetailedLegend(outgoingWeaponTargetLegend, outgoingWeaponTargetSlices, Localize("No outgoing weapon DP records."));
        RebuildCountLegend(batteryArmorLocationLegend, BuildIncomingBatteryResultLegend(shipLog), Localize("No incoming battery hits."));
        RebuildCountLegend(outgoingWeaponFireLegend, BuildOutgoingWeaponFireLegend(outgoingWeaponFireBars), Localize("No outgoing weapon fire records."));

        lastHistorySignature = signature;
    }

    bool IsElementActuallyVisible(VisualElement element)
    {
        return element != null
            && element.resolvedStyle.display != DisplayStyle.None
            && element.worldBound.width > 1f
            && element.worldBound.height > 1f;
    }

    string BuildHistorySignature(ShipLog shipLog)
    {
        var batteryLogCount = shipLog.logs.OfType<ShipLogBatteryHitLog>().Count();
        var rapidHitCount = shipLog.logs.OfType<ShipLogRapidFiringGunHitLog>().Count();
        var torpedoHitCount = shipLog.logs.OfType<ShipLogTorpedoHitLog>().Count();
        return string.Join("|", new[]
        {
            shipLog.objectId,
            shipLog.damagePoint.ToString("0.###"),
            shipLog.pendingDamagePoint.ToString("0.###"),
            batteryLogCount.ToString(),
            rapidHitCount.ToString(),
            torpedoHitCount.ToString(),
            BuildIncomingBatteryArmorSignature(shipLog),
            BuildOutgoingSignature(shipLog),
            BuildOutgoingWeaponFireSignature(shipLog)
        });
    }

    string BuildIncomingBatteryArmorSignature(ShipLog shipLog)
    {
        return string.Join(";",
            shipLog.logs
                .OfType<ShipLogBatteryHitLog>()
                .Where(log => log.damageSchema == DamageSchema.Warship || log.damageSchema == DamageSchema.LandBattery)
                .GroupBy(log => ((int)log.ArmorLocation, (int)log.hitPenDetType))
                .OrderBy(group => group.Key.Item1)
                .ThenBy(group => group.Key.Item2)
                .Select(group => $"{group.Key.Item1}:{group.Key.Item2}:{group.Count()}"));
    }

    string BuildOutgoingSignature(ShipLog shipLog)
    {
        var outgoingBatteryHits = shipLog.batteryStatus
            .SelectMany(b => b.mountStatus)
            .SelectMany(m => m.logs)
            .Count(l => l.hit);
        var outgoingBatteryDamage = shipLog.batteryStatus
            .SelectMany(b => b.mountStatus)
            .SelectMany(m => m.logs)
            .Where(l => l.hit)
            .Sum(l => l.ShellDamageResult?.damagePoint ?? 0);
        var outgoingRapidHits = shipLog.rapidFiringStatus
            .SelectMany(r => r.logs)
            .Count(l => l.hit);
        var outgoingRapidDamage = shipLog.rapidFiringStatus
            .SelectMany(r => r.logs)
            .Where(l => l.hit)
            .Sum(l => l.damagePoint);

        var outgoingTorpedos = SuperGameState.Instance.IsInNavalGame()
            ? NavalGameState.Instance.launchedTorpedos.Where(t => t.shooterId == shipLog.objectId && t.endgameType == LaunchedTorpedoEndgameType.Hit)
            : Enumerable.Empty<LaunchedTorpedo>();
        var outgoingTorpedoHits = outgoingTorpedos.Count();
        var outgoingTorpedoDamage = outgoingTorpedos.Sum(t => t.inflictDamagePoint);

        return string.Join(";", new[]
        {
            outgoingBatteryHits.ToString(),
            outgoingBatteryDamage.ToString("0.###"),
            outgoingRapidHits.ToString(),
            outgoingRapidDamage.ToString("0.###"),
            outgoingTorpedoHits.ToString(),
            outgoingTorpedoDamage.ToString("0.###"),
        });
    }

    string BuildOutgoingWeaponFireSignature(ShipLog shipLog)
    {
        var parts = new List<string>();

        for (int batteryIdx = 0; batteryIdx < shipLog.batteryStatus.Count; batteryIdx++)
        {
            var logs = shipLog.batteryStatus[batteryIdx].mountStatus.SelectMany(mount => mount.logs).ToList();
            parts.Add($"B{batteryIdx}:{logs.Count}:{logs.Count(log => log.hit)}");
        }

        for (int rapidIdx = 0; rapidIdx < shipLog.rapidFiringStatus.Count; rapidIdx++)
        {
            var logs = shipLog.rapidFiringStatus[rapidIdx].logs;
            parts.Add($"R{rapidIdx}:{logs.Count}:{logs.Count(log => log.hit)}");
        }

        if (SuperGameState.Instance.IsInNavalGame())
        {
            var torpedoGroups = NavalGameState.Instance.launchedTorpedos
                .Where(torpedo => torpedo.shooterId == shipLog.objectId)
                .GroupBy(torpedo => FallbackName(torpedo.sourceName?.GetShortName(), Localize("Torpedo")))
                .OrderBy(group => group.Key);
            foreach (var group in torpedoGroups)
            {
                parts.Add($"T{group.Key}:{group.Count()}:{group.Count(torpedo => torpedo.endgameType == LaunchedTorpedoEndgameType.Hit)}");
            }
        }

        return string.Join(";", parts);
    }

    List<HistoryPieSlice> BuildCurrentDamageSlices(ShipLog shipLog)
    {
        var totalDamagePoint = Mathf.Max(0f, shipLog.damagePoint + shipLog.pendingDamagePoint);
        var directDamageByShooter = new Dictionary<string, float>();

        foreach (var log in shipLog.logs)
        {
            switch (log)
            {
                case ShipLogBatteryHitLog batteryHit:
                    AddToFloatMap(directDamageByShooter, ResolveShipName(batteryHit.shooterId), batteryHit.damagePoint);
                    break;
                case ShipLogRapidFiringGunHitLog rapidHit:
                    AddToFloatMap(directDamageByShooter, ResolveShipName(rapidHit.shooterId), rapidHit.damagePoint);
                    break;
                case ShipLogTorpedoHitLog torpedoHit:
                    var torpedo = torpedoHit.GetTorpedo();
                    var torpedoShooterId = torpedo?.shooterId;
                    AddToFloatMap(directDamageByShooter, ResolveShipName(torpedoShooterId), torpedoHit.damagePoint);
                    break;
            }
        }

        var slices = new List<HistoryPieSlice>();
        foreach (var pair in directDamageByShooter.OrderByDescending(p => p.Value))
        {
            if (pair.Value <= 0)
                continue;
            slices.Add(new HistoryPieSlice
            {
                label = pair.Key,
                value = pair.Value,
                color = GetHistoryColor(slices.Count)
            });
        }

        var directDamagePoint = directDamageByShooter.Values.Sum();
        var otherDamagePoint = Mathf.Max(0f, totalDamagePoint - directDamagePoint);
        if (otherDamagePoint > 0.001f)
        {
            slices.Add(new HistoryPieSlice
            {
                label = Localize("Other DP Loss"),
                value = otherDamagePoint,
                color = new Color32(120, 120, 120, 255)
            });
        }

        return slices.OrderByDescending(s => s.value).ToList();
    }

    List<HistoryPieSlice> BuildIncomingWeaponDamageSlices(ShipLog shipLog)
    {
        var batteryCandidates = BuildBatteryHitCandidates(shipLog);
        var rapidCandidates = BuildRapidHitCandidates(shipLog);
        var damageByLabel = new Dictionary<string, HistoryPieSlice>();

        foreach (var log in shipLog.logs.OfType<ShipLogBatteryHitLog>().OrderBy(l => l.time))
        {
            var label = MatchBatteryHitLabel(shipLog, log, batteryCandidates)
                ?? $"{ResolveShipName(log.shooterId)} - {Localize("Battery")}";
            AddToDetailedMap(damageByLabel, label, log.damagePoint);
        }

        foreach (var log in shipLog.logs.OfType<ShipLogRapidFiringGunHitLog>().OrderBy(l => l.time))
        {
            var label = MatchRapidHitLabel(shipLog, log, rapidCandidates)
                ?? $"{ResolveShipName(log.shooterId)} - {Localize("Rapid Battery")}";
            AddToDetailedMap(damageByLabel, label, log.damagePoint);
        }

        foreach (var log in shipLog.logs.OfType<ShipLogTorpedoHitLog>().OrderBy(l => l.time))
        {
            var torpedo = log.GetTorpedo();
            var shooterName = ResolveShipName(torpedo?.shooterId);
            var sourceName = torpedo?.sourceName?.GetShortName();
            var label = string.IsNullOrWhiteSpace(sourceName)
                ? $"{shooterName} - {Localize("Torpedo")}"
                : $"{shooterName} - {sourceName}";
            AddToDetailedMap(damageByLabel, label, log.damagePoint);
        }

        return BuildSlicesFromDetailedMap(damageByLabel);
    }

    List<HistoryPieSlice> BuildOutgoingDamageByTargetSlices(ShipLog shipLog)
    {
        var damageByTarget = new Dictionary<string, float>();

        foreach (var mountLog in shipLog.batteryStatus.SelectMany(b => b.mountStatus).SelectMany(m => m.logs).Where(l => l.hit))
        {
            AddToFloatMap(damageByTarget, ResolveShipName(mountLog.firingTargetObjectId), mountLog.ShellDamageResult?.damagePoint ?? 0);
        }

        foreach (var rapidLog in shipLog.rapidFiringStatus.SelectMany(r => r.logs).Where(l => l.hit))
        {
            AddToFloatMap(damageByTarget, ResolveShipName(rapidLog.firingTargetObjectId), rapidLog.damagePoint);
        }

        if (SuperGameState.Instance.IsInNavalGame())
        {
            foreach (var torpedo in NavalGameState.Instance.launchedTorpedos.Where(t => t.shooterId == shipLog.objectId && t.endgameType == LaunchedTorpedoEndgameType.Hit))
            {
                AddToFloatMap(damageByTarget, ResolveShipName(torpedo.hitTargetObjectId), torpedo.inflictDamagePoint);
            }
        }

        return damageByTarget
            .OrderByDescending(p => p.Value)
            .Select((pair, idx) => new HistoryPieSlice
            {
                label = pair.Key,
                value = pair.Value,
                color = GetHistoryColor(idx)
            })
            .ToList();
    }

    List<HistoryPieSlice> BuildOutgoingWeaponTargetDamageSlices(ShipLog shipLog)
    {
        var damageByWeaponTarget = new Dictionary<string, HistoryPieSlice>();

        for (int batteryIdx = 0; batteryIdx < shipLog.batteryStatus.Count; batteryIdx++)
        {
            var batteryName = shipLog.shipClass?.batteryRecords.ElementAtOrDefault(batteryIdx)?.name?.GetShortName();
            var weaponName = FallbackName(batteryName, Localize("Battery {0}", batteryIdx + 1));
            foreach (var mountLog in shipLog.batteryStatus[batteryIdx].mountStatus.SelectMany(m => m.logs).Where(l => l.hit))
            {
                var label = $"{weaponName} -> {ResolveShipName(mountLog.firingTargetObjectId)}";
                AddToDetailedMap(damageByWeaponTarget, label, mountLog.ShellDamageResult?.damagePoint ?? 0);
            }
        }

        for (int rapidIdx = 0; rapidIdx < shipLog.rapidFiringStatus.Count; rapidIdx++)
        {
            var rapidName = shipLog.shipClass?.rapidFireBatteryRecords.ElementAtOrDefault(rapidIdx)?.name?.GetShortName();
            var weaponName = FallbackName(rapidName, Localize("Rapid Battery {0}", rapidIdx + 1));
            foreach (var rapidLog in shipLog.rapidFiringStatus[rapidIdx].logs.Where(l => l.hit))
            {
                var label = $"{weaponName} -> {ResolveShipName(rapidLog.firingTargetObjectId)}";
                AddToDetailedMap(damageByWeaponTarget, label, rapidLog.damagePoint);
            }
        }

        if (SuperGameState.Instance.IsInNavalGame())
        {
            foreach (var torpedo in NavalGameState.Instance.launchedTorpedos.Where(t => t.shooterId == shipLog.objectId && t.endgameType == LaunchedTorpedoEndgameType.Hit))
            {
                var sourceName = torpedo.sourceName?.GetShortName();
                var weaponName = FallbackName(sourceName, Localize("Torpedo"));
                var label = $"{weaponName} -> {ResolveShipName(torpedo.hitTargetObjectId)}";
                AddToDetailedMap(damageByWeaponTarget, label, torpedo.inflictDamagePoint);
            }
        }

        return BuildSlicesFromDetailedMap(damageByWeaponTarget);
    }

    List<HistoryBarItem> BuildIncomingBatteryArmorLocationBars(ShipLog shipLog)
    {
        var logs = shipLog.logs
            .OfType<ShipLogBatteryHitLog>()
            .Where(log => log.damageSchema == DamageSchema.Warship || log.damageSchema == DamageSchema.LandBattery)
            .ToList();

        var bars = new List<HistoryBarItem>();
        foreach (ArmorLocation armorLocation in Enum.GetValues(typeof(ArmorLocation)))
        {
            var locationLogs = logs.Where(log => log.ArmorLocation == armorLocation).ToList();
            if (locationLogs.Count == 0)
                continue;

            bars.Add(new HistoryBarItem
            {
                label = LocalizeEnum(armorLocation),
                segments = BuildPenetrationSegments(locationLogs)
            });
        }

        return bars;
    }

    List<HistoryLegendItem> BuildIncomingBatteryResultLegend(ShipLog shipLog)
    {
        var logs = shipLog.logs
            .OfType<ShipLogBatteryHitLog>()
            .Where(log => log.damageSchema == DamageSchema.Warship || log.damageSchema == DamageSchema.LandBattery)
            .ToList();

        return new List<HistoryLegendItem>
        {
            new()
            {
                label = LocalizeEnum(HitPenDetType.PenetrateWithDetonate),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.PenetrateWithDetonate),
                color = penetrateDetonateColor
            },
            new()
            {
                label = LocalizeEnum(HitPenDetType.PassThrough),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.PassThrough),
                color = passThroughColor
            },
            new()
            {
                label = LocalizeEnum(HitPenDetType.NoPenetration),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.NoPenetration),
                color = noPenetrationColor
            }
        };
    }

    List<HistoryBarItem> BuildOutgoingWeaponFireBars(ShipLog shipLog)
    {
        var bars = new List<HistoryBarItem>();

        for (int batteryIdx = 0; batteryIdx < shipLog.batteryStatus.Count; batteryIdx++)
        {
            var logs = shipLog.batteryStatus[batteryIdx].mountStatus.SelectMany(mount => mount.logs).ToList();
            if (logs.Count == 0)
                continue;

            var batteryName = shipLog.shipClass?.batteryRecords.ElementAtOrDefault(batteryIdx)?.name?.GetShortName();
            bars.Add(new HistoryBarItem
            {
                label = FallbackName(batteryName, Localize("Battery {0}", batteryIdx + 1)),
                segments = BuildHitMissSegments(logs.Count(log => log.hit), logs.Count(log => !log.hit))
            });
        }

        for (int rapidIdx = 0; rapidIdx < shipLog.rapidFiringStatus.Count; rapidIdx++)
        {
            var logs = shipLog.rapidFiringStatus[rapidIdx].logs;
            if (logs.Count == 0)
                continue;

            var rapidName = shipLog.shipClass?.rapidFireBatteryRecords.ElementAtOrDefault(rapidIdx)?.name?.GetShortName();
            bars.Add(new HistoryBarItem
            {
                label = FallbackName(rapidName, Localize("Rapid Battery {0}", rapidIdx + 1)),
                segments = BuildHitMissSegments(logs.Count(log => log.hit), logs.Count(log => !log.hit))
            });
        }

        if (SuperGameState.Instance.IsInNavalGame())
        {
            var torpedoBars = new List<HistoryBarItem>();
            var torpedoIndexMap = new Dictionary<string, int>();
            foreach (var torpedo in NavalGameState.Instance.launchedTorpedos.Where(torpedo => torpedo.shooterId == shipLog.objectId))
            {
                var label = FallbackName(torpedo.sourceName?.GetShortName(), Localize("Torpedo"));
                if (!torpedoIndexMap.TryGetValue(label, out var barIdx))
                {
                    barIdx = torpedoBars.Count;
                    torpedoIndexMap[label] = barIdx;
                    torpedoBars.Add(new HistoryBarItem
                    {
                        label = label,
                        segments = BuildHitMissSegments(0, 0)
                    });
                }

                var hitSegment = torpedoBars[barIdx].segments[0];
                var missSegment = torpedoBars[barIdx].segments[1];
                if (torpedo.endgameType == LaunchedTorpedoEndgameType.Hit)
                {
                    hitSegment.count += 1;
                }
                else
                {
                    missSegment.count += 1;
                }
            }

            bars.AddRange(torpedoBars.Where(bar => bar.TotalCount > 0));
        }

        return bars;
    }

    List<HistoryLegendItem> BuildOutgoingWeaponFireLegend(List<HistoryBarItem> bars)
    {
        var totalHit = bars.Sum(bar => bar.segments.ElementAtOrDefault(0)?.count ?? 0);
        var totalMiss = bars.Sum(bar => bar.segments.ElementAtOrDefault(1)?.count ?? 0);
        return new List<HistoryLegendItem>
        {
            new()
            {
                label = Localize("Hit"),
                count = totalHit,
                color = hitColor
            },
            new()
            {
                label = Localize("miss"),
                count = totalMiss,
                color = missColor
            }
        };
    }

    List<HistoryBarSegment> BuildPenetrationSegments(List<ShipLogBatteryHitLog> logs)
    {
        return new List<HistoryBarSegment>
        {
            new()
            {
                label = LocalizeEnum(HitPenDetType.PenetrateWithDetonate),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.PenetrateWithDetonate),
                color = penetrateDetonateColor
            },
            new()
            {
                label = LocalizeEnum(HitPenDetType.PassThrough),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.PassThrough),
                color = passThroughColor
            },
            new()
            {
                label = LocalizeEnum(HitPenDetType.NoPenetration),
                count = logs.Count(log => log.hitPenDetType == HitPenDetType.NoPenetration),
                color = noPenetrationColor
            }
        };
    }

    List<HistoryBarSegment> BuildHitMissSegments(int hitCount, int missCount)
    {
        return new List<HistoryBarSegment>
        {
            new()
            {
                label = Localize("Hit"),
                count = hitCount,
                color = hitColor
            },
            new()
            {
                label = Localize("miss"),
                count = missCount,
                color = missColor
            }
        };
    }

    List<HistoryHitCandidate> BuildBatteryHitCandidates(ShipLog targetShipLog)
    {
        var candidates = new List<HistoryHitCandidate>();
        foreach (var shooterId in targetShipLog.logs.OfType<ShipLogBatteryHitLog>().Select(l => l.shooterId).Distinct())
        {
            var shooter = EntityManager.Instance.Get<ShipLog>(shooterId);
            if (shooter?.shipClass == null)
                continue;

            for (int batteryIdx = 0; batteryIdx < shooter.batteryStatus.Count; batteryIdx++)
            {
                var batteryStatus = shooter.batteryStatus[batteryIdx];
                var batteryRecord = shooter.shipClass.batteryRecords.ElementAtOrDefault(batteryIdx);
                var batteryName = batteryRecord?.name?.GetShortName();
                var label = $"{ResolveShipName(shooterId)} - {FallbackName(batteryName, Localize("Battery {0}", batteryIdx + 1))}";

                foreach (var mountStatus in batteryStatus.mountStatus)
                {
                    foreach (var mountLog in mountStatus.logs.Where(l => l.hit && l.firingTargetObjectId == targetShipLog.objectId))
                    {
                        candidates.Add(new HistoryHitCandidate
                        {
                            shooterId = shooterId,
                            label = label,
                            time = mountLog.firingTime,
                            damagePoint = mountLog.ShellDamageResult?.damagePoint ?? 0,
                            hitPenDetType = mountLog.HitPenDetType,
                            damageSchema = mountLog.DamageSchema,
                        });
                    }
                }
            }
        }
        return candidates;
    }

    List<HistoryHitCandidate> BuildRapidHitCandidates(ShipLog targetShipLog)
    {
        var candidates = new List<HistoryHitCandidate>();
        foreach (var shooterId in targetShipLog.logs.OfType<ShipLogRapidFiringGunHitLog>().Select(l => l.shooterId).Distinct())
        {
            var shooter = EntityManager.Instance.Get<ShipLog>(shooterId);
            if (shooter?.shipClass == null)
                continue;

            for (int rapidIdx = 0; rapidIdx < shooter.rapidFiringStatus.Count; rapidIdx++)
            {
                var rapidStatus = shooter.rapidFiringStatus[rapidIdx];
                var rapidRecord = shooter.shipClass.rapidFireBatteryRecords.ElementAtOrDefault(rapidIdx);
                var rapidName = rapidRecord?.name?.GetShortName();
                var label = $"{ResolveShipName(shooterId)} - {FallbackName(rapidName, Localize("Rapid Battery {0}", rapidIdx + 1))}";

                foreach (var rapidLog in rapidStatus.logs.Where(l => l.hit && l.firingTargetObjectId == targetShipLog.objectId))
                {
                    candidates.Add(new HistoryHitCandidate
                    {
                        shooterId = shooterId,
                        label = label,
                        time = rapidLog.firingTime,
                        damagePoint = rapidLog.damagePoint,
                    });
                }
            }
        }
        return candidates;
    }

    string MatchBatteryHitLabel(ShipLog targetShipLog, ShipLogBatteryHitLog log, List<HistoryHitCandidate> candidates)
    {
        var matched = candidates.FirstOrDefault(candidate =>
            !candidate.consumed
            && candidate.shooterId == log.shooterId
            && candidate.damageSchema == log.damageSchema
            && candidate.hitPenDetType == log.hitPenDetType
            && IsSameHistoryTime(candidate.time, log.time)
            && IsSameHistoryValue(candidate.damagePoint, log.damagePoint)
        );

        if (matched == null)
            return null;

        matched.consumed = true;
        return matched.label;
    }

    string MatchRapidHitLabel(ShipLog targetShipLog, ShipLogRapidFiringGunHitLog log, List<HistoryHitCandidate> candidates)
    {
        var matched = candidates.FirstOrDefault(candidate =>
            !candidate.consumed
            && candidate.shooterId == log.shooterId
            && IsSameHistoryTime(candidate.time, log.time)
            && IsSameHistoryValue(candidate.damagePoint, log.damagePoint)
        );

        if (matched == null)
            return null;

        matched.consumed = true;
        return matched.label;
    }

    bool IsSameHistoryTime(DateTime left, DateTime right)
    {
        return Math.Abs((left - right).TotalSeconds) < 0.01d;
    }

    bool IsSameHistoryValue(float left, float right)
    {
        return Mathf.Abs(left - right) < 0.01f;
    }

    void RebuildLegend(VisualElement host, List<HistoryPieSlice> slices, string emptyText)
    {
        if (host == null)
            return;

        host.Clear();
        var total = slices.Sum(slice => Mathf.Max(0f, slice.value));
        if (total <= 0.0001f)
        {
            host.Add(new Label(emptyText)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return;
        }

        foreach (var slice in slices.OrderByDescending(s => s.value))
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            var colorBox = new VisualElement();
            colorBox.style.width = 12;
            colorBox.style.height = 12;
            colorBox.style.marginRight = 6;
            colorBox.style.backgroundColor = new StyleColor(slice.color);
            colorBox.style.borderTopWidth = 1;
            colorBox.style.borderRightWidth = 1;
            colorBox.style.borderBottomWidth = 1;
            colorBox.style.borderLeftWidth = 1;
            colorBox.style.borderTopColor = Color.black;
            colorBox.style.borderRightColor = Color.black;
            colorBox.style.borderBottomColor = Color.black;
            colorBox.style.borderLeftColor = Color.black;

            var label = new Label($"{slice.label}: {FormatLegendValue(slice.value)} ({slice.value / total:P1})");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;

            row.Add(colorBox);
            row.Add(label);
            host.Add(row);
        }
    }

    void RebuildDetailedLegend(VisualElement host, List<HistoryPieSlice> slices, string emptyText)
    {
        if (host == null)
            return;

        host.Clear();
        var total = slices.Sum(slice => Mathf.Max(0f, slice.value));
        if (total <= 0.0001f)
        {
            host.Add(new Label(emptyText)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return;
        }

        foreach (var slice in slices.OrderByDescending(s => s.value))
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 3;

            var colorBox = new VisualElement();
            colorBox.style.width = 12;
            colorBox.style.height = 12;
            colorBox.style.marginRight = 6;
            colorBox.style.marginTop = 2;
            colorBox.style.backgroundColor = new StyleColor(slice.color);
            colorBox.style.borderTopWidth = 1;
            colorBox.style.borderRightWidth = 1;
            colorBox.style.borderBottomWidth = 1;
            colorBox.style.borderLeftWidth = 1;
            colorBox.style.borderTopColor = Color.black;
            colorBox.style.borderRightColor = Color.black;
            colorBox.style.borderBottomColor = Color.black;
            colorBox.style.borderLeftColor = Color.black;

            var avg = slice.hitCount <= 0 ? 0 : slice.value / slice.hitCount;
            var stdDev = CalculateStdDev(slice.hitValues, avg);
            var labelText = Localize(
                "{0}: {1} DP ({2}), {3} hits, avg {4} DP",
                slice.label, FormatLegendValue(slice.value), (slice.value / total).ToString("P1"), slice.hitCount, FormatLegendValue(avg)
            );
            if (slice.hitCount >= 2)
            {
                labelText = Localize(
                    "{0}: {1} DP ({2}), {3} hits, avg {4} DP, std dev {5} DP",
                    slice.label, FormatLegendValue(slice.value), (slice.value / total).ToString("P1"), slice.hitCount, FormatLegendValue(avg), FormatLegendValue(stdDev)
                );
            }
            var label = new Label(labelText);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;

            row.Add(colorBox);
            row.Add(label);
            host.Add(row);
        }
    }

    void RebuildCountLegend(VisualElement host, List<HistoryLegendItem> items, string emptyText)
    {
        if (host == null)
            return;

        host.Clear();
        var total = items.Sum(item => Mathf.Max(0, item.count));
        if (total <= 0)
        {
            host.Add(new Label(emptyText)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return;
        }

        foreach (var item in items)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 3;

            var colorBox = new VisualElement();
            colorBox.style.width = 12;
            colorBox.style.height = 12;
            colorBox.style.marginRight = 6;
            colorBox.style.marginTop = 2;
            colorBox.style.backgroundColor = new StyleColor(item.color);
            colorBox.style.borderTopWidth = 1;
            colorBox.style.borderRightWidth = 1;
            colorBox.style.borderBottomWidth = 1;
            colorBox.style.borderLeftWidth = 1;
            colorBox.style.borderTopColor = Color.black;
            colorBox.style.borderRightColor = Color.black;
            colorBox.style.borderBottomColor = Color.black;
            colorBox.style.borderLeftColor = Color.black;

            var ratio = total <= 0 ? 0f : (float)item.count / total;
            var label = new Label($"{item.label}: {item.count} ({ratio:P1})");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;

            row.Add(colorBox);
            row.Add(label);
            host.Add(row);
        }
    }

    void AddToFloatMap(Dictionary<string, float> map, string label, float value)
    {
        if (value <= 0)
            return;

        label = FallbackName(label, Localize("Unknown"));
        map[label] = map.GetValueOrDefault(label) + value;
    }

    void AddToDetailedMap(Dictionary<string, HistoryPieSlice> map, string label, float value)
    {
        if (value <= 0)
            return;

        label = FallbackName(label, Localize("Unknown"));
        if (!map.TryGetValue(label, out var slice))
        {
            slice = new HistoryPieSlice
            {
                label = label,
                value = 0,
                hitCount = 0
            };
            map[label] = slice;
        }

        slice.value += value;
        slice.hitCount += 1;
        slice.hitValues.Add(value);
    }

    List<HistoryPieSlice> BuildSlicesFromDetailedMap(Dictionary<string, HistoryPieSlice> map)
    {
        return map.Values
            .OrderByDescending(slice => slice.value)
            .Select((slice, idx) =>
            {
                slice.color = GetHistoryColor(idx);
                return slice;
            })
            .ToList();
    }

    string ResolveShipName(string shipObjectId)
    {
        var shipLog = EntityManager.Instance.Get<ShipLog>(shipObjectId);
        return shipLog?.namedShip?.name?.GetShortName() ?? shipObjectId ?? Localize("Unknown");
    }

    string FallbackName(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    string FormatLegendValue(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) < 0.01f
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }

    Color GetHistoryColor(int idx)
    {
        return historyChartPalette[idx % historyChartPalette.Length];
    }

    float CalculateStdDev(List<float> values, float mean)
    {
        if (values == null || values.Count < 2)
            return 0;

        var variance = values.Sum(v =>
        {
            var delta = v - mean;
            return delta * delta;
        }) / values.Count;
        return Mathf.Sqrt(variance);
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    static string LocalizeEnum<T>(T value) => ServiceLocator.Get<ILocalizeService>().GetEnum(value);
}

class HistoryHitCandidate
{
    public string shooterId;
    public string label;
    public DateTime time;
    public float damagePoint;
    public DamageSchema damageSchema;
    public HitPenDetType hitPenDetType;
    public bool consumed;
}

public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    // public VisualTreeAsset shipClassSelectorDialogDocument;
    public ListView shipLogListView;
    ShipLogView shipLogViewBinder;

    // protected override void Awake()
    // {
    //     base.Awake();
    //     Bind();
    // }

    public string selectedShipLogObjectId;

    [CreateProperty]
    public ShipLog selectedShipLog
    {
        get
        {
            return EntityManager.Instance.Get<ShipLog>(selectedShipLogObjectId);
        }
    }

    void OnEnable()
    {
        // Debug.LogWarning("ShipLogEditor OnEnable");
        Bind();

        var shipLogView = new ShipLogView()
        {
            root = root.Q<VisualElement>("ShipLogView")
        };
        shipLogView.Bind();
        shipLogViewBinder = shipLogView;
    }

    public EventHandler shown;

    protected override void OnShow()
    {
        shown?.Invoke(this, EventArgs.Empty);
    }

    // protected override void Awake()
    void Bind()
    {
        // base.Awake();

        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"ShipLogEditor sortingOrder={sortingOrder}");

        root.dataSource = this;

        // foreach (var listView in root.Query<BaseListView>().ToList())
        // {
        //     listView.SetBinding("itemsSource", new DataBinding());
        // }
        Utils.BindItemsSourceRecursive(root);

        shipLogListView = root.Q<ListView>("ShipLogListView");
        // shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);
        Utils.BindItemsAddedRemoved<ShipLog>(shipLogListView, () => null);

        shipLogListView.selectionChanged += (IEnumerable<object> objs) =>
        {
            var shipLog = objs.FirstOrDefault() as ShipLog;
            if (shipLog != null)
            {
                selectedShipLogObjectId = shipLog.objectId;
                shipLogViewBinder?.RequestHistoryRefresh(true);
            }
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var resetAllStatesButton = root.Q<Button>("ResetAllStatesButton");
        resetAllStatesButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            foreach (var shipLog in gameState.shipLogs)
            {
                shipLog.ResetDamageExpenditureState(new());
                shipLog.logs.Clear();
            }
        };
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    // public void PopupWithSelection(ShipLog shipLog)
    // {
    //     var gameState = SuperGameState.Instance.GetCurrentGameState();
    //     var idx = gameState.shipLogs.IndexOf(shipLog);
    //     if (shipLog != null && idx != -1)
    //     {
    //         Show();
    //         // shipLogListView.SetSelection(idx);
    //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(shipLogListView, idx);
    //     }
    // }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

}
