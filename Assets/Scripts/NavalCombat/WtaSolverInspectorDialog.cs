using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NavalCombatCore;
using UnityEngine.UIElements;
using YYZ;

sealed class WtaSolverInspectorDialog
{
    enum SortMode
    {
        None,
        Shooter,
        Target,
        Gain,
    }

    sealed class OpposeSidePairOption
    {
        public string label;
        public ShipGroup rootGroup;
        public List<ShipLog> meShipLogs;
        public List<ShipLog> otherShipLogs;
    }

    static readonly SortMode[] SortModes =
    {
        SortMode.None,
        SortMode.Shooter,
        SortMode.Target,
        SortMode.Gain,
    };

    readonly List<OpposeSidePairOption> pairOptions = new();
    List<WeaponTargetAssignmentGainRow> displayedRows = new();

    DropdownField rootGroupDropdownField;
    DropdownField primarySortDropdownField;
    DropdownField secondarySortDropdownField;
    MultiColumnListView gainListView;
    Button startButton;
    Button nextButton;
    Button exportCsvButton;

    WeaponTargetAssignmentInspectionSession session;

    static string LocalizeDynamic(string key, params object[] args)
    {
        var fallback = args != null && args.Length > 0 ? string.Format(key, args) : key;
        try
        {
            var localized = ServiceLocator.Get<ILocalizeService>()?.Get(key, args);
            if (!string.IsNullOrEmpty(localized))
                return localized;
        }
        catch
        {
        }
        return fallback;
    }

    static string GetShipGroupLabel(ShipGroup group)
    {
        return group?.name?.GetMergedName()
            ?? group?.name?.GetShortName()
            ?? group?.objectId
            ?? "[Invalid]";
    }

    static string GetSortLabel(SortMode sortMode)
    {
        return sortMode switch
        {
            SortMode.None => LocalizeDynamic("None"),
            SortMode.Shooter => LocalizeDynamic("Shooter"),
            SortMode.Target => LocalizeDynamic("Target"),
            SortMode.Gain => LocalizeDynamic("Gain"),
            _ => sortMode.ToString(),
        };
    }

    public void OnCreated(object sender, VisualElement el)
    {
        rootGroupDropdownField = el.Q<DropdownField>("RootGroupDropdownField");
        primarySortDropdownField = el.Q<DropdownField>("PrimarySortDropdownField");
        secondarySortDropdownField = el.Q<DropdownField>("SecondarySortDropdownField");
        gainListView = el.Q<MultiColumnListView>("GainListView");
        startButton = el.Q<Button>("StartButton");
        nextButton = el.Q<Button>("NextButton");
        exportCsvButton = el.Q<Button>("ExportCsvButton");

        ConfigureSortDropdown(primarySortDropdownField, SortMode.Gain);
        ConfigureSortDropdown(secondarySortDropdownField, SortMode.None);
        ConfigureGainListView();
        RefreshPairOptions();

        rootGroupDropdownField?.RegisterValueChangedCallback(_ =>
        {
            InvalidateSession();
            RefreshDisplayedRows();
        });
        primarySortDropdownField?.RegisterValueChangedCallback(_ => RefreshDisplayedRows());
        secondarySortDropdownField?.RegisterValueChangedCallback(_ => RefreshDisplayedRows());

        if (startButton != null)
        {
            startButton.clicked += StartInspection;
        }

        if (nextButton != null)
        {
            nextButton.clicked += () =>
            {
                if (session != null)
                {
                    session.StepNext();
                    RefreshDisplayedRows();
                }
            };
        }

        if (exportCsvButton != null)
        {
            exportCsvButton.clicked += ExportCsv;
        }

        RefreshDisplayedRows();
    }

    void ConfigureSortDropdown(DropdownField dropdownField, SortMode defaultSortMode)
    {
        if (dropdownField == null)
            return;

        dropdownField.choices = SortModes.Select(GetSortLabel).ToList();
        var defaultIndex = Array.IndexOf(SortModes, defaultSortMode);
        dropdownField.index = defaultIndex >= 0 ? defaultIndex : 0;
    }

