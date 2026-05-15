using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

using NavalCombatCore;
using YYZ;
using YYZ.Ballistic;

public sealed class McCoyOkunCalculatorDialog
{
    enum McCoyOkunChartMode
    {
        Trajectory,
        Penetration
    }

    enum McCoyOkunTab
    {
        M79Apclc,
        Facehard69,
        McCoy,
        McCoyPlus,
        McCoyPlusFacehard,
        McCoyPlusFacehardM79,
        McCoyPlusM79,
        Jbm
    }

    enum JbmProgramMode
    {
        McDrag,
        McGyro,
        IntLift
    }

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

    sealed class TemplatePageSurface
    {
        public VisualElement Root;
        public ScrollView Input;
        public ScrollView Output;
        public VisualElement OutputContent;
    }

    sealed class TemplateFloatBinding
    {
        public bool Updating;
        public double? Min;
        public Action<double> Setter;
    }

    sealed class DropdownOption<T>
    {
        public T Value;
        public string Label;

        public DropdownOption(T value, string label)
        {
            Value = value;
            Label = label;
        }
    }

    sealed class TemplateDropdownBinding<T>
    {
        public bool Updating;
        public T Value;
        public List<DropdownOption<T>> Options = new();
        public Action<T> Setter;
    }

    sealed class TemplateTextBinding
    {
        public bool Updating;
        public Action<string> Setter;
    }

    sealed class TemplateToggleBinding
    {
        public bool Updating;
        public Action<bool> Setter;
    }

    sealed class TemplateButtonBinding
    {
        public Action Clicked;
    }

    readonly McCoyOkunTab[] tabs =
    {
        McCoyOkunTab.M79Apclc,
        McCoyOkunTab.Facehard69,
        McCoyOkunTab.McCoy,
        McCoyOkunTab.McCoyPlus,
        McCoyOkunTab.McCoyPlusFacehard,
        McCoyOkunTab.McCoyPlusFacehardM79,
        McCoyOkunTab.McCoyPlusM79,
        McCoyOkunTab.Jbm
    };

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
    static List<string> LocalizedChoices(params string[] keys) => keys.Select(key => Localize(key)).ToList();
    static List<string> LocalizedChoices(IEnumerable<string> keys) => keys.Select(key => Localize(key)).ToList();
    static DropdownOption<T> Option<T>(T value, string labelKey) => new(value, Localize(labelKey));
    static List<DropdownOption<T>> Options<T>(params DropdownOption<T>[] options) => options.ToList();

    TabView templateTabView;
    readonly Dictionary<McCoyOkunTab, Tab> templateTabs = new();
    readonly Dictionary<McCoyOkunTab, TemplatePageSurface> templatePages = new();
    readonly HashSet<McCoyOkunTab> outputBuiltTabs = new();
    bool templateBackedTabs;
    bool templateBackedPages;
    McCoyOkunTab activeTab = McCoyOkunTab.M79Apclc;

    M79Input m79Input = M79.DefaultInput();
    string m79SampleId = "";
    FacehardInput facehardInput = FacehardCalculator.DefaultFacehardInput();
    string facehardSampleId = "";
    readonly List<FacehardRecordedRun> facehardRecordedRuns = new();
    McCoyInput mccoyInput = McCoy.DefaultInput();
    string mccoyDragText = McCoy.DragTableToText(McCoy.DefaultDragTable());
    McCoyPlusInput mccoyPlusInput = McCoyPlus.DefaultInput();
    McCoyPlusDragFunction mccoyPlusPreset = McCoyPlusDragFunction.G1;
    string mccoyPlusDragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    McCoyPlusFacehardInput mccoyPlusFacehardInput = McCoyPlusFacehard.DefaultInput();
    FacehardInput mccoyPlusFacehardDetails = FacehardCalculator.DefaultFacehardInput();
    string mccoyPlusFacehardSampleId = "";
    McCoyPlusDragFunction mccoyPlusFacehardPreset = McCoyPlusDragFunction.G1;
    string mccoyPlusFacehardDragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    McCoyOkunChartMode mccoyPlusFacehardChartMode = McCoyOkunChartMode.Trajectory;
    McCoyPlusFacehardM79Input mccoyPlusFacehardM79Input = McCoyPlusFacehardM79.DefaultInput();
    FacehardInput mccoyPlusFacehardM79Details = FacehardCalculator.DefaultFacehardInput();
    string mccoyPlusFacehardM79SampleId = "";
    McCoyPlusDragFunction mccoyPlusFacehardM79Preset = McCoyPlusDragFunction.G1;
    string mccoyPlusFacehardM79DragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    McCoyOkunChartMode mccoyPlusFacehardM79ChartMode = McCoyOkunChartMode.Trajectory;
    McCoyPlusM79Input mccoyPlusM79Input = McCoyPlusM79.DefaultInput();
    string mccoyPlusM79SampleId = "";
    McCoyPlusDragFunction mccoyPlusM79Preset = McCoyPlusDragFunction.G1;
    string mccoyPlusM79DragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
    McCoyOkunChartMode mccoyPlusM79ChartMode = McCoyOkunChartMode.Trajectory;
    JbmProgramMode jbmMode = JbmProgramMode.McDrag;
    McDragInput mcDragInput = Jbm.DefaultMcDragInput();
    McGyroInput mcGyroInput = Jbm.DefaultMcGyroInput();
    IntLiftInput intLiftInput = Jbm.DefaultIntLiftInput();

    sealed class FacehardRecordedRun
    {
        public double Obliquity;
        public double Velocity;
        public double NavyBl;
        public double HolingBl;
        public double EffectiveBl;
        public FacehardStatus Status;
        public string Penetration;
    }

    public VisualElement BuildContent(VisualTreeAsset template)
    {
        if (template == null)
            throw new InvalidOperationException("McCoy Okun Calculator requires McCoyOkunCalculatorDialog.uxml to be assigned.");

        templateTabs.Clear();
        templatePages.Clear();
        outputBuiltTabs.Clear();
        templateBackedTabs = false;
        templateBackedPages = false;

        var root = template.CloneTree();
        root.style.position = Position.Relative;
        root.style.flexGrow = 1;
        root.style.flexShrink = 1;

        var calculatorRoot = root.Q<VisualElement>("McCoyOkunCalculatorRoot") ?? root;
        calculatorRoot.style.flexGrow = 1;
        calculatorRoot.style.flexShrink = 1;

        templateTabView = root.Q<TabView>("McCoyOkunTabView");
        if (templateTabView == null)
            throw new InvalidOperationException("McCoyOkunCalculatorDialog.uxml is missing McCoyOkunTabView.");

        WireTemplateTabs(root);
        WireTemplatePages(root);
        if (!templateBackedTabs)
            throw new InvalidOperationException("McCoyOkunCalculatorDialog.uxml is missing one or more tabs.");
        if (!templateBackedPages)
            throw new InvalidOperationException("McCoyOkunCalculatorDialog.uxml is missing one or more page/input/output containers.");
        templateTabView.selectedTabIndex = Math.Max(0, Array.IndexOf(tabs, activeTab));
        RebuildContent();
        return root;
    }

    void WireTemplateTabs(VisualElement root)
    {
        templateTabView.activeTabChanged += OnActiveTabChanged;

        var uxmlTabs = templateTabView.Children().OfType<Tab>().ToList();
        for (var index = 0; index < tabs.Length; index++)
        {
            if (index >= uxmlTabs.Count)
                continue;

            templateTabs[tabs[index]] = uxmlTabs[index];
        }

        templateBackedTabs = templateTabs.Count == tabs.Length;
    }

    void OnActiveTabChanged(Tab previousTab, Tab newTab)
    {
        var pair = templateTabs.FirstOrDefault(item => item.Value == newTab);
        if (pair.Value != null)
            SelectTab(pair.Key);
    }

    void SelectTab(McCoyOkunTab tab)
    {
        if (!templateTabs.ContainsKey(tab))
            return;

        activeTab = tab;
        RebuildContent(false);
    }

    void RebuildContent(bool rebuildOutput = true)
    {
        if (!templateBackedPages)
            throw new InvalidOperationException("McCoy Okun page surfaces have not been wired from UXML.");
        RebuildTemplateContent(rebuildOutput);
    }

    void WireTemplatePages(VisualElement root)
    {
        foreach (var tab in tabs)
        {
            var page = root.Q<VisualElement>(PageName(tab));
            var input = root.Q<ScrollView>(InputName(tab));
            var output = root.Q<ScrollView>(OutputName(tab));
            if (page == null || input == null || output == null)
                continue;

            templatePages[tab] = new TemplatePageSurface
            {
                Root = page,
                Input = input,
                Output = output,
                OutputContent = root.Q<VisualElement>($"{TabElementPrefix(tab)}OutputContent") ?? output.contentContainer
            };
        }

        templateBackedPages = templatePages.Count == tabs.Length;
    }

    void RebuildTemplateContent(bool rebuildOutput)
    {
        if (!templatePages.TryGetValue(activeTab, out var activeSurface))
            throw new InvalidOperationException($"McCoyOkunCalculatorDialog.uxml is missing page surface for {TabLabel(activeTab)}.");

        ConfigureTemplateInputs(activeTab, activeSurface.Root);
        ConfigureTemplateOutputControls(activeTab, activeSurface.Root);
        if (rebuildOutput || !outputBuiltTabs.Contains(activeTab))
        {
            activeSurface.OutputContent.Clear();
            BuildTemplateOutput(activeTab, activeSurface.OutputContent);
            outputBuiltTabs.Add(activeTab);
        }
    }

