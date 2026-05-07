using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

using YYZ.Ballistic;

public sealed class McCoyOkunCalculatorDialog
{
    sealed class TableColumnSpec<T>
    {
        public string Name;
        public string Title;
        public int Width;
        public Func<T, string> Selector;
    }

    public sealed class MiniChartSeries
    {
        public string Label;
        public List<Vector2> Points = new();
    }

    readonly string[] tabs =
    {
        "M79 APCLC",
        "Facehard69",
        "McCoy",
        "McCoy Plus",
        "McCoy Plus Facehard",
        "McCoy Plus M79",
        "JBM"
    };

    VisualElement tabBar;
    VisualElement content;
    string activeTab = "M79 APCLC";

    M79Input m79Input = M79.DefaultInput();
    string m79SampleId = "";
    FacehardInput facehardInput = FacehardCalculator.DefaultFacehardInput();
    string facehardSampleId = "";
    readonly List<FacehardRecordedRun> facehardRecordedRuns = new();
    McCoyInput mccoyInput = McCoy.DefaultInput();
    string mccoyDragText = McCoy.DragTableToText(McCoy.DefaultDragTable());
    McCoyPlusInput mccoyPlusInput = McCoyPlus.DefaultInput();
    string mccoyPlusPresetId = "g1";
    string mccoyPlusDragText = McCoyPlus.DragPresetToText("g1");
    McCoyPlusFacehardInput mccoyPlusFacehardInput = McCoyPlusFacehard.DefaultInput();
    FacehardInput mccoyPlusFacehardDetails = FacehardCalculator.DefaultFacehardInput();
    string mccoyPlusFacehardSampleId = "";
    string mccoyPlusFacehardPresetId = "g1";
    string mccoyPlusFacehardDragText = McCoyPlus.DragPresetToText("g1");
    string mccoyPlusFacehardChartMode = "trajectory";
    McCoyPlusM79Input mccoyPlusM79Input = McCoyPlusM79.DefaultInput();
    string mccoyPlusM79SampleId = "";
    string mccoyPlusM79PresetId = "g1";
    string mccoyPlusM79DragText = McCoyPlus.DragPresetToText("g1");
    string mccoyPlusM79ChartMode = "trajectory";
    string jbmMode = "mcdrag";
    McDragInput mcDragInput = Jbm.DefaultMcDragInput();
    McGyroInput mcGyroInput = Jbm.DefaultMcGyroInput();
    IntLiftInput intLiftInput = Jbm.DefaultIntLiftInput();

    sealed class BallisticSample
    {
        public string Id;
        public string Label;
        public string DragPresetId;
        public string ProjectilePresetId;
        public string CapType;
        public double ProjectileDiameter;
        public double ProjectileWeight;
        public double ProjectileBodyWeight;
        public double WindscreenWeight;
        public double BallisticCoefficient;
        public double MuzzleVelocity;
        public double MaxRange;
    }

    sealed class FacehardRecordedRun
    {
        public double Obliquity;
        public double Velocity;
        public double NavyBl;
        public double HolingBl;
        public double EffectiveBl;
        public string Status;
        public string Penetration;
    }

    static readonly List<BallisticSample> Samples = new()
    {
        new BallisticSample { Id = "britain-palliser-6", Label = "Palliser chilled cast iron shot and common shell / 6'' / Britain", DragPresetId = "g1", ProjectilePresetId = "BPR1", CapType = "none", ProjectileDiameter = 6, ProjectileWeight = 100, ProjectileBodyWeight = 100, WindscreenWeight = 0, BallisticCoefficient = 2.9727, MuzzleVelocity = 2230, MaxRange = 14600 },
        new BallisticSample { Id = "britain-palliser-10", Label = "Palliser chilled cast iron shot and common shell / 10'' / Britain", DragPresetId = "g1", ProjectilePresetId = "BPR1", CapType = "none", ProjectileDiameter = 10, ProjectileWeight = 500, ProjectileBodyWeight = 500, WindscreenWeight = 0, BallisticCoefficient = 5.1846, MuzzleVelocity = 2040, MaxRange = 11000 },
        new BallisticSample { Id = "britain-uncapped-75", Label = "Uncapped steel AP shot/shell 1890-1905 / 7.5'' / Britain", DragPresetId = "g1", ProjectilePresetId = "BPR2", CapType = "none", ProjectileDiameter = 7.5, ProjectileWeight = 200, ProjectileBodyWeight = 200, WindscreenWeight = 0, BallisticCoefficient = 2.489, MuzzleVelocity = 2827, MaxRange = 14328 },
        new BallisticSample { Id = "britain-uncapped-12", Label = "Uncapped steel AP shot/shell 1890-1905 / 12'' / Britain", DragPresetId = "g1", ProjectilePresetId = "BPR2", CapType = "none", ProjectileDiameter = 12, ProjectileWeight = 714, ProjectileBodyWeight = 715, WindscreenWeight = 0, BallisticCoefficient = 5.0293, MuzzleVelocity = 1914, MaxRange = 9450 },
        new BallisticSample { Id = "germany-38cm-psgr-bismarck", Label = "38cm Psgr.m.K. L/4.4 APC / Germany (Bismack)", DragPresetId = "g7", ProjectilePresetId = "GPR12", CapType = "hard", ProjectileDiameter = 14.96, ProjectileWeight = 1763.7, ProjectileBodyWeight = 1552.05, WindscreenWeight = 52.91, BallisticCoefficient = 7.7734, MuzzleVelocity = 2690, MaxRange = 38870 },
    };

