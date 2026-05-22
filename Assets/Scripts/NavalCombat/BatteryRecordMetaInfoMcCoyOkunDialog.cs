using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using NavalCombatCore;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using YYZ.Ballistic;
using YYZ;

public sealed class BatteryRecordMetaInfoMcCoyOkunDialog
{
    const string DefaultNewMetaInfoSampleId = "britain-uncapped-75";

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

    static readonly string[] ShatterResistanceLabelKeys =
    {
        "0 - shatter-resistant",
        "1 - shatter-prone",
        "2 - very weak / chilled cast iron"
    };

    static readonly string[] BreakUnderNblLabelKeys =
    {
        "0 - no early breakage",
        "1 - break-prone",
        "2 - severe breakage"
    };

    static readonly string[] LightCaseLabelKeys =
    {
        "0 - normal body",
        "1 - light case",
        "2 - severe large cavity"
    };

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    static string LocalizeEnum<T>(T value) => ServiceLocator.Get<ILocalizeService>().GetEnum(value);
    static List<string> LocalizedChoices(params string[] keys) => keys.Select(key => Localize(key)).ToList();
    static List<string> LocalizedChoices(IEnumerable<string> keys) => keys.Select(key => Localize(key)).ToList();

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

    public sealed class InputViewModel : INotifyBindablePropertyChanged
    {
        readonly BatteryRecordMetaInfoMcCoyOkunDialog owner;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public InputViewModel(BatteryRecordMetaInfoMcCoyOkunDialog owner)
        {
            this.owner = owner;
        }