    void ConfigureTemplateInputs(McCoyOkunTab tab, VisualElement root)
    {
        switch (tab)
        {
            case McCoyOkunTab.Facehard69:
                ConfigureFacehardTemplate(root, "Facehard69", facehardInput, FacehardCalculator.CalculateFacehard(facehardInput), true);
                BindButton(root, "Facehard69ResetButton", () =>
                {
                    facehardInput = FacehardCalculator.DefaultFacehardInput();
                    facehardSampleId = "";
                    facehardRecordedRuns.Clear();
                    RebuildContent();
                });
                BindSample(root, "Facehard69SampleField", facehardSampleId, id =>
                {
                    facehardSampleId = id;
                    var sample = SampleById(id);
                    if (sample != null)
                        ApplyFacehardSample(facehardInput, sample);
                });
                BindButton(root, "Facehard69RecordButton", () =>
                {
                    var result = FacehardCalculator.CalculateFacehard(facehardInput);
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
                });
                BindButton(root, "Facehard69ClearButton", () =>
                {
                    facehardRecordedRuns.Clear();
                    RebuildContent();
                });
                break;
            case McCoyOkunTab.McCoy:
                BindButton(root, "McCoyResetButton", () =>
                {
                    mccoyInput = McCoy.DefaultInput();
                    mccoyDragText = McCoy.DragTableToText(McCoy.DefaultDragTable());
                    RebuildContent();
                });
                BindDropdown(root, "McCoyAtmosphereField", LocalizedChoices("Army Standard Metro", "ICAO"), mccoyInput.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, index => mccoyInput.Atmosphere = index == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro);
                BindFloat(root, "McCoyMuzzleVelocityField", mccoyInput.MuzzleVelocity, value => mccoyInput.MuzzleVelocity = value, 1);
                BindFloat(root, "McCoyBallisticCoefficientField", mccoyInput.BallisticCoefficient, value => mccoyInput.BallisticCoefficient = value, 0.001);
                BindFloat(root, "McCoySightHeightField", mccoyInput.SightHeight, value => mccoyInput.SightHeight = value);
                BindFloat(root, "McCoyElevationMinutesField", mccoyInput.ElevationMinutes, value => mccoyInput.ElevationMinutes = value);
                BindFloat(root, "McCoyDensityRatioField", mccoyInput.DensityRatio, value => mccoyInput.DensityRatio = value, 0.001);
                BindFloat(root, "McCoyTemperatureField", mccoyInput.TemperatureF, value => mccoyInput.TemperatureF = value);
                BindFloat(root, "McCoyPrintIntervalField", mccoyInput.PrintInterval, value => mccoyInput.PrintInterval = value, 1);
                BindFloat(root, "McCoyMaxRangeField", mccoyInput.MaxRange, value => mccoyInput.MaxRange = value, 1);
                BindFloat(root, "McCoyRangeWindField", mccoyInput.RangeWindMph, value => mccoyInput.RangeWindMph = value);
                BindFloat(root, "McCoyCrossWindField", mccoyInput.CrossWindMph, value => mccoyInput.CrossWindMph = value);
                BindFloat(root, "McCoyMatchRangeField", mccoyInput.MatchRange, value => mccoyInput.MatchRange = value, 0);
                BindFloat(root, "McCoyMatchHeightField", mccoyInput.MatchHeight, value => mccoyInput.MatchHeight = value);
                BindText(root, "McCoyDragTableField", mccoyDragText, value => mccoyDragText = value);
                break;
            case McCoyOkunTab.McCoyPlus:
                BindButton(root, "McCoyPlusResetButton", () =>
                {
                    mccoyPlusInput = McCoyPlus.DefaultInput();
                    mccoyPlusPreset = McCoyPlusDragFunction.G1;
                    mccoyPlusDragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
                    RebuildContent();
                });
                ConfigureMcCoyPlusTemplate(root, "McCoyPlus", mccoyPlusInput, () => mccoyPlusPreset, value => mccoyPlusPreset = value, () => mccoyPlusDragText, value => mccoyPlusDragText = value);
                break;
            case McCoyOkunTab.McCoyPlusFacehard:
                SyncComboMcCoy(mccoyPlusFacehardInput.McCoy, mccoyPlusFacehardDragText);
                SyncFacehardBridge();
                var facehardPreview = FacehardCalculator.CalculateFacehard(mccoyPlusFacehardDetails, false);
                BindButton(root, "McCoyPlusFacehardResetButton", () =>
                {
                    mccoyPlusFacehardInput = McCoyPlusFacehard.DefaultInput();
                    mccoyPlusFacehardDetails = FacehardCalculator.DefaultFacehardInput();
                    mccoyPlusFacehardSampleId = "";
                    mccoyPlusFacehardPreset = McCoyPlusDragFunction.G1;
                    mccoyPlusFacehardDragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
                    mccoyPlusFacehardChartMode = McCoyOkunChartMode.Trajectory;
                    RebuildContent();
                });
                BindSample(root, "McCoyPlusFacehardSampleField", mccoyPlusFacehardSampleId, id =>
                {
                    mccoyPlusFacehardSampleId = id;
                    var sample = SampleById(id);
                    if (sample != null)
                    {
                        ApplyMcCoyPlusSample(mccoyPlusFacehardInput.McCoy, sample, value => mccoyPlusFacehardPreset = value, value => mccoyPlusFacehardDragText = value);
                        ApplyFacehardSample(mccoyPlusFacehardDetails, sample);
                        SyncFacehardBridge();
                    }
                });
                ConfigureMcCoyPlusTemplate(root, "McCoyPlusFacehard", mccoyPlusFacehardInput.McCoy, () => mccoyPlusFacehardPreset, value => mccoyPlusFacehardPreset = value, () => mccoyPlusFacehardDragText, value => mccoyPlusFacehardDragText = value);
                ConfigureFacehardComboTemplate(root, "McCoyPlusFacehard", mccoyPlusFacehardDetails, facehardPreview);
                BindText(root, "McCoyPlusFacehardDragTableField", mccoyPlusFacehardDragText, value =>
                {
                    mccoyPlusFacehardDragText = value;
                    mccoyPlusFacehardInput.McCoy.DragTable = McCoy.NormalizeDragTable(value);
                });
                break;
            case McCoyOkunTab.McCoyPlusFacehardM79:
                SyncComboMcCoy(mccoyPlusFacehardM79Input.McCoy, mccoyPlusFacehardM79DragText);
                SyncFacehardM79Bridge();
                var facehardM79Preview = FacehardCalculator.CalculateFacehard(mccoyPlusFacehardM79Details, false);
                BindButton(root, "McCoyPlusFacehardM79ResetButton", () =>
                {
                    mccoyPlusFacehardM79Input = McCoyPlusFacehardM79.DefaultInput();
                    mccoyPlusFacehardM79Details = FacehardCalculator.DefaultFacehardInput();
                    mccoyPlusFacehardM79SampleId = "";
                    mccoyPlusFacehardM79Preset = McCoyPlusDragFunction.G1;
                    mccoyPlusFacehardM79DragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
                    mccoyPlusFacehardM79ChartMode = McCoyOkunChartMode.Trajectory;
                    RebuildContent();
                });
                BindSample(root, "McCoyPlusFacehardM79SampleField", mccoyPlusFacehardM79SampleId, id =>
                {
                    mccoyPlusFacehardM79SampleId = id;
                    var sample = SampleById(id);
                    if (sample != null)
                    {
                        ApplyMcCoyPlusSample(mccoyPlusFacehardM79Input.McCoy, sample, value => mccoyPlusFacehardM79Preset = value, value => mccoyPlusFacehardM79DragText = value);
                        ApplyFacehardSample(mccoyPlusFacehardM79Details, sample);
                        ApplyM79Sample(mccoyPlusFacehardM79Input.M79, sample);
                        SyncFacehardM79Bridge();
                    }
                });
                ConfigureMcCoyPlusFacehardM79Template(root, facehardM79Preview);
                break;
            case McCoyOkunTab.McCoyPlusM79:
                SyncComboMcCoy(mccoyPlusM79Input.McCoy, mccoyPlusM79DragText);
                BindButton(root, "McCoyPlusM79ResetButton", () =>
                {
                    mccoyPlusM79Input = McCoyPlusM79.DefaultInput();
                    mccoyPlusM79SampleId = "";
                    mccoyPlusM79Preset = McCoyPlusDragFunction.G1;
                    mccoyPlusM79DragText = McCoyPlus.DragPresetToText(McCoyPlusDragFunction.G1);
                    mccoyPlusM79ChartMode = McCoyOkunChartMode.Trajectory;
                    RebuildContent();
                });
                BindSample(root, "McCoyPlusM79SampleField", mccoyPlusM79SampleId, id =>
                {
                    mccoyPlusM79SampleId = id;
                    var sample = SampleById(id);
                    if (sample != null)
                    {
                        ApplyMcCoyPlusSample(mccoyPlusM79Input.McCoy, sample, value => mccoyPlusM79Preset = value, value => mccoyPlusM79DragText = value);
                        ApplyM79Sample(mccoyPlusM79Input.M79, sample);
                    }
                });
                ConfigureM79ComboTemplate(root, "McCoyPlusM79", mccoyPlusM79Input.M79, mccoyPlusM79Input.McCoy, () => mccoyPlusM79Preset, value => mccoyPlusM79Preset = value, () => mccoyPlusM79DragText, value => mccoyPlusM79DragText = value, () => mccoyPlusM79SampleId = "");
                BindText(root, "McCoyPlusM79DragTableField", mccoyPlusM79DragText, value =>
                {
                    mccoyPlusM79DragText = value;
                    mccoyPlusM79Input.McCoy.DragTable = McCoy.NormalizeDragTable(value);
                });
                break;
            case McCoyOkunTab.Jbm:
                BindButton(root, "JbmResetButton", () =>
                {
                    mcDragInput = Jbm.DefaultMcDragInput();
                    mcGyroInput = Jbm.DefaultMcGyroInput();
                    intLiftInput = Jbm.DefaultIntLiftInput();
                    RebuildContent();
                });
                BindDropdown(root, "JbmProgramField", LocalizedChoices("MCDRAG", "MCGYRO", "INTLIFT"), JbmProgramIndex(jbmMode),
                    index => jbmMode = index == 1 ? JbmProgramMode.McGyro : index == 2 ? JbmProgramMode.IntLift : JbmProgramMode.McDrag);
                ConfigureJbmTemplate(root);
                break;
            default:
                BindButton(root, "M79ApclcResetButton", () =>
                {
                    m79Input = M79.DefaultInput();
                    m79SampleId = "";
                    RebuildContent();
                });
                BindSample(root, "M79ApclcSampleField", m79SampleId, id =>
                {
                    m79SampleId = id;
                    var sample = SampleById(id);
                    if (sample != null)
                        ApplyM79Sample(m79Input, sample);
                });
                ConfigureM79Template(root, "M79Apclc", m79Input);
                break;
        }
    }