    public VisualElement BuildContent()
    {
        var root = new VisualElement
        {
            style =
            {
                width = 1050,
                height = 600,
                flexGrow = 1,
                flexShrink = 1
            }
        };

        tabBar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexShrink = 0,
                marginBottom = 6
            }
        };
        root.Add(tabBar);

        content = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        root.Add(content);

        BuildTabs();
        RebuildContent();
        return root;
    }

    void BuildTabs()
    {
        tabBar.Clear();
        foreach (var tab in tabs)
        {
            var button = new Button(() =>
            {
                activeTab = tab;
                BuildTabs();
                RebuildContent();
            })
            {
                text = tab
            };
            button.style.flexGrow = 1;
            button.style.minHeight = 28;
            if (tab == activeTab)
                button.SetEnabled(false);
            tabBar.Add(button);
        }
    }

    void RebuildContent()
    {
        content.Clear();
        content.Add(activeTab switch
        {
            "Facehard69" => BuildFacehardPage(),
            "McCoy" => BuildMcCoyPage(),
            "McCoy Plus" => BuildMcCoyPlusPage(),
            "McCoy Plus Facehard" => BuildMcCoyPlusFacehardPage(),
            "McCoy Plus M79" => BuildMcCoyPlusM79Page(),
            "JBM" => BuildJbmPage(),
            _ => BuildM79Page()
        });
    }

    VisualElement BuildM79Page()
    {
        var result = M79.Calculate(m79Input);
        var root = PageRoot(out var input, out var output);
        input.Add(Header("M79 APCLC", () =>
        {
            m79Input = M79.DefaultInput();
            m79SampleId = "";
            RebuildContent();
        }));
        AddSampleDropdown(input, "Sample", m79SampleId, id =>
        {
            m79SampleId = id;
            var sample = SampleById(id);
            if (sample != null)
                ApplyM79Sample(m79Input, sample);
        });
        AddM79Inputs(input, m79Input);

        output.Add(Banner(("Navy BL", F(result.NavyBallisticLimitRounded), "ft/s"),
            ("Mode", result.PenetrationMode, ""),
            ("Remaining Velocity", F(result.RemainingVelocity), "ft/s")));
        output.Add(Chart("Thickness Scan", M79.Scan(m79Input, m79Input.ProjectileDiameter * 6, 80)
            .Select(row => new Vector2((float)row.Thickness, (float)row.Result.NavyBallisticLimit))));
        output.Add(Pre("M79 Legacy BASIC Report", result.LegacyReport));
        return root;
    }

    VisualElement BuildFacehardPage()
    {
        var result = FacehardCalculator.CalculateFacehard(facehardInput);
        var root = PageRoot(out var input, out var output);
        input.Add(Header("Facehard69 Baseline", () =>
        {
            facehardInput = FacehardCalculator.DefaultFacehardInput();
            facehardSampleId = "";
            facehardRecordedRuns.Clear();
            RebuildContent();
        }));
        AddSampleDropdown(input, "Sample", facehardSampleId, id =>
        {
            facehardSampleId = id;
            var sample = SampleById(id);
            if (sample != null)
                ApplyFacehardSample(facehardInput, sample);
        });
        var recordRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };
        var recordButton = new Button(() =>
        {
            facehardRecordedRuns.Add(new FacehardRecordedRun
            {
                Obliquity = facehardInput.Obliquity,
                Velocity = facehardInput.StrikingVelocity,
                NavyBl = result.NavyBl,
                HolingBl = result.HolingBl,
                EffectiveBl = result.EffectiveBl,
                Status = result.Status,
                Penetration = result.Penetration?.Label ?? ""
            });
            RebuildContent();
        })
        {
            text = "Record Current Velocity and Obliquity"
        };
        recordButton.style.flexGrow = 1;
        recordRow.Add(recordButton);
        recordRow.Add(new Button(() =>
        {
            facehardRecordedRuns.Clear();
            RebuildContent();
        })
        {
            text = "Clear"
        });
        input.Add(recordRow);
        AddFacehardInputs(input, facehardInput, true);

        output.Add(Banner(("NBL", F(result.NavyBl), "ft/s"),
            ("HBL", F(result.HolingBl), "ft/s"),
            ("Status", result.Status, "")));
        output.Add(Warnings(result.Notes));
        output.Add(Pre("Recorded Runs", FacehardRunLines(result)));
        output.Add(Pre("Metrics", FacehardMetricLines(result)));
        output.Add(Pre("Facehard69 Legacy BASIC Report", result.Legacy.Report));
        output.Add(Pre("Process Report", result.Legacy.ProcessReport));
        return root;
    }

    VisualElement BuildMcCoyPage()
    {
        mccoyInput.DragTable = McCoy.NormalizeDragTable(mccoyDragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoy.Calculate(mccoyInput);
        stopwatch.Stop();
        var calculationElapsed = stopwatch.Elapsed;
        var last = result.Points.LastOrDefault();
        var root = PageRoot(out var input, out var output);
        input.Add(Header("McCoy Point-Mass Trajectory", () =>
        {
            mccoyInput = McCoy.DefaultInput();
            mccoyDragText = McCoy.DragTableToText(McCoy.DefaultDragTable());
            RebuildContent();
        }));
        input.Add(Dropdown("Atmosphere Model", new List<string> { "Army Standard Metro", "ICAO" }, mccoyInput.Atmosphere == "icao" ? 1 : 0,
            index => mccoyInput.Atmosphere = index == 1 ? "icao" : "standard"));
        input.Add(NumberField("Muzzle Velocity", mccoyInput.MuzzleVelocity, "ft/s", value => mccoyInput.MuzzleVelocity = value, 1));
        input.Add(NumberField("Ballistic Coefficient", mccoyInput.BallisticCoefficient, "lb/in2", value => mccoyInput.BallisticCoefficient = value, 0.001));
        input.Add(NumberField("Sight Height", mccoyInput.SightHeight, "in", value => mccoyInput.SightHeight = value));
        input.Add(NumberField("Elevation", mccoyInput.ElevationMinutes, "min", value => mccoyInput.ElevationMinutes = value));
        input.Add(NumberField("Density Ratio", mccoyInput.DensityRatio, "", value => mccoyInput.DensityRatio = value, 0.001));
        input.Add(NumberField("Temperature", mccoyInput.TemperatureF, "deg F", value => mccoyInput.TemperatureF = value));
        input.Add(NumberField("Print Interval", mccoyInput.PrintInterval, mccoyInput.RangeUnit, value => mccoyInput.PrintInterval = value, 1));
        input.Add(NumberField("Maximum Range", mccoyInput.MaxRange, mccoyInput.RangeUnit, value => mccoyInput.MaxRange = value, 1));
        input.Add(NumberField("Range Wind", mccoyInput.RangeWindMph, "mph", value => mccoyInput.RangeWindMph = value));
        input.Add(NumberField("Crosswind", mccoyInput.CrossWindMph, "mph", value => mccoyInput.CrossWindMph = value));
        input.Add(NumberField("Match Range", mccoyInput.MatchRange, mccoyInput.RangeUnit, value => mccoyInput.MatchRange = value, 0));
        input.Add(NumberField("Match Height", mccoyInput.MatchHeight, "in", value => mccoyInput.MatchHeight = value));
        input.Add(TextArea("Mach-CD Drag Table", mccoyDragText, value => mccoyDragText = value));

        output.Add(Banner(("Terminal Velocity", F(last?.Velocity), "ft/s"),
            ("Terminal Height", F(last?.HeightInches), "in"),
            ("Elevation Used", F(result.AdjustedElevationMinutes, 3), "min")));
        output.Add(Warnings(result.Warnings));
        output.Add(Chart("Height Curve", result.Points.Select(point => new Vector2((float)point.Range, (float)point.HeightInches))));
        output.Add(Chart("Velocity Curve", result.Points.Select(point => new Vector2((float)point.Range, (float)point.Velocity))));
        output.Add(Pre("McCoy Legacy BASIC Report", result.LegacyReport));
        output.Add(CalculationTime(calculationElapsed));
        output.Add(Table("Trajectory Points", result.Points.Take(80).ToList(),
            Col<TrajectoryPoint>("range", "Range", 90, point => F(point.Range, 0)),
            Col<TrajectoryPoint>("height", "Height", 90, point => F(point.HeightInches, 1)),
            Col<TrajectoryPoint>("deflection", "Defl.", 90, point => F(point.DeflectionInches, 1)),
            Col<TrajectoryPoint>("velocity", "Vel", 80, point => F(point.Velocity, 0)),
            Col<TrajectoryPoint>("time", "Time", 90, point => F(point.Time, 3)),
            Col<TrajectoryPoint>("vx", "VX", 80, point => F(point.Vx, 0)),
            Col<TrajectoryPoint>("vy", "VY", 80, point => F(point.Vy, 0)),
            Col<TrajectoryPoint>("vz", "VZ", 80, point => F(point.Vz, 0))));
        return root;
    }

    VisualElement BuildMcCoyPlusPage()
    {
        mccoyPlusInput.DragTable = McCoy.NormalizeDragTable(mccoyPlusDragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlus.CalculateParallel(mccoyPlusInput);
        stopwatch.Stop();
        var calculationElapsed = stopwatch.Elapsed;
        var last = result.Rows.LastOrDefault();
        var root = PageRoot(out var input, out var output);
        BuildMcCoyPlusInputs(input, mccoyPlusInput, () =>
        {
            mccoyPlusInput = McCoyPlus.DefaultInput();
            mccoyPlusPresetId = "g1";
            mccoyPlusDragText = McCoyPlus.DragPresetToText("g1");
            RebuildContent();
        });
        output.Add(Banner(("Solved Ranges", F(result.Rows.Count, 0), "Rows"),
            ("Maximum Range", F(last?.Range), mccoyPlusInput.RangeUnit),
            ("Last Velocity", F(last?.Velocity), "ft/s")));
        output.Add(Warnings(result.Warnings));
        output.Add(ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusInput.RangeUnit)));
        output.Add(CalculationTime(calculationElapsed));
        output.Add(Table("Range Sweep", result.Rows,
            Col<McCoyPlusRow>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusRow>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusRow>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusRow>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusRow>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3))));
        return root;
    }

    VisualElement BuildMcCoyPlusFacehardPage()
    {
        SyncComboMcCoy(mccoyPlusFacehardInput.McCoy, mccoyPlusFacehardDragText);
        SyncFacehardBridge();
        McCoyPlusFacehard.FacehardCalculator = bridge => FacehardBridgeCalculate(bridge, mccoyPlusFacehardDetails);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusFacehard.Calculate(mccoyPlusFacehardInput);
        stopwatch.Stop();
        var calculationElapsed = stopwatch.Elapsed;
        var root = PageRoot(out var input, out var output);
        input.Add(Header("McCoy Plus Facehard", () =>
        {
            mccoyPlusFacehardInput = McCoyPlusFacehard.DefaultInput();
            mccoyPlusFacehardDetails = FacehardCalculator.DefaultFacehardInput();
            mccoyPlusFacehardSampleId = "";
            mccoyPlusFacehardPresetId = "g1";
            mccoyPlusFacehardDragText = McCoyPlus.DragPresetToText("g1");
            mccoyPlusFacehardChartMode = "trajectory";
            RebuildContent();
        }));
        AddSampleDropdown(input, "Sample", mccoyPlusFacehardSampleId, id =>
        {
            mccoyPlusFacehardSampleId = id;
            var sample = SampleById(id);
            if (sample != null)
            {
                ApplyMcCoyPlusSample(mccoyPlusFacehardInput.McCoy, sample, value => mccoyPlusFacehardPresetId = value, value => mccoyPlusFacehardDragText = value);
                ApplyFacehardSample(mccoyPlusFacehardDetails, sample);
                SyncFacehardBridge();
            }
        });
        AddMcCoyPlusCoreInputs(input, mccoyPlusFacehardInput.McCoy, true, () => mccoyPlusFacehardPresetId, value => mccoyPlusFacehardPresetId = value, () => mccoyPlusFacehardDragText, value => mccoyPlusFacehardDragText = value);
        input.Add(Section("Facehard Inputs"));
        AddFacehardComboInputs(input, mccoyPlusFacehardDetails);
        AddMcCoyPlusDragEditor(input, mccoyPlusFacehardInput.McCoy, () => mccoyPlusFacehardDragText, value => mccoyPlusFacehardDragText = value);
        output.Add(Warnings(result.Warnings));
        output.Add(Dropdown("Chart Mode", new List<string> { "Matched Trajectories", "Penetration By Range" }, mccoyPlusFacehardChartMode == "trajectory" ? 0 : 1,
            index => mccoyPlusFacehardChartMode = index == 0 ? "trajectory" : "penetration"));
        output.Add(mccoyPlusFacehardChartMode == "trajectory"
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusFacehardInput.McCoy.RangeUnit))
            : Chart("Facehard Penetration", result.Rows.Where(row => row.PenetrationInches.HasValue).Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))));
        output.Add(CalculationTime(calculationElapsed));
        output.Add(Table("Rows", result.Rows,
            Col<McCoyPlusFacehardRow>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusFacehardRow>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusFacehardRow>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusFacehardRow>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusFacehardRow>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusFacehardRow>("penetration", "Penetration", 110, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusFacehardRow>("horizontal", "Horizontal Penetration", 160, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a")));
        return root;
    }

    VisualElement BuildMcCoyPlusM79Page()
    {
        SyncComboMcCoy(mccoyPlusM79Input.McCoy, mccoyPlusM79DragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusM79.Calculate(mccoyPlusM79Input);
        stopwatch.Stop();
        var calculationElapsed = stopwatch.Elapsed;
        var root = PageRoot(out var input, out var output);
        input.Add(Header("McCoy Plus M79 APCLC", () =>
        {
            mccoyPlusM79Input = McCoyPlusM79.DefaultInput();
            mccoyPlusM79SampleId = "";
            mccoyPlusM79PresetId = "g1";
            mccoyPlusM79DragText = McCoyPlus.DragPresetToText("g1");
            mccoyPlusM79ChartMode = "trajectory";
            RebuildContent();
        }));
        AddSampleDropdown(input, "Sample", mccoyPlusM79SampleId, id =>
        {
            mccoyPlusM79SampleId = id;
            var sample = SampleById(id);
            if (sample != null)
            {
                ApplyMcCoyPlusSample(mccoyPlusM79Input.McCoy, sample, value => mccoyPlusM79PresetId = value, value => mccoyPlusM79DragText = value);
                ApplyM79Sample(mccoyPlusM79Input.M79, sample);
            }
        });
        input.Add(Section("M79 Inputs"));
        AddM79ComboInputs(input, mccoyPlusM79Input.M79, mccoyPlusM79Input.McCoy, () => mccoyPlusM79PresetId, value => mccoyPlusM79PresetId = value, () => mccoyPlusM79DragText, value => mccoyPlusM79DragText = value, () => mccoyPlusM79SampleId = "");
        AddMcCoyPlusDragEditor(input, mccoyPlusM79Input.McCoy, () => mccoyPlusM79DragText, value => mccoyPlusM79DragText = value);
        output.Add(Warnings(result.Warnings));
        output.Add(Dropdown("Chart Mode", new List<string> { "Matched Trajectories", "Penetration By Range" }, mccoyPlusM79ChartMode == "trajectory" ? 0 : 1,
            index => mccoyPlusM79ChartMode = index == 0 ? "trajectory" : "penetration"));
        output.Add(mccoyPlusM79ChartMode == "trajectory"
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusM79Input.McCoy.RangeUnit))
            : Chart("M79 Penetration", result.Rows.Where(row => row.PenetrationInches.HasValue).Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))));
        output.Add(CalculationTime(calculationElapsed));
        output.Add(Table("Rows", result.Rows,
            Col<McCoyPlusM79Row>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusM79Row>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusM79Row>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusM79Row>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusM79Row>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusM79Row>("penetration", "Penetration", 110, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusM79Row>("horizontal", "Horizontal Penetration", 160, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a"),
            Col<McCoyPlusM79Row>("nbl", "M79 NBL", 100, row => F(row.M79NavyBallisticLimit, 0)),
            Col<McCoyPlusM79Row>("mode", "Mode", 160, row => row.PenetrationMode ?? "outside-range"),
            Col<McCoyPlusM79Row>("remaining", "Remaining Velocity", 140, row => F(row.RemainingVelocity, 0))));
        return root;
    }

    VisualElement BuildJbmPage()
    {
        var root = PageRoot(out var input, out var output);
        input.Add(Header("JBM / McCoy Aerodynamics", () =>
        {
            mcDragInput = Jbm.DefaultMcDragInput();
            mcGyroInput = Jbm.DefaultMcGyroInput();
            intLiftInput = Jbm.DefaultIntLiftInput();
            RebuildContent();
        }));
        input.Add(Dropdown("Program", new List<string> { "MCDRAG", "MCGYRO", "INTLIFT" }, jbmMode == "mcgyro" ? 1 : jbmMode == "intlift" ? 2 : 0,
            index => jbmMode = index == 1 ? "mcgyro" : index == 2 ? "intlift" : "mcdrag"));

        if (jbmMode == "mcgyro")
        {
            AddJbmGeometryInputs(input, mcGyroInput);
            input.Add(NumberField("Projectile Density", mcGyroInput.ProjectileDensityGramsPerCc, "g/cc", value => mcGyroInput.ProjectileDensityGramsPerCc = value, 0.001));
            input.Add(NumberField("Rifling Twist", mcGyroInput.RiflingTwistCalibersPerTurn, "cal/turn", value => mcGyroInput.RiflingTwistCalibersPerTurn = value, 0.001));
            var result = Jbm.CalculateMcGyro(mcGyroInput);
            output.Add(Chart("Stability Factor", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.StabilityFactor))));
            output.Add(Pre("MCGYRO Report", result.LegacyReport));
        }
        else if (jbmMode == "intlift")
        {
            AddJbmGeometryInputs(input, intLiftInput);
            input.Add(NumberField("Center of Gravity", intLiftInput.CenterOfGravityCalibers, "cal", value => intLiftInput.CenterOfGravityCalibers = value, 0.001));
            var result = Jbm.CalculateIntLift(intLiftInput);
            output.Add(Warnings(result.Warnings));
            output.Add(Chart("CLA", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.Cla))));
            output.Add(Pre("INTLIFT Report", result.LegacyReport));
        }
        else
        {
            AddJbmGeometryInputs(input, mcDragInput);
            input.Add(NumberField("Rotating Band Diameter", mcDragInput.RotatingBandDiameterCalibers, "cal", value => mcDragInput.RotatingBandDiameterCalibers = value, 0.001));
            input.Add(NumberField("Center of Gravity", mcDragInput.CenterOfGravityCalibers, "cal", value => mcDragInput.CenterOfGravityCalibers = value, 0.001));
            input.Add(Dropdown("Boundary Layer", new List<string> { "Laminar/Laminar", "Laminar/Turbulent", "Turbulent/Turbulent" }, BoundaryLayerIndex(mcDragInput.BoundaryLayer),
                index => mcDragInput.BoundaryLayer = index == 0 ? JbmBoundaryLayer.LaminarLaminar : index == 2 ? JbmBoundaryLayer.TurbulentTurbulent : JbmBoundaryLayer.LaminarTurbulent));
            var result = Jbm.CalculateMcDrag(mcDragInput);
            output.Add(Warnings(result.Warnings));
            output.Add(Chart("CD0", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.Cd0))));
            output.Add(Pre("MCDRAG Report", result.LegacyReport));
        }

        return root;
    }

    void AddSampleDropdown(VisualElement input, string label, string selectedId, Action<string> setter)
    {
        var choices = new List<string> { "Not Specified" };
        choices.AddRange(Samples.Select(sample => sample.Label));
        var index = 0;
        var sampleIndex = Samples.FindIndex(sample => sample.Id == selectedId);
        if (sampleIndex >= 0)
            index = sampleIndex + 1;
        input.Add(Dropdown(label, choices, index, selected =>
        {
            setter(selected <= 0 ? "" : Samples[Mathf.Clamp(selected - 1, 0, Samples.Count - 1)].Id);
        }));
    }

    static BallisticSample SampleById(string id)
    {
        return Samples.FirstOrDefault(sample => sample.Id == id);
    }

    void AddM79Inputs(VisualElement input, M79Input target)
    {
        input.Add(NumberField("Projectile Diameter", target.ProjectileDiameter, "in", value => target.ProjectileDiameter = value, 0.001));
        input.Add(NumberField("Projectile Weight", target.ProjectileWeight, "lb", value => target.ProjectileWeight = value, 0.001));
        input.Add(NumberField("Plate Thickness", target.PlateThickness, "in", value => target.PlateThickness = value, 0.001));
        input.Add(NumberField("Plate Quality", target.PlateQuality, "", value => target.PlateQuality = value, 0.001));
        input.Add(NumberField("Obliquity", target.Obliquity, "deg", value => target.Obliquity = value, 0));
        input.Add(NumberField("Striking Velocity", target.StrikingVelocity, "ft/s", value => target.StrikingVelocity = value, 1));
        input.Add(NumberField("Elongation", target.Elongation, "%", value => target.Elongation = value, 10));
    }

    void AddM79ComboInputs(VisualElement input, M79Input m79, McCoyPlusInput mccoy, Func<string> getPresetId, Action<string> setPresetId, Func<string> getDragText, Action<string> setDragText, Action onPresetChanged)
    {
        input.Add(NumberField("Projectile Diameter", m79.ProjectileDiameter, "in", value => m79.ProjectileDiameter = value, 0.001));
        input.Add(NumberField("Projectile Weight", m79.ProjectileWeight, "lb", value => m79.ProjectileWeight = value, 0.001));
        input.Add(NumberField("Muzzle Velocity", mccoy.MuzzleVelocity, "ft/s", value => mccoy.MuzzleVelocity = value, 1));
        input.Add(NumberField("Ballistic Coefficient", mccoy.BallisticCoefficient, "lb/in2", value => mccoy.BallisticCoefficient = value, 0.001));
        input.Add(NumberField("Maximum Range", mccoy.MaxRange, mccoy.RangeUnit, value => mccoy.MaxRange = value, 1));
        input.Add(NumberField("Match Height", mccoy.MatchHeight, "in", value => mccoy.MatchHeight = value));
        input.Add(McCoyPlusElevationSearchDropdown(mccoy));
        input.Add(Dropdown("Atmosphere Model", new List<string> { "Army Standard Metro", "ICAO" }, mccoy.Atmosphere == "icao" ? 1 : 0,
            index => mccoy.Atmosphere = index == 1 ? "icao" : "standard"));
        input.Add(NumberField("Density Ratio", mccoy.DensityRatio, "", value => mccoy.DensityRatio = value, 0.001));
        input.Add(NumberField("Temperature", mccoy.TemperatureF, "deg F", value => mccoy.TemperatureF = value));
        input.Add(NumberField("Armor Quality", m79.PlateQuality, "", value => m79.PlateQuality = value, 0.001));
        input.Add(NumberField("Elongation", m79.Elongation, "%", value => m79.Elongation = value, 10));
        AddMcCoyPlusPresetDropdown(input, mccoy, getPresetId, setPresetId, setDragText, onPresetChanged);
    }

    void AddFacehardInputs(VisualElement input, FacehardInput target, bool includeVelocity)
    {
        var selectedPreset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == target.ProjectilePresetId)
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
        var selectedNation = selectedPreset?.Nation ?? "Custom";
        var nations = FacehardCalculator.FacehardProjectileNations;
        input.Add(Dropdown("Projectile Nation", nations.Select(item => item.Name).ToList(),
            Mathf.Max(0, nations.FindIndex(item => item.Id == selectedNation)), index =>
            {
                var nation = nations[Mathf.Max(0, index)];
                target.ProjectilePresetId = nation.DefaultProjectileId;
            }));

        var projectileChoices = FacehardCalculator.FacehardProjectilePresets
            .Where(item => item.Nation == selectedNation)
            .ToList();
        if (projectileChoices.Count == 0)
            projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        input.Add(Dropdown("Projectile Type", projectileChoices.Select(ProjectilePresetLabel).ToList(),
            Mathf.Max(0, projectileChoices.FindIndex(item => item.Id == target.ProjectilePresetId)),
            index => target.ProjectilePresetId = projectileChoices[Mathf.Max(0, index)].Id));

        var isCustom = target.ProjectilePresetId == "custom";
        var capLabels = new List<string> { "Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap" };
        var capValues = new List<string> { "hard", "thin-hard", "soft", "hood", "none" };
        input.Add(Dropdown("Cap Or Hood", capLabels, Mathf.Max(0, capValues.IndexOf(ResolvedCapType(target))), index => target.CapType = capValues[Mathf.Max(0, index)], isCustom));

        var schemaValues = new List<string> { "standard", "japanese-cap-head" };
        input.Add(Dropdown("Nose Schema", new List<string> { "Standard", "Japanese Cap Head" }, Mathf.Max(0, schemaValues.IndexOf(target.NoseSchema)),
            index =>
            {
                target.NoseSchema = schemaValues[Mathf.Max(0, index)];
                target.NoseCondition = "intact";
            }, isCustom));
        if (isCustom && target.NoseSchema == "japanese-cap-head")
        {
            input.Add(Dropdown("Japanese Cap Head Type", new List<string> { "Uncapped Type 91 AP", "Capped Type 88/91/1 APC" }, target.JapaneseCapHead <= 1 ? 0 : 1,
                index =>
                {
                    target.JapaneseCapHead = index == 0 ? 1 : 2;
                    target.NoseCondition = "intact";
                }));
        }

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        var conditionLabels = noseWeights.ConditionOptions.Select(NoseConditionLabel).ToList();
        input.Add(Dropdown("Pre-Impact Nose Condition", conditionLabels,
            Mathf.Max(0, noseWeights.ConditionOptions.IndexOf(noseWeights.Condition)),
            index => target.NoseCondition = noseWeights.ConditionOptions[Mathf.Max(0, index)]));

        input.Add(NumberField("Projectile Diameter", target.ProjectileDiameter, "in", value => target.ProjectileDiameter = value, 0.001));
        input.Add(NumberField("Projectile Weight", target.ProjectileWeight, "lb", value => target.ProjectileWeight = value, 0.001));
        input.Add(NumberField("Projectile Body Weight", target.ProjectileBodyWeight, "lb", value => target.ProjectileBodyWeight = value, 0.001));
        if (noseWeights.SchemaKind == "japanese-cap-head" && noseWeights.CapHead == 2)
            input.Add(NumberField("Windscreen + Cap Head Weight", target.WindscreenCapHeadWeight, "lb", value => target.WindscreenCapHeadWeight = value, 0));
        else if (noseWeights.SchemaKind == "standard")
            input.Add(NumberField("Windscreen Weight", target.WindscreenWeight, "lb", value => target.WindscreenWeight = value, 0));
        input.Add(NumberField(RemainingNoseWeightLabel(target, noseWeights.CapHead), noseWeights.RemainWeight, "lb", _ => { }, 0, false));
        input.Add(NumberField("PLIM", isCustom ? target.ProjectileLimitQuality : selectedPreset?.ProjectileLimitQuality ?? target.ProjectileLimitQuality, "", value => target.ProjectileLimitQuality = value, 0.001, isCustom));
        input.Add(NumberField("PDAM", isCustom ? target.ProjectileDamageQuality : selectedPreset?.ProjectileDamageQuality ?? target.ProjectileDamageQuality, "", value => target.ProjectileDamageQuality = value, 0.001, isCustom));

        if (includeVelocity)
        {
            input.Add(NumberField("Striking Velocity", target.StrikingVelocity, "ft/s", value => target.StrikingVelocity = value, 1));
            input.Add(NumberField("Obliquity", target.Obliquity, "deg", value => target.Obliquity = value, 0));
        }

        input.Add(Dropdown("Armor Type", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == target.ArmorId),
            index => target.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, index)].Id));
        input.Add(ToggleField("Strongly Curved Plate", target.CurvedPlate, value => target.CurvedPlate = value));
        input.Add(NumberField("Plate Thickness", target.PlateThickness, "in", value => target.PlateThickness = value, 0.001));
        input.Add(NumberField("Wood Backing", target.WoodBackingThickness, "in", value => target.WoodBackingThickness = value, 0));
        input.Add(NumberField("Cement Backing", target.CementBackingThickness, "in", value => target.CementBackingThickness = value, 0));
        input.Add(NumberField("Metal Backing", target.MetalBackingThickness, "in", value => target.MetalBackingThickness = value, 0));
        input.Add(NumberField("Backing Quality", target.BackingQuality, "", value => target.BackingQuality = value, 0.001));
        input.Add(NumberField("Backing Plates", target.BackingPlates, "", value => target.BackingPlates = value, 0));
        if (((noseWeights.Condition == "windscreen-removed" && ResolvedCapType(target) != "none") || noseWeights.Condition == "caphead-removed") && noseWeights.RemainWeight <= 0)
            input.Add(Warnings(new[] { "The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state." }));
    }

    void AddFacehardComboInputs(VisualElement input, FacehardInput target)
    {
        input.Add(Dropdown("Armor Type", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == target.ArmorId),
            index => target.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, index)].Id));

        var selectedPreset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == target.ProjectilePresetId)
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
        var selectedNation = selectedPreset?.Nation ?? "Custom";
        var nations = FacehardCalculator.FacehardProjectileNations;
        input.Add(Dropdown("Projectile Nation", nations.Select(item => item.Name).ToList(),
            Mathf.Max(0, nations.FindIndex(item => item.Id == selectedNation)), index =>
            {
                var nation = nations[Mathf.Max(0, index)];
                target.ProjectilePresetId = nation.DefaultProjectileId;
            }));

        var projectileChoices = FacehardCalculator.FacehardProjectilePresets
            .Where(item => item.Nation == selectedNation)
            .ToList();
        if (projectileChoices.Count == 0)
            projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        input.Add(Dropdown("Projectile Type", projectileChoices.Select(ProjectilePresetLabel).ToList(),
            Mathf.Max(0, projectileChoices.FindIndex(item => item.Id == target.ProjectilePresetId)),
            index => target.ProjectilePresetId = projectileChoices[Mathf.Max(0, index)].Id));

        AddFacehardProjectileDetailInputs(input, target, selectedPreset, target.ProjectilePresetId == "custom");
        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        input.Add(NumberField("Wood Backing", target.WoodBackingThickness, "in", value => target.WoodBackingThickness = value, 0));
        input.Add(NumberField("Cement Backing", target.CementBackingThickness, "in", value => target.CementBackingThickness = value, 0));
        input.Add(NumberField("Metal Backing", target.MetalBackingThickness, "in", value => target.MetalBackingThickness = value, 0));
        if (((noseWeights.Condition == "windscreen-removed" && ResolvedCapType(target) != "none") || noseWeights.Condition == "caphead-removed") && noseWeights.RemainWeight <= 0)
            input.Add(Warnings(new[] { "The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state." }));
    }

    void AddFacehardProjectileDetailInputs(VisualElement input, FacehardInput target, FacehardProjectilePreset selectedPreset, bool isCustom)
    {
        var capLabels = new List<string> { "Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap" };
        var capValues = new List<string> { "hard", "thin-hard", "soft", "hood", "none" };
        input.Add(Dropdown("Cap Or Hood", capLabels, Mathf.Max(0, capValues.IndexOf(ResolvedCapType(target))), index => target.CapType = capValues[Mathf.Max(0, index)], isCustom));

        var schemaValues = new List<string> { "standard", "japanese-cap-head" };
        input.Add(Dropdown("Nose Schema", new List<string> { "Standard", "Japanese Cap Head" }, Mathf.Max(0, schemaValues.IndexOf(target.NoseSchema)),
            index =>
            {
                target.NoseSchema = schemaValues[Mathf.Max(0, index)];
                target.NoseCondition = "intact";
            }, isCustom));
        if (isCustom && target.NoseSchema == "japanese-cap-head")
        {
            input.Add(Dropdown("Japanese Cap Head Type", new List<string> { "Uncapped Type 91 AP", "Capped Type 88/91/1 APC" }, target.JapaneseCapHead <= 1 ? 0 : 1,
                index =>
                {
                    target.JapaneseCapHead = index == 0 ? 1 : 2;
                    target.NoseCondition = "intact";
                }));
        }

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        var conditionLabels = noseWeights.ConditionOptions.Select(NoseConditionLabel).ToList();
        input.Add(Dropdown("Pre-Impact Nose Condition", conditionLabels,
            Mathf.Max(0, noseWeights.ConditionOptions.IndexOf(noseWeights.Condition)),
            index => target.NoseCondition = noseWeights.ConditionOptions[Mathf.Max(0, index)]));

        input.Add(NumberField("Projectile Diameter", target.ProjectileDiameter, "in", value => target.ProjectileDiameter = value, 0.001));
        input.Add(NumberField("Projectile Weight", target.ProjectileWeight, "lb", value => target.ProjectileWeight = value, 0.001));
        input.Add(NumberField("Body Weight", target.ProjectileBodyWeight, "lb", value => target.ProjectileBodyWeight = value, 0.001));
        if (noseWeights.SchemaKind == "japanese-cap-head" && noseWeights.CapHead == 2)
            input.Add(NumberField("Windscreen + Cap Head Weight", target.WindscreenCapHeadWeight, "lb", value => target.WindscreenCapHeadWeight = value, 0));
        else if (noseWeights.SchemaKind == "standard")
            input.Add(NumberField("Windscreen Weight", target.WindscreenWeight, "lb", value => target.WindscreenWeight = value, 0));
        input.Add(NumberField(RemainingNoseWeightLabel(target, noseWeights.CapHead), noseWeights.RemainWeight, "lb", _ => { }, 0, false));
        input.Add(NumberField("PLIM", isCustom ? target.ProjectileLimitQuality : selectedPreset?.ProjectileLimitQuality ?? target.ProjectileLimitQuality, "", value => target.ProjectileLimitQuality = value, 0.001, isCustom));
        input.Add(NumberField("PDAM", isCustom ? target.ProjectileDamageQuality : selectedPreset?.ProjectileDamageQuality ?? target.ProjectileDamageQuality, "", value => target.ProjectileDamageQuality = value, 0.001, isCustom));
    }

    void AddJbmGeometryInputs(VisualElement input, JbmProjectileGeometryInput target)
    {
        input.Add(TextField("Projectile ID", target.ProjectileId, value => target.ProjectileId = value));
        input.Add(NumberField("Reference Diameter", target.ReferenceDiameterMm, "mm", value => target.ReferenceDiameterMm = value, 0.001));
        input.Add(NumberField("Total Length", target.TotalLengthCalibers, "cal", value => target.TotalLengthCalibers = value, 0.001));
        input.Add(NumberField("Nose Length", target.NoseLengthCalibers, "cal", value => target.NoseLengthCalibers = value, 0.001));
        input.Add(NumberField("RT/R", target.TangentRadiusRatio, "", value => target.TangentRadiusRatio = value, 0));
        input.Add(NumberField("Boattail Length", target.BoattailLengthCalibers, "cal", value => target.BoattailLengthCalibers = value, 0));
        input.Add(NumberField("Base Diameter", target.BaseDiameterCalibers, "cal", value => target.BaseDiameterCalibers = value, 0.001));
        input.Add(NumberField("Meplat Diameter", target.MeplatDiameterCalibers, "cal", value => target.MeplatDiameterCalibers = value, 0));
    }

    static int BoundaryLayerIndex(string boundaryLayer)
    {
        return boundaryLayer == JbmBoundaryLayer.LaminarLaminar ? 0 : boundaryLayer == JbmBoundaryLayer.TurbulentTurbulent ? 2 : 1;
    }

    static string NoseConditionLabel(string condition)
    {
        return condition switch
        {
            "windscreen-removed" => "Only windscreen lost",
            "caphead-removed" => "Windscreen and cap-head lost",
            "all-removed" => "All nose coverings lost",
            _ => "All nose coverings intact"
        };
    }

    static string ProjectilePresetLabel(FacehardProjectilePreset preset)
    {
        if (preset == null)
            return "";
        return preset.Id == "custom"
            ? preset.Name
            : $"{preset.Id}. {preset.Name} ({preset.BranchLabel ?? preset.OriginalCode})";
    }

    static string RemainingNoseWeightLabel(FacehardInput input, double capHead)
    {
        if (input?.NoseSchema == "japanese-cap-head" && capHead == 1)
            return "Remaining Nose Plug Weight";
        if (input?.NoseSchema == "japanese-cap-head" && capHead == 2)
            return "Remaining AP Cap Weight";
        return "Remaining Nose Covering Weight";
    }

    static string ResolvedCapType(FacehardInput input)
    {
        var preset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId);
        return input.ProjectilePresetId == "custom" || preset == null ? input.CapType : preset.CapType;
    }

    IEnumerable<string> FacehardRunLines(FacehardResult result)
    {
        yield return $"{"#",4} {"OB",8} {"VS",8} {"HBL",8} {"NBL",8} {"EBL",8} {"PENTP",7} Status";
        yield return $"{"live",4} {facehardInput.Obliquity,8:0.#} {facehardInput.StrikingVelocity,8:0} {result.HolingBl,8:0} {result.NavyBl,8:0} {result.EffectiveBl,8:0} {result.Penetration?.Type ?? 0,7:0} {result.Status}";
        for (var i = 0; i < facehardRecordedRuns.Count; i++)
        {
            var run = facehardRecordedRuns[i];
            yield return $"{i + 1,4} {run.Obliquity,8:0.#} {run.Velocity,8:0} {run.HolingBl,8:0} {run.NavyBl,8:0} {run.EffectiveBl,8:0} {run.Penetration,7} {run.Status}";
        }
    }

    static IEnumerable<string> FacehardMetricLines(FacehardResult result)
    {
        yield return $"EBL - Effective Ballistic Limit: {F(result.EffectiveBl, 0)} ft/s";
        yield return $"Raw EBL / MINEV - Minimum Effective Velocity: {F(result.RawEffectiveBl, 0)} / {F(result.MinimumEffectiveVelocity, 0)} ft/s";
        yield return $"TEFF - Effective Plate Thickness: {F(result.EffectiveThickness, 2)} in";
        yield return $"TP - Penetration Effective Thickness: {F(result.PenetrationThickness, 2)} in";
        yield return $"TD - Projectile Damage Effective Thickness: {F(result.DamageThickness, 2)} in";
        yield return $"UB - Unhardened Back Layer: {F(result.Armor?.Ub, 0)} %";
        yield return $"Q / QDAM - Armor / Damage Quality: {F(result.Armor?.Q, 3)} / {F(result.Armor?.QDam, 3)}";
        yield return $"SC - Scale Factor: {F(result.ScalingFactor, 3)}";
        yield return $"MO - Obliquity Multiplier: {F(result.ObliquityMultiplier, 3)}";
        yield return $"VDF - Velocity Difference Factor: {F(result.Vdf, 4)}";
        yield return $"BEND VDF - Bending Velocity Difference Factor: {F(result.BendVdf, 4)}";
        yield return $"PLIM / PDAM - Limit / Damage Quality: {F(result.ResolvedProjectileLimitQuality, 3)} / {F(result.ResolvedProjectileDamageQuality, 3)}";
        yield return $"POLMOD / POIMOD - Limit / Effective Modifiers: {F(result.ProjectileLimitModifier, 3)} / {F(result.ProjectileEffectiveModifier, 3)}";
        yield return $"Cap: {result.ResolvedCapType}";
        yield return $"Shatter: {result.Shatter?.Type}";
        yield return $"SHAT HBL / NBL - Shattered Limits: {F(result.Shatter?.HolingBl, 0)} / {F(result.Shatter?.NavyBl, 0)} ft/s";
        yield return $"VLTRU / VHTRU - Unshattered NBL / HBL: {F(result.Limits?.UnshatteredNavyBl, 0)} / {F(result.Limits?.UnshatteredHolingBl, 0)} ft/s";
        yield return $"VLND / VHND - Undamaged NBL / HBL: {F(result.Limits?.UndamagedNavyBl, 0)} / {F(result.Limits?.UndamagedHolingBl, 0)} ft/s";
        yield return $"VDF used - Limit / Post-impact: {F(result.Limits?.VdfUsed, 4)} / {F(result.Limits?.VdfPostImpact, 4)}";
        yield return $"SHATMULT - Shatter Thickness Multiplier: {F(result.Shatter?.Multiplier, 3)}";
        yield return $"MSHAT - Shattered Obliquity Multiplier: {F(result.Shatter?.ObliquityMultiplier, 3)}";
        yield return $"PPLUS - Projectile Quality Bonus: {F(result.ProjectileQualityBonus, 3)}";
        yield return $"PENTP - Penetration Type / Flag: {F(result.Penetration?.Type, 0)} / {result.Penetration?.PenetrationFlag}";
        yield return $"Remaining Velocity: {F(result.Penetration?.ProjectileRemainingVelocity, 0)} ft/s";
        yield return $"Plug / Pieces Velocity: {F(result.Penetration?.PlugOrPiecesVelocity, 0)} ft/s";
        yield return $"Exit / Deflect: {F(result.Penetration?.ExitAngle, 1)} / {F(result.Penetration?.DeflectionAngle, 1)} deg";
        yield return $"Plug Weight: {F(result.Plugs?.NormalPlugWeight, 1)} / {F(result.Plugs?.DeltaPlugWeight, 1)} lb";
        yield return $"Plug Multiplier: {F(result.Plugs?.PlugMultiplier, 2)}";
        yield return $"Damage - Body / Nose Break: BDYDM {F(result.Penetration?.BodyDamage, 0)} / NSBRK {F(result.Penetration?.NoseDamage, 0)}";
        yield return $"Thin range: {F(result.TrueThinBoundary, 2)}-{F(result.ThinBoundary, 2)} cal";
        yield return $"Backing: {F(result.BackingEffectiveThickness, 2)} in";
        yield return $"Wood/Cement: {F(result.WoodBackingEffectiveThickness, 2)} / {F(result.CementBackingEffectiveThickness, 2)} in";
        yield return $"Metal backing: {F(result.MetalBackingEffectiveThickness, 2)} in";
        yield return $"Current Armor Parameters: {result.Armor?.Name} CARTWL {F(result.Armor?.Cartwheel, 0)} CMPND {(result.Armor?.Compound == true ? 1 : 0)} SOFTSHAT {F(result.Armor?.SoftShat, 0)} THNCHL {(result.Armor?.ThinChill == true ? 1 : 0)} THKTHN {F(result.Armor?.ThkThn, 0)}";
        yield return $"Projectile: {result.ProjectilePreset?.Name} Source code {result.ProjectilePreset?.OriginalCode ?? "custom"}";
        yield return $"BEND / CARDONALD - Special Projectile Flags: {F(result.ProjectilePreset?.Bend, 0)} / {F(result.ProjectilePreset?.Cardonald, 0)}";
        yield return $"OBCRIT / VSCRIT - Critical Obliquity / Velocity: {F(result.BendCriticalObliquity, 1)} deg / {F(result.CardonaldCriticalVelocity, 0)} ft/s";
        yield return $"PENCONST - Penetration Formula Constant: {(double.IsFinite(result.PenConst) ? result.PenConst.ToString("0.000E+0", CultureInfo.InvariantCulture) : "-")}";
    }

    void ApplyM79Sample(M79Input target, BallisticSample sample)
    {
        target.ProjectileDiameter = sample.ProjectileDiameter;
        target.ProjectileWeight = sample.ProjectileWeight;
    }

    void ApplyFacehardSample(FacehardInput target, BallisticSample sample)
    {
        target.ProjectilePresetId = sample.ProjectilePresetId;
        target.ProjectileDiameter = sample.ProjectileDiameter;
        target.ProjectileWeight = sample.ProjectileWeight;
        target.ProjectileBodyWeight = sample.ProjectileBodyWeight;
        target.CapType = sample.CapType;
        target.NoseSchema = "standard";
        target.NoseCondition = "intact";
        target.WindscreenWeight = sample.WindscreenWeight;
        target.WindscreenCapHeadWeight = 0;
    }

    void ApplyMcCoyPlusSample(McCoyPlusInput target, BallisticSample sample, Action<string> setPresetId, Action<string> setDragText)
    {
        ApplyMcCoyPlusPreset(target, sample.DragPresetId, setPresetId, setDragText);
        target.ProjectileId = sample.Label;
        target.BallisticCoefficient = sample.BallisticCoefficient;
        target.MuzzleVelocity = sample.MuzzleVelocity;
        target.MaxRange = sample.MaxRange;
    }

    void SyncFacehardBridge()
    {
        mccoyPlusFacehardInput.Facehard.ProjectileDiameter = mccoyPlusFacehardDetails.ProjectileDiameter;
        mccoyPlusFacehardInput.Facehard.PlateThickness = mccoyPlusFacehardDetails.PlateThickness;
        mccoyPlusFacehardInput.Facehard.Obliquity = mccoyPlusFacehardDetails.Obliquity;
        mccoyPlusFacehardInput.Facehard.StrikingVelocity = mccoyPlusFacehardDetails.StrikingVelocity;
    }

    void BuildMcCoyPlusInputs(VisualElement input, McCoyPlusInput target, Action reset)
    {
        input.Add(Header("McCoy Plus Range Sweep", reset));
        AddMcCoyPlusCoreInputs(input, target, true, () => mccoyPlusPresetId, value => mccoyPlusPresetId = value, () => mccoyPlusDragText, value => mccoyPlusDragText = value);
        AddMcCoyPlusDragEditor(input, target, () => mccoyPlusDragText, value => mccoyPlusDragText = value);
    }

    void AddMcCoyPlusCoreInputs(VisualElement input, McCoyPlusInput target, bool includePreset, Func<string> getPresetId, Action<string> setPresetId, Func<string> getDragText, Action<string> setDragText)
    {
        input.Add(McCoyPlusElevationSearchDropdown(target));
        input.Add(Dropdown("Atmosphere Model", new List<string> { "Army Standard Metro", "ICAO" }, target.Atmosphere == "icao" ? 1 : 0,
            index => target.Atmosphere = index == 1 ? "icao" : "standard"));
        if (includePreset)
            AddMcCoyPlusPresetDropdown(input, target, getPresetId, setPresetId, setDragText);
        input.Add(NumberField("Muzzle Velocity", target.MuzzleVelocity, "ft/s", value => target.MuzzleVelocity = value, 1));
        input.Add(NumberField("Ballistic Coefficient", target.BallisticCoefficient, "lb/in2", value => target.BallisticCoefficient = value, 0.001));
        input.Add(NumberField("Maximum Range", target.MaxRange, target.RangeUnit, value => target.MaxRange = value, 1));
        input.Add(NumberField("Density Ratio", target.DensityRatio, "", value => target.DensityRatio = value, 0.001));
        input.Add(NumberField("Temperature", target.TemperatureF, "deg F", value => target.TemperatureF = value));
        input.Add(NumberField("Match Height", target.MatchHeight, "in", value => target.MatchHeight = value));
    }

    VisualElement McCoyPlusElevationSearchDropdown(McCoyPlusInput target)
    {
        return Dropdown("Elevation Search", new List<string> { "Cached Binary Search", "Matched Range" },
            target.ElevationSearchMode == McCoyPlusElevationSearchMode.MatchedRange ? 1 : 0,
            index => target.ElevationSearchMode = index == 1
                ? McCoyPlusElevationSearchMode.MatchedRange
                : McCoyPlusElevationSearchMode.CachedBinarySearch);
    }

    void AddMcCoyPlusPresetDropdown(VisualElement input, McCoyPlusInput target, Func<string> getPresetId, Action<string> setPresetId, Action<string> setDragText, Action onPresetChanged = null)
    {
        var presets = McCoyPlus.DragPresets();
        input.Add(Dropdown("Mach-CD Preset", presets.Select(item => item.Label).ToList(), Mathf.Max(0, presets.FindIndex(item => item.Id == getPresetId())), index =>
        {
            var preset = presets[Mathf.Max(0, index)];
            ApplyMcCoyPlusPreset(target, preset.Id, setPresetId, setDragText);
            onPresetChanged?.Invoke();
        }));
    }

    static void ApplyMcCoyPlusPreset(McCoyPlusInput target, string presetId, Action<string> setPresetId, Action<string> setDragText)
    {
        var preset = McCoyPlus.DragPresets().FirstOrDefault(item => item.Id == presetId)
            ?? McCoyPlus.DragPresets()[0];
        setPresetId(preset.Id);
        setDragText(McCoyPlus.DragPresetToText(preset.Id));
        target.DragName = preset.Label;
        target.DragTable = preset.Points;
        target.RangeUnit = "yards";
    }

    void AddMcCoyPlusDragEditor(VisualElement input, McCoyPlusInput target, Func<string> getDragText, Action<string> setDragText)
    {
        input.Add(TextArea("Mach-CD Drag Table", getDragText(), value =>
        {
            setDragText(value);
            target.DragTable = McCoy.NormalizeDragTable(value);
        }));
    }

    void SyncComboMcCoy(McCoyPlusInput input, string dragText)
    {
        input.DragTable = input.DragTable != null && input.DragTable.Count >= 2
            ? input.DragTable
            : McCoy.NormalizeDragTable(dragText);
    }

    static VisualElement PageRoot(out ScrollView input, out ScrollView output)
    {
        var root = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                flexShrink = 1
            }
        };
        input = new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexBasis = 340,
                flexShrink = 0,
                marginRight = 8
            }
        };
        output = new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        root.Add(input);
        root.Add(output);
        return root;
    }

    static VisualElement Header(string title, Action reset)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };
        var label = new Label(title)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                flexGrow = 1
            }
        };
        var button = new Button(reset) { text = "Reset" };
        row.Add(label);
        row.Add(button);
        return row;
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

    VisualElement NumberField(string label, double value, string unit, Action<double> setter, double? min = null, bool enabled = true)
    {
        var field = new FloatField(label)
        {
            value = (float)value
        };
        field.SetEnabled(enabled);
        field.RegisterValueChangedCallback(evt =>
        {
            var next = evt.newValue;
            if (min.HasValue)
                next = Mathf.Max((float)min.Value, next);
            setter(next);
            RebuildContent();
        });
        if (!string.IsNullOrEmpty(unit))
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            field.style.flexGrow = 1;
            row.Add(field);
            var unitLabel = new Label(unit) { style = { width = 48, unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 4 } };
            row.Add(unitLabel);
            return row;
        }
        return field;
    }

    VisualElement Dropdown(string label, List<string> choices, int index, Action<int> setter, bool enabled = true)
    {
        if (choices.Count == 0)
            choices.Add("");
        index = Mathf.Clamp(index, 0, choices.Count - 1);
        var field = new DropdownField(label, choices, index);
        field.SetEnabled(enabled);
        field.RegisterValueChangedCallback(evt =>
        {
            setter(choices.IndexOf(evt.newValue));
            RebuildContent();
        });
        return field;
    }

    VisualElement TextField(string label, string value, Action<string> setter)
    {
        var field = new UnityEngine.UIElements.TextField(label)
        {
            value = value ?? ""
        };
        field.RegisterValueChangedCallback(evt =>
        {
            setter(evt.newValue);
            RebuildContent();
        });
        return field;
    }

    VisualElement ToggleField(string label, bool value, Action<bool> setter)
    {
        var toggle = new Toggle(label)
        {
            value = value
        };
        toggle.RegisterValueChangedCallback(evt =>
        {
            setter(evt.newValue);
            RebuildContent();
        });
        return toggle;
    }

    VisualElement TextArea(string label, string value, Action<string> setter)
    {
        var field = new TextField(label)
        {
            multiline = true,
            value = value
        };
        field.style.height = 120;
        field.RegisterValueChangedCallback(evt =>
        {
            setter(evt.newValue);
            RebuildContent();
        });
        return field;
    }

    static VisualElement Banner(params (string label, string value, string unit)[] items)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 8
            }
        };
        foreach (var item in items)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    borderBottomWidth = 1,
                    borderTopWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    paddingBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 6,
                    paddingRight = 6,
                    marginRight = 4
                }
            };
            box.Add(new Label(item.label));
            var value = new Label(item.value) { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            box.Add(value);
            if (!string.IsNullOrEmpty(item.unit))
                box.Add(new Label(item.unit));
            row.Add(box);
        }
        return row;
    }

    static VisualElement Warnings(IEnumerable<string> Warnings)
    {
        var list = Warnings?.Where(warning => !string.IsNullOrWhiteSpace(warning)).ToList() ?? new();
        if (list.Count == 0)
            return new VisualElement();
        var root = new VisualElement { style = { marginBottom = 8 } };
        foreach (var warning in list)
            root.Add(new Label(warning));
        return root;
    }

    static VisualElement Chart(string title, IEnumerable<Vector2> Points)
    {
        var root = new VisualElement { style = { minHeight = 190, marginBottom = 8 } };
        root.Add(Section(title));
        var chart = new McCoyOkunMiniChart();
        chart.SetPoints(Points);
        root.Add(chart);
        return root;
    }

    static VisualElement ChartSeries(string title, IEnumerable<MiniChartSeries> series)
    {
        var resolvedSeries = series?.Where(item => item != null && item.Points.Count > 0).ToList() ?? new();
        var root = new VisualElement { style = { minHeight = 210, marginBottom = 8 } };
        root.Add(Section(title));
        var chart = new McCoyOkunMiniChart();
        chart.SetSeries(resolvedSeries);
        root.Add(chart);
        if (resolvedSeries.Count > 1)
        {
            var legend = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 4
                }
            };
            for (var i = 0; i < resolvedSeries.Count; i++)
            {
                var label = new Label(resolvedSeries[i].Label)
                {
                    style =
                    {
                        marginRight = 12,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                };
                label.style.color = McCoyOkunMiniChart.SeriesColor(i);
                legend.Add(label);
            }
            root.Add(legend);
        }
        return root;
    }

    static IEnumerable<MiniChartSeries> TrajectorySeries<T>(IEnumerable<T> rows, string rangeUnit) where T : McCoyPlusRow
    {
        foreach (var row in rows ?? Enumerable.Empty<T>())
        {
            yield return new MiniChartSeries
            {
                Label = $"{F(row.Range, 0)} {rangeUnit}",
                Points = row.Trajectory.Select(point => new Vector2((float)point.Range, (float)point.HeightInches)).ToList()
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
                    label.text = rows != null && index >= 0 && index < rows.Count
                        ? column.Selector(rows[index])
                        : "";
                }
            });
        }
        root.Add(listView);
        return root;
    }

    static VisualElement Pre(string title, IEnumerable<string> lines)
    {
        var root = new VisualElement { style = { marginTop = 6 } };
        root.Add(Section(title));
        var label = new Label(string.Join("\n", lines ?? Enumerable.Empty<string>()));
        label.style.whiteSpace = WhiteSpace.Normal;
        root.Add(label);
        return root;
    }

    static string F(double? value, int digits = 1)
        => value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("0." + new string('#', digits), CultureInfo.InvariantCulture)
            : "-";

    static FacehardBridgeResult FacehardBridgeCalculate(FacehardBridgeInput input, FacehardInput template)
    {
        var facehard = new FacehardInput
        {
            ArmorId = template?.ArmorId ?? FacehardInput.CreateDefault().ArmorId,
            ProjectilePresetId = template?.ProjectilePresetId ?? FacehardInput.CreateDefault().ProjectilePresetId,
            PlateThickness = input.PlateThickness,
            ProjectileDiameter = input.ProjectileDiameter,
            ProjectileWeight = template?.ProjectileWeight ?? facehardWeightFallback,
            ProjectileBodyWeight = template?.ProjectileBodyWeight ?? facehardWeightFallback,
            StrikingVelocity = input.StrikingVelocity,
            Obliquity = input.Obliquity,
            ProjectileLimitQuality = template?.ProjectileLimitQuality ?? 1,
            ProjectileDamageQuality = template?.ProjectileDamageQuality ?? 1,
            CapType = template?.CapType ?? "hard",
            CurvedPlate = template?.CurvedPlate ?? false,
            WoodBackingThickness = template?.WoodBackingThickness ?? 0,
            CementBackingThickness = template?.CementBackingThickness ?? 0,
            MetalBackingThickness = template?.MetalBackingThickness ?? 0,
            BackingQuality = template?.BackingQuality ?? 1,
            BackingPlates = template?.BackingPlates ?? 1,
            NoseSchema = template?.NoseSchema ?? "standard",
            JapaneseCapHead = template?.JapaneseCapHead ?? 2,
            NoseCondition = template?.NoseCondition ?? "intact",
            WindscreenWeight = template?.WindscreenWeight ?? 0,
            WindscreenCapHeadWeight = template?.WindscreenCapHeadWeight ?? 0,
        };
        return new FacehardBridgeResult
        {
            NavyBl = FacehardCalculator.CalculateFacehardNavyBl(facehard),
        };
    }

    const double facehardWeightFallback = 1500;
}