    void ConfigureGainListView()
    {
        if (gainListView == null)
            return;

        gainListView.selectionType = SelectionType.None;
        AddTextColumn("scanOrder", LocalizeDynamic("Scan"), 70, row => row.scanOrder.ToString());
        AddTextColumn("shooterName", LocalizeDynamic("Shooter"), 140, row => row.shooterName);
        AddTextColumn("batteryName", LocalizeDynamic("Battery"), 170, row => row.batteryName);
        AddTextColumn("targetName", LocalizeDynamic("Target"), 140, row => row.targetName);
        AddTextColumn("finalGain", LocalizeDynamic("Gain"), 90, row => FormatFloat(row.finalGain));
        AddTextColumn("rawGainBeforeStickiness", LocalizeDynamic("Raw Gain"), 110, row => FormatFloat(row.rawGainBeforeStickiness));
        AddTextColumn("distanceYards", LocalizeDynamic("Distance"), 90, row => FormatFloat(row.distanceYards));
        AddTextColumn("targetSelfFirepower", LocalizeDynamic("Target FP"), 95, row => FormatFloat(row.targetSelfFirepower));
        AddTextColumn("targetSurvivability", LocalizeDynamic("Target SV"), 95, row => FormatFloat(row.targetSurvivability));
        AddTextColumn("targetUrgencyFactor", LocalizeDynamic("Urgency"), 85, row => FormatFloat(row.targetUrgencyFactor));
        AddTextColumn("currentUnderFirepower", LocalizeDynamic("Under FP"), 95, row => FormatFloat(row.currentUnderFirepower));
        AddTextColumn("currentOverConcentrationScore", LocalizeDynamic("Over Conc"), 95, row => row.currentOverConcentrationScore.ToString());
        AddTextColumn("tryAddedFirepowerScoreBase", LocalizeDynamic("FP Base"), 90, row => FormatFloat(row.tryAddedFirepowerScoreBase));
        AddTextColumn("tryAddedFirepowerScoreEffective", LocalizeDynamic("FP Eff"), 90, row => FormatFloat(row.tryAddedFirepowerScoreEffective));
        AddTextColumn("tryAddedOverconcentrationScore", LocalizeDynamic("Try OC"), 85, row => row.tryAddedOverconcentrationScore.ToString());
        AddTextColumn("isCurrentTarget", LocalizeDynamic("Current"), 80, row => row.isCurrentTarget ? "Y" : "");
        AddTextColumn("currentTargetFireEffectivenessFactor", LocalizeDynamic("Current Eff"), 100, row => FormatFloat(row.currentTargetFireEffectivenessFactor));
        AddTextColumn("changeTargetMultiplier", LocalizeDynamic("Stickiness"), 95, row => FormatFloat(row.changeTargetMultiplier));
    }

    void AddTextColumn(string name, string title, int width, Func<WeaponTargetAssignmentGainRow, string> valueSelector)
    {
        var column = new Column
        {
            name = name,
            title = title,
            width = width,
            minWidth = width,
            stretchable = false,
            makeCell = () => new Label(),
            bindCell = (element, index) =>
            {
                var label = element as Label;
                var row = GetDisplayedRow(index);
                if (label != null)
                    label.text = row != null ? valueSelector(row) : string.Empty;
            }
        };
        gainListView.columns.Add(column);
    }

    void RefreshPairOptions()
    {
        var previousRootObjectId = GetSelectedPair()?.rootGroup?.objectId;
        pairOptions.Clear();

        foreach ((var meShipLogs, var otherShipLogs) in NavalGameState.Instance.GetOpposeSidePairs())
        {
            var rootGroup = (meShipLogs.FirstOrDefault() as IShipGroupMember)?.GetRootParent() as ShipGroup;
            pairOptions.Add(new OpposeSidePairOption
            {
                label = GetShipGroupLabel(rootGroup),
                rootGroup = rootGroup,
                meShipLogs = meShipLogs.ToList(),
                otherShipLogs = otherShipLogs.ToList(),
            });
        }

        if (rootGroupDropdownField != null)
        {
            rootGroupDropdownField.choices = pairOptions.Select(option => option.label).ToList();
            if (pairOptions.Count == 0)
            {
                rootGroupDropdownField.index = -1;
            }
            else
            {
                var selectedIndex = !string.IsNullOrEmpty(previousRootObjectId)
                    ? pairOptions.FindIndex(option => option.rootGroup?.objectId == previousRootObjectId)
                    : -1;
                rootGroupDropdownField.index = selectedIndex >= 0 ? selectedIndex : 0;
            }
        }
    }

    void StartInspection()
    {
        RefreshPairOptions();
        var pair = GetSelectedPair();
        if (pair == null)
        {
            InvalidateSession();
            RefreshDisplayedRows();
            return;
        }

        var solver = new WeaponTargetAssignmentSolver();
        var shooterObjects = pair.meShipLogs
            .Where(shipLog => shipLog.doctrine.GetFireAutomaticType() == AutomaticType.Automatic)
            .Cast<IWTAObject>()
            .ToList();
        var targetObjects = pair.otherShipLogs.Cast<IWTAObject>().ToList();

        session = solver.CreateInspectionSession(shooterObjects, targetObjects);
        RefreshDisplayedRows();
    }

    void InvalidateSession()
    {
        session = null;
    }

    OpposeSidePairOption GetSelectedPair()
    {
        if (rootGroupDropdownField == null)
            return null;
        var index = rootGroupDropdownField.index;
        if (index < 0 || index >= pairOptions.Count)
            return null;
        return pairOptions[index];
    }