        [CreateProperty]
        public int sampleIndex
        {
            get
            {
                var index = owner.Samples.FindIndex(sample => sample.Id == owner.sampleId);
                return index >= 0 ? index + 1 : 0;
            }
            set
            {
                value = Mathf.Clamp(value, 0, owner.Samples.Count);
                if (value == sampleIndex)
                    return;

                if (value <= 0)
                {
                    owner.sampleId = "";
                    owner.input.McCoy.ProjectileId = "Example projectile";
                }
                else
                {
                    var sample = owner.Samples[Mathf.Clamp(value - 1, 0, owner.Samples.Count - 1)];
                    owner.sampleId = sample.Id;
                    owner.ApplyBallisticSample(sample);
                }

                owner.RefreshInputBindings(true);
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int presetIndex
        {
            get => Math.Max(0, owner.DragPresets.FindIndex(item => item.Function == owner.preset));
            set
            {
                value = Mathf.Clamp(value, 0, owner.DragPresets.Count - 1);
                var selected = owner.DragPresets[value];
                if (selected.Function == owner.preset)
                    return;
                owner.ApplyMcCoyPlusPreset(selected.Function);
                owner.NotifyInputProperties();
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty] public float muzzleVelocity { get => (float)owner.input.McCoy.MuzzleVelocity; set => SetDouble(owner.input.McCoy.MuzzleVelocity, value, v => owner.input.McCoy.MuzzleVelocity = v, nameof(muzzleVelocity), 1); }
        [CreateProperty] public float ballisticCoefficient { get => (float)owner.input.McCoy.BallisticCoefficient; set => SetDouble(owner.input.McCoy.BallisticCoefficient, value, v => owner.input.McCoy.BallisticCoefficient = v, nameof(ballisticCoefficient), 0.001); }
        [CreateProperty] public float maxRange { get => (float)owner.input.McCoy.MaxRange; set => SetDouble(owner.input.McCoy.MaxRange, value, v => owner.input.McCoy.MaxRange = v, nameof(maxRange), 1); }

        [CreateProperty]
        public int armorTypeIndex
        {
            get => Math.Max(0, FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == owner.facehardDetails.ArmorId));
            set
            {
                value = Mathf.Clamp(value, 0, FacehardCalculator.FacehardArmors.Count - 1);
                var armorId = FacehardCalculator.FacehardArmors[value].Id;
                if (owner.facehardDetails.ArmorId == armorId)
                    return;
                owner.facehardDetails.ArmorId = armorId;
                Notify(nameof(armorTypeIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty] public bool curvedPlate { get => owner.facehardDetails.CurvedPlate; set => SetBool(owner.facehardDetails.CurvedPlate, value, v => owner.facehardDetails.CurvedPlate = v, nameof(curvedPlate)); }
        [CreateProperty] public float woodBacking { get => (float)owner.facehardDetails.WoodBackingThickness; set => SetDouble(owner.facehardDetails.WoodBackingThickness, value, v => owner.facehardDetails.WoodBackingThickness = v, nameof(woodBacking), 0); }
        [CreateProperty] public float cementBacking { get => (float)owner.facehardDetails.CementBackingThickness; set => SetDouble(owner.facehardDetails.CementBackingThickness, value, v => owner.facehardDetails.CementBackingThickness = v, nameof(cementBacking), 0); }
        [CreateProperty] public float metalBacking { get => (float)owner.facehardDetails.MetalBackingThickness; set => SetDouble(owner.facehardDetails.MetalBackingThickness, value, v => owner.facehardDetails.MetalBackingThickness = v, nameof(metalBacking), 0); }
        [CreateProperty] public float backingQuality { get => (float)owner.facehardDetails.BackingQuality; set => SetDouble(owner.facehardDetails.BackingQuality, value, v => owner.facehardDetails.BackingQuality = v, nameof(backingQuality), 0.001); }
        [CreateProperty] public float backingPlates { get => (float)owner.facehardDetails.BackingPlates; set => SetDouble(owner.facehardDetails.BackingPlates, value, v => owner.facehardDetails.BackingPlates = v, nameof(backingPlates), 0); }

        [CreateProperty] public float m79ArmorQuality { get => (float)owner.input.M79.PlateQuality; set => SetDouble(owner.input.M79.PlateQuality, value, v => owner.input.M79.PlateQuality = v, nameof(m79ArmorQuality), 0.001); }
        [CreateProperty] public float m79Elongation { get => (float)owner.input.M79.Elongation; set => SetDouble(owner.input.M79.Elongation, value, v => owner.input.M79.Elongation = v, nameof(m79Elongation), 10); }

        [CreateProperty]
        public int elevationSearchIndex
        {
            get => owner.input.McCoy.ElevationSearchMode == McCoyPlusElevationSearchMode.MatchedRange ? 1 : 0;
            set
            {
                var next = value == 1 ? McCoyPlusElevationSearchMode.MatchedRange : McCoyPlusElevationSearchMode.CachedBinarySearch;
                if (owner.input.McCoy.ElevationSearchMode == next)
                    return;
                owner.input.McCoy.ElevationSearchMode = next;
                Notify(nameof(elevationSearchIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int atmosphereIndex
        {
            get => owner.input.McCoy.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0;
            set
            {
                var next = value == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro;
                if (owner.input.McCoy.Atmosphere == next)
                    return;
                owner.input.McCoy.Atmosphere = next;
                Notify(nameof(atmosphereIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty] public float densityRatio { get => (float)owner.input.McCoy.DensityRatio; set => SetDouble(owner.input.McCoy.DensityRatio, value, v => owner.input.McCoy.DensityRatio = v, nameof(densityRatio), 0.001); }
        [CreateProperty] public float temperature { get => (float)owner.input.McCoy.TemperatureF; set => SetDouble(owner.input.McCoy.TemperatureF, value, v => owner.input.McCoy.TemperatureF = v, nameof(temperature)); }
        [CreateProperty] public float matchHeight { get => (float)owner.input.McCoy.MatchHeight; set => SetDouble(owner.input.McCoy.MatchHeight, value, v => owner.input.McCoy.MatchHeight = v, nameof(matchHeight)); }

        [CreateProperty]
        public int rangeModeIndex
        {
            get => owner.rangeMode == RangeMode.SearchSk5 ? 1 : 0;
            set
            {
                var next = value == 1 ? RangeMode.SearchSk5 : RangeMode.Sweep;
                if (owner.rangeMode == next)
                    return;
                owner.rangeMode = next;
                Notify(nameof(rangeModeIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int chartModeIndex
        {
            get => owner.chartMode == ChartMode.Penetration ? 1 : 0;
            set
            {
                var next = value == 1 ? ChartMode.Penetration : ChartMode.Trajectory;
                if (owner.chartMode == next)
                    return;
                owner.chartMode = next;
                Notify(nameof(chartModeIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int okunModeIndex
        {
            get => owner.okunMode switch
            {
                OkunMode.FacehardOnly => 1,
                OkunMode.M79Only => 2,
                _ => 0,
            };
            set
            {
                var next = value switch
                {
                    1 => OkunMode.FacehardOnly,
                    2 => OkunMode.M79Only,
                    _ => OkunMode.FacehardM79,
                };
                if (owner.okunMode == next)
                    return;
                owner.okunMode = next;
                Notify(nameof(okunModeIndex));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public string dragTableText
        {
            get => owner.dragText;
            set
            {
                value ??= "";
                if (string.Equals(owner.dragText, value, StringComparison.Ordinal))
                    return;
                owner.dragText = value;
                owner.input.McCoy.DragTable = McCoy.NormalizeDragTable(owner.dragText);
                Notify(nameof(dragTableText));
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int projectileNationIndex
        {
            get
            {
                var selectedPreset = owner.CurrentProjectilePreset;
                var selectedNation = selectedPreset?.Nation ?? "Custom";
                return Math.Max(0, FacehardCalculator.FacehardProjectileNations.FindIndex(item => item.Id == selectedNation));
            }
            set
            {
                value = Mathf.Clamp(value, 0, FacehardCalculator.FacehardProjectileNations.Count - 1);
                var nation = FacehardCalculator.FacehardProjectileNations[value];
                if (owner.CurrentProjectilePreset?.Nation == nation.Id)
                    return;
                owner.facehardDetails.ProjectilePresetId = nation.DefaultProjectileId;
                owner.facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                owner.RefreshInputBindings(true);
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int projectileTypeIndex
        {
            get => Math.Max(0, owner.ProjectileChoices.FindIndex(item => item.Id == owner.facehardDetails.ProjectilePresetId));
            set
            {
                var choices = owner.ProjectileChoices;
                value = Mathf.Clamp(value, 0, choices.Count - 1);
                var projectileId = choices[value].Id;
                if (owner.facehardDetails.ProjectilePresetId == projectileId)
                    return;
                owner.facehardDetails.ProjectilePresetId = projectileId;
                owner.facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                owner.RefreshInputBindings(true);
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int capOrHoodIndex
        {
            get => Math.Max(0, owner.CapValues.IndexOf(owner.ResolvedCapType));
            set
            {
                if (!isCustomProjectile)
                    return;
                value = Mathf.Clamp(value, 0, owner.CapValues.Count - 1);
                var next = owner.CapValues[value];
                if (owner.facehardDetails.CapType == next)
                    return;
                owner.facehardDetails.CapType = next;
                owner.RefreshInputBindings();
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int noseSchemaIndex
        {
            get => owner.facehardDetails.NoseSchema == FacehardNoseSchema.JapaneseCapHead ? 1 : 0;
            set
            {
                if (!isCustomProjectile)
                    return;
                var next = value == 1 ? FacehardNoseSchema.JapaneseCapHead : FacehardNoseSchema.Standard;
                if (owner.facehardDetails.NoseSchema == next)
                    return;
                owner.facehardDetails.NoseSchema = next;
                owner.facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                owner.RefreshInputBindings();
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int japaneseCapHeadIndex
        {
            get => owner.facehardDetails.JapaneseCapHead <= 1 ? 0 : 1;
            set
            {
                var next = value == 0 ? 1 : 2;
                if (owner.facehardDetails.JapaneseCapHead == next)
                    return;
                owner.facehardDetails.JapaneseCapHead = next;
                owner.facehardDetails.NoseCondition = FacehardNoseCondition.Intact;
                owner.RefreshInputBindings();
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty]
        public int noseConditionIndex
        {
            get => Math.Max(0, owner.NoseWeights.ConditionOptions.IndexOf(owner.NoseWeights.Condition));
            set
            {
                var options = owner.NoseWeights.ConditionOptions;
                value = Mathf.Clamp(value, 0, options.Count - 1);
                var next = options[value];
                if (owner.facehardDetails.NoseCondition == next)
                    return;
                owner.facehardDetails.NoseCondition = next;
                owner.RefreshInputBindings();
                owner.MarkOutputDirty();
            }
        }

        [CreateProperty] public float projectileDiameter { get => (float)owner.facehardDetails.ProjectileDiameter; set { if (SetDouble(owner.facehardDetails.ProjectileDiameter, value, v => owner.facehardDetails.ProjectileDiameter = v, nameof(projectileDiameter), 0.001)) owner.RefreshProjectileDerivedState(); } }
        [CreateProperty] public float projectileWeight { get => (float)owner.facehardDetails.ProjectileWeight; set { if (SetDouble(owner.facehardDetails.ProjectileWeight, value, v => owner.facehardDetails.ProjectileWeight = v, nameof(projectileWeight), 0.001)) owner.RefreshProjectileDerivedState(); } }
        [CreateProperty] public float projectileBodyWeight { get => (float)owner.facehardDetails.ProjectileBodyWeight; set { if (SetDouble(owner.facehardDetails.ProjectileBodyWeight, value, v => owner.facehardDetails.ProjectileBodyWeight = v, nameof(projectileBodyWeight), 0.001)) owner.RefreshProjectileDerivedState(); } }
        [CreateProperty] public float windscreenCapHeadWeight { get => (float)owner.facehardDetails.WindscreenCapHeadWeight; set { if (SetDouble(owner.facehardDetails.WindscreenCapHeadWeight, value, v => owner.facehardDetails.WindscreenCapHeadWeight = v, nameof(windscreenCapHeadWeight), 0)) owner.RefreshProjectileDerivedState(); } }
        [CreateProperty] public float windscreenWeight { get => (float)owner.facehardDetails.WindscreenWeight; set { if (SetDouble(owner.facehardDetails.WindscreenWeight, value, v => owner.facehardDetails.WindscreenWeight = v, nameof(windscreenWeight), 0)) owner.RefreshProjectileDerivedState(); } }
        [CreateProperty] public float remainingNoseWeight => (float)owner.NoseWeights.RemainWeight;
        [CreateProperty] public string remainingNoseWeightLabel => RemainingNoseWeightLabel(owner.NoseWeights.CapHead, owner.ResolvedCapType);
        [CreateProperty] public float plim { get => (float)(isCustomProjectile ? owner.facehardDetails.ProjectileLimitQuality : owner.ResolvedProjectilePreset?.ProjectileLimitQuality ?? owner.facehardDetails.ProjectileLimitQuality); set { if (isCustomProjectile) SetDouble(owner.facehardDetails.ProjectileLimitQuality, value, v => owner.facehardDetails.ProjectileLimitQuality = v, nameof(plim), 0.001); } }
        [CreateProperty] public float pdam { get => (float)(isCustomProjectile ? owner.facehardDetails.ProjectileDamageQuality : owner.ResolvedProjectilePreset?.ProjectileDamageQuality ?? owner.facehardDetails.ProjectileDamageQuality); set { if (isCustomProjectile) SetDouble(owner.facehardDetails.ProjectileDamageQuality, value, v => owner.facehardDetails.ProjectileDamageQuality = v, nameof(pdam), 0.001); } }
        [CreateProperty] public int shatResIndex { get => Mathf.Clamp((int)Math.Round(isCustomProjectile ? owner.facehardDetails.ShatterResistance : owner.ResolvedProjectilePreset?.ShatterResistance ?? owner.facehardDetails.ShatterResistance), 0, ShatterResistanceLabelKeys.Length - 1); set { if (isCustomProjectile) SetDouble(owner.facehardDetails.ShatterResistance, value, v => owner.facehardDetails.ShatterResistance = v, nameof(shatResIndex)); } }
        [CreateProperty] public int breakUnderNblIndex { get => Mathf.Clamp((int)Math.Round(isCustomProjectile ? owner.facehardDetails.BreakUnderNbl : owner.ResolvedProjectilePreset?.BreakUnderNbl ?? owner.facehardDetails.BreakUnderNbl), 0, BreakUnderNblLabelKeys.Length - 1); set { if (isCustomProjectile) SetDouble(owner.facehardDetails.BreakUnderNbl, value, v => owner.facehardDetails.BreakUnderNbl = v, nameof(breakUnderNblIndex)); } }
        [CreateProperty] public int lightCaseIndex { get => Mathf.Clamp((int)Math.Round(isCustomProjectile ? owner.facehardDetails.LightCase : owner.ResolvedProjectilePreset?.LightCase ?? owner.facehardDetails.LightCase), 0, LightCaseLabelKeys.Length - 1); set { if (isCustomProjectile) SetDouble(owner.facehardDetails.LightCase, value, v => owner.facehardDetails.LightCase = v, nameof(lightCaseIndex)); } }

        [CreateProperty] public bool isCustomProjectile => owner.facehardDetails.ProjectilePresetId == "custom";
        [CreateProperty] public bool projectileCustomFieldEnabled => isCustomProjectile;
        [CreateProperty] public bool readOnlyFieldEnabled => false;
        [CreateProperty] public DisplayStyle japaneseCapHeadDisplay => isCustomProjectile && owner.facehardDetails.NoseSchema == FacehardNoseSchema.JapaneseCapHead ? DisplayStyle.Flex : DisplayStyle.None;
        [CreateProperty] public DisplayStyle windscreenCapHeadWeightDisplay => owner.NoseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead && owner.NoseWeights.CapHead == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        [CreateProperty] public DisplayStyle windscreenWeightDisplay => owner.NoseWeights.SchemaKind == FacehardNoseSchema.Standard ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty] public float sk5MaxRateOfFire => owner.CurrentMaxRateOfFireShootPerMin();
        [CreateProperty] public float fallToNextFireSeconds { get => owner.fallToNextFireSeconds; set => SetFloat(owner.fallToNextFireSeconds, value, v => owner.fallToNextFireSeconds = v, nameof(fallToNextFireSeconds), 0); }
        [CreateProperty] public bool roundSyncBackValues { get => owner.roundSyncBackValuesToOneDecimal; set => SetBool(owner.roundSyncBackValuesToOneDecimal, value, v => owner.roundSyncBackValuesToOneDecimal = v, nameof(roundSyncBackValues)); }

        bool SetFloat(float current, float value, Action<float> setter, string propertyName, float? min = null)
        {
            var next = min.HasValue ? Math.Max(min.Value, value) : value;
            if (Mathf.Abs(current - next) <= 0.000001f)
                return false;
            setter(next);
            Notify(propertyName);
            owner.MarkOutputDirty();
            return true;
        }

        bool SetDouble(double current, double value, Action<double> setter, string propertyName, double? min = null)
        {
            var next = min.HasValue ? Math.Max(min.Value, value) : value;
            if (Math.Abs(current - next) <= 0.000001)
                return false;
            setter(next);
            Notify(propertyName);
            owner.MarkOutputDirty();
            return true;
        }

        bool SetBool(bool current, bool value, Action<bool> setter, string propertyName)
        {
            if (current == value)
                return false;
            setter(value);
            Notify(propertyName);
            owner.MarkOutputDirty();
            return true;
        }

        public void NotifyAll()
        {
            foreach (var propertyName in AllInputPropertyNames)
                Notify(propertyName);
        }

        public void NotifyProjectileState()
        {
            foreach (var propertyName in ProjectileStatePropertyNames)
                Notify(propertyName);
        }

        void Notify(string propertyName)
        {
            var bindingId = new BindingId(propertyName);
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(in bindingId));
        }

        static readonly string[] ProjectileStatePropertyNames =
        {
            nameof(projectileNationIndex), nameof(projectileTypeIndex), nameof(capOrHoodIndex), nameof(noseSchemaIndex),
            nameof(japaneseCapHeadIndex), nameof(noseConditionIndex), nameof(projectileDiameter), nameof(projectileWeight),
            nameof(projectileBodyWeight), nameof(windscreenCapHeadWeight), nameof(windscreenWeight), nameof(remainingNoseWeight),
            nameof(remainingNoseWeightLabel), nameof(plim), nameof(pdam), nameof(shatResIndex), nameof(breakUnderNblIndex),
            nameof(lightCaseIndex), nameof(isCustomProjectile), nameof(projectileCustomFieldEnabled), nameof(readOnlyFieldEnabled), nameof(japaneseCapHeadDisplay),
            nameof(windscreenCapHeadWeightDisplay), nameof(windscreenWeightDisplay)
        };

        static readonly string[] AllInputPropertyNames =
        {
            nameof(sampleIndex), nameof(presetIndex), nameof(muzzleVelocity), nameof(ballisticCoefficient), nameof(maxRange),
            nameof(armorTypeIndex), nameof(curvedPlate), nameof(woodBacking), nameof(cementBacking), nameof(metalBacking),
            nameof(backingQuality), nameof(backingPlates), nameof(m79ArmorQuality), nameof(m79Elongation),
            nameof(elevationSearchIndex), nameof(atmosphereIndex), nameof(densityRatio), nameof(temperature), nameof(matchHeight),
            nameof(rangeModeIndex), nameof(chartModeIndex), nameof(okunModeIndex), nameof(dragTableText),
            nameof(sk5MaxRateOfFire), nameof(fallToNextFireSeconds), nameof(roundSyncBackValues),
            nameof(projectileNationIndex), nameof(projectileTypeIndex), nameof(capOrHoodIndex), nameof(noseSchemaIndex),
            nameof(japaneseCapHeadIndex), nameof(noseConditionIndex), nameof(projectileDiameter), nameof(projectileWeight),
            nameof(projectileBodyWeight), nameof(windscreenCapHeadWeight), nameof(windscreenWeight), nameof(remainingNoseWeight),
            nameof(remainingNoseWeightLabel), nameof(plim), nameof(pdam), nameof(shatResIndex), nameof(breakUnderNblIndex),
            nameof(lightCaseIndex), nameof(isCustomProjectile), nameof(projectileCustomFieldEnabled), nameof(readOnlyFieldEnabled), nameof(japaneseCapHeadDisplay),
            nameof(windscreenCapHeadWeightDisplay), nameof(windscreenWeightDisplay)
        };
    }

    readonly BatteryRecord batteryRecord;
    readonly Action callback;

    VisualElement root;
    VisualElement outputContent;
    VisualElement fitProgressRoot;
    Button calculateButton;
    Button fitExternalButton;
    Button loadFromBatteryButton;
    Button fitPauseButton;
    Button fitCancelButton;
    ProgressBar fitProgressBar;
    Label fitProgressLabel;
    MultiColumnListView sk5DataListView;
    IVisualElementScheduledItem fitSchedule;
    ExternalFitJob currentFitJob;
    InputViewModel viewModel;

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
    FacehardResult inputPreview;

    List<BallisticSample> Samples => BallisticSampleCatalog.All();
    List<McCoyPlusDragPreset> DragPresets => McCoyPlus.DragPresets();
    List<FacehardCapType> CapValues { get; } = new() { FacehardCapType.Hard, FacehardCapType.ThinHard, FacehardCapType.Soft, FacehardCapType.Hood, FacehardCapType.None };
    FacehardProjectilePreset CurrentProjectilePreset => FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == facehardDetails.ProjectilePresetId)
        ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
    FacehardProjectilePreset ResolvedProjectilePreset => inputPreview?.ProjectilePreset ?? CurrentProjectilePreset;
    List<FacehardProjectilePreset> ProjectileChoices
    {
        get
        {
            var selectedNation = CurrentProjectilePreset?.Nation ?? "Custom";
            var choices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == selectedNation).ToList();
            return choices.Count > 0
                ? choices
                : FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        }
    }
    FacehardNoseCoveringWeights NoseWeights => FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
    FacehardCapType ResolvedCapType => inputPreview?.ResolvedCapType ?? ResolvedCapTypeFor(facehardDetails);

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
            ApplyNewMetaInfoDefaults();
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
        loadFromBatteryButton = root.Q<Button>("LoadFromBatteryButton");
        if (loadFromBatteryButton != null)
            loadFromBatteryButton.clicked += LoadFromBattery;
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

        viewModel = new InputViewModel(this);
        RefreshInputChoices();
        root.dataSource = viewModel;
        RebuildInputs();
        RefreshFitControls();
        Calculate();
        return root;
    }

    void ApplyNewMetaInfoDefaults()
    {
        var sample = BallisticSampleCatalog.SampleById(DefaultNewMetaInfoSampleId);
        if (sample != null)
            ApplyBallisticSample(sample);
        LoadBatteryRecordCoreValues();
    }

    void LoadBatteryRecordCoreValues()
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
    }

    void LoadFromBattery()
    {
        if (currentFitJob != null)
            return;

        LoadBatteryRecordCoreValues();
        RebuildInputs();
        MarkOutputDirty();
        ShowPendingOutput(Localize("Loaded max range, projectile weight, and projectile diameter from BatteryRecord."));
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
        RefreshInputBindings(true);
        RefreshFitControls();
    }

    void RefreshInputBindings(bool notifyAll = false)
    {
        RefreshResolvedInputPreview();
        RefreshInputChoices();
        UpdateInputWarnings();
        if (notifyAll)
            viewModel?.NotifyAll();
        else
            viewModel?.NotifyProjectileState();
    }

    void NotifyInputProperties()
    {
        viewModel?.NotifyAll();
    }

    void RefreshProjectileDerivedState()
    {
        RefreshResolvedInputPreview();
        UpdateInputWarnings();
        viewModel?.NotifyProjectileState();
    }

    void RefreshResolvedInputPreview()
    {
        inputPreview = FacehardCalculator.CalculateFacehard(facehardDetails, false);
    }

    void RefreshInputChoices()
    {
        SetDropdownChoices("SampleField", new[] { Localize("Not Specified") }.Concat(Samples.Select(sample => sample.Label)));
        SetDropdownChoices("PresetField", DragPresets.Select(item => item.Label));
        SetDropdownChoices("ArmorTypeField", FacehardCalculator.FacehardArmors.Select(item => item.Name));
        SetDropdownChoices("ElevationSearchField", LocalizedChoices("Cached Binary Search", "Matched Range"));
        SetDropdownChoices("AtmosphereField", LocalizedChoices("Army Standard Metro", "ICAO"));
        SetDropdownChoices("RangeModeField", LocalizedChoices("Sweep", "Search SK5"));
        SetDropdownChoices("ChartModeField", LocalizedChoices("Matched Trajectories", "Penetration By Range"));
        SetDropdownChoices("OkunModeField", LocalizedChoices("Facehard + M79", "Facehard Only", "M79 Only"));
        SetDropdownChoices("ProjectileNationField", FacehardCalculator.FacehardProjectileNations.Select(item => item.Name));
        SetDropdownChoices("ProjectileTypeField", ProjectileChoices.Select(ProjectilePresetLabel));
        SetDropdownChoices("CapOrHoodField", LocalizedChoices("Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap"));
        SetDropdownChoices("NoseSchemaField", LocalizedChoices("Standard", "Japanese Cap Head"));
        SetDropdownChoices("JapaneseCapHeadField", LocalizedChoices("Uncapped Type 91 AP", "Capped Type 88/91/1 APC"));
        SetDropdownChoices("NoseConditionField", NoseWeights.ConditionOptions.Select(NoseConditionLabel));
        SetDropdownChoices("ShatResField", LocalizedChoices(ShatterResistanceLabelKeys));
        SetDropdownChoices("BreakUnderNblField", LocalizedChoices(BreakUnderNblLabelKeys));
        SetDropdownChoices("LightCaseField", LocalizedChoices(LightCaseLabelKeys));

        var batteryLabel = root?.Q<Label>("Sk5BatteryShortNameLabel");
        if (batteryLabel != null)
            batteryLabel.text = batteryRecord?.name?.GetShortName() ?? Localize("Battery");
    }

    void SetDropdownChoices(string name, IEnumerable<string> choices)
    {
        var field = root?.Q<DropdownField>(name);
        if (field == null)
            return;
        var choiceList = choices?.ToList() ?? new List<string>();
        if (choiceList.Count == 0)
            choiceList.Add("");
        field.choices = choiceList;
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
                title = Localize(title),
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

        AddLabelColumn("rangeBand", "Range Band", 120, row => LocalizeEnum(row.rangeBand));
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
            Col<ResultRow>("rangeBand", "Range Band Calc/SK5", 160, row => row.Sk5Record == null ? LocalizeEnum(row.SimulatedRangeBand) : $"{LocalizeEnum(row.SimulatedRangeBand)}/{LocalizeEnum(row.Sk5Record.rangeBand)}"),
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
            ShowPendingOutput(setupError);
            return;
        }

        currentFitJob = job;
        PrepareExternalFitPass(job);
        UpdateFitProgress(job, Localize("External ballistic fit started."));
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
            return Localize("No valid SK5 rows.");
        if (input.McCoy.BallisticCoefficient <= 0)
            return Localize("Ballistic coefficient must be greater than 0.");
        if (input.McCoy.MaxRange <= 0)
            return Localize("Maximum range must be greater than 0.");

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
            FinishFitJob(false, Localize("Cancelled by user"));
            return;
        }

        if (job.PauseRequested)
        {
            FinishFitJob(true, Localize("Paused by user"));
            return;
        }

        if (job.Pass >= 4 && job.CandidateIndex >= job.Candidates.Count)
        {
            FinishFitJob(true, Localize("Completed"));
            return;
        }

        if (job.CandidateIndex >= job.Candidates.Count)
        {
            job.Pass++;
            job.BcSpan *= 0.42;
            if (job.Pass >= 4)
            {
                FinishFitJob(true, Localize("Completed"));
                return;
            }
            PrepareExternalFitPass(job);
        }

        var candidate = job.Candidates[job.CandidateIndex++];
        job.CurrentBallisticCoefficient = candidate.BallisticCoefficient;
        var detail = ScoreExterior(job.Source, candidate.BallisticCoefficient, job.Records);
        job.CurrentScore = detail.Score;
        job.CurrentDetail = double.IsInfinity(detail.MaxRangeErrorYards)
            ? Localize("mismatch {0}, max range target failed", detail.RangeBandMismatchCount)
            : Localize("mismatch {0}, max range error {1} yd", detail.RangeBandMismatchCount, detail.MaxRangeErrorYards.ToString("0", CultureInfo.InvariantCulture));
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
                ? Localize("{0}: best BC {1}, score {2}.", reason, job.BestBallisticCoefficient.ToString("0.####", CultureInfo.InvariantCulture), job.BestScore.ToString("0.####", CultureInfo.InvariantCulture))
                : Localize("{0}: restored BC {1}.", reason, job.OriginalBallisticCoefficient.ToString("0.####", CultureInfo.InvariantCulture));
            ShowPendingOutput(status);
            Calculate();
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
        loadFromBatteryButton?.SetEnabled(!fitting);
        if (fitPauseButton != null)
            fitPauseButton.style.display = fitting ? DisplayStyle.Flex : DisplayStyle.None;
        if (fitCancelButton != null)
            fitCancelButton.style.display = fitting ? DisplayStyle.Flex : DisplayStyle.None;
        if (fitProgressRoot != null)
            fitProgressRoot.style.display = fitting ? DisplayStyle.Flex : DisplayStyle.None;
    }

    string BuildExternalFitDiagnostic(ExternalFitJob job)
    {
        return string.Join("\n", new[]
        {
            Localize("External ballistic fit"),
            Localize("pass {0}/4, candidate {1}/{2}", job.Pass + 1, job.CandidateIndex, job.Candidates.Count),
            Localize("current BC {0}, score {1}", job.CurrentBallisticCoefficient.ToString("0.####", CultureInfo.InvariantCulture), job.CurrentScore.ToString("0.####", CultureInfo.InvariantCulture)),
            Localize("best BC {0}, score {1}", job.BestBallisticCoefficient.ToString("0.####", CultureInfo.InvariantCulture), job.BestScore.ToString("0.####", CultureInfo.InvariantCulture)),
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
            ShowFitStatus(Localize("Finish or cancel the fitting job before saving."), 0);
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
            ShowPendingOutput(Localize("Calculate successful rows before saving."));
            return false;
        }

        var newRecords = BuildCalculatedPenetrationRecords();
        batteryRecord.rangeYards = (float)input.McCoy.MaxRange;
        batteryRecord.shellSizeInch = (float)facehardDetails.ProjectileDiameter;
        batteryRecord.shellWeightPounds = (float)facehardDetails.ProjectileWeight;
        batteryRecord.UpdateDamageRatingDefault();
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
        ShowPendingOutput(Localize("Input changed. Click Calculate to refresh."));
    }

    void ShowPendingOutput(string message = null)
    {
        outputContent?.Clear();
        outputContent?.Add(new Label(message ?? Localize("Click Calculate to run McCoy Plus Facehard."))
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
            ShatterResistance = facehardDetails.ShatterResistance,
            BreakUnderNbl = facehardDetails.BreakUnderNbl,
            LightCase = facehardDetails.LightCase,
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
            facehardDetails.ShatterResistance = sample.ShatterResistance;
            facehardDetails.BreakUnderNbl = sample.BreakUnderNbl;
            facehardDetails.LightCase = sample.LightCase;
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
            ShatterResistance = template?.ShatterResistance ?? 0,
            BreakUnderNbl = template?.BreakUnderNbl ?? 0,
            LightCase = template?.LightCase ?? 0,
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
        var resolvedCapType = ResolvedCapType;
        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(facehardDetails);
        if (((noseWeights.Condition == FacehardNoseCondition.WindscreenRemoved && resolvedCapType != FacehardCapType.None) || noseWeights.Condition == FacehardNoseCondition.CapHeadRemoved) && noseWeights.RemainWeight <= 0)
            warningRoot.Add(Warnings(new[] { Localize("The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state.") }));
    }

    static FacehardCapType ResolvedCapTypeFor(FacehardInput input)
    {
        var preset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId);
        return input.ProjectilePresetId == "custom" || preset == null ? input.CapType : preset.CapType;
    }

    static Label Section(string title)
    {
        var label = new Label(Localize(title));
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 8;
        label.style.marginBottom = 4;
        return label;
    }

    static Label CalculationTime(TimeSpan elapsed)
    {
        return new Label(Localize("Calculation time: {0} ms", elapsed.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture)))
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
            Label = Localize("V Pen"),
            Points = rowList
                .Where(row => row.PenetrationInches.HasValue)
                .Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))
                .ToList()
        };
        yield return new McCoyOkunCalculatorDialog.MiniChartSeries
        {
            Label = Localize("H Pen"),
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
                title = Localize(column.Title),
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
            FacehardNoseCondition.WindscreenRemoved => Localize("Only windscreen lost"),
            FacehardNoseCondition.CapHeadRemoved => Localize("Windscreen and cap-head lost"),
            FacehardNoseCondition.AllRemoved => Localize("All nose coverings lost"),
            _ => Localize("Intact")
        };
    }

    static string RemainingNoseWeightLabel(double capHead, FacehardCapType resolvedCapType)
    {
        if (resolvedCapType == FacehardCapType.Hood)
            return Localize("Remaining Hood Weight");
        if (resolvedCapType == FacehardCapType.None)
            return Localize("Remaining Nose Covering Weight");
        return capHead > 0 ? Localize("Remaining Cap Head Weight") : Localize("Remaining AP Cap Weight");
    }

    const double facehardWeightFallback = 1500;
}
