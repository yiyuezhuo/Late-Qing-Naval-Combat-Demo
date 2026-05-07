using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace YYZ.Ballistic
{
    public sealed class FacehardArmor
    {
        public string Id;
        public string Name;
        public double Q;
        public double QDam;
        public double Ub;
        public double Cartwheel;
        public bool Compound;
        public double SoftShat;
        public double ThkThn;
        public bool ThinChill;
        public string VariableUb;
        public double? HarveyBaseQ;

        public FacehardArmor Clone()
        {
            return (FacehardArmor)MemberwiseClone();
        }
    }

    public sealed class FacehardInput
    {
        public string ArmorId = "us-class-a-ww2";
        public string ProjectilePresetId = "UPR11";
        public double PlateThickness = 12;
        public double ProjectileDiameter = 14;
        public double ProjectileWeight = 1500;
        public double ProjectileBodyWeight = 1400;
        public double StrikingVelocity = 1800;
        public double Obliquity = 30;
        public double ProjectileLimitQuality = 1;
        public double ProjectileDamageQuality = 1;
        public string CapType = "hard";
        public bool CurvedPlate;
        public double WoodBackingThickness;
        public double CementBackingThickness;
        public double MetalBackingThickness;
        public double BackingQuality = 1;
        public double BackingPlates = 1;
        public string NoseSchema = "standard";
        public double JapaneseCapHead = 2;
        public string NoseCondition = "intact";
        public double WindscreenWeight;
        public double WindscreenCapHeadWeight;

        public static FacehardInput CreateDefault()
        {
            return new FacehardInput();
        }
    }

    public sealed class FacehardProjectileNation
    {
        public string Id;
        public string Name;
        public string DefaultProjectileId;
    }

    public sealed class FacehardProjectilePreset
    {
        public string Id;
        public string Name;
        public string Nation;
        public string OriginalCode;
        public string BranchLabel;
        public double ProjectileLimitQuality = 1;
        public double ProjectileDamageQuality = 1;
        public string CapType = "hard";
        public double ShatterResistance;
        public double NoShatterDamageAngle = 15;
        public double LightCase;
        public double BreakUnderNbl;
        public double CriticalAngle;
        public double? Bend;
        public double? Cardonald;
        public double? CapHead;
        public double? Ald;
        public double? Bld;
        public double? Cld;
        public double? Aed;
        public double? Bed;
        public double? Ced;
        public string Note = "";

        public FacehardProjectilePreset Clone()
        {
            return (FacehardProjectilePreset)MemberwiseClone();
        }
    }

    public sealed class FacehardNoseCoveringWeights
    {
        public double TotalWeight;
        public double BodyWeight;
        public double MaxNoseCoveringWeight;
        public string SchemaKind;
        public double CapHead;
        public string Condition;
        public List<string> ConditionOptions = new List<string>();
        public double WindscreenWeight;
        public double WindscreenCapHeadWeight;
        public double LossWeight;
        public double RemainWeight;
    }

    public sealed class FacehardPlugResult
    {
        public double NormalPlugWeight;
        public double DeltaPlugWeight;
        public double TotalPlugWeight;
        public double PlugMultiplier;
    }

    public sealed class FacehardLimitSelection
    {
        public double UnshatteredNavyBl;
        public double UnshatteredHolingBl;
        public double UndamagedNavyBl;
        public double UndamagedHolingBl;
        public double DamageNavyBl;
        public double DamageHolingBl;
        public double SelectedNavyBl;
        public double SelectedHolingBl;
        public double VdfUsed;
        public double VdfPostImpact;
        public double ShatterVdf;
        public double ShatterVdfPostImpact;
        public string Note;
    }

    public sealed class FacehardPenetrationResult
    {
        public double Type;
        public string Label;
        public string PenetrationFlag;
        public double? ProjectileRemainingVelocity;
        public double? PlugOrPiecesVelocity;
        public double? AverageRemainingVelocity;
        public double? ExitAngle;
        public double? DeflectionAngle;
        public double BodyDamage;
        public double NoseDamage;
    }

    public sealed class FacehardShatterResult
    {
        public bool Occurs;
        public string Type;
        public string Reason;
        public double Multiplier;
        public double ObliquityMultiplier;
        public double HolingBl;
        public double NavyBl;
        public double BestHolingBl;
        public double BestNavyBl;
    }

    public sealed class FacehardLegacyResult
    {
        public Facehard69LegacyInput Input;
        public Facehard69LegacyState State;
        public List<string> Report = new List<string>();
        public List<string> SecondPageReport = new List<string>();
        public List<string> ProcessReport = new List<string>();
    }

    public sealed class FacehardResult
    {
        public FacehardArmor Armor;
        public FacehardProjectilePreset ProjectilePreset;
        public double ResolvedProjectileLimitQuality;
        public double ResolvedProjectileDamageQuality;
        public string ResolvedCapType;
        public double ProjectileQualityBonus;
        public FacehardShatterResult Shatter;
        public FacehardLimitSelection Limits;
        public FacehardPenetrationResult Penetration;
        public FacehardPlugResult Plugs;
        public double EffectiveThickness;
        public double PenetrationThickness;
        public double DamageThickness;
        public double BackingEffectiveThickness;
        public double WoodBackingEffectiveThickness;
        public double CementBackingEffectiveThickness;
        public double MetalBackingEffectiveThickness;
        public double ThinBoundary;
        public double TrueThinBoundary;
        public double ScalingFactor;
        public double ObliquityMultiplier;
        public double ProjectileDensityTerm;
        public double PenConst;
        public double Vdf;
        public double NavyBl;
        public double HolingBl;
        public double EffectiveBl;
        public double RawEffectiveBl;
        public double MinimumEffectiveVelocity;
        public double ProjectileLimitModifier;
        public double ProjectileEffectiveModifier;
        public double BendVdf;
        public double BendCriticalObliquity;
        public double? CardonaldCriticalVelocity;
        public string Status;
        public List<string> Notes = new List<string>();
        public FacehardLegacyResult Legacy;
    }

    public static class FacehardCalculator
    {
        public static readonly List<FacehardArmor> FacehardArmors = new List<FacehardArmor>
        {
            Armor("gruson", "Gruson Chilled Cast Iron", 0.7, 0.7, 45, 1, false, 0, 0, false, "gruson"),
            Armor("compound", "Average Compound", 0.75, 0.6, 70, 0, true, 0, 0, false),
            Armor("harvey-ms", "Harveyized Mild Steel", 0.78, 0.67, 80, 0, false, 0, 0, true, "harvey", 0.78),
            Armor("harvey-ns", "Harveyized Nickel-Steel", 0.805, 0.692, 80, 0, false, 0, 0, true, "harvey", 0.805),
            Armor("kc-aa", "German KC a/A", 0.828, 0.828, 65, 0, false, 0, 0, false),
            Armor("kc-na-1928", "German KC n/A 1928", 0.9, 0.9, 59, 0, false, 0, 1, false),
            Armor("kc-na-final", "German KC n/A final", 0.96, 0.96, 59, 0, false, 1, 1, false),
            Armor("ah-kc", "Austro-Hungarian Witkowitzer KC", 0.947, 0.947, 65, 0, false, 1, 1, false),
            Armor("brit-ww1-kc", "British WWI average KC", 0.85, 0.85, 65, 0, false, 2, 2, false),
            Armor("brit-1922-kc", "British 1922-30 KC", 0.9, 0.9, 65, 0, false, 2, 2, false),
            Armor("brit-ca", "British post-1930 CA", 0.928, 0.928, 70, 0, false, 1, 1, false),
            Armor("terni", "Italian Terni Cemented", 0.98, 0.98, 70, 0, false, 1, 1, false, "terni"),
            Armor("japan-vh", "Japanese VH", 0.839, 0.839, 65, 0, false, 0, 0, false),
            Armor("midvale-nc", "U.S. Midvale Non-Cemented Class A", 0.881, 0.881, 18, 1, false, 1, 0, false),
            Armor("bethlehem-thin-chill", "U.S. Bethlehem Thin Chill Class A", 0.889, 0.85, 85, 0, false, 0, 0, true),
            Armor("us-class-a-1911", "U.S. average Class A 1911-25", 0.889, 0.889, 65, 0, false, 0, 0, false),
            Armor("us-class-a-ww2", "U.S. WWII Class A original", 0.928, 0.928, 45, 0, false, 1, 1, false),
            Armor("us-class-a-1944", "U.S. improved Class A 1944", 1.025, 1.025, 45, 0, false, 1, 1, false),
            Armor("avg-kc-1898", "Average KC 1898-1910", 0.828, 0.828, 65, 2, false, 0, 0, false),
            Armor("avg-kc-1911", "Average KC 1911-1921", 0.85, 0.85, 65, 0, false, 0, 0, false),
            Armor("avg-kc-1922", "Average KC 1922-1930", 0.9, 0.9, 65, 0, false, 0, 0, false),
            Armor("avg-kc-1930", "Average post-1930 KC", 1, 1, 65, 0, false, 1, 1, false),
        };

        public static readonly List<FacehardProjectileNation> FacehardProjectileNations = new List<FacehardProjectileNation>
        {
            Nation("US", "United States", "UPR11"),
            Nation("Britain", "Great Britain", "BPR9"),
            Nation("Germany", "Germany", "GPR12"),
            Nation("France", "France", "FPR8"),
            Nation("Italy", "Italy", "IPR12"),
            Nation("Japan", "Japan", "JPR9"),
            Nation("Austria-Hungary", "Austria-Hungary", "APR8"),
            Nation("Russia", "Russia", "RPR6"),
            Nation("Custom", "Custom / Manual", "custom"),
        };

        public static readonly List<FacehardProjectilePreset> FacehardProjectilePresets = CreateProjectilePresets();

        static readonly Dictionary<string, double> ArmorIdToLegacyArmor = new Dictionary<string, double>
        {
            ["gruson"] = 1, ["compound"] = 2, ["harvey-ms"] = 3, ["harvey-ns"] = 4, ["kc-aa"] = 5,
            ["kc-na-1928"] = 6, ["kc-na-final"] = 7, ["ah-kc"] = 8, ["brit-ww1-kc"] = 9,
            ["brit-1922-kc"] = 10, ["brit-ca"] = 11, ["terni"] = 12, ["japan-vh"] = 13,
            ["midvale-nc"] = 14, ["bethlehem-thin-chill"] = 15, ["us-class-a-1911"] = 16,
            ["us-class-a-ww2"] = 17, ["us-class-a-1944"] = 18, ["avg-kc-1898"] = 19,
            ["avg-kc-1911"] = 20, ["avg-kc-1922"] = 21, ["avg-kc-1930"] = 22,
        };

        static readonly Dictionary<string, double> ProjectileCodeToLegacyNation = new Dictionary<string, double>
        {
            ["UPR"] = 1, ["BPR"] = 2, ["GPR"] = 3, ["FPR"] = 4, ["IPR"] = 5, ["JPR"] = 6, ["APR"] = 7, ["RPR"] = 8,
        };

        static readonly Regex ProjectileCodeRegex = new Regex("^([A-Z]{3})(\\d+)$", RegexOptions.Compiled);

        public static FacehardInput DefaultFacehardInput()
        {
            return FacehardInput.CreateDefault();
        }

        public static FacehardNoseCoveringWeights FacehardNoseCoveringWeights(FacehardInput input)
        {
            input ??= FacehardInput.CreateDefault();
            var capHead = ResolvedCapHead(input);
            var schemaKind = capHead > 0 ? "japanese-cap-head" : "standard";
            var conditionOptions = NoseConditionOptions(capHead);
            var condition = NormalizeNoseCondition(input.NoseCondition, capHead);
            var totalWeight = Math.Max(input.ProjectileWeight, 1);
            var bodyWeight = Math.Min(Math.Max(input.ProjectileBodyWeight, 1), totalWeight);
            var maxNoseCoveringWeight = Math.Max(0, totalWeight - bodyWeight);
            var windscreenWeight = Math.Min(Math.Max(input.WindscreenWeight, 0), maxNoseCoveringWeight);
            var windscreenCapHeadWeight = Math.Min(Math.Max(input.WindscreenCapHeadWeight, 0), maxNoseCoveringWeight);
            var lossWeight = condition == "windscreen-removed"
                ? windscreenWeight
                : condition == "caphead-removed"
                    ? windscreenCapHeadWeight
                    : condition == "all-removed"
                        ? maxNoseCoveringWeight
                        : 0;
            return new FacehardNoseCoveringWeights
            {
                TotalWeight = totalWeight,
                BodyWeight = bodyWeight,
                MaxNoseCoveringWeight = maxNoseCoveringWeight,
                SchemaKind = schemaKind,
                CapHead = capHead,
                Condition = condition,
                ConditionOptions = conditionOptions,
                WindscreenWeight = windscreenWeight,
                WindscreenCapHeadWeight = windscreenCapHeadWeight,
                LossWeight = lossWeight,
                RemainWeight = Math.Max(0, maxNoseCoveringWeight - lossWeight),
            };
        }

        public static FacehardResult CalculateFacehard(FacehardInput input)
        {
            input ??= FacehardInput.CreateDefault();
            var legacyInput = ToLegacyInput(input);
            var state = Facehard69Legacy.RunSlice(legacyInput);
            var baseArmor = FacehardArmors.FirstOrDefault(armor => armor.Id == input.ArmorId) ?? FacehardArmors[16];
            var armorResult = baseArmor.Clone();
            armorResult.Q = state.Q;
            armorResult.QDam = state.QDAM;
            armorResult.Ub = state.UB;
            armorResult.Cartwheel = state.CARTWL;
            armorResult.Compound = state.CMPND == 1;
            armorResult.SoftShat = state.SOFTSHAT;
            armorResult.ThinChill = state.THNCHL == 1;
            armorResult.ThkThn = state.THKTHN;

            var shatterType = LegacyShatterType(state);
            var penetrationFlag = LegacyPenetrationFlag(state.PENFLG);
            var penetrationType = Math.Min(Math.Max(state.PENTP, 0), 6);
            var status = "no-hole";
            if (input.StrikingVelocity >= state.VLMT) status = input.StrikingVelocity >= state.MINEV ? "effective-complete" : "complete";
            else if (input.StrikingVelocity >= state.VHOL) status = "holed";

            var notes = new List<string> { "Facehard69 legacy kernel is the authoritative calculation path for this panel." };
            if (state.PROCESS_REPORT != null) notes.AddRange(state.PROCESS_REPORT);

            return new FacehardResult
            {
                Armor = armorResult,
                ProjectilePreset = SelectedLegacyPreset(input, state),
                ResolvedProjectileLimitQuality = state.PLM,
                ResolvedProjectileDamageQuality = state.PDM,
                ResolvedCapType = LegacyCapType(state.APCAP),
                ProjectileQualityBonus = state.PPLUS,
                Shatter = new FacehardShatterResult
                {
                    Occurs = state.SHAT == 1 || state.NSSHAT > 0,
                    Type = shatterType,
                    Reason = shatterType == "none"
                        ? "Legacy IMPACTSETUP did not select a shatter branch."
                        : $"Legacy IMPACTSETUP selected SHAT={state.SHAT}, NSSHAT={state.NSSHAT}.",
                    Multiplier = state.SHATMULT,
                    ObliquityMultiplier = state.MSHAT,
                    HolingBl = state.VHSHAT,
                    NavyBl = state.VLSHAT,
                    BestHolingBl = state.VHSHATMAX,
                    BestNavyBl = state.VLSHATMAX,
                },
                Limits = new FacehardLimitSelection
                {
                    UnshatteredNavyBl = state.VLTRU,
                    UnshatteredHolingBl = state.VHTRU,
                    UndamagedNavyBl = state.VLND,
                    UndamagedHolingBl = state.VHND,
                    DamageNavyBl = state.VLDAM,
                    DamageHolingBl = state.VHDAM,
                    SelectedNavyBl = state.VLMT,
                    SelectedHolingBl = state.VHOL,
                    VdfUsed = state.VDFUSED,
                    VdfPostImpact = state.VDFUSEDPR,
                    ShatterVdf = state.SHATVDF,
                    ShatterVdfPostImpact = state.SHATVDFPR,
                    Note = $"Selected by legacy markers: N={MarkerList(("N1", state.N1), ("N2", state.N2), ("N3", state.N3), ("N4", state.N4))}, H={MarkerList(("H1", state.H1), ("H2", state.H2), ("H3", state.H3), ("H4", state.H4))}.",
                },
                Penetration = new FacehardPenetrationResult
                {
                    Type = penetrationType,
                    Label = PenetrationLabel(penetrationType),
                    PenetrationFlag = penetrationFlag,
                    ProjectileRemainingVelocity = state.VR >= 0 ? state.VR : null,
                    PlugOrPiecesVelocity = state.VDPLUG >= 0 ? state.VDPLUG : state.VNPLUG >= 0 ? state.VNPLUG : null,
                    AverageRemainingVelocity = state.VTOTAL >= 0 ? state.VTOTAL : null,
                    ExitAngle = state.EX >= 0 ? state.EX : null,
                    DeflectionAngle = state.OBDF >= 0 ? state.OBDF : null,
                    BodyDamage = state.BDYDM,
                    NoseDamage = state.NSBRK,
                },
                Plugs = new FacehardPlugResult
                {
                    NormalPlugWeight = state.NORMPLUGWT,
                    DeltaPlugWeight = state.DELTAPLUGWT,
                    TotalPlugWeight = state.TOTPLUGWT,
                    PlugMultiplier = state.RNDPLUGWT,
                },
                EffectiveThickness = state.TEFF,
                PenetrationThickness = state.TP,
                DamageThickness = state.TD,
                BackingEffectiveThickness = state.BKEFF + state.WD + state.CMT,
                WoodBackingEffectiveThickness = state.WD,
                CementBackingEffectiveThickness = state.CMT,
                MetalBackingEffectiveThickness = state.BKEFF,
                ThinBoundary = state.THIN,
                TrueThinBoundary = state.TRUTHIN,
                ScalingFactor = state.SC,
                ObliquityMultiplier = state.MO,
                ProjectileDensityTerm = Math.Pow(state.WT / Math.Pow(state.D, 3), 0.2),
                PenConst = state.PENCONST,
                Vdf = state.VDFSTD,
                NavyBl = state.VLMT,
                HolingBl = state.VHOL,
                EffectiveBl = state.MINEV > 0 ? state.MINEV : state.VLMT,
                RawEffectiveBl = state.VITRU,
                MinimumEffectiveVelocity = state.MINEV,
                ProjectileLimitModifier = state.POLMOD,
                ProjectileEffectiveModifier = state.POIMOD,
                BendVdf = state.VDFBND,
                BendCriticalObliquity = state.OBCRIT,
                CardonaldCriticalVelocity = state.VSCRIT > 0 ? state.VSCRIT : null,
                Status = status,
                Notes = notes,
                Legacy = new FacehardLegacyResult
                {
                    Input = legacyInput,
                    State = state,
                    Report = state.REPORT ?? new List<string>(),
                    SecondPageReport = state.SECOND_PAGE_REPORT ?? new List<string>(),
                    ProcessReport = state.PROCESS_REPORT ?? new List<string>(),
                },
            };
        }

        static Facehard69LegacyInput ToLegacyInput(FacehardInput input)
        {
            var preset = ProjectilePresetForLegacy(input);
            var selection = LegacyProjectileSelection(preset);
            var isCustom = preset.Id == "custom";
            var noseWeights = FacehardNoseCoveringWeights(input);
            var backingPlates = Math.Max(Math.Round(input.BackingPlates), 1);
            var armor = ArmorIdToLegacyArmor.TryGetValue(input.ArmorId, out var legacyArmor) ? legacyArmor : ArmorIdToLegacyArmor["us-class-a-ww2"];

            var result = new Facehard69LegacyInput
            {
                ARMOR = armor,
                Q = 1,
                QDAM = 1,
                UB = 65,
                CARTWL = 0,
                CMPND = 0,
                SOFTSHAT = 0,
                THNCHL = 0,
                THKTHN = 0,
                TA = Math.Max(input.PlateThickness, 0.1),
                TEFF = Math.Max(input.PlateThickness, 0.1),
                D = Math.Max(input.ProjectileDiameter, 0.1),
                WT = noseWeights.TotalWeight,
                WB = noseWeights.BodyWeight,
                WTSAVE = noseWeights.TotalWeight,
                OB = Clamp(input.Obliquity, 0, 80),
                VS = Math.Max(input.StrikingVelocity, 0),
                CURV = input.CurvedPlate ? 1 : 0,
                WD = Math.Max(input.WoodBackingThickness, 0) / 100,
                CMT = Math.Max(input.CementBackingThickness, 0) / 25,
                MTLBACK = Math.Max(input.MetalBackingThickness, 0),
                NBK = input.MetalBackingThickness > 0 ? backingPlates : 0,
                QBK = Math.Max(input.BackingQuality, 0),
                CAPHD = noseWeights.CapHead,
                noseCoveringState = noseWeights.Condition,
                WWT = noseWeights.Condition == "windscreen-removed" ? noseWeights.WindscreenWeight : 0,
                WCHWT = noseWeights.Condition == "caphead-removed" ? noseWeights.WindscreenCapHeadWeight : 0,
            };

            if (isCustom)
            {
                result.PLIM = Math.Max(input.ProjectileLimitQuality, 0.05);
                result.PDAM = Math.Max(input.ProjectileDamageQuality, 0.05);
                result.APCAP = CapTypeToLegacyCode(input.CapType);
                result.SHATRES = preset.ShatterResistance;
                result.BRAAK = preset.BreakUnderNbl;
                result.BRAIK = preset.BreakUnderNbl;
                result.LTCASE = preset.LightCase;
                result.NSDAMAGL = preset.NoShatterDamageAngle;
                result.CRITAGL = preset.CriticalAngle;
                result.BEND = preset.Bend ?? 0;
                result.CARDONALD = preset.Cardonald ?? 0;
                result.ALD = preset.Ald;
                result.BLD = preset.Bld;
                result.CLD = preset.Cld;
                result.AED = preset.Aed;
                result.BED = preset.Bed;
                result.CED = preset.Ced;
            }
            else
            {
                result.NATN = selection.nation;
                result.PRJTL = selection.projectile;
            }
            return result;
        }

        static FacehardProjectilePreset ProjectilePresetForLegacy(FacehardInput input)
        {
            return FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId)
                ?? FacehardProjectilePresets.FirstOrDefault(item => item.Id == FacehardInput.CreateDefault().ProjectilePresetId)
                ?? CustomProjectilePreset();
        }

        static (double? nation, double? projectile) LegacyProjectileSelection(FacehardProjectilePreset preset)
        {
            var match = preset.OriginalCode == null ? null : ProjectileCodeRegex.Match(preset.OriginalCode);
            if (match == null || !match.Success) return (null, null);
            if (!ProjectileCodeToLegacyNation.TryGetValue(match.Groups[1].Value, out var nation)) return (null, null);
            if (!double.TryParse(match.Groups[2].Value, out var projectile)) return (null, null);
            return (nation, projectile);
        }

        static double AutomaticCapHead(FacehardProjectilePreset preset)
        {
            var selection = LegacyProjectileSelection(preset);
            if (preset.CapHead.HasValue) return preset.CapHead.Value;
            if (selection.nation == 6 && selection.projectile.HasValue && selection.projectile.Value >= 8)
                return selection.projectile.Value == 10 ? 1 : 2;
            return 0;
        }

        static double ResolvedCapHead(FacehardInput input)
        {
            var preset = ProjectilePresetForLegacy(input);
            if (preset.Id == "custom") return input.NoseSchema == "japanese-cap-head" ? Clamp(Math.Round(input.JapaneseCapHead), 1, 2) : 0;
            return AutomaticCapHead(preset);
        }

        static List<string> NoseConditionOptions(double capHead)
        {
            if (capHead == 1) return new List<string> { "intact", "all-removed" };
            if (capHead == 2) return new List<string> { "intact", "caphead-removed" };
            return new List<string> { "intact", "windscreen-removed", "all-removed" };
        }

        static string NormalizeNoseCondition(string condition, double capHead)
        {
            if (capHead == 1) return condition == "intact" ? "intact" : "all-removed";
            if (capHead == 2) return condition == "intact" ? "intact" : "caphead-removed";
            return condition == "caphead-removed" ? "intact" : condition;
        }

        static double CapTypeToLegacyCode(string capType)
        {
            if (capType == "hood") return -1;
            if (capType == "soft") return 1;
            if (capType == "hard") return 2;
            if (capType == "thin-hard") return 3;
            return 0;
        }

        static string LegacyCapType(double code)
        {
            if (code == -1) return "hood";
            if (code == 1) return "soft";
            if (code == 2) return "hard";
            if (code == 3) return "thin-hard";
            return "none";
        }

        static string PenetrationLabel(double type)
        {
            if (type == 1) return "Effective complete penetration";
            if (type == 2) return "Complete penetration";
            if (type == 3) return "Plate holed, damaged projectile rejected";
            if (type == 4) return "Partial penetration by lower body pieces";
            if (type == 5) return "Partial penetration by plug or fragments";
            if (type == 6) return "Projectile shattered against plate";
            return "No caliber-size hole";
        }

        static string LegacyPenetrationFlag(double flag)
        {
            if (flag == 0) return "none";
            if (flag == 1) return "holing";
            return "complete";
        }

        static string LegacyShatterType(Facehard69LegacyState state)
        {
            if (state.NSSHAT == 1 || state.NSSHAT == 4) return "nose-only";
            return state.SHAT == 1 ? "complete" : "none";
        }

        static FacehardProjectilePreset SelectedLegacyPreset(FacehardInput input, Facehard69LegacyState state)
        {
            var preset = ProjectilePresetForLegacy(input).Clone();
            preset.ProjectileLimitQuality = state.PLIM;
            preset.ProjectileDamageQuality = state.PDAM;
            preset.CapType = LegacyCapType(state.APCAP);
            preset.ShatterResistance = state.SHATRES;
            preset.NoShatterDamageAngle = state.NSDAMAGL;
            preset.LightCase = state.LTCASE;
            preset.BreakUnderNbl = state.BRAAK;
            preset.CriticalAngle = state.CRITAGL;
            preset.Bend = state.BEND;
            preset.Cardonald = state.CARDONALD;
            preset.CapHead = state.CAPHD;
            return preset;
        }

        static string MarkerList(params (string name, string marker)[] markers)
        {
            var selected = markers.Where(item => !string.IsNullOrEmpty(item.marker)).Select(item => item.name).ToList();
            return selected.Count == 0 ? "unmarked" : string.Join("/", selected);
        }

        static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        static FacehardArmor Armor(string id, string name, double q, double qDam, double ub, double cartwheel, bool compound, double softShat, double thkThn, bool thinChill, string variableUb = null, double? harveyBaseQ = null)
        {
            return new FacehardArmor
            {
                Id = id,
                Name = name,
                Q = q,
                QDam = qDam,
                Ub = ub,
                Cartwheel = cartwheel,
                Compound = compound,
                SoftShat = softShat,
                ThkThn = thkThn,
                ThinChill = thinChill,
                VariableUb = variableUb,
                HarveyBaseQ = harveyBaseQ,
            };
        }

        static FacehardProjectileNation Nation(string id, string name, string defaultProjectileId)
        {
            return new FacehardProjectileNation { Id = id, Name = name, DefaultProjectileId = defaultProjectileId };
        }

        static FacehardProjectilePreset LegacyProjectilePreset(string code, string nation, string name, string branch)
        {
            return new FacehardProjectilePreset
            {
                Id = code,
                Name = name,
                Nation = nation,
                OriginalCode = code,
                BranchLabel = branch,
                Note = $"{branch}; resolved by Facehard ALLPROJDATA.",
            };
        }

        static FacehardProjectilePreset CustomProjectilePreset()
        {
            return new FacehardProjectilePreset
            {
                Id = "custom",
                Name = "Custom / manual PLIM-PDAM",
                Nation = "Custom",
                ProjectileLimitQuality = 1,
                ProjectileDamageQuality = 1,
                CapType = "hard",
                ShatterResistance = 0,
                NoShatterDamageAngle = 15,
                LightCase = 0,
                BreakUnderNbl = 0,
                CriticalAngle = 0,
                Note = "Use the PLIM, PDAM, and cap fields exactly as entered.",
            };
        }

        static List<FacehardProjectilePreset> CreateProjectilePresets()
        {
            var entries = new (string code, string nation, string name, string branch)[]
            {
                ("UPR1", "US", "Ave. Army/Navy chilled cast iron shot and steel common", "UPR1 -> DEFAULT1"),
                ("UPR2", "US", "Ave. capped chilled cast iron Army Coast Defense shot/shell", "UPR2 -> DEFAULT2"),
                ("UPR3", "US", "A/N steel AP shot/shell, 1890-1910", "UPR3 -> DEFAULT3"),
                ("UPR4", "US", "A/N soft-capped steel APC shot/shell, 1897-1910", "UPR4 -> DEFAULT4"),
                ("UPR5", "US", "Midvale tough-body steel AP shot/shell, 1895-1910", "UPR5"),
                ("UPR6", "US", "Midvale tough soft-capped APC shot/shell, 1897-1910", "UPR6"),
                ("UPR7", "US", "Base-fuzed railroad gun large-filler HE shell", "UPR7"),
                ("UPR8", "US", "Ave. Navy 1911-23 APC except Midvale variants", "UPR8"),
                ("UPR9", "US", "Navy Midvale Unbreakable 1911/1916 APC", "UPR9"),
                ("UPR10", "US", "Ave. 1921-1935 ACD APC shot", "UPR10"),
                ("UPR11", "US", "Ave. post-1935 ACD APC shot", "UPR11"),
                ("UPR12", "US", "WWI-era base-fuzed common without hood", "UPR12"),
                ("UPR13", "US", "Special Common with hood and windscreen, except 6-inch Mk 27 / 8-inch Mk 15", "UPR13"),
                ("UPR14", "US", "6-inch Mk 27 Special Common with hood", "UPR14"),
                ("UPR15", "US", "8-inch Mk 15 hard-capped Special Common", "UPR15"),
                ("UPR16", "US", "3-inch Mk 29-1/30-1, 8-inch Mk 19-1-3, and ACD Mk 20-1 APC", "UPR16"),
                ("UPR17", "US", "8-inch Mk 19-4-6 APC", "UPR17"),
                ("UPR18", "US", "6-inch Mk 35-1-8 and 16-inch Mk 8-1-5 APC", "UPR18"),
                ("UPR19", "US", "8-inch Mk 21-1-4, 14-inch Mk 16-1-6, and heavy APC to 1944", "UPR19"),
                ("UPR20", "US", "Late improved U.S. APC models", "UPR20"),
                ("BPR1", "Britain", "Palliser chilled cast iron shot and common shells", "BPR1 -> DEFAULT1"),
                ("BPR2", "Britain", "Uncapped steel AP shot/shell, 1890-1905", "BPR2 -> DEFAULT3"),
                ("BPR3", "Britain", "CP light-case", "BPR3"),
                ("BPR4", "Britain", "CPC light-case", "BPR4"),
                ("BPR5", "Britain", "Original 6-inch to 12-inch APC, 1905-1912", "BPR5 -> DEFAULT4"),
                ("BPR6", "Britain", "Ave. 6-13.5-inch cast-steel APC, 1913-1918", "BPR6"),
                ("BPR7", "Britain", "13.5-inch heavy, 14-inch, and 15-inch forged-steel APC", "BPR7"),
                ("BPR8", "Britain", "12-inch Mk 7A APC", "BPR8"),
                ("BPR9", "Britain", "13.5-inch heavy, 14-inch, and 15-inch Mk 5A APC", "BPR9"),
                ("BPR10", "Britain", "15-inch Mk 5A Blue Band APC", "BPR10"),
                ("BPR11", "Britain", "Post-WWI CPBC/SAP with hood", "BPR11"),
                ("BPR12", "Britain", "8-inch SAPC", "BPR12"),
                ("BPR13", "Britain", "9.2-inch Green Boy coast defense APC", "BPR13"),
                ("BPR14", "Britain", "9.2-inch coast-defense APC, 1935-1950", "BPR14"),
                ("BPR15", "Britain", "16-inch Mk 1B APC", "BPR15"),
                ("BPR16", "Britain", "14-16-inch post-1930 APC", "BPR16"),
                ("BPR17", "Britain", "15-inch Cardonald APC", "BPR17"),
                ("GPR1", "Germany", "Gruson chilled cast iron AP shot/shell and common shells", "GPR1 -> DEFAULT1"),
                ("GPR2", "Germany", "Krupp steel AP shot/shell up to 1918", "GPR2"),
                ("GPR3", "Germany", "Uncapped common up to 1929", "GPR3"),
                ("GPR4", "Germany", "Ave. pre-1911 steel APC shot/shell", "GPR4"),
                ("GPR5", "Germany", "Krupp tough-capped L/3.2 and L/3.4 APC", "GPR5"),
                ("GPR6", "Germany", "Common with hood after 1929", "GPR6 -> DEFAULT5"),
                ("GPR7", "Germany", "38cm and projected 40.6cm capped common", "GPR7 -> DEFAULT6"),
                ("GPR8", "Germany", "15cm Psgr.m.K. L/3.7 APC", "GPR8"),
                ("GPR9", "Germany", "28.3cm Psgr.m.K. L/3.7 APC", "GPR9"),
                ("GPR10", "Germany", "20.3cm and 30.5cm Psgr.m.K. L/4.4 APC", "GPR10"),
                ("GPR11", "Germany", "28.3cm Psgr.m.K. L/4.4 APC", "GPR11"),
                ("GPR12", "Germany", "38cm Psgr.m.K. L/4.4 APC", "GPR12"),
                ("GPR13", "Germany", "40.6cm Psgr.m.K. L/4.4 APC", "GPR13"),
                ("GPR14", "Germany", "Lightweight 38cm and 40.6cm coast-defense APC", "GPR14"),
                ("GPR15", "Germany", "Projected 53cm Gerat 36 APC", "GPR15"),
                ("FPR1", "France", "Palliser/Gruson chilled cast iron shot and common shell", "FPR1 -> DEFAULT1"),
                ("FPR2", "France", "Soft-capped chilled cast iron APC shot/shell", "FPR2 -> DEFAULT2"),
                ("FPR3", "France", "Steel AP shot/shell, 1890-1922", "FPR3 -> DEFAULT3"),
                ("FPR4", "France", "Soft-capped SAPC, c.1900-c.1909", "FPR4 -> DEFAULT4"),
                ("FPR5", "France", "Hard-capped SAPC, c.1909-1945", "FPR5 -> DEFAULT7"),
                ("FPR6", "France", "Average SAP, 1923-1960", "FPR6 -> DEFAULT5"),
                ("FPR7", "France", "33cm APC / SAPC O.Pf(RC) KMle 34", "FPR7"),
                ("FPR8", "France", "38cm APC original French 1940 O.Pf(RC) KMle 40", "FPR8"),
                ("FPR9", "France", "38cm APC U.S. Crucible Steel AP Mk 1, 1943", "FPR9 (author-intent correction)"),
                ("IPR1", "Italy", "Ave. Palliser/Gruson chilled cast iron shot/shell and common shells", "IPR1 -> DEFAULT1"),
                ("IPR2", "Italy", "Ave. steel AP shot/shell, 1890-1923", "IPR2 -> DEFAULT3"),
                ("IPR3", "Italy", "Ave. soft-capped steel APC shot/shell, 1905-1930", "IPR3 -> DEFAULT4"),
                ("IPR4", "Italy", "Ave. British-type uncapped CP light-case, 1900-1923", "IPR4 -> BPR3"),
                ("IPR5", "Italy", "Ave. British-type CPC light-case, 1912-1923", "IPR5 -> BPR4"),
                ("IPR6", "Italy", "Ave. British-type improved 6-12-inch cast-steel APC", "IPR6 -> BPR6"),
                ("IPR7", "Italy", "Ave. British-type improved 15-inch forged-steel APC", "IPR7 -> BPR7"),
                ("IPR8", "Italy", "Ave. British 12-inch Mk 7A-type post-Jutland APC", "IPR8 -> BPR8"),
                ("IPR9", "Italy", "Ave. British 15-inch Mk 5A-type post-Jutland APC", "IPR9 -> BPR9"),
                ("IPR10", "Italy", "Post-1930 uncapped common / SAP", "IPR10 -> DEFAULT5"),
                ("IPR11", "Italy", "Post-1930 hard-capped common / SAPC", "IPR11 -> DEFAULT6"),
                ("IPR12", "Italy", "Post-1930 15-38cm APC", "IPR12"),
                ("JPR1", "Japan", "Palliser chilled cast iron AP shot/shell and common shells", "JPR1 -> DEFAULT1"),
                ("JPR2", "Japan", "AP shot/shell with 0-6% filler", "JPR2 -> DEFAULT3"),
                ("JPR3", "Japan", "Soft-capped APC shot/shell with 0-6% filler", "JPR3 -> DEFAULT4"),
                ("JPR4", "Japan", "British CP with 9-10% filler", "JPR4"),
                ("JPR5", "Japan", "British CPC with 9-10% filler", "JPR5"),
                ("JPR6", "Japan", "14-inch British pre-Jutland APC, 1912-1921", "JPR6"),
                ("JPR7", "Japan", "14-inch British Mk 5 APC Japanese copy", "JPR7"),
                ("JPR8", "Japan", "20cm, 36cm, and 41cm Mk 6 / Type 88 APC", "JPR8"),
                ("JPR9", "Japan", "All capped Type 91 AP / APC", "JPR9"),
                ("JPR10", "Japan", "All uncapped Type 91 AP / SAP", "JPR10"),
                ("APR1", "Austria-Hungary", "Chilled cast iron AP shot/shell and common shells", "APR1 -> DEFAULT1"),
                ("APR2", "Austria-Hungary", "Soft-capped chilled cast iron AP shot", "APR2 -> DEFAULT2"),
                ("APR3", "Austria-Hungary", "Average steel AP shot/shell, 1890-1908", "APR3 -> DEFAULT3"),
                ("APR4", "Austria-Hungary", "Average soft-capped steel APC shot/shell, 1898-1908", "APR4 -> DEFAULT4"),
                ("APR5", "Austria-Hungary", "Skoda British-type CP, uncapped, 1895-1918", "APR5"),
                ("APR6", "Austria-Hungary", "Tough-capped AP shell/common, 1909-1918", "APR6 -> DEFAULT6"),
                ("APR7", "Austria-Hungary", "Skoda British-type tough-capped CPC, 1909-1918", "APR7"),
                ("APR8", "Austria-Hungary", "Skoda WWI tough-capped APC, 1909-1918", "APR8"),
                ("RPR1", "Russia", "Palliser/Gruson chilled cast iron shot and common shells", "RPR1 -> DEFAULT1"),
                ("RPR2", "Russia", "Soft-capped chilled cast iron APC shot/shell, 1896-1900", "RPR2 -> DEFAULT2"),
                ("RPR3", "Russia", "Steel AP shot/shell, 1890-c.1905", "RPR3 -> DEFAULT3"),
                ("RPR4", "Russia", "Soft-capped APC shot/shell, 1896-c.1905", "RPR4 -> DEFAULT4"),
                ("RPR5", "Russia", "Post-1906 AP / M190X-quality steel AP shell", "RPR5"),
                ("RPR6", "Russia", "Post-1906 tough-capped M190X steel APC shell", "RPR6"),
                ("RPR7", "Russia", "Post-1906 uncapped M190X common", "RPR7"),
                ("RPR8", "Russia", "Post-1906 tough-capped M190X common", "RPR8"),
            };

            var presets = entries.Select(entry => LegacyProjectilePreset(entry.code, entry.nation, entry.name, entry.branch)).ToList();
            presets.Add(CustomProjectilePreset());
            return presets;
        }
    }
}
