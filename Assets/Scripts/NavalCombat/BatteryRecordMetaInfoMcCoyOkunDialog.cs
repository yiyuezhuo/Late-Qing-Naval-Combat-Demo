using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using YYZ.Ballistic;

public sealed class BatteryRecordMetaInfoMcCoyOkunDialog
{
    enum ChartMode
    {
        Trajectory,
        Penetration
    }

    enum RangeMode
    {
        Sweep,
        SearchSk5
    }

    enum OkunMode
    {
        FacehardM79,
        FacehardOnly,
        M79Only
    }

    sealed class ResultRow
    {
        public McCoyPlusFacehardM79Row Result;
        public PenetrationTableRecord Sk5Record;
        public RangeBand SimulatedRangeBand;
        public float CalculatedRateOfFire;
    }

    sealed class TableColumnSpec<T>
    {
        public string Name;
        public string Title;
        public int Width;
        public Func<T, string> Selector;
    }

    sealed class FloatBinding
    {
        public bool Updating;
        public double? Min;
        public Action<double> Setter;
    }

    sealed class DropdownBinding
    {
        public bool Updating;
        public List<string> Choices = new();
        public Action<int> Setter;
    }

    sealed class TextBinding
    {
        public bool Updating;
        public Action<string> Setter;
    }

    sealed class ToggleBinding
    {
        public bool Updating;
        public Action<bool> Setter;
    }

    sealed class FitCandidate
    {
        public double BallisticCoefficient;
    }

    sealed class ExternalFitJob
    {
        public McCoyPlusInput Source;
        public List<PenetrationTableRecord> Records = new();
        public readonly List<FitCandidate> Candidates = new();

        public int Pass;
        public int CandidateIndex;
        public int ProcessedCandidates;
        public int TotalCandidates;
        public bool PauseRequested;
        public bool CancelRequested;

        public double OriginalBallisticCoefficient;
        public double BestBallisticCoefficient;
        public double CurrentBallisticCoefficient;
        public double BestScore = double.PositiveInfinity;
        public double CurrentScore = double.PositiveInfinity;
        public string CurrentDetail = "";
        public double BcSpan = 0.75;
    }

    readonly struct ExteriorFitScore
    {
        public readonly double Score;
        public readonly int RangeBandMismatchCount;
        public readonly double MaxRangeErrorYards;

        public ExteriorFitScore(double score, int rangeBandMismatchCount, double maxRangeErrorYards)
        {
            Score = score;
            RangeBandMismatchCount = rangeBandMismatchCount;
            MaxRangeErrorYards = maxRangeErrorYards;
        }
    }

    readonly BatteryRecord batteryRecord;
    readonly Action callback;

    VisualElement root;
    VisualElement outputContent;
    VisualElement fitProgressRoot;
    Button calculateButton;
    Button fitExternalButton;
    Button fitPauseButton;
    Button fitCancelButton;
    ProgressBar fitProgressBar;
    Label fitProgressLabel;
    MultiColumnListView sk5DataListView;
    IVisualElementScheduledItem fitSchedule;
    ExternalFitJob currentFitJob;

    McCoyPlusFacehardM79Input input = McCoyPlusFacehardM79.DefaultInput();
    FacehardInput facehardDetails = FacehardCalculator.DefaultFacehardInput();
    string sampleId = "";
    McCoyPlusDragFunction preset = McCoyPlusDragFunction.G1;
    string dragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    ChartMode chartMode = ChartMode.Trajectory;
    RangeMode rangeMode = RangeMode.SearchSk5;
    OkunMode okunMode = OkunMode.FacehardM79;
    float fallToNextFireSeconds = 12f;
    bool roundSyncBackValuesToOneDecimal = true;
    List<ResultRow> tableRows = new();
    bool hasCalculated;

    public BatteryRecordMetaInfoMcCoyOkunDialog(BatteryRecord batteryRecord, Action callback)
    {
        this.batteryRecord = batteryRecord;
        this.callback = callback;
        fallToNextFireSeconds = batteryRecord?.metaInfoMcCoyOkun?.fallToNextFireSeconds ?? 12f;
        var storedSample = batteryRecord?.metaInfoMcCoyOkun?.ballisticSample;
        if (storedSample != null)
        {
            ApplyBallisticSample(storedSample);
        }
        else
        {
            ApplyBatteryRecordDefaults();
        }
        EnsureMetaInfoBallisticSample();
    }

    public VisualElement BuildContent(VisualTreeAsset template)
    {
        if (template == null)
            throw new InvalidOperationException("Meta Info (McCoy Okun) requires BatteryRecordMetaInfoMcCoyOkunDialog.uxml to be assigned.");

        root = template.CloneTree();
        root.style.flexGrow = 1;
        root.style.flexShrink = 1;

        calculateButton = root.Q<Button>("CalculateButton");
        if (calculateButton != null)
            calculateButton.clicked += Calculate;
        fitExternalButton = root.Q<Button>("FitExternalBallisticButton");
        if (fitExternalButton != null)
            fitExternalButton.clicked += StartExternalFit;
        fitPauseButton = root.Q<Button>("FitPauseButton");
        if (fitPauseButton != null)
            fitPauseButton.clicked += PauseCurrentFit;
        fitCancelButton = root.Q<Button>("FitCancelButton");
        if (fitCancelButton != null)
            fitCancelButton.clicked += CancelCurrentFit;
        fitProgressRoot = root.Q<VisualElement>("FitProgressRoot");
        fitProgressBar = root.Q<ProgressBar>("FitProgressBar");
        fitProgressLabel = root.Q<Label>("FitProgressLabel");
        sk5DataListView = root.Q<MultiColumnListView>("Sk5PenetrationTableListView");
        ConfigureSk5DataListView();
        outputContent = root.Q<VisualElement>("OutputContent");

        RebuildInputs();
        RefreshFitControls();
        ShowPendingOutput();
        return root;
    }

    void ApplyBatteryRecordDefaults()
    {
        if (batteryRecord == null)
            return;

        if (batteryRecord.shellSizeInch > 0f)
            facehardDetails.ProjectileDiameter = batteryRecord.shellSizeInch;
        if (batteryRecord.shellWeightPounds > 0f)
        {
            facehardDetails.ProjectileWeight = batteryRecord.shellWeightPounds;
            facehardDetails.ProjectileBodyWeight = batteryRecord.shellWeightPounds;
        }
        if (batteryRecord.rangeYards > 0f)
            input.McCoy.MaxRange = batteryRecord.rangeYards;
        var shortName = batteryRecord.name?.GetShortName();
        if (!string.IsNullOrWhiteSpace(shortName))
            input.McCoy.ProjectileId = shortName;
    }

    void EnsureMetaInfoBallisticSample()
    {
        if (batteryRecord == null)
            return;

        var changed = false;
        if (batteryRecord.metaInfoMcCoyOkun == null)
        {
            batteryRecord.metaInfoMcCoyOkun = new BatteryRecordMetaInfoMcCoyOkun();
            changed = true;
        }
        if (batteryRecord.metaInfoMcCoyOkun.ballisticSample == null)
        {
            SaveMetaInfo();
            changed = true;
        }
        if (changed)
            callback?.Invoke();
    }

    void RebuildInputs()
    {
        BindProjectileTab();
        BindArmorTab();
        BindDeckArmorTab();
        BindMiscTab();
        BindSk5DataTab();
        RefreshFitControls();
    }