    void ConfigureTemplateOutputControls(McCoyOkunTab tab, VisualElement root)
    {
        SetDisplay(root, "McCoyPlusFacehardChartModeField", tab == McCoyOkunTab.McCoyPlusFacehard);
        SetDisplay(root, "McCoyPlusFacehardM79ChartModeField", tab == McCoyOkunTab.McCoyPlusFacehardM79);
        SetDisplay(root, "McCoyPlusM79ChartModeField", tab == McCoyOkunTab.McCoyPlusM79);
        if (tab == McCoyOkunTab.McCoyPlusFacehard)
            BindDropdown(root, "McCoyPlusFacehardChartModeField", LocalizedChoices("Matched Trajectories", "Penetration By Range"), mccoyPlusFacehardChartMode == McCoyOkunChartMode.Trajectory ? 0 : 1,
                index => mccoyPlusFacehardChartMode = index == 0 ? McCoyOkunChartMode.Trajectory : McCoyOkunChartMode.Penetration);
        if (tab == McCoyOkunTab.McCoyPlusFacehardM79)
            BindDropdown(root, "McCoyPlusFacehardM79ChartModeField", LocalizedChoices("Matched Trajectories", "Penetration By Range"), mccoyPlusFacehardM79ChartMode == McCoyOkunChartMode.Trajectory ? 0 : 1,
                index => mccoyPlusFacehardM79ChartMode = index == 0 ? McCoyOkunChartMode.Trajectory : McCoyOkunChartMode.Penetration);
        if (tab == McCoyOkunTab.McCoyPlusM79)
            BindDropdown(root, "McCoyPlusM79ChartModeField", LocalizedChoices("Matched Trajectories", "Penetration By Range"), mccoyPlusM79ChartMode == McCoyOkunChartMode.Trajectory ? 0 : 1,
                index => mccoyPlusM79ChartMode = index == 0 ? McCoyOkunChartMode.Trajectory : McCoyOkunChartMode.Penetration);
    }

    void BuildTemplateOutput(McCoyOkunTab tab, VisualElement output)
    {
        switch (tab)
        {
            case McCoyOkunTab.Facehard69:
                BuildFacehardOutput(output);
                break;
            case McCoyOkunTab.McCoy:
                BuildMcCoyOutput(output);
                break;
            case McCoyOkunTab.McCoyPlus:
                BuildMcCoyPlusOutput(output);
                break;
            case McCoyOkunTab.McCoyPlusFacehard:
                BuildMcCoyPlusFacehardOutput(output);
                break;
            case McCoyOkunTab.McCoyPlusFacehardM79:
                BuildMcCoyPlusFacehardM79Output(output);
                break;
            case McCoyOkunTab.McCoyPlusM79:
                BuildMcCoyPlusM79Output(output);
                break;
            case McCoyOkunTab.Jbm:
                BuildJbmOutput(output);
                break;
            default:
                BuildM79Output(output);
                break;
        }
    }

    void BuildM79Output(VisualElement output)
    {
        var result = M79.Calculate(m79Input);
        output.Add(Banner(("Navy BL", F(result.NavyBallisticLimitRounded), "ft/s"),
            ("Mode", BallisticOptions.ToLegacyCode(result.PenetrationMode), ""),
            ("Remaining Velocity", F(result.RemainingVelocity), "ft/s")));
        output.Add(Chart("Thickness Scan", M79.Scan(m79Input, m79Input.ProjectileDiameter * 6, 80)
            .Select(row => new Vector2((float)row.Thickness, (float)row.Result.NavyBallisticLimit))));
        output.Add(Pre("M79 Legacy BASIC Report", result.LegacyReport));
    }

    void BuildFacehardOutput(VisualElement output)
    {
        var result = FacehardCalculator.CalculateFacehard(facehardInput);
        output.Add(Banner(("NBL", F(result.NavyBl), "ft/s"),
            ("HBL", F(result.HolingBl), "ft/s"),
            ("Status", BallisticOptions.ToLegacyCode(result.Status), "")));
        output.Add(Warnings(result.Notes));
        output.Add(Pre("Recorded Runs", FacehardRunLines(result)));
        output.Add(Pre("Metrics", FacehardMetricLines(result)));
        output.Add(Pre("Facehard69 Legacy BASIC Report", result.Legacy.Report));
        output.Add(Pre("Process Report", result.Legacy.ProcessReport));
    }