    void RefreshDisplayedRows()
    {
        displayedRows = BuildDisplayedRows();
        if (gainListView != null)
        {
            gainListView.itemsSource = displayedRows;
            gainListView.Rebuild();
        }

        nextButton?.SetEnabled(session?.CanStep == true);
        startButton?.SetEnabled(GetSelectedPair() != null);
        exportCsvButton?.SetEnabled(displayedRows.Count > 0);
    }

    List<WeaponTargetAssignmentGainRow> BuildDisplayedRows()
    {
        var rows = session?.CurrentRows ?? new List<WeaponTargetAssignmentGainRow>();
        var distinctModes = new List<SortMode>();
        foreach (var mode in new[] { GetSortMode(primarySortDropdownField), GetSortMode(secondarySortDropdownField) })
        {
            if (mode == SortMode.None || distinctModes.Contains(mode))
                continue;
            distinctModes.Add(mode);
        }

        IOrderedEnumerable<WeaponTargetAssignmentGainRow> orderedRows = null;
        foreach (var mode in distinctModes)
        {
            orderedRows = ApplySort(orderedRows, rows, mode);
        }

        if (orderedRows == null)
            return rows.OrderBy(row => row.scanOrder).ToList();
        return orderedRows.ThenBy(row => row.scanOrder).ToList();
    }

    static IOrderedEnumerable<WeaponTargetAssignmentGainRow> ApplySort(
        IOrderedEnumerable<WeaponTargetAssignmentGainRow> existing,
        IEnumerable<WeaponTargetAssignmentGainRow> source,
        SortMode mode)
    {
        return mode switch
        {
            SortMode.Shooter => existing == null
                ? source.OrderBy(row => row.shooterName, StringComparer.CurrentCulture)
                : existing.ThenBy(row => row.shooterName, StringComparer.CurrentCulture),
            SortMode.Target => existing == null
                ? source.OrderBy(row => row.targetName, StringComparer.CurrentCulture)
                : existing.ThenBy(row => row.targetName, StringComparer.CurrentCulture),
            SortMode.Gain => existing == null
                ? source.OrderByDescending(row => row.finalGain)
                : existing.ThenByDescending(row => row.finalGain),
            _ => existing,
        };
    }

    static SortMode GetSortMode(DropdownField dropdownField)
    {
        if (dropdownField == null)
            return SortMode.None;
        var index = dropdownField.index;
        if (index < 0 || index >= SortModes.Length)
            return SortMode.None;
        return SortModes[index];
    }

    WeaponTargetAssignmentGainRow GetDisplayedRow(int index)
    {
        if (index < 0 || index >= displayedRows.Count)
            return null;
        return displayedRows[index];
    }

    static string FormatFloat(float value)
    {
        return value.ToString("0.###");
    }

    void ExportCsv()
    {
        var rows = displayedRows ?? new List<WeaponTargetAssignmentGainRow>();
        var builder = new StringBuilder();
        var headers = new[]
        {
            "scanOrder",
            "shooterName",
            "batteryName",
            "targetName",
            "finalGain",
            "rawGainBeforeStickiness",
            "distanceYards",
            "targetSelfFirepower",
            "targetSurvivability",
            "targetUrgencyFactor",
            "currentUnderFirepower",
            "currentOverConcentrationScore",
            "tryAddedFirepowerScoreBase",
            "tryAddedFirepowerScoreEffective",
            "tryAddedOverconcentrationScore",
            "isCurrentTarget",
            "currentTargetFireEffectivenessFactor",
            "changeTargetMultiplier",
        };
        builder.AppendLine(string.Join(",", headers));

        foreach (var row in rows)
        {
            var values = new[]
            {
                row.scanOrder.ToString(),
                row.shooterName,
                row.batteryName,
                row.targetName,
                FormatFloat(row.finalGain),
                FormatFloat(row.rawGainBeforeStickiness),
                FormatFloat(row.distanceYards),
                FormatFloat(row.targetSelfFirepower),
                FormatFloat(row.targetSurvivability),
                FormatFloat(row.targetUrgencyFactor),
                FormatFloat(row.currentUnderFirepower),
                row.currentOverConcentrationScore.ToString(),
                FormatFloat(row.tryAddedFirepowerScoreBase),
                FormatFloat(row.tryAddedFirepowerScoreEffective),
                row.tryAddedOverconcentrationScore.ToString(),
                row.isCurrentTarget ? "true" : "false",
                FormatFloat(row.currentTargetFireEffectivenessFactor),
                FormatFloat(row.changeTargetMultiplier),
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        IOManager.Instance.SaveTextFile(builder.ToString(), "wta_solver_inspector", "csv");
    }

    static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