    void BindProjectileTab()
    {
        var samples = BallisticSampleCatalog.All();
        var sampleChoices = new List<string> { "Not Specified" };
        sampleChoices.AddRange(samples.Select(sample => sample.Label));
        var sampleIndex = samples.FindIndex(sample => sample.Id == sampleId);
        BindDropdown("SampleField", sampleChoices, sampleIndex >= 0 ? sampleIndex + 1 : 0, selected =>
        {
            if (selected <= 0)
            {
                sampleId = "";
                input.McCoy.ProjectileId = "Example projectile";
            }
            else
            {
                var sample = samples[Mathf.Clamp(selected - 1, 0, samples.Count - 1)];
                sampleId = sample.Id;
                ApplyBallisticSample(sample);
            }
            RebuildInputs();
            MarkOutputDirty();
        });

        var presets = McCoyPlus.DragPresets();
        BindDropdown("PresetField", presets.Select(item => item.Label).ToList(),
            Math.Max(0, presets.FindIndex(item => item.Function == preset)), selected =>
            {
                ApplyMcCoyPlusPreset(presets[Mathf.Max(0, selected)].Function);
                MarkOutputDirty();
            });
        BindFloat("MuzzleVelocityField", input.McCoy.MuzzleVelocity, value => input.McCoy.MuzzleVelocity = value, 1);
        BindFloat("BallisticCoefficientField", input.McCoy.BallisticCoefficient, value => input.McCoy.BallisticCoefficient = value, 0.001);
        BindFloat("MaxRangeField", input.McCoy.MaxRange, value => input.McCoy.MaxRange = value, 1);

        var preview = FacehardCalculator.CalculateFacehard(facehardDetails, false);
        BindFacehardProjectileFields(preview);
    }