    void BuildMcCoyOutput(VisualElement output)
    {
        mccoyInput.DragTable = McCoy.NormalizeDragTable(mccoyDragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoy.Calculate(mccoyInput);
        stopwatch.Stop();
        var last = result.Points.LastOrDefault();
        output.Add(Banner(("Terminal Velocity", F(last?.Velocity), "ft/s"),
            ("Terminal Height", F(last?.HeightInches), "in"),
            ("Elevation Used", F(result.AdjustedElevationMinutes, 3), "min")));
        output.Add(Warnings(result.Warnings));
        output.Add(Chart("Height Curve", result.Points.Select(point => new Vector2((float)point.Range, (float)point.HeightInches))));
        output.Add(Chart("Velocity Curve", result.Points.Select(point => new Vector2((float)point.Range, (float)point.Velocity))));
        output.Add(Pre("McCoy Legacy BASIC Report", result.LegacyReport));
        output.Add(CalculationTime(stopwatch.Elapsed));
        output.Add(Table("Trajectory Points", result.Points.Take(80).ToList(),
            Col<TrajectoryPoint>("range", "Range", 90, point => F(point.Range, 0)),
            Col<TrajectoryPoint>("height", "Height", 90, point => F(point.HeightInches, 1)),
            Col<TrajectoryPoint>("deflection", "Defl.", 90, point => F(point.DeflectionInches, 1)),
            Col<TrajectoryPoint>("velocity", "Vel", 80, point => F(point.Velocity, 0)),
            Col<TrajectoryPoint>("time", "Time", 90, point => F(point.Time, 3)),
            Col<TrajectoryPoint>("vx", "VX", 80, point => F(point.Vx, 0)),
            Col<TrajectoryPoint>("vy", "VY", 80, point => F(point.Vy, 0)),
            Col<TrajectoryPoint>("vz", "VZ", 80, point => F(point.Vz, 0))));
    }

    void BuildMcCoyPlusOutput(VisualElement output)
    {
        mccoyPlusInput.DragTable = McCoy.NormalizeDragTable(mccoyPlusDragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlus.CalculateParallel(mccoyPlusInput);
        stopwatch.Stop();
        var last = result.Rows.LastOrDefault();
        output.Add(Banner(("Solved Ranges", F(result.Rows.Count, 0), "Rows"),
            ("Maximum Range", F(last?.Range), BallisticOptions.ToLegacyCode(mccoyPlusInput.RangeUnit)),
            ("Last Velocity", F(last?.Velocity), "ft/s")));
        output.Add(Warnings(result.Warnings));
        output.Add(ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusInput.RangeUnit), BallisticOptions.ToLegacyCode(mccoyPlusInput.RangeUnit), "ft"));
        output.Add(CalculationTime(stopwatch.Elapsed));
        output.Add(Table("Range Sweep", result.Rows,
            Col<McCoyPlusRow>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusRow>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusRow>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusRow>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusRow>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3))));
    }

    void BuildMcCoyPlusFacehardOutput(VisualElement output)
    {
        SyncComboMcCoy(mccoyPlusFacehardInput.McCoy, mccoyPlusFacehardDragText);
        SyncFacehardBridge();
        McCoyPlusFacehard.FacehardCalculator = bridge => FacehardBridgeCalculate(bridge, mccoyPlusFacehardDetails);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusFacehard.Calculate(mccoyPlusFacehardInput);
        stopwatch.Stop();
        output.Add(Warnings(result.Warnings));
        output.Add(mccoyPlusFacehardChartMode == McCoyOkunChartMode.Trajectory
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusFacehardInput.McCoy.RangeUnit), BallisticOptions.ToLegacyCode(mccoyPlusFacehardInput.McCoy.RangeUnit), "ft")
            : Chart("Facehard Penetration", result.Rows.Where(row => row.PenetrationInches.HasValue).Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))));
        output.Add(CalculationTime(stopwatch.Elapsed));
        output.Add(Table("Rows", result.Rows,
            Col<McCoyPlusFacehardRow>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusFacehardRow>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusFacehardRow>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusFacehardRow>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusFacehardRow>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusFacehardRow>("penetration", "Penetration", 110, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusFacehardRow>("horizontal", "Horizontal Penetration", 160, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a")));
    }

    void BuildMcCoyPlusFacehardM79Output(VisualElement output)
    {
        SyncComboMcCoy(mccoyPlusFacehardM79Input.McCoy, mccoyPlusFacehardM79DragText);
        SyncFacehardM79Bridge();
        McCoyPlusFacehard.FacehardCalculator = bridge => FacehardBridgeCalculate(bridge, mccoyPlusFacehardM79Details);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusFacehardM79.Calculate(mccoyPlusFacehardM79Input);
        stopwatch.Stop();
        output.Add(Warnings(result.Warnings));
        output.Add(mccoyPlusFacehardM79ChartMode == McCoyOkunChartMode.Trajectory
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusFacehardM79Input.McCoy.RangeUnit), BallisticOptions.ToLegacyCode(mccoyPlusFacehardM79Input.McCoy.RangeUnit), "ft")
            : ChartSeries("Penetration By Range", PenetrationSeries(result.Rows), BallisticOptions.ToLegacyCode(mccoyPlusFacehardM79Input.McCoy.RangeUnit), "in"));
        output.Add(CalculationTime(stopwatch.Elapsed));
        output.Add(Table("Rows", result.Rows,
            Col<McCoyPlusFacehardM79Row>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusFacehardM79Row>("penetration", "V Pen", 90, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusFacehardM79Row>("horizontal", "H Pen", 90, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a"),
            Col<McCoyPlusFacehardM79Row>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusFacehardM79Row>("fall", "Fall Angle", 100, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusFacehardM79Row>("elevation", "Elevation", 90, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusFacehardM79Row>("time", "Time", 90, row => F(row.Time, 3))));
    }

    void BuildMcCoyPlusM79Output(VisualElement output)
    {
        SyncComboMcCoy(mccoyPlusM79Input.McCoy, mccoyPlusM79DragText);
        var stopwatch = Stopwatch.StartNew();
        var result = McCoyPlusM79.Calculate(mccoyPlusM79Input);
        stopwatch.Stop();
        output.Add(Warnings(result.Warnings));
        output.Add(mccoyPlusM79ChartMode == McCoyOkunChartMode.Trajectory
            ? ChartSeries("Matched Trajectories", TrajectorySeries(result.ChartRows, mccoyPlusM79Input.McCoy.RangeUnit), BallisticOptions.ToLegacyCode(mccoyPlusM79Input.McCoy.RangeUnit), "ft")
            : Chart("M79 Penetration", result.Rows.Where(row => row.PenetrationInches.HasValue).Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))));
        output.Add(CalculationTime(stopwatch.Elapsed));
        output.Add(Table("Rows", result.Rows,
            Col<McCoyPlusM79Row>("range", "Range", 90, row => F(row.Range, 0)),
            Col<McCoyPlusM79Row>("time", "Time", 90, row => F(row.Time, 3)),
            Col<McCoyPlusM79Row>("elevation", "Elevation (degree)", 140, row => F(row.ElevationDegrees, 4)),
            Col<McCoyPlusM79Row>("velocity", "Velocity", 90, row => F(row.Velocity, 0)),
            Col<McCoyPlusM79Row>("fall", "Fall Angle (degree)", 140, row => F(row.FallAngleDegrees, 3)),
            Col<McCoyPlusM79Row>("penetration", "Penetration", 110, row => F(row.PenetrationInches, 2)),
            Col<McCoyPlusM79Row>("horizontal", "Horizontal Penetration", 160, row => row.HorizontalPenetrationInches.HasValue ? F(row.HorizontalPenetrationInches, 2) : "n/a"),
            Col<McCoyPlusM79Row>("nbl", "M79 NBL", 100, row => F(row.M79NavyBallisticLimit, 0)),
            Col<McCoyPlusM79Row>("mode", "Mode", 160, row => row.PenetrationMode.HasValue ? BallisticOptions.ToLegacyCode(row.PenetrationMode.Value) : "outside-range"),
            Col<McCoyPlusM79Row>("remaining", "Remaining Velocity", 140, row => F(row.RemainingVelocity, 0))));
    }

    void BuildJbmOutput(VisualElement output)
    {
        if (jbmMode == JbmProgramMode.McGyro)
        {
            var result = Jbm.CalculateMcGyro(mcGyroInput);
            output.Add(Chart("Stability Factor", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.StabilityFactor))));
            output.Add(Pre("MCGYRO Report", result.LegacyReport));
        }
        else if (jbmMode == JbmProgramMode.IntLift)
        {
            var result = Jbm.CalculateIntLift(intLiftInput);
            output.Add(Warnings(result.Warnings));
            output.Add(Chart("CLA", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.Cla))));
            output.Add(Pre("INTLIFT Report", result.LegacyReport));
        }
        else
        {
            var result = Jbm.CalculateMcDrag(mcDragInput);
            output.Add(Warnings(result.Warnings));
            output.Add(Chart("CD0", result.Rows.Select(row => new Vector2((float)row.Mach, (float)row.Cd0))));
            output.Add(Pre("MCDRAG Report", result.LegacyReport));
        }
    }

    void ConfigureM79Template(VisualElement root, string prefix, M79Input target)
    {
        BindFloat(root, $"{prefix}ProjectileDiameterField", target.ProjectileDiameter, value => target.ProjectileDiameter = value, 0.001);
        BindFloat(root, $"{prefix}ProjectileWeightField", target.ProjectileWeight, value => target.ProjectileWeight = value, 0.001);
        BindFloat(root, $"{prefix}PlateThicknessField", target.PlateThickness, value => target.PlateThickness = value, 0.001);
        BindFloat(root, $"{prefix}PlateQualityField", target.PlateQuality, value => target.PlateQuality = value, 0.001);
        BindFloat(root, $"{prefix}ObliquityField", target.Obliquity, value => target.Obliquity = value, 0);
        BindFloat(root, $"{prefix}StrikingVelocityField", target.StrikingVelocity, value => target.StrikingVelocity = value, 1);
        BindFloat(root, $"{prefix}ElongationField", target.Elongation, value => target.Elongation = value, 10);
    }

    void ConfigureM79ComboTemplate(VisualElement root, string prefix, M79Input m79, McCoyPlusInput mccoy, Func<McCoyPlusDragFunction> getPreset, Action<McCoyPlusDragFunction> setPreset, Func<string> getDragText, Action<string> setDragText, Action onPresetChanged)
    {
        BindFloat(root, $"{prefix}ProjectileDiameterField", m79.ProjectileDiameter, value => m79.ProjectileDiameter = value, 0.001);
        BindFloat(root, $"{prefix}ProjectileWeightField", m79.ProjectileWeight, value => m79.ProjectileWeight = value, 0.001);
        BindFloat(root, $"{prefix}MuzzleVelocityField", mccoy.MuzzleVelocity, value => mccoy.MuzzleVelocity = value, 1);
        BindFloat(root, $"{prefix}BallisticCoefficientField", mccoy.BallisticCoefficient, value => mccoy.BallisticCoefficient = value, 0.001);
        BindFloat(root, $"{prefix}MaxRangeField", mccoy.MaxRange, value => mccoy.MaxRange = value, 1);
        BindFloat(root, $"{prefix}MatchHeightField", mccoy.MatchHeight, value => mccoy.MatchHeight = value);
        BindMcCoyPlusElevationSearch(root, prefix, mccoy);
        BindDropdown(root, $"{prefix}AtmosphereField", LocalizedChoices("Army Standard Metro", "ICAO"), mccoy.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, index => mccoy.Atmosphere = index == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro);
        BindFloat(root, $"{prefix}DensityRatioField", mccoy.DensityRatio, value => mccoy.DensityRatio = value, 0.001);
        BindFloat(root, $"{prefix}TemperatureField", mccoy.TemperatureF, value => mccoy.TemperatureF = value);
        BindFloat(root, $"{prefix}ArmorQualityField", m79.PlateQuality, value => m79.PlateQuality = value, 0.001);
        BindFloat(root, $"{prefix}ElongationField", m79.Elongation, value => m79.Elongation = value, 10);
        BindMcCoyPlusPreset(root, prefix, mccoy, getPreset, setPreset, setDragText, onPresetChanged);
    }

    void ConfigureMcCoyPlusFacehardM79Template(VisualElement root, FacehardResult facehardPreview)
    {
        const string prefix = "McCoyPlusFacehardM79";
        ConfigureFacehardProjectileTemplate(root, prefix, mccoyPlusFacehardM79Details, facehardPreview);
        BindMcCoyPlusPreset(root, prefix, mccoyPlusFacehardM79Input.McCoy, () => mccoyPlusFacehardM79Preset, value => mccoyPlusFacehardM79Preset = value, value => mccoyPlusFacehardM79DragText = value, () => mccoyPlusFacehardM79SampleId = "");
        BindFloat(root, $"{prefix}MuzzleVelocityField", mccoyPlusFacehardM79Input.McCoy.MuzzleVelocity, value => mccoyPlusFacehardM79Input.McCoy.MuzzleVelocity = value, 1);
        BindFloat(root, $"{prefix}BallisticCoefficientField", mccoyPlusFacehardM79Input.McCoy.BallisticCoefficient, value => mccoyPlusFacehardM79Input.McCoy.BallisticCoefficient = value, 0.001);
        BindFloat(root, $"{prefix}MaxRangeField", mccoyPlusFacehardM79Input.McCoy.MaxRange, value => mccoyPlusFacehardM79Input.McCoy.MaxRange = value, 1);

        ConfigureFacehardArmorTemplate(root, prefix, mccoyPlusFacehardM79Details, facehardPreview);

        BindFloat(root, $"{prefix}ArmorQualityField", mccoyPlusFacehardM79Input.M79.PlateQuality, value => mccoyPlusFacehardM79Input.M79.PlateQuality = value, 0.001);
        BindFloat(root, $"{prefix}ElongationField", mccoyPlusFacehardM79Input.M79.Elongation, value => mccoyPlusFacehardM79Input.M79.Elongation = value, 10);

        BindMcCoyPlusElevationSearch(root, prefix, mccoyPlusFacehardM79Input.McCoy);
        BindDropdown(root, $"{prefix}AtmosphereField", LocalizedChoices("Army Standard Metro", "ICAO"), mccoyPlusFacehardM79Input.McCoy.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, index => mccoyPlusFacehardM79Input.McCoy.Atmosphere = index == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro);
        BindFloat(root, $"{prefix}DensityRatioField", mccoyPlusFacehardM79Input.McCoy.DensityRatio, value => mccoyPlusFacehardM79Input.McCoy.DensityRatio = value, 0.001);
        BindFloat(root, $"{prefix}TemperatureField", mccoyPlusFacehardM79Input.McCoy.TemperatureF, value => mccoyPlusFacehardM79Input.McCoy.TemperatureF = value);
        BindFloat(root, $"{prefix}MatchHeightField", mccoyPlusFacehardM79Input.McCoy.MatchHeight, value => mccoyPlusFacehardM79Input.McCoy.MatchHeight = value);
        BindText(root, $"{prefix}DragTableField", mccoyPlusFacehardM79DragText, value =>
        {
            mccoyPlusFacehardM79DragText = value;
            mccoyPlusFacehardM79Input.McCoy.DragTable = McCoy.NormalizeDragTable(value);
        });
    }

    void ConfigureMcCoyPlusTemplate(VisualElement root, string prefix, McCoyPlusInput target, Func<McCoyPlusDragFunction> getPreset, Action<McCoyPlusDragFunction> setPreset, Func<string> getDragText, Action<string> setDragText)
    {
        BindMcCoyPlusElevationSearch(root, prefix, target);
        BindDropdown(root, $"{prefix}AtmosphereField", LocalizedChoices("Army Standard Metro", "ICAO"), target.Atmosphere == McCoyAtmosphere.Icao ? 1 : 0, index => target.Atmosphere = index == 1 ? McCoyAtmosphere.Icao : McCoyAtmosphere.StandardMetro);
        BindMcCoyPlusPreset(root, prefix, target, getPreset, setPreset, setDragText);
        BindFloat(root, $"{prefix}MuzzleVelocityField", target.MuzzleVelocity, value => target.MuzzleVelocity = value, 1);
        BindFloat(root, $"{prefix}BallisticCoefficientField", target.BallisticCoefficient, value => target.BallisticCoefficient = value, 0.001);
        BindFloat(root, $"{prefix}MaxRangeField", target.MaxRange, value => target.MaxRange = value, 1);
        BindFloat(root, $"{prefix}DensityRatioField", target.DensityRatio, value => target.DensityRatio = value, 0.001);
        BindFloat(root, $"{prefix}TemperatureField", target.TemperatureF, value => target.TemperatureF = value);
        BindFloat(root, $"{prefix}MatchHeightField", target.MatchHeight, value => target.MatchHeight = value);
        BindText(root, $"{prefix}DragTableField", getDragText(), value =>
        {
            setDragText(value);
            target.DragTable = McCoy.NormalizeDragTable(value);
        });
    }

    void BindMcCoyPlusElevationSearch(VisualElement root, string prefix, McCoyPlusInput target)
    {
        BindDropdown(root, $"{prefix}ElevationSearchField", LocalizedChoices("Cached Binary Search", "Matched Range"),
            target.ElevationSearchMode == McCoyPlusElevationSearchMode.MatchedRange ? 1 : 0,
            index => target.ElevationSearchMode = index == 1
                ? McCoyPlusElevationSearchMode.MatchedRange
                : McCoyPlusElevationSearchMode.CachedBinarySearch);
    }

    void BindMcCoyPlusPreset(VisualElement root, string prefix, McCoyPlusInput target, Func<McCoyPlusDragFunction> getPreset, Action<McCoyPlusDragFunction> setPreset, Action<string> setDragText, Action onPresetChanged = null)
    {
        var presets = McCoyPlus.DragPresets();
        BindDropdown(root, $"{prefix}PresetField", presets.Select(item => item.Label).ToList(), Mathf.Max(0, presets.FindIndex(item => item.Function == getPreset())), index =>
        {
            var preset = presets[Mathf.Max(0, index)];
            ApplyMcCoyPlusPreset(target, preset.Function, setPreset, setDragText);
            onPresetChanged?.Invoke();
        });
    }

    void ConfigureFacehardTemplate(VisualElement root, string prefix, FacehardInput target, FacehardResult preview, bool includeVelocity)
    {
        ConfigureFacehardProjectileTemplate(root, prefix, target, preview);
        if (includeVelocity)
        {
            BindFloat(root, $"{prefix}StrikingVelocityField", target.StrikingVelocity, value => target.StrikingVelocity = value, 1);
            BindFloat(root, $"{prefix}ObliquityField", target.Obliquity, value => target.Obliquity = value, 0);
        }
        BindDropdown(root, $"{prefix}ArmorTypeField", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == target.ArmorId),
            index => target.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, index)].Id);
        BindToggle(root, $"{prefix}CurvedPlateToggle", target.CurvedPlate, value => target.CurvedPlate = value);
        BindFloat(root, $"{prefix}PlateThicknessField", target.PlateThickness, value => target.PlateThickness = value, 0.001);
        BindFloat(root, $"{prefix}WoodBackingField", target.WoodBackingThickness, value => target.WoodBackingThickness = value, 0);
        BindFloat(root, $"{prefix}CementBackingField", target.CementBackingThickness, value => target.CementBackingThickness = value, 0);
        BindFloat(root, $"{prefix}MetalBackingField", target.MetalBackingThickness, value => target.MetalBackingThickness = value, 0);
        BindFloat(root, $"{prefix}BackingQualityField", target.BackingQuality, value => target.BackingQuality = value, 0.001);
        BindFloat(root, $"{prefix}BackingPlatesField", target.BackingPlates, value => target.BackingPlates = value, 0);
        UpdateFacehardInputWarnings(root, prefix, target, preview?.ResolvedCapType ?? ResolvedCapType(target));
    }

    void ConfigureFacehardComboTemplate(VisualElement root, string prefix, FacehardInput target, FacehardResult preview)
    {
        ConfigureFacehardProjectileTemplate(root, prefix, target, preview);
        ConfigureFacehardArmorTemplate(root, prefix, target, preview);
    }

    void ConfigureFacehardArmorTemplate(VisualElement root, string prefix, FacehardInput target, FacehardResult preview)
    {
        BindDropdown(root, $"{prefix}ArmorTypeField", FacehardCalculator.FacehardArmors.Select(item => item.Name).ToList(),
            FacehardCalculator.FacehardArmors.FindIndex(item => item.Id == target.ArmorId),
            index => target.ArmorId = FacehardCalculator.FacehardArmors[Mathf.Max(0, index)].Id);
        BindToggle(root, $"{prefix}CurvedPlateToggle", target.CurvedPlate, value => target.CurvedPlate = value);
        BindFloat(root, $"{prefix}WoodBackingField", target.WoodBackingThickness, value => target.WoodBackingThickness = value, 0);
        BindFloat(root, $"{prefix}CementBackingField", target.CementBackingThickness, value => target.CementBackingThickness = value, 0);
        BindFloat(root, $"{prefix}MetalBackingField", target.MetalBackingThickness, value => target.MetalBackingThickness = value, 0);
        BindFloat(root, $"{prefix}BackingQualityField", target.BackingQuality, value => target.BackingQuality = value, 0.001);
        BindFloat(root, $"{prefix}BackingPlatesField", target.BackingPlates, value => target.BackingPlates = value, 0);
        UpdateFacehardInputWarnings(root, prefix, target, preview?.ResolvedCapType ?? ResolvedCapType(target));
    }

    void ConfigureFacehardProjectileTemplate(VisualElement root, string prefix, FacehardInput target, FacehardResult preview)
    {
        var selectedPreset = preview?.ProjectilePreset
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == target.ProjectilePresetId)
            ?? FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == "custom");
        var selectedNation = selectedPreset?.Nation ?? "Custom";
        var resolvedCapType = preview?.ResolvedCapType ?? ResolvedCapType(target);
        var nations = FacehardCalculator.FacehardProjectileNations;
        BindDropdown(root, $"{prefix}ProjectileNationField", nations.Select(item => item.Name).ToList(),
            Mathf.Max(0, nations.FindIndex(item => item.Id == selectedNation)), index =>
            {
                var nation = nations[Mathf.Max(0, index)];
                target.ProjectilePresetId = nation.DefaultProjectileId;
            });

        var projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == selectedNation).ToList();
        if (projectileChoices.Count == 0)
            projectileChoices = FacehardCalculator.FacehardProjectilePresets.Where(item => item.Nation == "Custom").ToList();
        BindDropdown(root, $"{prefix}ProjectileTypeField", projectileChoices.Select(ProjectilePresetLabel).ToList(),
            Mathf.Max(0, projectileChoices.FindIndex(item => item.Id == target.ProjectilePresetId)),
            index => target.ProjectilePresetId = projectileChoices[Mathf.Max(0, index)].Id);

        var isCustom = target.ProjectilePresetId == "custom";
        var capLabels = LocalizedChoices("Hard AP cap", "Thin/Tough hard cap", "Soft AP cap", "Hood", "No cap");
        var capValues = new List<FacehardCapType> { FacehardCapType.Hard, FacehardCapType.ThinHard, FacehardCapType.Soft, FacehardCapType.Hood, FacehardCapType.None };
        BindDropdown(root, $"{prefix}CapOrHoodField", capLabels, Mathf.Max(0, capValues.IndexOf(resolvedCapType)), index => target.CapType = capValues[Mathf.Max(0, index)], isCustom);

        var schemaValues = new List<FacehardNoseSchema> { FacehardNoseSchema.Standard, FacehardNoseSchema.JapaneseCapHead };
        BindDropdown(root, $"{prefix}NoseSchemaField", LocalizedChoices("Standard", "Japanese Cap Head"), Mathf.Max(0, schemaValues.IndexOf(target.NoseSchema)), index =>
        {
            target.NoseSchema = schemaValues[Mathf.Max(0, index)];
            target.NoseCondition = FacehardNoseCondition.Intact;
        }, isCustom);

        SetDisplay(root, $"{prefix}JapaneseCapHeadField", isCustom && target.NoseSchema == FacehardNoseSchema.JapaneseCapHead);
        BindDropdown(root, $"{prefix}JapaneseCapHeadField", LocalizedChoices("Uncapped Type 91 AP", "Capped Type 88/91/1 APC"), target.JapaneseCapHead <= 1 ? 0 : 1, index =>
        {
            target.JapaneseCapHead = index == 0 ? 1 : 2;
            target.NoseCondition = FacehardNoseCondition.Intact;
        });

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        BindDropdown(root, $"{prefix}NoseConditionField", noseWeights.ConditionOptions.Select(NoseConditionLabel).ToList(),
            Mathf.Max(0, noseWeights.ConditionOptions.IndexOf(noseWeights.Condition)),
            index => target.NoseCondition = noseWeights.ConditionOptions[Mathf.Max(0, index)]);
        BindFloat(root, $"{prefix}ProjectileDiameterField", target.ProjectileDiameter, value => target.ProjectileDiameter = value, 0.001);
        BindFloat(root, $"{prefix}ProjectileWeightField", target.ProjectileWeight, value => target.ProjectileWeight = value, 0.001);
        BindFloat(root, $"{prefix}ProjectileBodyWeightField", target.ProjectileBodyWeight, value => target.ProjectileBodyWeight = value, 0.001);
        SetDisplay(root, $"{prefix}WindscreenCapHeadWeightField", noseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead && noseWeights.CapHead == 2);
        BindFloat(root, $"{prefix}WindscreenCapHeadWeightField", target.WindscreenCapHeadWeight, value => target.WindscreenCapHeadWeight = value, 0);
        SetDisplay(root, $"{prefix}WindscreenWeightField", noseWeights.SchemaKind == FacehardNoseSchema.Standard);
        BindFloat(root, $"{prefix}WindscreenWeightField", target.WindscreenWeight, value => target.WindscreenWeight = value, 0);
        BindFloat(root, $"{prefix}RemainingNoseWeightField", noseWeights.RemainWeight, _ => { }, 0, false, RemainingNoseWeightLabel(noseWeights.CapHead, resolvedCapType));
        BindFloat(root, $"{prefix}PlimField", isCustom ? target.ProjectileLimitQuality : selectedPreset?.ProjectileLimitQuality ?? target.ProjectileLimitQuality, value => target.ProjectileLimitQuality = value, 0.001, isCustom);
        BindFloat(root, $"{prefix}PdamField", isCustom ? target.ProjectileDamageQuality : selectedPreset?.ProjectileDamageQuality ?? target.ProjectileDamageQuality, value => target.ProjectileDamageQuality = value, 0.001, isCustom);
        BindProjectileFlag(root, $"{prefix}ShatResField", isCustom ? target.ShatterResistance : selectedPreset?.ShatterResistance ?? target.ShatterResistance, value => target.ShatterResistance = value, isCustom, LocalizedChoices(ShatterResistanceLabelKeys));
        BindProjectileFlag(root, $"{prefix}BreakUnderNblField", isCustom ? target.BreakUnderNbl : selectedPreset?.BreakUnderNbl ?? target.BreakUnderNbl, value => target.BreakUnderNbl = value, isCustom, LocalizedChoices(BreakUnderNblLabelKeys));
        BindProjectileFlag(root, $"{prefix}LightCaseField", isCustom ? target.LightCase : selectedPreset?.LightCase ?? target.LightCase, value => target.LightCase = value, isCustom, LocalizedChoices(LightCaseLabelKeys));
    }

    void BindProjectileFlag(VisualElement root, string name, double value, Action<double> setter, bool enabled, List<string> labels)
    {
        BindDropdown(root, name, labels, Mathf.Clamp((int)Math.Round(value), 0, labels.Count - 1), index => setter(index), enabled);
    }

    void UpdateFacehardInputWarnings(VisualElement root, string prefix, FacehardInput target, FacehardCapType resolvedCapType)
    {
        var warningRoot = root.Q<VisualElement>($"{prefix}InputWarnings");
        if (warningRoot == null)
            return;
        warningRoot.Clear();
        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        if (((noseWeights.Condition == FacehardNoseCondition.WindscreenRemoved && resolvedCapType != FacehardCapType.None) || noseWeights.Condition == FacehardNoseCondition.CapHeadRemoved) && noseWeights.RemainWeight <= 0)
            warningRoot.Add(Warnings(new[] { Localize("The selected lost-covering weight consumes all non-body weight; Facehard69 requires remaining cap/hood weight for this state.") }));
    }

    void ConfigureJbmTemplate(VisualElement root)
    {
        var geometry = jbmMode == JbmProgramMode.McGyro ? (JbmProjectileGeometryInput)mcGyroInput : jbmMode == JbmProgramMode.IntLift ? intLiftInput : mcDragInput;
        BindText(root, "JbmProjectileIdField", geometry.ProjectileId, value => geometry.ProjectileId = value);
        BindFloat(root, "JbmReferenceDiameterField", geometry.ReferenceDiameterMm, value => geometry.ReferenceDiameterMm = value, 0.001);
        BindFloat(root, "JbmTotalLengthField", geometry.TotalLengthCalibers, value => geometry.TotalLengthCalibers = value, 0.001);
        BindFloat(root, "JbmNoseLengthField", geometry.NoseLengthCalibers, value => geometry.NoseLengthCalibers = value, 0.001);
        BindFloat(root, "JbmTangentRadiusRatioField", geometry.TangentRadiusRatio, value => geometry.TangentRadiusRatio = value, 0);
        BindFloat(root, "JbmBoattailLengthField", geometry.BoattailLengthCalibers, value => geometry.BoattailLengthCalibers = value, 0);
        BindFloat(root, "JbmBaseDiameterField", geometry.BaseDiameterCalibers, value => geometry.BaseDiameterCalibers = value, 0.001);
        BindFloat(root, "JbmMeplatDiameterField", geometry.MeplatDiameterCalibers, value => geometry.MeplatDiameterCalibers = value, 0);

        SetDisplay(root, "JbmProjectileDensityField", jbmMode == JbmProgramMode.McGyro);
        SetDisplay(root, "JbmRiflingTwistField", jbmMode == JbmProgramMode.McGyro);
        SetDisplay(root, "JbmCenterOfGravityField", jbmMode != JbmProgramMode.McGyro);
        SetDisplay(root, "JbmRotatingBandDiameterField", jbmMode == JbmProgramMode.McDrag);
        SetDisplay(root, "JbmBoundaryLayerField", jbmMode == JbmProgramMode.McDrag);
        BindFloat(root, "JbmProjectileDensityField", mcGyroInput.ProjectileDensityGramsPerCc, value => mcGyroInput.ProjectileDensityGramsPerCc = value, 0.001);
        BindFloat(root, "JbmRiflingTwistField", mcGyroInput.RiflingTwistCalibersPerTurn, value => mcGyroInput.RiflingTwistCalibersPerTurn = value, 0.001);
        if (jbmMode == JbmProgramMode.IntLift)
            BindFloat(root, "JbmCenterOfGravityField", intLiftInput.CenterOfGravityCalibers, value => intLiftInput.CenterOfGravityCalibers = value, 0.001);
        else
            BindFloat(root, "JbmCenterOfGravityField", mcDragInput.CenterOfGravityCalibers, value => mcDragInput.CenterOfGravityCalibers = value, 0.001);
        BindFloat(root, "JbmRotatingBandDiameterField", mcDragInput.RotatingBandDiameterCalibers, value => mcDragInput.RotatingBandDiameterCalibers = value, 0.001);
        BindDropdown(root, "JbmBoundaryLayerField", LocalizedChoices("Laminar/Laminar", "Laminar/Turbulent", "Turbulent/Turbulent"), BoundaryLayerIndex(mcDragInput.BoundaryLayer),
            index => mcDragInput.BoundaryLayer = index == 0 ? JbmBoundaryLayer.LaminarLaminar : index == 2 ? JbmBoundaryLayer.TurbulentTurbulent : JbmBoundaryLayer.LaminarTurbulent);
    }

    void BindSample(VisualElement root, string name, string selectedId, Action<string> setter)
    {
        var samples = BallisticSampleCatalog.All();
        var choices = new List<string> { Localize("Not Specified") };
        choices.AddRange(samples.Select(sample => sample.Label));
        var index = 0;
        var sampleIndex = samples.FindIndex(sample => sample.Id == selectedId);
        if (sampleIndex >= 0)
            index = sampleIndex + 1;
        BindDropdown(root, name, choices, index, selected => setter(selected <= 0 ? "" : samples[Mathf.Clamp(selected - 1, 0, samples.Count - 1)].Id));
    }

    void BindButton(VisualElement root, string name, Action clicked)
    {
        var button = root.Q<Button>(name);
        if (button == null)
            return;
        if (button.userData is not TemplateButtonBinding binding)
        {
            binding = new TemplateButtonBinding();
            button.userData = binding;
            button.clicked += () =>
            {
                if (button.userData is TemplateButtonBinding current)
                    current.Clicked?.Invoke();
            };
        }
        binding.Clicked = clicked;
    }

    void BindFloat(VisualElement root, string name, double value, Action<double> setter, double? min = null, bool enabled = true, string label = null)
    {
        var field = root.Q<FloatField>(name);
        if (field == null)
            return;
        if (field.userData is not TemplateFloatBinding binding)
        {
            binding = new TemplateFloatBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not TemplateFloatBinding current || current.Updating)
                    return;
                var next = (double)evt.newValue;
                if (current.Min.HasValue)
                    next = Math.Max(current.Min.Value, next);
                current.Setter?.Invoke(next);
                RebuildContent();
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

    void BindDropdown(VisualElement root, string name, List<string> choices, int index, Action<int> setter, bool enabled = true)
    {
        if (choices.Count == 0)
            choices.Add("");
        index = Mathf.Clamp(index, 0, choices.Count - 1);
        BindDropdown(root, name, choices.Select((choice, choiceIndex) => new DropdownOption<int>(choiceIndex, choice)).ToList(), index, setter, enabled);
    }

    void BindDropdown<T>(VisualElement root, string name, List<DropdownOption<T>> options, T value, Action<T> setter, bool enabled = true)
    {
        var field = root.Q<DropdownField>(name);
        if (field == null)
            return;
        if (options.Count == 0)
            options.Add(new DropdownOption<T>(default, ""));
        var selectedIndex = options.FindIndex(option => EqualityComparer<T>.Default.Equals(option.Value, value));
        if (selectedIndex < 0)
            selectedIndex = 0;
        if (field.userData is not TemplateDropdownBinding<T> binding)
        {
            binding = new TemplateDropdownBinding<T>();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not TemplateDropdownBinding<T> current || current.Updating)
                    return;
                var optionIndex = current.Options.FindIndex(option => string.Equals(option.Label, evt.newValue, StringComparison.Ordinal));
                if (optionIndex < 0 || optionIndex >= current.Options.Count)
                    return;
                var selectedValue = current.Options[optionIndex].Value;
                if (EqualityComparer<T>.Default.Equals(selectedValue, current.Value))
                    return;
                current.Value = selectedValue;
                current.Setter?.Invoke(selectedValue);
                RebuildContent();
            });
        }
        var choices = options.Select(option => option.Label).ToList();
        binding.Updating = true;
        binding.Value = value;
        binding.Options = options;
        binding.Setter = setter;
        field.choices = choices;
        field.SetEnabled(enabled);
        field.SetValueWithoutNotify(choices[selectedIndex]);
        binding.Updating = false;
    }

    void BindText(VisualElement root, string name, string value, Action<string> setter)
    {
        var field = root.Q<TextField>(name);
        if (field == null)
            return;
        if (field.userData is not TemplateTextBinding binding)
        {
            binding = new TemplateTextBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not TemplateTextBinding current || current.Updating)
                    return;
                var next = evt.newValue ?? "";
                if (!string.Equals(next, field.value ?? "", StringComparison.Ordinal))
                    return;
                current.Setter?.Invoke(next);
                RebuildContent();
            });
        }
        binding.Updating = true;
        binding.Setter = setter;
        field.SetValueWithoutNotify(value ?? "");
        binding.Updating = false;
    }

    void BindToggle(VisualElement root, string name, bool value, Action<bool> setter)
    {
        var field = root.Q<Toggle>(name);
        if (field == null)
            return;
        if (field.userData is not TemplateToggleBinding binding)
        {
            binding = new TemplateToggleBinding();
            field.userData = binding;
            field.RegisterValueChangedCallback(evt =>
            {
                if (field.userData is not TemplateToggleBinding current || current.Updating)
                    return;
                current.Setter?.Invoke(evt.newValue);
                RebuildContent();
            });
        }
        binding.Updating = true;
        binding.Setter = setter;
        field.SetValueWithoutNotify(value);
        binding.Updating = false;
    }

    static void SetDisplay(VisualElement root, string name, bool visible)
    {
        var element = root.Q<VisualElement>(name);
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    static string PageName(McCoyOkunTab tab) => $"{TabElementPrefix(tab)}Page";

    static string InputName(McCoyOkunTab tab) => $"{TabElementPrefix(tab)}Input";

    static string OutputName(McCoyOkunTab tab) => $"{TabElementPrefix(tab)}Output";

    static string TabElementPrefix(McCoyOkunTab tab)
    {
        return tab switch
        {
            McCoyOkunTab.M79Apclc => "M79Apclc",
            McCoyOkunTab.Facehard69 => "Facehard69",
            McCoyOkunTab.McCoy => "McCoy",
            McCoyOkunTab.McCoyPlus => "McCoyPlus",
            McCoyOkunTab.McCoyPlusFacehard => "McCoyPlusFacehard",
            McCoyOkunTab.McCoyPlusFacehardM79 => "McCoyPlusFacehardM79",
            McCoyOkunTab.McCoyPlusM79 => "McCoyPlusM79",
            McCoyOkunTab.Jbm => "Jbm",
            _ => string.Empty
        };
    }

    static string TabLabel(McCoyOkunTab tab)
    {
        return tab switch
        {
            McCoyOkunTab.M79Apclc => "M79 APCLC",
            McCoyOkunTab.Facehard69 => "Facehard69",
            McCoyOkunTab.McCoy => "McCoy",
            McCoyOkunTab.McCoyPlus => "McCoy Plus",
            McCoyOkunTab.McCoyPlusFacehard => "McCoy Plus Facehard",
            McCoyOkunTab.McCoyPlusFacehardM79 => "McCoy Plus Facehard M79",
            McCoyOkunTab.McCoyPlusM79 => "McCoy Plus M79",
            McCoyOkunTab.Jbm => "JBM",
            _ => string.Empty
        };
    }

    static bool TryTabFromLabel(string label, out McCoyOkunTab tab)
    {
        tab = label switch
        {
            "M79 APCLC" => McCoyOkunTab.M79Apclc,
            "Facehard69" => McCoyOkunTab.Facehard69,
            "McCoy" => McCoyOkunTab.McCoy,
            "McCoy Plus" => McCoyOkunTab.McCoyPlus,
            "McCoy Plus Facehard" => McCoyOkunTab.McCoyPlusFacehard,
            "McCoy Plus Facehard M79" => McCoyOkunTab.McCoyPlusFacehardM79,
            "McCoy Plus M79" => McCoyOkunTab.McCoyPlusM79,
            "JBM" => McCoyOkunTab.Jbm,
            _ => McCoyOkunTab.M79Apclc
        };
        return !string.IsNullOrEmpty(label) && label == TabLabel(tab);
    }

    static BallisticSample SampleById(string id)
    {
        return BallisticSampleCatalog.SampleById(id);
    }

    static int JbmProgramIndex(JbmProgramMode program)
    {
        return program == JbmProgramMode.McGyro ? 1 : program == JbmProgramMode.IntLift ? 2 : 0;
    }

    static int BoundaryLayerIndex(JbmBoundaryLayer boundaryLayer)
    {
        return boundaryLayer == JbmBoundaryLayer.LaminarLaminar ? 0 : boundaryLayer == JbmBoundaryLayer.TurbulentTurbulent ? 2 : 1;
    }

    static string NoseConditionLabel(FacehardNoseCondition condition)
    {
        return condition switch
        {
            FacehardNoseCondition.WindscreenRemoved => Localize("Only windscreen lost"),
            FacehardNoseCondition.CapHeadRemoved => Localize("Windscreen and cap-head lost"),
            FacehardNoseCondition.AllRemoved => Localize("All nose coverings lost"),
            _ => Localize("All nose coverings intact")
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

    static string RemainingNoseWeightLabel(double capHead, FacehardCapType resolvedCapType)
    {
        if (capHead == 2)
            return Localize("Remaining AP Cap Body Weight");
        if (capHead == 1)
            return Localize("Remaining Nose Covering Weight");
        if (resolvedCapType == FacehardCapType.Hood)
            return Localize("Remaining Hood Weight");
        if (resolvedCapType == FacehardCapType.None)
            return Localize("Remaining Nose Covering Weight");
        return Localize("Remaining AP Cap Weight");
    }

    static FacehardCapType ResolvedCapType(FacehardInput input)
    {
        var preset = FacehardCalculator.FacehardProjectilePresets.FirstOrDefault(item => item.Id == input.ProjectilePresetId);
        return input.ProjectilePresetId == "custom" || preset == null ? input.CapType : preset.CapType;
    }

    IEnumerable<string> FacehardRunLines(FacehardResult result)
    {
        yield return $"{"#",4} {"OB",8} {"VS",8} {"HBL",8} {"NBL",8} {"EBL",8} {"PENTP",7} Status";
        yield return $"{"live",4} {facehardInput.Obliquity,8:0.#} {facehardInput.StrikingVelocity,8:0} {result.HolingBl,8:0} {result.NavyBl,8:0} {result.EffectiveBl,8:0} {(int)(result.Penetration?.Type ?? FacehardPenetrationType.NoCaliberSizeHole),7:0} {BallisticOptions.ToLegacyCode(result.Status)}";
        for (var i = 0; i < facehardRecordedRuns.Count; i++)
        {
            var run = facehardRecordedRuns[i];
            yield return $"{i + 1,4} {run.Obliquity,8:0.#} {run.Velocity,8:0} {run.HolingBl,8:0} {run.NavyBl,8:0} {run.EffectiveBl,8:0} {run.Penetration,7} {BallisticOptions.ToLegacyCode(run.Status)}";
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
        yield return $"Cap: {BallisticOptions.ToLegacyCode(result.ResolvedCapType)}";
        yield return $"Shatter: {BallisticOptions.ToLegacyCode(result.Shatter?.Type ?? FacehardShatterType.None)}";
        yield return $"SHAT HBL / NBL - Shattered Limits: {F(result.Shatter?.HolingBl, 0)} / {F(result.Shatter?.NavyBl, 0)} ft/s";
        yield return $"VLTRU / VHTRU - Unshattered NBL / HBL: {F(result.Limits?.UnshatteredNavyBl, 0)} / {F(result.Limits?.UnshatteredHolingBl, 0)} ft/s";
        yield return $"VLND / VHND - Undamaged NBL / HBL: {F(result.Limits?.UndamagedNavyBl, 0)} / {F(result.Limits?.UndamagedHolingBl, 0)} ft/s";
        yield return $"VDF used - Limit / Post-impact: {F(result.Limits?.VdfUsed, 4)} / {F(result.Limits?.VdfPostImpact, 4)}";
        yield return $"SHATMULT - Shatter Thickness Multiplier: {F(result.Shatter?.Multiplier, 3)}";
        yield return $"MSHAT - Shattered Obliquity Multiplier: {F(result.Shatter?.ObliquityMultiplier, 3)}";
        yield return $"PPLUS - Projectile Quality Bonus: {F(result.ProjectileQualityBonus, 3)}";
        yield return $"PENTP - Penetration Type / Flag: {(int)(result.Penetration?.Type ?? FacehardPenetrationType.NoCaliberSizeHole)} / {BallisticOptions.ToLegacyCode(result.Penetration?.PenetrationFlag ?? FacehardPenetrationFlag.None)}";
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
        target.NoseCondition = FacehardNoseCondition.Intact;
        target.WindscreenWeight = sample.WindscreenWeight;

        var isCustomProjectile = target.ProjectilePresetId == "custom";
        if (isCustomProjectile)
        {
            target.NoseSchema = sample.NoseSchema;
            target.JapaneseCapHead = sample.JapaneseCapHead <= 1 ? 1 : 2;
            target.ProjectileLimitQuality = sample.ProjectileLimitQuality;
            target.ProjectileDamageQuality = sample.ProjectileDamageQuality;
        }
        else
        {
            target.NoseSchema = FacehardNoseSchema.Standard;
            target.JapaneseCapHead = 2;
        }

        var noseWeights = FacehardCalculator.FacehardNoseCoveringWeights(target);
        if (noseWeights.SchemaKind == FacehardNoseSchema.JapaneseCapHead &&
            noseWeights.CapHead == 2)
        {
            target.WindscreenCapHeadWeight = sample.WindscreenCapHeadWeight;
        }
        else if (noseWeights.SchemaKind != FacehardNoseSchema.JapaneseCapHead || noseWeights.CapHead != 2)
        {
            target.WindscreenCapHeadWeight = 0;
        }
    }

    void ApplyMcCoyPlusSample(McCoyPlusInput target, BallisticSample sample, Action<McCoyPlusDragFunction> setPreset, Action<string> setDragText)
    {
        ApplyMcCoyPlusPreset(target, sample.DragFunction, setPreset, setDragText);
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

    void SyncFacehardM79Bridge()
    {
        mccoyPlusFacehardM79Input.Facehard.ProjectileDiameter = mccoyPlusFacehardM79Details.ProjectileDiameter;
        mccoyPlusFacehardM79Input.Facehard.PlateThickness = mccoyPlusFacehardM79Details.PlateThickness;
        mccoyPlusFacehardM79Input.Facehard.Obliquity = mccoyPlusFacehardM79Details.Obliquity;
        mccoyPlusFacehardM79Input.Facehard.StrikingVelocity = mccoyPlusFacehardM79Details.StrikingVelocity;
        mccoyPlusFacehardM79Input.M79.ProjectileDiameter = mccoyPlusFacehardM79Details.ProjectileDiameter;
        mccoyPlusFacehardM79Input.M79.ProjectileWeight = mccoyPlusFacehardM79Details.ProjectileWeight;
    }

    static void ApplyMcCoyPlusPreset(McCoyPlusInput target, McCoyPlusDragFunction function, Action<McCoyPlusDragFunction> setPreset, Action<string> setDragText)
    {
        var preset = McCoyPlus.DragPresets().FirstOrDefault(item => item.Function == function)
            ?? McCoyPlus.DragPresets()[0];
        setPreset(preset.Function);
        setDragText(McCoyPlus.DragPresetToText(preset.Function));
        target.DragName = preset.Label;
        target.DragTable = preset.Points;
        target.RangeUnit = McCoyRangeUnit.Yards;
    }

    void SyncComboMcCoy(McCoyPlusInput input, string dragText)
    {
        input.DragTable = input.DragTable != null && input.DragTable.Count >= 2
            ? input.DragTable
            : McCoy.NormalizeDragTable(dragText);
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
            box.Add(new Label(Localize(item.label)));
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

    static VisualElement ChartSeries(string title, IEnumerable<MiniChartSeries> series, string xUnit, string yUnit)
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

    static VisualElement ChartLegend(List<MiniChartSeries> series)
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
                    marginRight = 16,
                    marginBottom = 2
                }
            };
            var swatch = new VisualElement
            {
                style =
                {
                    width = 18,
                    height = 3,
                    marginRight = 5
                }
            };
            swatch.style.backgroundColor = McCoyOkunMiniChart.SeriesColor(i);
            item.Add(swatch);
            var label = new Label(series[i].Label)
            {
                style =
                {
                    fontSize = 11
                }
            };
            label.style.color = McCoyOkunMiniChart.SeriesColor(i);
            item.Add(label);
            legend.Add(item);
        }
        return legend;
    }

    static IEnumerable<MiniChartSeries> TrajectorySeries<T>(IEnumerable<T> rows, McCoyRangeUnit rangeUnit) where T : McCoyPlusRow
    {
        var unitLabel = BallisticOptions.ToLegacyCode(rangeUnit);
        foreach (var row in rows ?? Enumerable.Empty<T>())
        {
            yield return new MiniChartSeries
            {
                Label = $"{F(row.Range, 0)} {unitLabel}",
                Points = row.Trajectory.Select(point => new Vector2((float)point.Range, (float)(point.HeightInches / 12d))).ToList()
            };
        }
    }

    static IEnumerable<MiniChartSeries> PenetrationSeries(IEnumerable<McCoyPlusFacehardM79Row> rows)
    {
        var rowList = rows?.ToList() ?? new List<McCoyPlusFacehardM79Row>();
        yield return new MiniChartSeries
        {
            Label = Localize("V Pen"),
            Points = rowList
                .Where(row => row.PenetrationInches.HasValue)
                .Select(row => new Vector2((float)row.Range, (float)row.PenetrationInches.Value))
                .ToList()
        };
        yield return new MiniChartSeries
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
                title = Localize(column.Title),
                width = column.Width,
                minWidth = Math.Min(column.Width, 80),
                stretchable = false,
                makeCell = () => new Label
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.NoWrap
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
    readonly VisualElement labelLayer = new();
    string xAxisUnit = "";
    string yAxisUnit = "";

    public McCoyOkunMiniChart()
    {
        style.height = 150;
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

    public void SetAxisLabels(string xUnit, string yUnit)
    {
        xAxisUnit = xUnit ?? "";
        yAxisUnit = yUnit ?? "";
        RebuildLabels();
        MarkDirtyRepaint();
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
        RebuildLabels();
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

        var plot = GetPlotRect(rect);
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

    void RebuildLabels()
    {
        labelLayer.Clear();
        var rect = contentRect;
        var allPoints = Series.SelectMany(item => item.Points).ToList();
        if (rect.width <= 8 || rect.height <= 8 || allPoints.Count < 2)
            return;

        var plot = GetPlotRect(rect);
        var maxX = allPoints.Max(point => point.x);
        var maxY = allPoints.Max(point => point.y);
        var axisColor = HtmlColor("#aab3ab");
        var yLabel = string.IsNullOrWhiteSpace(yAxisUnit) ? "" : yAxisUnit;
        var xLabel = string.IsNullOrWhiteSpace(xAxisUnit)
            ? maxX.ToString("0", CultureInfo.InvariantCulture)
            : $"{maxX.ToString("0", CultureInfo.InvariantCulture)} {AbbreviateUnit(xAxisUnit)}";
        labelLayer.Add(BuildLabel(yLabel, 2f, plot.yMin - 6f, plot.xMin - 8f, 18f, TextAnchor.MiddleRight, axisColor));
        labelLayer.Add(BuildLabel(maxY.ToString("0", CultureInfo.InvariantCulture), 2f, plot.yMin + 10f, plot.xMin - 8f, 18f, TextAnchor.MiddleRight, axisColor));
        labelLayer.Add(BuildLabel(xLabel, plot.xMax - 82f, plot.yMax + 8f, 86f, 18f, TextAnchor.UpperRight, axisColor));
    }

    static Rect GetPlotRect(Rect rect)
    {
        return new Rect(rect.x + 42, rect.y + 12, rect.width - 56, rect.height - 34);
    }

    static string AbbreviateUnit(string unit)
    {
        return unit switch
        {
            "yards" => "yd",
            "yard" => "yd",
            "feet" => "ft",
            "foot" => "ft",
            _ => unit
        };
    }

    static Label BuildLabel(string text, float left, float top, float width, float height, TextAnchor align, Color color)
    {
        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.left = left;
        label.style.top = top;
        label.style.width = width;
        label.style.height = height;
        label.style.unityTextAlign = align;
        label.style.fontSize = 11;
        label.style.color = color;
        return label;
    }

    static Color HtmlColor(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
    }
}
