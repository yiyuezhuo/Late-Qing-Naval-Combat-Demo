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

    sealed class TableColumnSpec<T>
    {
        public string Name;
        public string Title;
        public int Width;
        public Func<T, string> Selector;
    }

    readonly BatteryRecord batteryRecord;
    readonly Action callback;

    VisualElement root;
    VisualElement inputRoot;
    VisualElement outputContent;
    Button createDeleteButton;
    Button syncBackButton;

    McCoyPlusFacehardInput input = McCoyPlusFacehard.DefaultInput();
    FacehardInput facehardDetails = FacehardCalculator.DefaultFacehardInput();
    string sampleId = "";
    McCoyPlusDragFunction preset = McCoyPlusDragFunction.G1;
    string dragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    ChartMode chartMode = ChartMode.Trajectory;
    float fallToNextFireSeconds = 15f;
    bool hasCalculated;

    public BatteryRecordMetaInfoMcCoyOkunDialog(BatteryRecord batteryRecord, Action callback)
    {
        this.batteryRecord = batteryRecord;
        this.callback = callback;
        fallToNextFireSeconds = batteryRecord?.metaInfoMcCoyOkun?.fallToNextFireSeconds ?? 15f;
        var storedSample = batteryRecord?.metaInfoMcCoyOkun?.ballisticSample;
        if (storedSample != null)
            ApplyBallisticSample(storedSample);
    }

    public VisualElement BuildContent()
    {
        root = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                flexShrink = 1
            }
        };

        var left = new VisualElement
        {
            style =
            {
                width = 390,
                flexShrink = 0,
                marginRight = 8
            }
        };

        var calculateRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 6
            }
        };
        calculateRow.Add(Button("Calculate", Calculate, true));
        left.Add(calculateRow);

        var metaRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 6
            }
        };
        createDeleteButton = Button("", ToggleMetaInfo, true);
        syncBackButton = Button("Sync Back", SyncBack, true);
        metaRow.Add(createDeleteButton);
        metaRow.Add(syncBackButton);
        left.Add(metaRow);

        inputRoot = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
        left.Add(inputRoot);

        outputContent = new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };

        root.Add(left);
        root.Add(outputContent);
        RebuildInputs();
        ShowPendingOutput();
        return root;
    }

    void RebuildInputs()
    {
        inputRoot.Clear();

        var tabView = new TabView { style = { flexGrow = 1, flexShrink = 1 } };
        tabView.Add(Tab("Projectile", BuildProjectileTab()));
        tabView.Add(Tab("Armor", BuildArmorTab()));
        tabView.Add(Tab("Misc", BuildMiscTab()));
        inputRoot.Add(tabView);
        RefreshMetaButtons();
    }

    VisualElement BuildProjectileTab()
    {
        var scroll = InputScroll();
        var samples = BallisticSampleCatalog.All();
        var sampleChoices = new List<string> { "Not Specified" };
        sampleChoices.AddRange(samples.Select(sample => sample.Label));
        var sampleIndex = samples.FindIndex(sample => sample.Id == sampleId);
        scroll.Add(Dropdown("Sample", sampleChoices, sampleIndex >= 0 ? sampleIndex + 1 : 0, selected =>
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
        }));

        scroll.Add(Dropdown("Mach-CD Preset", McCoyPlus.DragPresets().Select(item => item.Label).ToList(),
            Math.Max(0, McCoyPlus.DragPresets().FindIndex(item => item.Function == preset)), selected =>
            {
                var selectedPreset = McCoyPlus.DragPresets()[Mathf.Max(0, selected)];
                ApplyMcCoyPlusPreset(selectedPreset.Function);
                MarkOutputDirty();
            }));
        scroll.Add(FloatField("Muzzle Velocity", input.McCoy.MuzzleVelocity, value => input.McCoy.MuzzleVelocity = value, 1));
        scroll.Add(FloatField("Ballistic Coefficient", input.McCoy.BallisticCoefficient, value => input.McCoy.BallisticCoefficient = value, 0.001));
        scroll.Add(FloatField("Maximum Range", input.McCoy.MaxRange, value => input.McCoy.MaxRange = value, 1));

        scroll.Add(Section("Projectile Inputs"));
        var preview = FacehardCalculator.CalculateFacehard(facehardDetails, false);
        BuildFacehardProjectileFields(scroll, preview);
        return scroll;
    }

    VisualElement BuildArmorTab()
    {
        var scroll = InputScroll();
        scroll.Add(Dropdown("Armor Type", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == facehardDetails.ArmorId), selected =>
            {
                facehardDetails.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, selected)].Id;
                MarkOutputDirty();
            }));
        scroll.Add(ToggleField("Strongly Curved Plate", facehardDetails.CurvedPlate, value => facehardDetails.CurvedPlate = value));
        scroll.Add(FloatField("Wood Backing", facehardDetails.WoodBackingThickness, value => facehardDetails.WoodBackingThickness = value, 0));
        scroll.Add(FloatField("Cement Backing", facehardDetails.CementBackingThickness, value => facehardDetails.CementBackingThickness = value, 0));
        scroll.Add(FloatField("Metal Backing", facehardDetails.MetalBackingThickness, value => facehardDetails.MetalBackingThickness = value, 0));
        scroll.Add(FloatField("Backing Quality", facehardDetails.BackingQuality, value => facehardDetails.BackingQuality = value, 0.001));
        scroll.Add(FloatField("Backing Plates", facehardDetails.BackingPlates, value => facehardDetails.BackingPlates = value, 0));
        scroll.Add(InputWarnings());
        return scroll;
    }

    VisualElement BuildMiscTab()
    {
        var scroll = InputScroll();
        scroll.Add(Dropdown("Elevation Search", new List<string> { "Cached Binary Search", "Matched Range" },
            input.McCoy.ElevationSearchMode == McCoyPlusElevationSearchMode.MatchedRange ? 1 : 0, selected =>
            {
                input.McCoy.ElevationSearchMode = selected == 1
                    ? McCoyPlusElevationSearchMode.MatchedRange
                    : McCoyPlusElevationSearchMode.CachedBinarySearch;
                MarkOutputDirty();
            }));
        scroll.Add(Dropdown("Atmosphere Model", new List<string> { "Army Standard Metro", "ICAO" },
            input.McCoy.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, selected =>
            {
                input.McCoy.Atmosphere = selected == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro;
                MarkOutputDirty();
            }));
        scroll.Add(FloatField("Density Ratio", input.McCoy.DensityRatio, value => input.McCoy.DensityRatio = value, 0.001));
        scroll.Add(FloatField("Temperature", input.McCoy.TemperatureF, value => input.McCoy.TemperatureF = value));
        scroll.Add(FloatField("Match Height", input.McCoy.MatchHeight, value => input.McCoy.MatchHeight = value));
        scroll.Add(FloatField("Fall To Next Fire (s)", fallToNextFireSeconds, value => fallToNextFireSeconds = (float)value, 0));
        scroll.Add(Dropdown("Chart Mode", new List<string> { "Matched Trajectories", "Penetration By Range" },
            chartMode == ChartMode.Trajectory ? 0 : 1, selected =>
            {
                chartMode = selected == 0 ? ChartMode.Trajectory : ChartMode.Penetration;
                MarkOutputDirty();
            }));

        var dragTable = new TextField("Mach-CD Drag Table")
        {
            multiline = true,
            style =
            {
                height = 120
            }
        };
        dragTable.SetValueWithoutNotify(dragText);
        dragTable.RegisterValueChangedCallback(evt =>
        {
            dragText = evt.newValue;
            input.McCoy.DragTable = McCoy.NormalizeDragTable(dragText);
            MarkOutputDirty();
        });
        scroll.Add(dragTable);
        return scroll;
    }

    void BuildFacehardProjectileFields(VisualElement parent, FacehardResult preview)
    {
        var selectedPreset = preview?.ProjectilePreset
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == facehardDetails.ProjectilePresetId)
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
        var selectedNation = selectedPreset?.Nation ?? "Custom";
        var nations = FacehardCalculator.FacehardProjectileNations;
        var resolvedCapType = preview?.ResolvedCapType ?? ResolvedCapType(facehardDetails);

        parent.Add(Dropdown("Projectile Nation", nations.Select(item => item.Name).ToList(),
            Math.Max(0, nations.FindIndex(item => item.Id == selectedNation)), selected =>
            {
                facehardDetails.ProjectilePresetId = nations[Mathf.Max(0, selected)].DefaultProjectileId;
                RebuildInputs();
                MarkOutputDirty();
            }));

        var projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == selectedNation).ToList();
        if (projectileChoices.Count == 0)
            projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        parent.Add(Dropdown("Projectile Type", projectileChoices.Select(ProjectilePresetLabel).ToList(),
            Math.Max(0, projectileChoices.FindIndex(item => item.Id == facehardDetails.ProjectilePresetId)), selected =>
            {
                facehardDetails.ProjectilePresetId = projectileChoices[Mathf.Max(0, selected)].Id;
                RebuildInputs();
                MarkOutputDirty();
            }));

        var isCustom = facehardDetails.ProjectilePresetId == "custom";
        var capLabels = new List<string> { "Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap" };
        var capValues = new List<FacehardCapType> { FacehardCapType.Hard, FacehardCapType.ThinHard, FacehardCapType.Soft, FacehardCapType.Hood, FacehardCapType.None };
        parent.Add(Dropdown("Cap Or Hood", capLabels, Math.Max(0, capValues.IndexOf(resolvedCapType)), selected =>
        {
            facehardDetails.CapType = capValues[Mathf.Max(0, selected)];
            RebuildInputs();
            MarkOutputDirty();
        }, isCustom));

        var schemaValues = new List<FacehardNoseSchema> { FacehardNoseSchema.Standard, FacehardNoseSchema.JapaneseCapHead };
        parent.Add(Dropdown("Nose Schema", new List<string> { "Standard", "Japanese Cap Head" },
            Math.Max(0, schemaValues.IndexOf(facehardDetails.NoseSchema)), selected =>
            {
                facehardDetails.NoseSchema = schemaValues[Mathf.Max(0, selected)];
                facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                RebuildInputs();
                MarkOutputDirty();
            }, isCustom));

        if (isCustom && facehardDetails.NoseSchema == FacehardNoseSchema.JapaneseCapHead)
        {
            parent.Add(Dropdown("Japanese Cap Head Type", new List<string> { "Uncapped Type 91 AP", "Capped Type 88/91/1 APC" },
                facehardDetails.JapaneseCapHead <= 1 ? 0 : 1, selected =>
                {
                    facehardDetails.JapaneseCapHead = selected == 0 ? 1 : 2;
                    facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                    RebuildInputs();
                    MarkOutputDirty();
                }));
        }

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        parent.Add(Dropdown("Pre-Impact Nose Condition", noseWeights.ConditionOptions.Select(NoseConditionLabel).ToList(),
            Math.Max(0, noseWeights.ConditionOptions.IndexOf(noseWeights.Condition)), selected =>
            {
                facehardDetails.NoseCondition = noseWeights.ConditionOptions[Mathf.Max(0, selected)];
                RebuildInputs();
                MarkOutputDirty();
            }));
        parent.Add(FloatField("Projectile Diameter", facehardDetails.ProjectileDiameter, value => facehardDetails.ProjectileDiameter = value, 0.001));
        parent.Add(FloatField("Projectile Weight", facehardDetails.ProjectileWeight, value => facehardDetails.ProjectileWeight = value, 0.001));
        parent.Add(FloatField("Body Weight", facehardDetails.ProjectileBodyWeight, value => facehardDetails.ProjectileBodyWeight = value, 0.001));

        if (noseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead && noseWeights.CapHead == 2)
            parent.Add(FloatField("Windscreen + Cap Head Weight", facehardDetails.WindscreenCapHeadWeight, value => facehardDetails.WindscreenCapHeadWeight = value, 0));
        if (noseWeights.SchemaKind == FacehardNoseSchema.Standard)
            parent.Add(FloatField("Windscreen Weight", facehardDetails.WindscreenWeight, value => facehardDetails.WindscreenWeight = value, 0));

        parent.Add(FloatField(RemainingNoseWeightLabel(noseWeights.CapHead, resolvedCapType), noseWeights.RemainWeight, _ => { }, 0, false));
        parent.Add(FloatField("PLIM", isCustom ? facehardDetails.ProjectileLimitQuality : selectedPreset?.ProjectileLimitQuality ?? facehardDetails.ProjectileLimitQuality,
            value => facehardDetails.ProjectileLimitQuality = value, 0.001, isCustom));
        parent.Add(FloatField("PDAM", isCustom ? facehardDetails.ProjectileDamageQuality : selectedPreset?.ProjectileDamageQuality ?? facehardDetails.ProjectileDamageQuality,
            value => facehardDetails.ProjectileDamageQuality = value, 0.001, isCustom));
    }

    void Calculate()
    {
        SyncComboMcCoy();
        SyncFacehardBridge();
        McCoyPlusFacehard.FacehardCalculator = bridge => FacehardBridgeCalculate(bridge, facehardDetails);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusFacehard.Calculate(input);
        stopwatch.Stop();

        outputContent.Clear();
        outputContent.Add(Warnings(result.Warnings));
        outputContent.Add(chartMode == ChartMode.Trajectory
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, input.McCoy.RangeUnit), BallisticOptions.ToLegacyCode(input.McCoy.RangeUnit), "ft")
            : Chart("Facehard Penetration", result.Rows.Where(row => row.PenetrationInches.HasValue).Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))));
        outputContent.Add(CalculationTime(stopwatch.Elapsed));
        outputContent.Add(Table("Rows", result.Rows,
            Col<McCoyPlusFacehardRow>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusFacehardRow>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusFacehardRow>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusFacehardRow>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusFacehardRow>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusFacehardRow>("penetration", "Penetration", 110, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusFacehardRow>("horizontal", "Horizontal Penetration", 160, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a")));
        hasCalculated = true;
    }

    void ToggleMetaInfo()
    {
        if (batteryRecord == null)
            return;

        if (batteryRecord.metaInfoMcCoyOkun == null)
        {
            batteryRecord.metaInfoMcCoyOkun = new BatteryRecordMetaInfoMcCoyOkun();
            SyncBack();
        }
        else
        {
            batteryRecord.metaInfoMcCoyOkun = null;
            callback?.Invoke();
            RefreshMetaButtons();
        }
    }

    void SyncBack()
    {
        if (batteryRecord?.metaInfoMcCoyOkun == null)
            return;

        batteryRecord.metaInfoMcCoyOkun.ballisticSample = CreateBallisticSampleFromCurrentInput();
        batteryRecord.metaInfoMcCoyOkun.fallToNextFireSeconds = fallToNextFireSeconds;
        callback?.Invoke();
        RefreshMetaButtons();
    }

    void RefreshMetaButtons()
    {
        if (createDeleteButton != null)
            createDeleteButton.text = batteryRecord?.metaInfoMcCoyOkun == null ? "Create" : "Delete";
        if (syncBackButton != null)
            syncBackButton.style.display = batteryRecord?.metaInfoMcCoyOkun == null ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void MarkOutputDirty()
    {
        if (!hasCalculated || outputContent == null)
            return;
        hasCalculated = false;
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

    VisualElement InputWarnings()
    {
        var warningRoot = new VisualElement();
        var resolvedCapType = ResolvedCapType(facehardDetails);
        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        if (((noseWeights.Condition == FacehardNoseCondition.WindscreenRemoved && resolvedCapType != FacehardCapType.None) || noseWeights.Condition == FacehardNoseCondition.CapHeadRemoved) && noseWeights.RemainWeight <= 0)
            warningRoot.Add(Warnings(new[] { "The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state." }));
        return warningRoot;
    }

    static FacehardCapType ResolvedCapType(FacehardInput input)
    {
        var preset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId);
        return input.ProjectilePresetId == "custom" || preset == null ? input.CapType : preset.CapType;
    }

    static VisualElement InputScroll()
    {
        return new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
    }

    static Tab Tab(string label, VisualElement content)
    {
        var tab = new Tab { label = label };
        tab.Add(content);
        return tab;
    }

    Button Button(string text, Action clicked, bool grow = false)
    {
        var button = new Button(clicked) { text = text };
        button.style.marginRight = 4;
        if (grow)
            button.style.flexGrow = 1;
        return button;
    }

    VisualElement FloatField(string label, double value, Action<double> setter, double? min = null, bool enabled = true)
    {
        var field = new FloatField(label);
        field.SetEnabled(enabled);
        field.SetValueWithoutNotify((float)value);
        field.RegisterValueChangedCallback(evt =>
        {
            var next = (double)evt.newValue;
            if (min.HasValue)
                next = Math.Max(min.Value, next);
            setter?.Invoke(next);
            MarkOutputDirty();
        });
        return field;
    }

    VisualElement Dropdown(string label, List<string> choices, int index, Action<int> setter, bool enabled = true)
    {
        if (choices.Count == 0)
            choices.Add("");
        index = Mathf.Clamp(index, 0, choices.Count - 1);
        var field = new DropdownField(label, choices, index);
        field.SetEnabled(enabled);
        field.RegisterValueChangedCallback(evt => setter?.Invoke(Mathf.Max(0, choices.IndexOf(evt.newValue))));
        return field;
    }

    VisualElement ToggleField(string label, bool value, Action<bool> setter)
    {
        var field = new Toggle(label);
        field.SetValueWithoutNotify(value);
        field.RegisterValueChangedCallback(evt =>
        {
            setter?.Invoke(evt.newValue);
            MarkOutputDirty();
        });
        return field;
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
            var item = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginRight = 10
                }
            };
            var swatch = new VisualElement
            {
                style =
                {
                    width = 10,
                    height = 10,
                    marginRight = 4,
                    backgroundColor = McCoyOkunMiniChart.SeriesColor(i)
                }
            };
            item.Add(swatch);
            item.Add(new Label(series[i].Label) { style = { fontSize = 10 } });
            legend.Add(item);
        }
        return legend;
    }

    static IEnumerable<McCoyOkunCalculatorDialog.MiniChartSeries> TrajectorySeries(IEnumerable<McCoyPlusFacehardRow> rows, McCoyRangeUnit rangeUnit)
    {
        var unitLabel = BallisticOptions.ToLegacyCode(rangeUnit);
        foreach (var row in rows ?? Enumerable.Empty<McCoyPlusFacehardRow>())
        {
            yield return new McCoyOkunCalculatorDialog.MiniChartSeries
            {
                Label = $"{F(row.Range, 0)} {unitLabel}",
                Points = row.Trajectory.Select(point => new Vector2((float)point.Range, (float)(point.HeightInches / 12d))).ToList()
            };
        }
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
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            style =
            {
                height = Mathf.Clamp((rows?.Count ?? 0) * 26 + 42, 120, 300),
                flexShrink = 0
            }
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
                makeCell = () => new Label { style = { whiteSpace = WhiteSpace.Normal } },
                bindCell = (element, index) =>
                {
                    if (element is not Label label)
                        return;
                    label.text = rows != null && index >= 0 && index < rows.Count
                        ? column.Selector(rows[index])
                        : "";
                }
            });
        }
        root.Add(listView);
        return root;
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