public sealed class McCoyOkunMiniChart : VisualElement
{
    static readonly Color[] ChartColors =
    {
        HtmlColor("#2d7567"),
        HtmlColor("#9c5a2e"),
        HtmlColor("#4f6597"),
        HtmlColor("#7b6a3a"),
    };

    readonly List<McCoyOkunCalculatorDialog.MiniChartSeries> Series = new();

    public McCoyOkunMiniChart()
    {
        style.height = 150;
        style.flexGrow = 1;
        generateVisualContent += OnGenerateVisualContent;
    }

    public static Color SeriesColor(int index)
    {
        return ChartColors[Mathf.Abs(index) % ChartColors.Length];
    }

    public void SetPoints(IEnumerable<Vector2> newPoints)
    {
        SetSeries(new[]
        {
            new McCoyOkunCalculatorDialog.MiniChartSeries
            {
                Label = "",
                Points = (newPoints ?? Enumerable.Empty<Vector2>()).ToList()
            }
        });
    }

    public void SetSeries(IEnumerable<McCoyOkunCalculatorDialog.MiniChartSeries> newSeries)
    {
        Series.Clear();
        if (newSeries != null)
        {
            foreach (var item in newSeries)
            {
                var points = item?.Points?.Where(point => float.IsFinite(point.x) && float.IsFinite(point.y)).ToList() ?? new();
                if (points.Count == 0)
                    continue;
                Series.Add(new McCoyOkunCalculatorDialog.MiniChartSeries
                {
                    Label = item.Label,
                    Points = points
                });
            }
        }
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var rect = contentRect;
        var allPoints = Series.SelectMany(item => item.Points).ToList();
        if (rect.width <= 8 || rect.height <= 8 || allPoints.Count < 2)
            return;

        var minX = allPoints.Min(point => point.x);
        var maxX = allPoints.Max(point => point.x);
        var minY = allPoints.Min(point => point.y);
        var maxY = allPoints.Max(point => point.y);
        if (Mathf.Approximately(minX, maxX) || Mathf.Approximately(minY, maxY))
            return;

        var plot = new Rect(rect.x + 42, rect.y + 12, rect.width - 56, rect.height - 34);
        var painter = context.painter2D;
        painter.lineWidth = 1;
        painter.strokeColor = HtmlColor("#aab3ab");
        painter.BeginPath();
        painter.MoveTo(new Vector2(plot.xMin, plot.yMin));
        painter.LineTo(new Vector2(plot.xMin, plot.yMax));
        painter.LineTo(new Vector2(plot.xMax, plot.yMax));
        painter.Stroke();

        painter.lineWidth = 3;
        for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
        {
            var points = Series[seriesIndex].Points;
            if (points.Count < 2)
                continue;
            painter.strokeColor = SeriesColor(seriesIndex);
            painter.BeginPath();
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var x = Mathf.Lerp(plot.xMin, plot.xMax, (point.x - minX) / (maxX - minX));
                var y = Mathf.Lerp(plot.yMax, plot.yMin, (point.y - minY) / (maxY - minY));
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    static Color HtmlColor(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
    }
}
