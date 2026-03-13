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

public class ShipLogView
{
    public VisualElement root;

    VisualElement shipLogView;
    VisualElement historyTabContent;
    VisualElement currentDpLossLegend;
    VisualElement allHitsLegend;
    VisualElement outgoingDpByTargetLegend;
    VisualElement outgoingWeaponTargetLegend;
    HistoryPieChart currentDpLossChart;
    HistoryPieChart allHitsChart;
    HistoryPieChart outgoingDpByTargetChart;
    HistoryPieChart outgoingWeaponTargetChart;
    string lastHistorySignature;

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
                        DialogRoot.Instance.PopupMessageDialog(mountStatus.DescribeDetail(), "Mount Detail");
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
                    DialogRoot.Instance.PopupMessageDialog(batteryStatus.DescribeDetail(), "Battery Detail");
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
                    DialogRoot.Instance.PopupMessageDialog(r.DescribeDetail());
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

    void InitializeHistoryTab()
    {
        historyTabContent = root.Q<VisualElement>("HistoryTabContent");
        if (historyTabContent == null)
            return;

        var currentDpLossChartHost = root.Q<VisualElement>("CurrentDpLossChartHost");
        var allHitsChartHost = root.Q<VisualElement>("AllHitsChartHost");
        var outgoingDpByTargetChartHost = root.Q<VisualElement>("OutgoingDpByTargetChartHost");
        var outgoingWeaponTargetChartHost = root.Q<VisualElement>("OutgoingWeaponTargetChartHost");
        currentDpLossLegend = root.Q<VisualElement>("CurrentDpLossLegend");
        allHitsLegend = root.Q<VisualElement>("AllHitsLegend");
        outgoingDpByTargetLegend = root.Q<VisualElement>("OutgoingDpByTargetLegend");
        outgoingWeaponTargetLegend = root.Q<VisualElement>("OutgoingWeaponTargetLegend");

        currentDpLossChart = new HistoryPieChart();
        allHitsChart = new HistoryPieChart();
        outgoingDpByTargetChart = new HistoryPieChart();
        outgoingWeaponTargetChart = new HistoryPieChart();
        currentDpLossChartHost?.Add(currentDpLossChart);
        allHitsChartHost?.Add(allHitsChart);
        outgoingDpByTargetChartHost?.Add(outgoingDpByTargetChart);
        outgoingWeaponTargetChartHost?.Add(outgoingWeaponTargetChart);

        historyTabContent.RegisterCallback<GeometryChangedEvent>(_ => RequestHistoryRefresh());
        historyTabContent.schedule.Execute(() => RequestHistoryRefresh()).Every(500);
        RequestHistoryRefresh(true);
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

        currentDpLossChart?.SetSlices(currentDamageSlices);
        allHitsChart?.SetSlices(incomingWeaponSlices);
        outgoingDpByTargetChart?.SetSlices(outgoingTargetSlices);
        outgoingWeaponTargetChart?.SetSlices(outgoingWeaponTargetSlices);
        RebuildLegend(currentDpLossLegend, currentDamageSlices, Localize("No current DP loss."));
        RebuildDetailedLegend(allHitsLegend, incomingWeaponSlices, Localize("No incoming DP records."));
        RebuildLegend(outgoingDpByTargetLegend, outgoingTargetSlices, Localize("No outgoing DP."));
        RebuildDetailedLegend(outgoingWeaponTargetLegend, outgoingWeaponTargetSlices, Localize("No outgoing weapon DP records."));

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
            BuildOutgoingSignature(shipLog)
        });
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

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipLogsToXML();
            // IOManager.Instance.SaveTextFile(content, "ShipLogs" + GameManager.scenarioSuffix, "xml");
            IOManager.Instance.SaveTextFile(content, "ShipLogs.xml", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipLogsXmlLoaded;
            IOManager.Instance.LoadTextFile(OnShipLogsXmlLoaded, "xml");
        };

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

    void OnShipLogsXmlLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipLogsFromXML(text);
        gameState.ResetAndRegisterAll();
    }

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