    void BindArmorTab()
    {
        BindDropdown("ArmorTypeField", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == facehardDetails.ArmorId), selected =>
            {
                facehardDetails.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, selected)].Id;
                MarkOutputDirty();
            });
        BindToggle("CurvedPlateToggle", facehardDetails.CurvedPlate, value => facehardDetails.CurvedPlate = value);
        BindFloat("WoodBackingField", facehardDetails.WoodBackingThickness, value => facehardDetails.WoodBackingThickness = value, 0);
        BindFloat("CementBackingField", facehardDetails.CementBackingThickness, value => facehardDetails.CementBackingThickness = value, 0);
        BindFloat("MetalBackingField", facehardDetails.MetalBackingThickness, value => facehardDetails.MetalBackingThickness = value, 0);
        BindFloat("BackingQualityField", facehardDetails.BackingQuality, value => facehardDetails.BackingQuality = value, 0.001);
        BindFloat("BackingPlatesField", facehardDetails.BackingPlates, value => facehardDetails.BackingPlates = value, 0);
        UpdateInputWarnings();
    }

    void BindDeckArmorTab()
    {
        BindFloat("M79ArmorQualityField", input.M79.PlateQuality, value => input.M79.PlateQuality = value, 0.001);
        BindFloat("M79ElongationField", input.M79.Elongation, value => input.M79.Elongation = value, 10);
    }

    void BindMiscTab()
    {
        BindDropdown("ElevationSearchField", new List<string> { "Cached Binary Search", "Matched Range" },
            input.McCoy.ElevationSearchMode == McCoyPlusElevationSearchMode.MatchedRange ? 1 : 0, selected =>
            {
                input.McCoy.ElevationSearchMode = selected == 1
                    ? McCoyPlusElevationSearchMode.MatchedRange
                    : McCoyPlusElevationSearchMode.CachedBinarySearch;
                MarkOutputDirty();
            });
        BindDropdown("AtmosphereField", new List<string> { "Army Standard Metro", "ICAO" },
            input.McCoy.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, selected =>
            {
                input.McCoy.Atmosphere = selected == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro;
                MarkOutputDirty();
            });
        BindFloat("DensityRatioField", input.McCoy.DensityRatio, value => input.McCoy.DensityRatio = value, 0.001);
        BindFloat("TemperatureField", input.McCoy.TemperatureF, value => input.McCoy.TemperatureF = value);
        BindFloat("MatchHeightField", input.McCoy.MatchHeight, value => input.McCoy.MatchHeight = value);
        BindDropdown("RangeModeField", new List<string> { "Sweep", "Search SK5" },
            rangeMode == RangeMode.SearchSk5 ? 1 : 0, selected =>
            {
                rangeMode = selected == 1 ? RangeMode.SearchSk5 : RangeMode.Sweep;
                MarkOutputDirty();
            });
        BindDropdown("ChartModeField", new List<string> { "Matched Trajectories", "Penetration By Range" },
            chartMode == ChartMode.Trajectory ? 0 : 1, selected =>
            {
                chartMode = selected == 0 ? ChartMode.Trajectory : ChartMode.Penetration;
                MarkOutputDirty();
            });
        BindDropdown("OkunModeField", new List<string> { "Facehard + M79", "Facehard Only", "M79 Only" },
            okunMode switch
            {
                OkunMode.FacehardOnly => 1,
                OkunMode.M79Only => 2,
                _ => 0,
            }, selected =>
            {
                okunMode = selected switch
                {
                    1 => OkunMode.FacehardOnly,
                    2 => OkunMode.M79Only,
                    _ => OkunMode.FacehardM79,
                };
                MarkOutputDirty();
            });
        BindText("DragTableField", dragText, value =>
        {
            dragText = value;
            input.McCoy.DragTable = McCoy.NormalizeDragTable(dragText);
            MarkOutputDirty();
        });
    }

    void BindSk5DataTab()
    {
        var batteryLabel = root?.Q<Label>("Sk5BatteryShortNameLabel");
        if (batteryLabel != null)
            batteryLabel.text = batteryRecord?.name?.GetShortName() ?? "Battery";
        BindFloat("Sk5MaxRateOfFireField", CurrentMaxRateOfFireShootPerMin(), _ => { }, 0, false);
        BindFloat("Sk5FallToNextFireSecondsField", fallToNextFireSeconds, value => fallToNextFireSeconds = (float)value, 0);
        BindToggle("RoundSyncBackValuesField", roundSyncBackValuesToOneDecimal, value => roundSyncBackValuesToOneDecimal = value);
    }

    void ConfigureSk5DataListView()
    {
        if (sk5DataListView == null)
            return;

        sk5DataListView.itemsSource = CurrentPenetrationTableRecords();
        sk5DataListView.selectionType = SelectionType.None;
        sk5DataListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        sk5DataListView.fixedItemHeight = 24;
        sk5DataListView.showAddRemoveFooter = false;
        sk5DataListView.columns.Clear();

        void AddLabelColumn(string name, string title, int width, Func<PenetrationTableRecord, string> selector)
        {
            sk5DataListView.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 80),
                stretchable = false,
                makeCell = () => new Label { style = { whiteSpace = WhiteSpace.NoWrap } },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;
                    var row = GetSk5Row(index);
                    label.text = row == null ? "" : selector(row);
                }
            });
        }

        AddLabelColumn("rangeBand", "Range Band", 120, row => row.rangeBand.ToString());
        AddLabelColumn("distanceYards", "Distance Yards", 130, row => F(row.distanceYards, 0));
        AddLabelColumn("rateOfFire", "Rate of Fire", 120, row => F(row.rateOfFire, 2));
        AddLabelColumn("horizontalPenetration", "Hor Pen", 110, row => F(row.horizontalPenetrationInchs, 2));
        AddLabelColumn("verticalPenetration", "Vert Pen", 110, row => F(row.verticalPenetrationInchs, 2));

        sk5DataListView.Rebuild();
    }

    PenetrationTableRecord GetSk5Row(int index)
    {
        var records = CurrentPenetrationTableRecords();
        return index >= 0 && index < records.Count
            ? records[index]
            : null;
    }

    void BindFacehardProjectileFields(FacehardResult preview)
    {
        var selectedPreset = preview?.ProjectilePreset
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == facehardDetails.ProjectilePresetId)
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
        var selectedNation = selectedPreset?.Nation ?? "Custom";
        var nations = FacehardCalculator.FacehardProjectileNations;
        var resolvedCapType = preview?.ResolvedCapType ?? ResolvedCapType(facehardDetails);

        BindDropdown("ProjectileNationField", nations.Select(item => item.Name).ToList(),
            Math.Max(0, nations.FindIndex(item => item.Id == selectedNation)), selected =>
            {
                facehardDetails.ProjectilePresetId = nations[Mathf.Max(0, selected)].DefaultProjectileId;
                RebuildInputs();
                MarkOutputDirty();
            });

        var projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == selectedNation).ToList();
        if (projectileChoices.Count == 0)
            projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        BindDropdown("ProjectileTypeField", projectileChoices.Select(ProjectilePresetLabel).ToList(),
            Math.Max(0, projectileChoices.FindIndex(item => item.Id == facehardDetails.ProjectilePresetId)), selected =>
            {
                facehardDetails.ProjectilePresetId = projectileChoices[Mathf.Max(0, selected)].Id;
                RebuildInputs();
                MarkOutputDirty();
            });

        var isCustom = facehardDetails.ProjectilePresetId == "custom";
        var capLabels = new List<string> { "Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap" };
        var capValues = new List<FacehardCapType> { FacehardCapType.Hard, FacehardCapType.ThinHard, FacehardCapType.Soft, FacehardCapType.Hood, FacehardCapType.None };
        BindDropdown("CapOrHoodField", capLabels, Math.Max(0, capValues.IndexOf(resolvedCapType)), selected =>
        {
            facehardDetails.CapType = capValues[Mathf.Max(0, selected)];
            RebuildInputs();
            MarkOutputDirty();
        }, isCustom);

        var schemaValues = new List<FacehardNoseSchema> { FacehardNoseSchema.Standard, FacehardNoseSchema.JapaneseCapHead };
        BindDropdown("NoseSchemaField", new List<string> { "Standard", "Japanese Cap Head" },
            Math.Max(0, schemaValues.IndexOf(facehardDetails.NoseSchema)), selected =>
            {
                facehardDetails.NoseSchema = schemaValues[Mathf.Max(0, selected)];
                facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                RebuildInputs();
                MarkOutputDirty();
            }, isCustom);

        SetDisplay("JapaneseCapHeadField", isCustom && facehardDetails.NoseSchema == FacehardNoseSchema.JapaneseCapHead);
        BindDropdown("JapaneseCapHeadField", new List<string> { "Uncapped Type 91 AP", "Capped Type 88/91/1 APC" },
            facehardDetails.JapaneseCapHead <= 1 ? 0 : 1, selected =>
            {
                facehardDetails.JapaneseCapHead = selected == 0 ? 1 : 2;
                facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                RebuildInputs();
                MarkOutputDirty();
            });

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        BindDropdown("NoseConditionField", noseWeights.ConditionOptions.Select(NoseConditionLabel).ToList(),
            Math.Max(0, noseWeights.ConditionOptions.IndexOf(noseWeights.Condition)), selected =>
            {
                facehardDetails.NoseCondition = noseWeights.ConditionOptions[Mathf.Max(0, selected)];
                RebuildInputs();
                MarkOutputDirty();
            });
        BindFloat("ProjectileDiameterField", facehardDetails.ProjectileDiameter, value => facehardDetails.ProjectileDiameter = value, 0.001);
        BindFloat("ProjectileWeightField", facehardDetails.ProjectileWeight, value => facehardDetails.ProjectileWeight = value, 0.001);
        BindFloat("ProjectileBodyWeightField", facehardDetails.ProjectileBodyWeight, value => facehardDetails.ProjectileBodyWeight = value, 0.001);
        SetDisplay("WindscreenCapHeadWeightField", noseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead && noseWeights.CapHead == 2);
        BindFloat("WindscreenCapHeadWeightField", facehardDetails.WindscreenCapHeadWeight, value => facehardDetails.WindscreenCapHeadWeight = value, 0);
        SetDisplay("WindscreenWeightField", noseWeights.SchemaKind == FacehardNoseSchema.Standard);
        BindFloat("WindscreenWeightField", facehardDetails.WindscreenWeight, value => facehardDetails.WindscreenWeight = value, 0);
        BindFloat("RemainingNoseWeightField", noseWeights.RemainWeight, _ => { }, 0, false, RemainingNoseWeightLabel(noseWeights.CapHead, resolvedCapType));
        BindFloat("PlimField", isCustom ? facehardDetails.ProjectileLimitQuality : selectedPreset?.ProjectileLimitQuality ?? facehardDetails.ProjectileLimitQuality,
            value => facehardDetails.ProjectileLimitQuality = value, 0.001, isCustom);
        BindFloat("PdamField", isCustom ? facehardDetails.ProjectileDamageQuality : selectedPreset?.ProjectileDamageQuality ?? facehardDetails.ProjectileDamageQuality,
            value => facehardDetails.ProjectileDamageQuality = value, 0.001, isCustom);
    }

    void Calculate()
    {
        if (currentFitJob != null)
            return;

        SyncComboMcCoy();
        SyncFacehardBridge();
        McCoyPlusFacehard.FacehardCalculator = bridge => FacehardBridgeCalculate(bridge, facehardDetails);
        var stopwatch = Stopwatch.StartNew();
        var targetRanges = rangeMode == RangeMode.SearchSk5 ? GetSearchSk5TargetRanges() : null;
        var result = CalculateOkun(input, targetRanges, okunMode);
        stopwatch.Stop();
        tableRows = result.Rows.Select(BuildRow).ToList();

        outputContent.Clear();
        outputContent.Add(Warnings(result.Warnings));
        outputContent.Add(chartMode == ChartMode.Trajectory
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, input.McCoy.RangeUnit), BallisticOptions.ToLegacyCode(input.McCoy.RangeUnit), "ft")
            : ChartSeries("Penetration By Range", PenetrationSeries(result.Rows), BallisticOptions.ToLegacyCode(input.McCoy.RangeUnit), "in"));
        outputContent.Add(CalculationTime(stopwatch.Elapsed));
        outputContent.Add(Table("Rows", tableRows,
            Col<ResultRow>("range", "Range", 90, row => F(row.Result?.Range, 0)),
            Col<ResultRow>("horizontal", "Horizontal Pen Calc/SK5", 170, row => FormatPenetrationComparison(row.Result?.HorizontalPenetrationInches, row.Sk5Record?.horizontalPenetrationInchs)),
            Col<ResultRow>("vertical", "Vertical Pen Calc/SK5", 160, row => FormatPenetrationComparison(row.Result?.PenetrationInches, row.Sk5Record?.verticalPenetrationInchs)),
            Col<ResultRow>("rof", "ROF Calc/SK5", 120, row => FormatFloatComparison(row.CalculatedRateOfFire, row.Sk5Record?.rateOfFire, 2)),
            Col<ResultRow>("rangeBand", "Range Band Calc/SK5", 160, row => row.Sk5Record == null ? row.SimulatedRangeBand.ToString() : $"{row.SimulatedRangeBand}/{row.Sk5Record.rangeBand}"),
            Col<ResultRow>("time", "Time", 90, row => F(row.Result?.Time, 3)),
            Col<ResultRow>("velocity", "Velocity", 90, row => F(row.Result?.Velocity, 0)),
            Col<ResultRow>("fall", "Fall Angle", 100, row => F(row.Result?.FallAngleDegrees, 3)),
            Col<ResultRow>("elevation", "Elevation", 90, row => F(row.Result?.ElevationDegrees, 4))));
        hasCalculated = true;
    }

    static McCoyPlusFacehardM79Result CalculateOkun(McCoyPlusFacehardM79Input source, IEnumerable<double> targetRanges, OkunMode mode)
    {
        if (mode == OkunMode.FacehardM79)
            return McCoyPlusFacehardM79.Calculate(source, targetRanges);

        source ??= McCoyPlusFacehardM79.DefaultInput();
        var trajectory = targetRanges == null
            ? McCoyPlus.CalculateParallel(source.McCoy)
            : McCoyPlus.CalculateTargetsParallel(source.McCoy, targetRanges);
        var warnings = new List<string>(trajectory.Warnings);
        var rows = new List<McCoyPlusFacehardM79Row>();

        foreach (var trajectoryRow in trajectory.Rows)
        {
            var row = mode == OkunMode.FacehardOnly
                ? BuildFacehardOnlyRow(source.Facehard, trajectoryRow, warnings)
                : BuildM79OnlyRow(source.M79, trajectoryRow, warnings);
            rows.Add(row);
        }

        return new McCoyPlusFacehardM79Result
        {
            Rows = rows,
            ChartRows = McCoyPlus.SelectChartRows(rows),
            Warnings = warnings
                .Where(warning => !string.IsNullOrEmpty(warning))
                .Distinct()
                .ToList(),
        };
    }

    static McCoyPlusFacehardM79Row BuildFacehardOnlyRow(FacehardBridgeInput facehard, McCoyPlusRow trajectoryRow, List<string> warnings)
    {
        var verticalSolved = McCoyPlusFacehard.SolvePenetrationThicknessForRow(facehard, trajectoryRow, trajectoryRow.FallAngleDegrees);
        var horizontalSolved = McCoyPlusFacehard.SolvePenetrationThicknessForRow(facehard, trajectoryRow, 90 - trajectoryRow.FallAngleDegrees, true);
        AddWarning(warnings, verticalSolved.Warning);
        AddWarning(warnings, horizontalSolved.Warning);

        return new McCoyPlusFacehardM79Row
        {
            Range = trajectoryRow.Range,
            Time = trajectoryRow.Time,
            ElevationDegrees = trajectoryRow.ElevationDegrees,
            Velocity = trajectoryRow.Velocity,
            FallAngleDegrees = trajectoryRow.FallAngleDegrees,
            Trajectory = trajectoryRow.Trajectory,
            PenetrationInches = verticalSolved.PenetrationInches,
            HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
            FacehardNavyBl = verticalSolved.FacehardNavyBl,
            FacehardObliquity = verticalSolved.FacehardObliquity,
        };
    }

    static McCoyPlusFacehardM79Row BuildM79OnlyRow(M79Input m79, McCoyPlusRow trajectoryRow, List<string> warnings)
    {
        var verticalSolved = McCoyPlusM79.SolvePenetrationThicknessForRow(m79, trajectoryRow, trajectoryRow.FallAngleDegrees);
        var horizontalSolved = McCoyPlusM79.SolvePenetrationThicknessForRow(m79, trajectoryRow, 90 - trajectoryRow.FallAngleDegrees, true);
        AddWarning(warnings, verticalSolved.Warning);
        AddWarning(warnings, horizontalSolved.Warning);

        return new McCoyPlusFacehardM79Row
        {
            Range = trajectoryRow.Range,
            Time = trajectoryRow.Time,
            ElevationDegrees = trajectoryRow.ElevationDegrees,
            Velocity = trajectoryRow.Velocity,
            FallAngleDegrees = trajectoryRow.FallAngleDegrees,
            Trajectory = trajectoryRow.Trajectory,
            PenetrationInches = verticalSolved.PenetrationInches,
            HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
            M79NavyBallisticLimit = horizontalSolved.M79NavyBallisticLimit,
            M79Obliquity = horizontalSolved.M79Obliquity,
            PenetrationMode = horizontalSolved.PenetrationMode,
            RemainingVelocity = horizontalSolved.RemainingVelocity,
        };
    }

    static void AddWarning(List<string> warnings, string warning)
    {
        if (!string.IsNullOrEmpty(warning))
            warnings.Add(warning);
    }

    ResultRow BuildRow(McCoyPlusFacehardM79Row result)
    {
        var angleOfFall = result == null ? 0f : (float)result.FallAngleDegrees;
        var simulatedBand = Sk5RangeBandRules.FromAngleOfFallDeg(angleOfFall);
        return new ResultRow
        {
            Result = result,
            Sk5Record = result == null ? null : FindSk5Record((float)result.Range),
            SimulatedRangeBand = simulatedBand,
            CalculatedRateOfFire = result == null ? 0f : CalculateRateOfFirePerTwoMinutes((float)result.Time)
        };
    }

    void StartExternalFit()
    {
        if (currentFitJob != null)
            return;

        var setupError = CreateExternalFitJob(out var job);
        if (setupError != null)
        {
            ShowFitStatus(setupError, 0);
            return;
        }

        currentFitJob = job;
        PrepareExternalFitPass(job);
        UpdateFitProgress(job, "External ballistic fit started.");
        RefreshFitControls();
        fitSchedule = root.schedule.Execute(ProcessFitStep).Every(1);
    }

    string CreateExternalFitJob(out ExternalFitJob job)
    {
        job = null;
        SyncComboMcCoy();
        var fitRecords = CurrentPenetrationTableRecords()
            .Where(record => record != null && record.distanceYards > 0f)
            .OrderBy(record => record.distanceYards)
            .ToList();
        if (fitRecords.Count == 0)
            return "No valid SK5 rows.";
        if (input.McCoy.BallisticCoefficient <= 0)
            return "Ballistic coefficient must be greater than 0.";
        if (input.McCoy.MaxRange <= 0)
            return "Maximum range must be greater than 0.";

        var source = CloneMcCoyPlusInput(input.McCoy);
        source.MaxRange = Math.Max(source.MaxRange, fitRecords.Max(record => record.distanceYards));
        job = new ExternalFitJob
        {
            Source = source,
            Records = fitRecords,
            OriginalBallisticCoefficient = input.McCoy.BallisticCoefficient,
            BestBallisticCoefficient = Math.Max(input.McCoy.BallisticCoefficient, 0.01),
            CurrentBallisticCoefficient = input.McCoy.BallisticCoefficient,
            TotalCandidates = 4 * 9,
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

        if (job.CancelRequested)
        {
            FinishFitJob(false, "Cancelled by user");
            return;
        }

        if (job.PauseRequested)
        {
            FinishFitJob(true, "Paused by user");
            return;
        }

        if (job.Pass >= 4 && job.CandidateIndex >= job.Candidates.Count)
        {
            FinishFitJob(true, "Completed");
            return;
        }

        if (job.CandidateIndex >= job.Candidates.Count)
        {
            job.Pass++;
            job.BcSpan *= 0.42;
            if (job.Pass >= 4)
            {
                FinishFitJob(true, "Completed");
                return;
            }
            PrepareExternalFitPass(job);
        }

        var candidate = job.Candidates[job.CandidateIndex++];
        job.CurrentBallisticCoefficient = candidate.BallisticCoefficient;
        var detail = ScoreExterior(job.Source, candidate.BallisticCoefficient, job.Records);
        job.CurrentScore = detail.Score;
        job.CurrentDetail = double.IsInfinity(detail.MaxRangeErrorYards)
            ? $"mismatch {detail.RangeBandMismatchCount}, max range target failed"
            : $"mismatch {detail.RangeBandMismatchCount}, max range error {detail.MaxRangeErrorYards:0} yd";
        job.ProcessedCandidates++;
        if (detail.Score < job.BestScore)
        {
            job.BestScore = detail.Score;
            job.BestBallisticCoefficient = candidate.BallisticCoefficient;
        }
        UpdateFitProgress(job, BuildExternalFitDiagnostic(job));
    }

    static void PrepareExternalFitPass(ExternalFitJob job)
    {
        job.Candidates.Clear();
        job.CandidateIndex = 0;
        for (var index = -4; index <= 4; index++)
        {
            job.Candidates.Add(new FitCandidate
            {
                BallisticCoefficient = Math.Max(0.01, job.BestBallisticCoefficient * Math.Pow(1 + job.BcSpan, index / 4d))
            });
        }
    }

    void PauseCurrentFit()
    {
        if (currentFitJob != null)
            currentFitJob.PauseRequested = true;
    }

    void CancelCurrentFit()
    {
        if (currentFitJob != null)
            currentFitJob.CancelRequested = true;
    }

    void FinishFitJob(bool applyBest, string reason)
    {
        var job = currentFitJob;
        fitSchedule?.Pause();
        fitSchedule = null;
        currentFitJob = null;

        if (job != null)
        {
            input.McCoy.BallisticCoefficient = applyBest
                ? job.BestBallisticCoefficient
                : job.OriginalBallisticCoefficient;
            RebuildInputs();
            tableRows.Clear();
            hasCalculated = false;
            var status = applyBest
                ? $"{reason}: best BC {job.BestBallisticCoefficient:0.####}, score {job.BestScore:0.####}. Click Calculate to refresh."
                : $"{reason}: restored BC {job.OriginalBallisticCoefficient:0.####}.";
            ShowPendingOutput(status);
            ShowFitStatus(status, applyBest ? 1 : 0);
        }

        RefreshFitControls();
    }

    void UpdateFitProgress(ExternalFitJob job, string diagnostic)
    {
        var progress = job.TotalCandidates <= 0 ? 0 : Math.Clamp((double)job.ProcessedCandidates / job.TotalCandidates, 0, 1);
        ShowFitStatus(diagnostic, progress);
    }

    void ShowFitStatus(string message, double progress)
    {
        if (fitProgressBar != null)
            fitProgressBar.value = (float)Math.Clamp(progress, 0, 1);
        if (fitProgressLabel != null)
            fitProgressLabel.text = message ?? "";
        RefreshFitControls();
    }

    void RefreshFitControls()
    {
        var fitting = currentFitJob != null;
        calculateButton?.SetEnabled(!fitting);
        fitExternalButton?.SetEnabled(!fitting);
        if (fitPauseButton != null)
            fitPauseButton.style.display = fitting ? DisplayStyle.Flex : DisplayStyle.None;
        if (fitCancelButton != null)
            fitCancelButton.style.display = fitting ? DisplayStyle.Flex : DisplayStyle.None;
        if (fitProgressRoot != null)
        {
            var hasMessage = !string.IsNullOrEmpty(fitProgressLabel?.text);
            fitProgressRoot.style.display = fitting || hasMessage ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    string BuildExternalFitDiagnostic(ExternalFitJob job)
    {
        return string.Join("\n", new[]
        {
            "External ballistic fit",
            $"pass {job.Pass + 1}/4, candidate {job.CandidateIndex}/{job.Candidates.Count}",
            $"current BC {job.CurrentBallisticCoefficient:0.####}, score {job.CurrentScore:0.####}",
            $"best BC {job.BestBallisticCoefficient:0.####}, score {job.BestScore:0.####}",
            job.CurrentDetail
        });
    }

    ExteriorFitScore ScoreExterior(McCoyPlusInput seed, double ballisticCoefficient, List<PenetrationTableRecord> records)
    {
        var source = CloneMcCoyPlusInput(seed);
        source.BallisticCoefficient = ballisticCoefficient;
        var targetRanges = new List<double>(records.Count);
        for (var index = 0; index < records.Count; index++)
            targetRanges.Add(GetFitRecordTargetRange(records, index, source.MaxRange));

        var result = McCoyPlus.CalculateTargetsParallel(source, targetRanges, 8);
        var score = 0d;
        var mismatchCount = 0;
        for (var index = 0; index < records.Count; index++)
        {
            var targetRange = targetRanges[index];
            var row = FindTrajectoryRow(result.Rows, targetRange);
            if (row != null)
            {
                var predicted = Sk5RangeBandRules.FromAngleOfFallDeg((float)row.FallAngleDegrees);
                var bandDelta = Math.Abs((int)predicted - (int)records[index].rangeBand);
                if (bandDelta > 0)
                    mismatchCount++;
                score += bandDelta * bandDelta * 10d;
            }
            else
            {
                mismatchCount++;
                score += 100d;
            }
        }

        var maxRangeTarget = targetRanges.Count == 0 ? source.MaxRange : targetRanges[^1];
        var maxRangeRow = FindTrajectoryRow(result.Rows, maxRangeTarget);
        var maxRangeErrorYards = maxRangeRow == null ? double.PositiveInfinity : maxRangeRow.Range - source.MaxRange;
        if (maxRangeRow == null)
            score += 250d;

        return new ExteriorFitScore(score, mismatchCount, maxRangeErrorYards);
    }

    static McCoyPlusRow FindTrajectoryRow(List<McCoyPlusRow> rows, double targetRange)
    {
        var tolerance = Math.Max(0.5, Math.Abs(targetRange) * 0.0001);
        return rows?
            .Where(row => row != null)
            .OrderBy(row => Math.Abs(row.Range - targetRange))
            .FirstOrDefault(row => Math.Abs(row.Range - targetRange) <= tolerance);
    }

    static double GetFitRecordTargetRange(IReadOnlyList<PenetrationTableRecord> records, int index, double maxRange)
    {
        if (records == null || index < 0 || index >= records.Count)
            return 0;
        return index == records.Count - 1 ? maxRange : records[index].distanceYards;
    }

    static McCoyPlusInput CloneMcCoyPlusInput(McCoyPlusInput source)
    {
        source ??= McCoyPlus.DefaultInput();
        return new McCoyPlusInput
        {
            DragName = source.DragName,
            DragTable = source.DragTable,
            RangeUnit = source.RangeUnit,
            Atmosphere = source.Atmosphere,
            ProjectileId = source.ProjectileId,
            MuzzleVelocity = source.MuzzleVelocity,
            BallisticCoefficient = source.BallisticCoefficient,
            MaxRange = source.MaxRange,
            DensityRatio = source.DensityRatio,
            TemperatureF = source.TemperatureF,
            MatchHeight = source.MatchHeight,
            ElevationSearchMode = source.ElevationSearchMode,
        };
    }

    List<double> GetSearchSk5TargetRanges()
    {
        var records = CurrentPenetrationTableRecords();
        var sourceRanges = records.Any(record => record != null && record.distanceYards > 0f)
            ? records
                .Where(record => record != null && record.distanceYards > 0f)
                .Select(record => record.distanceYards)
                .Distinct()
                .OrderBy(range => range)
                .ToList()
            : ShipClassEditor.PenetrationTableDistanceYards.ToList();
        return BuildSearchSk5TargetRanges(sourceRanges, (float)input.McCoy.MaxRange)
            .Select(range => (double)range)
            .ToList();
    }

    static List<float> BuildSearchSk5TargetRanges(List<float> sourceRanges, float maxRangeYards)
    {
        var maxRange = MathF.Max(maxRangeYards, 0f);
        var ranges = (sourceRanges ?? new List<float>())
            .Where(range => range > 0f && range < maxRange - 0.5f)
            .Distinct()
            .OrderBy(range => range)
            .ToList();

        if (maxRange > 0f && !ranges.Any(range => MathF.Abs(range - maxRange) <= 0.5f))
            ranges.Add(maxRange);

        return ranges;
    }

    PenetrationTableRecord FindSk5Record(float rangeYards)
    {
        var recordsSource = CurrentPenetrationTableRecords();
        if (rangeMode != RangeMode.SearchSk5 || recordsSource.Count == 0)
            return null;
        var records = recordsSource
            .Where(record => record != null && record.distanceYards > 0f)
            .OrderBy(record => MathF.Abs(record.distanceYards - rangeYards))
            .ToList();
        var exact = records.FirstOrDefault(record => MathF.Abs(record.distanceYards - rangeYards) <= 0.5f);
        if (exact != null)
            return exact;
        var last = records.OrderBy(record => record.distanceYards).LastOrDefault();
        return last != null && MathF.Abs(rangeYards - (float)input.McCoy.MaxRange) <= 0.5f ? last : null;
    }

    float CalculateRateOfFirePerTwoMinutes(float timeOfFlightSeconds)
    {
        var firingCycleSeconds = timeOfFlightSeconds + fallToNextFireSeconds;
        if (firingCycleSeconds <= 0f)
            return 0f;
        var flightLimitedRate = 120f / firingCycleSeconds;
        var gunLimitedRate = MathF.Max(CurrentMaxRateOfFireShootPerMin(), 0f) * 2f;
        return MathF.Min(flightLimitedRate, gunLimitedRate);
    }

    public bool SaveAndClose()
    {
        if (currentFitJob != null)
        {
            ShowFitStatus("Finish or cancel the fitting job before saving.", 0);
            return false;
        }

        return SyncBack();
    }

    public bool ClearAndClose()
    {
        if (currentFitJob != null)
        {
            ShowFitStatus("Finish or cancel the fitting job before clearing.", 0);
            return false;
        }

        if (batteryRecord == null)
            return true;

        batteryRecord.metaInfoMcCoyOkun = null;
        callback?.Invoke();
        return true;
    }

    public void OnClosed()
    {
        fitSchedule?.Pause();
        fitSchedule = null;
        currentFitJob = null;
    }

    bool SyncBack()
    {
        if (currentFitJob != null)
        {
            ShowFitStatus("Finish or cancel the fitting job before saving.", 0);
            return false;
        }

        if (batteryRecord == null)
            return true;

        if (tableRows.Count == 0)
        {
            ShowPendingOutput("Calculate successful rows before saving.");
            return false;
        }

        var newRecords = BuildCalculatedPenetrationRecords();
        batteryRecord.rangeYards = (float)input.McCoy.MaxRange;
        batteryRecord.penetrationTableRecords ??= new List<PenetrationTableRecord>();
        batteryRecord.penetrationTableRecords.Clear();
        batteryRecord.penetrationTableRecords.AddRange(newRecords);
        if (sk5DataListView != null)
        {
            sk5DataListView.itemsSource = CurrentPenetrationTableRecords();
            sk5DataListView.Rebuild();
        }

        batteryRecord.metaInfoMcCoyOkun ??= new BatteryRecordMetaInfoMcCoyOkun();
        SaveMetaInfo();
        callback?.Invoke();
        return true;
    }

    void SaveMetaInfo()
    {
        if (batteryRecord?.metaInfoMcCoyOkun == null)
            return;

        batteryRecord.metaInfoMcCoyOkun.ballisticSample = CreateBallisticSampleFromCurrentInput();
        batteryRecord.metaInfoMcCoyOkun.fallToNextFireSeconds = fallToNextFireSeconds;
    }

    List<PenetrationTableRecord> BuildCalculatedPenetrationRecords()
    {
        return tableRows
            .Where(row => row?.Result != null)
            .Select(row => new PenetrationTableRecord
            {
                distanceYards = GetBatteryRecordPenetrationTableRange((float)row.Result.Range),
                rateOfFire = RoundSyncBackValue(row.CalculatedRateOfFire),
                rangeBand = row.SimulatedRangeBand,
                horizontalPenetrationInchs = RoundSyncBackValue((float)(row.Result.HorizontalPenetrationInches ?? 0d)),
                verticalPenetrationInchs = RoundSyncBackValue((float)(row.Result.PenetrationInches ?? 0d))
            })
            .ToList();
    }

    float GetBatteryRecordPenetrationTableRange(float resultRangeYards)
    {
        if (MathF.Abs(resultRangeYards - (float)input.McCoy.MaxRange) > 0.5f)
            return resultRangeYards;

        return GetNextSk5RangeThreshold((float)input.McCoy.MaxRange);
    }

    static float GetNextSk5RangeThreshold(float rangeYards)
    {
        var threshold = ShipClassEditor.PenetrationTableDistanceYards.FirstOrDefault(range => range >= rangeYards - 0.5f);
        return threshold > 0f ? threshold : rangeYards;
    }

    float RoundSyncBackValue(float value)
    {
        return roundSyncBackValuesToOneDecimal
            ? MathF.Round(value, 1, MidpointRounding.AwayFromZero)
            : value;
    }

    void MarkOutputDirty()
    {
        if (!hasCalculated || outputContent == null)
            return;
        hasCalculated = false;
        tableRows.Clear();
        ShowPendingOutput("Input changed. Click Calculate to refresh.");
    }

    void ShowPendingOutput(string message = "Click Calculate to run McCoy Plus Facehard.")
    {
        outputContent?.Clear();
        outputContent?.Add(new Label(message)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 6
            }
        });
    }

    BallisticSample CreateBallisticSampleFromCurrentInput()
    {
        return new BallisticSample
        {
            Id = sampleId,
            Label = string.IsNullOrWhiteSpace(input.McCoy.ProjectileId) ? "Custom projectile" : input.McCoy.ProjectileId,
            DragFunction = preset,
            ProjectilePresetId = facehardDetails.ProjectilePresetId,
            CapType = facehardDetails.CapType,
            NoseSchema = facehardDetails.NoseSchema,
            JapaneseCapHead = facehardDetails.JapaneseCapHead,
            ProjectileDiameter = facehardDetails.ProjectileDiameter,
            ProjectileWeight = facehardDetails.ProjectileWeight,
            ProjectileBodyWeight = facehardDetails.ProjectileBodyWeight,
            WindscreenWeight = facehardDetails.WindscreenWeight,
            WindscreenCapHeadWeight = facehardDetails.WindscreenCapHeadWeight,
            ProjectileLimitQuality = facehardDetails.ProjectileLimitQuality,
            ProjectileDamageQuality = facehardDetails.ProjectileDamageQuality,
            BallisticCoefficient = input.McCoy.BallisticCoefficient,
            MuzzleVelocity = input.McCoy.MuzzleVelocity,
            MaxRange = input.McCoy.MaxRange
        };
    }

    void ApplyBallisticSample(BallisticSample sample)
    {
        if (sample == null)
            return;

        sampleId = sample.Id ?? "";
        ApplyMcCoyPlusPreset(sample.DragFunction);
        input.McCoy.ProjectileId = string.IsNullOrWhiteSpace(sample.Label) ? "Custom projectile" : sample.Label;
        input.McCoy.BallisticCoefficient = sample.BallisticCoefficient;
        input.McCoy.MuzzleVelocity = sample.MuzzleVelocity;
        input.McCoy.MaxRange = sample.MaxRange;

        facehardDetails.ProjectilePresetId = string.IsNullOrWhiteSpace(sample.ProjectilePresetId)
            ? FacehardInput.CreateDefault().ProjectilePresetId
            : sample.ProjectilePresetId;
        facehardDetails.ProjectileDiameter = sample.ProjectileDiameter;
        facehardDetails.ProjectileWeight = sample.ProjectileWeight;
        facehardDetails.ProjectileBodyWeight = sample.ProjectileBodyWeight;
        facehardDetails.CapType = sample.CapType;
        facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
        facehardDetails.WindscreenWeight = sample.WindscreenWeight;

        var isCustomProjectile = facehardDetails.ProjectilePresetId == "custom";
        if (isCustomProjectile)
        {
            facehardDetails.NoseSchema = sample.NoseSchema;
            facehardDetails.JapaneseCapHead = sample.JapaneseCapHead <= 1 ? 1 : 2;
            facehardDetails.ProjectileLimitQuality = sample.ProjectileLimitQuality;
            facehardDetails.ProjectileDamageQuality = sample.ProjectileDamageQuality;
        }
        else
        {
            facehardDetails.NoseSchema = FacehardNoseSchema.Standard;
            facehardDetails.JapaneseCapHead = 2;
        }

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        facehardDetails.WindscreenCapHeadWeight = noseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead && noseWeights.CapHead == 2
            ? sample.WindscreenCapHeadWeight
            : 0;
    }

    void ApplyMcCoyPlusPreset(McCoyPlusDragFunction function)
    {
        var selected = McCoyPlus.DragPresets().FirstOrDefault(item => item.Function == function)
            ?? McCoyPlus.DragPresets()[0];
        preset = selected.Function;
        dragText = McCoyPlus.DragPresetToText(selected.Function);
        input.McCoy.DragName = selected.Label;
        input.McCoy.DragTable = selected.Points;
        input.McCoy.RangeUnit = McCoyRangeUnit.Yards;
    }

    void SyncComboMcCoy()
    {
        input.McCoy.DragTable = input.McCoy.DragTable != null && input.McCoy.DragTable.Count >= 2
            ? input.McCoy.DragTable
            : McCoy.NormalizeDragTable(dragText);
    }

    void SyncFacehardBridge()
    {
        input.Facehard.ProjectileDiameter = facehardDetails.ProjectileDiameter;
        input.Facehard.PlateThickness = facehardDetails.PlateThickness;
        input.Facehard.Obliquity = facehardDetails.Obliquity;
        input.Facehard.StrikingVelocity = facehardDetails.StrikingVelocity;
        input.M79.ProjectileDiameter = facehardDetails.ProjectileDiameter;
        input.M79.ProjectileWeight = facehardDetails.ProjectileWeight;
    }

    static FacehardBridgeResult FacehardBridgeCalculate(FacehardBridgeInput bridge, FacehardInput template)
    {
        var facehard = new FacehardInput
        {
            ArmorId = template?.ArmorId ?? FacehardInput.CreateDefault().ArmorId,
            ProjectilePresetId = template?.ProjectilePresetId ?? FacehardInput.CreateDefault().ProjectilePresetId,
            PlateThickness = bridge.PlateThickness,
            ProjectileDiameter = bridge.ProjectileDiameter,
            ProjectileWeight = template?.ProjectileWeight ?? facehardWeightFallback,
            ProjectileBodyWeight = template?.ProjectileBodyWeight ?? facehardWeightFallback,
            StrikingVelocity = bridge.StrikingVelocity,
            Obliquity = bridge.Obliquity,
            ProjectileLimitQuality = template?.ProjectileLimitQuality ?? 1,
            ProjectileDamageQuality = template?.ProjectileDamageQuality ?? 1,
            CapType = template?.CapType ?? FacehardCapType.Hard,
            CurvedPlate = template?.CurvedPlate ?? false,
            WoodBackingThickness = template?.WoodBackingThickness ?? 0,
            CementBackingThickness = template?.CementBackingThickness ?? 0,
            MetalBackingThickness = template?.MetalBackingThickness ?? 0,
            BackingQuality = template?.BackingQuality ?? 1,
            BackingPlates = template?.BackingPlates ?? 1,
            NoseSchema = template?.NoseSchema ?? FacehardNoseSchema.Standard,
            JapaneseCapHead = template?.JapaneseCapHead ?? 2,
            NoseCondition = template?.NoseCondition ?? FacehardNoseCondition.Intact,
            WindscreenWeight = template?.WindscreenWeight ?? 0,
            WindscreenCapHeadWeight = template?.WindscreenCapHeadWeight ?? 0,
        };
        return new FacehardBridgeResult { NavyBl = FacehardCalculator.CalculateFacehardNavyBl(facehard) };
    }

    void UpdateInputWarnings()
    {
        var warningRoot = root?.Q<VisualElement>("InputWarnings");
        if (warningRoot == null)
            return;
        warningRoot.Clear();
        var resolvedCapType = ResolvedCapType(facehardDetails);
        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        if (((noseWeights.Condition == FacehardNoseCondition.WindscreenRemoved && resolvedCapType != FacehardCapType.None) || noseWeights.Condition == FacehardNoseCondition.CapHeadRemoved) && noseWeights.RemainWeight <= 0)
            warningRoot.Add(Warnings(new[] { "The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state." }));
    }

    void BindFloat(string name, double value, Action<double> setter, double? min = null, bool enabled = true, string label = null)
    {
        var field = root.Q<FloatField>(name);
        if (field == null)
            return;
        if (field.userData is not FloatBinding binding)
        {
            binding = new FloatBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not FloatBinding current || current.Updating)
                    return;
                var next = (double)evt.newValue;
                if (current.Min.HasValue)
                    next = Math.Max(current.Min.Value, next);
                current.Setter?.Invoke(next);
                MarkOutputDirty();
            });
        }
        binding.Updating = true;
        binding.Min = min;
        binding.Setter = setter;
        if (!string.IsNullOrEmpty(label))
            field.label = label;
        field.SetEnabled(enabled);
        field.SetValueWithoutNotify((float)value);
        binding.Updating = false;
    }

    void BindDropdown(string name, List<string> choices, int index, Action<int> setter, bool enabled = true)
    {
        var field = root.Q<DropdownField>(name);
        if (field == null)
            return;
        if (choices.Count == 0)
            choices.Add("");
        index = Mathf.Clamp(index, 0, choices.Count - 1);
        if (field.userData is not DropdownBinding binding)
        {
            binding = new DropdownBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not DropdownBinding current || current.Updating)
                    return;
                current.Setter?.Invoke(Mathf.Max(0, current.Choices.IndexOf(evt.newValue)));
            });
        }
        binding.Updating = true;
        binding.Choices = choices;
        binding.Setter = setter;
        field.choices = choices;
        field.SetEnabled(enabled);
        field.SetValueWithoutNotify(choices[index]);
        binding.Updating = false;
    }

    void BindText(string name, string value, Action<string> setter)
    {
        var field = root.Q<TextField>(name);
        if (field == null)
            return;
        if (field.userData is not TextBinding binding)
        {
            binding = new TextBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not TextBinding current || current.Updating)
                    return;
                current.Setter?.Invoke(evt.newValue);
            });
        }
        binding.Updating = true;
        binding.Setter = setter;
        field.SetValueWithoutNotify(value ?? "");
        binding.Updating = false;
    }

    void BindToggle(string name, bool value, Action<bool> setter)
    {
        var field = root.Q<Toggle>(name);
        if (field == null)
            return;
        if (field.userData is not ToggleBinding binding)
        {
            binding = new ToggleBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not ToggleBinding current || current.Updating)
                    return;
                current.Setter?.Invoke(evt.newValue);
                MarkOutputDirty();
            });
        }
        binding.Updating = true;
        binding.Setter = setter;
        field.SetValueWithoutNotify(value);
        binding.Updating = false;
    }

    void SetDisplay(string name, bool visible)
    {
        var element = root.Q<VisualElement>(name);
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    static FacehardCapType ResolvedCapType(FacehardInput input)
    {
        var preset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId);
        return input.ProjectilePresetId == "custom" || preset == null ? input.CapType : preset.CapType;
    }

    static Label Section(string title)
    {
        var label = new Label(title);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 8;
        label.style.marginBottom = 4;
        return label;
    }

    static Label CalculationTime(TimeSpan elapsed)
    {
        return new Label($"Calculation time: {elapsed.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture)} ms")
        {
            style =
            {
                marginTop = 6,
                marginBottom = 4,
                fontSize = 11,
                color = new StyleColor(new Color(0.55f, 0.55f, 0.55f))
            }
        };
    }

    static VisualElement Warnings(IEnumerable<string> warnings)
    {
        var list = warnings?.Where(warning => !string.IsNullOrWhiteSpace(warning)).ToList() ?? new();
        if (list.Count == 0)
            return new VisualElement();
        var root = new VisualElement { style = { marginBottom = 8 } };
        foreach (var warning in list)
            root.Add(new Label(warning));
        return root;
    }

    static VisualElement Chart(string title, IEnumerable<Vector2> points)
    {
        var root = new VisualElement { style = { minHeight = 190, marginBottom = 8 } };
        root.Add(Section(title));
        var chart = new McCoyOkunMiniChart();
        chart.SetPoints(points);
        root.Add(chart);
        return root;
    }

    static VisualElement ChartSeries(string title, IEnumerable<McCoyOkunCalculatorDialog.MiniChartSeries> series, string xUnit, string yUnit)
    {
        var resolvedSeries = series?.Where(item => item != null && item.Points.Count > 0).ToList() ?? new();
        var root = new VisualElement { style = { minHeight = 230, marginBottom = 8 } };
        root.Add(Section(title));
        if (resolvedSeries.Count > 1)
            root.Add(ChartLegend(resolvedSeries));
        var chart = new McCoyOkunMiniChart();
        chart.SetAxisLabels(xUnit, yUnit);
        chart.SetSeries(resolvedSeries);
        root.Add(chart);
        return root;
    }

    static VisualElement ChartLegend(List<McCoyOkunCalculatorDialog.MiniChartSeries> series)
    {
        var legend = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexWrap = Wrap.Wrap,
                marginTop = 2,
                marginBottom = 2,
                marginLeft = 42
            }
        };
        for (var i = 0; i < series.Count; i++)
        {
            var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 10 } };
            item.Add(new VisualElement { style = { width = 10, height = 10, marginRight = 4, backgroundColor = McCoyOkunMiniChart.SeriesColor(i) } });
            item.Add(new Label(series[i].Label) { style = { fontSize = 10 } });
            legend.Add(item);
        }
        return legend;
    }

    static IEnumerable<McCoyOkunCalculatorDialog.MiniChartSeries> TrajectorySeries(IEnumerable<McCoyPlusFacehardM79Row> rows, McCoyRangeUnit rangeUnit)
    {
        var unitLabel = BallisticOptions.ToLegacyCode(rangeUnit);
        foreach (var row in rows ?? Enumerable.Empty<McCoyPlusFacehardM79Row>())
        {
            yield return new McCoyOkunCalculatorDialog.MiniChartSeries
            {
                Label = $"{F(row.Range, 0)} {unitLabel}",
                Points = row.Trajectory.Select(point => new Vector2((float)point.Range, (float)(point.HeightInches / 12d))).ToList()
            };
        }
    }

    static IEnumerable<McCoyOkunCalculatorDialog.MiniChartSeries> PenetrationSeries(IEnumerable<McCoyPlusFacehardM79Row> rows)
    {
        var rowList = rows?.ToList() ?? new List<McCoyPlusFacehardM79Row>();
        yield return new McCoyOkunCalculatorDialog.MiniChartSeries
        {
            Label = "V Pen",
            Points = rowList
                .Where(row => row.PenetrationInches.HasValue)
                .Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))
                .ToList()
        };
        yield return new McCoyOkunCalculatorDialog.MiniChartSeries
        {
            Label = "H Pen",
            Points = rowList
                .Where(row => row.HorizontalPenetrationInches.HasValue)
                .Select(row => new Vector2((float)row.Range, (float)row.HorizontalPenetrationInches.Value))
                .ToList()
        };
    }

    static TableColumnSpec<T> Col<T>(string name, string title, int width, Func<T, string> selector)
    {
        return new TableColumnSpec<T> { Name = name, Title = title, Width = width, Selector = selector };
    }

    static VisualElement Table<T>(string title, List<T> rows, params TableColumnSpec<T>[] columns)
    {
        var root = new VisualElement { style = { marginTop = 6, marginBottom = 8 } };
        root.Add(Section(title));
        var listView = new MultiColumnListView
        {
            itemsSource = rows,
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            fixedItemHeight = 24,
            style = { height = Mathf.Clamp((rows?.Count ?? 0) * 26 + 42, 120, 300), flexShrink = 0 }
        };
        listView.columns.Clear();
        foreach (var column in columns)
        {
            listView.columns.Add(new Column
            {
                name = column.Name,
                title = column.Title,
                width = column.Width,
                minWidth = Math.Min(column.Width, 80),
                stretchable = false,
                makeCell = () => new Label { style = { whiteSpace = WhiteSpace.NoWrap } },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;
                    label.text = rows != null && index >= 0 && index < rows.Count ? column.Selector(rows[index]) : "";
                }
            });
        }
        root.Add(listView);
        return root;
    }

    List<PenetrationTableRecord> CurrentPenetrationTableRecords()
    {
        return batteryRecord?.penetrationTableRecords ?? new List<PenetrationTableRecord>();
    }

    float CurrentMaxRateOfFireShootPerMin()
    {
        return batteryRecord?.maxRateOfFireShootPerMin ?? 0f;
    }

    static string FormatPenetrationComparison(double? calculated, float? sk5)
    {
        return FormatFloatComparison(calculated, sk5, 2);
    }

    static string FormatFloatComparison(double? calculated, float? sk5, int digits)
    {
        var calculatedText = F(calculated, digits);
        return sk5.HasValue ? $"{calculatedText}/{F(sk5.Value, digits)}" : calculatedText;
    }

    static string F(double? value, int digits = 1)
        => value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("0." + new string('#', digits), CultureInfo.InvariantCulture)
            : "-";

    static string ProjectilePresetLabel(FacehardProjectilePreset preset)
    {
        if (preset == null)
            return "";
        return string.IsNullOrWhiteSpace(preset.BranchLabel) ? preset.Name : $"{preset.Name} / {preset.BranchLabel}";
    }

    static string NoseConditionLabel(FacehardNoseCondition condition)
    {
        return condition switch
        {
            FacehardNoseCondition.WindscreenRemoved => "Only windscreen lost",
            FacehardNoseCondition.CapHeadRemoved => "Windscreen and cap-head lost",
            FacehardNoseCondition.AllRemoved => "All nose coverings lost",
            _ => "Intact"
        };
    }

    static string RemainingNoseWeightLabel(double capHead, FacehardCapType resolvedCapType)
    {
        if (resolvedCapType == FacehardCapType.Hood)
            return "Remaining Hood Weight";
        if (resolvedCapType == FacehardCapType.None)
            return "Remaining Nose Covering Weight";
        return capHead > 0 ? "Remaining Cap Head Weight" : "Remaining AP Cap Weight";
    }

    const double facehardWeightFallback = 1500;
}
