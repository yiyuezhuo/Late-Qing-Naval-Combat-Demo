using System.Diagnostics;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ;

var options = RunnerOptions.Parse(args);
ServiceLocator.Register<ILoggerService>(new HeadlessLogger(options.ShowWarnings), logOverride: false);

if (options.ShowHelp)
{
    RunnerOptions.PrintUsage();
    return;
}

if (options.RunSmoke || string.IsNullOrWhiteSpace(options.Scenario))
{
    RunSmokeProbe();
    return;
}

RunSimulation(options);

static void RunSimulation(RunnerOptions options)
{
    var wallClock = Stopwatch.StartNew();
    var loadedState = HeadlessScenarioLoader.Load(options.Scenario);
    var gameState = loadedState.navalGameState ?? throw new InvalidOperationException("Scenario has no navalGameState.");
    gameState.scenarioState ??= new ScenarioState();
    gameState.scenarioState.FillBeginDateTimeIfMissing();

    NavalGameState.UpdateInstance(gameState);
    VictoryStatus.CaptureInitialBaselineIfMissing(NavalGameState.Instance);
    SetTopLevelGroupsAutomatic(NavalGameState.Instance);

    var beginTime = NavalGameState.Instance.scenarioState.dateTime;
    var runLimit = ResolveRunLimit(NavalGameState.Instance.scenarioState, options);
    var stepSeconds = options.StepSeconds;
    var stopReason = "time limit";
    var steps = 0L;

    Console.WriteLine($"Scenario: {Path.GetFileName(loadedState.SourcePath)}");
    Console.WriteLine($"Start:    {beginTime:u}");
    Console.WriteLine($"Limit:    {runLimit.TotalMinutes:0.##} min, step={stepSeconds:0.###}s");

    var lastProgressRender = Stopwatch.StartNew();
    RenderProgress(beginTime, runLimit, steps, force: true);

    while (true)
    {
        var elapsed = NavalGameState.Instance.scenarioState.dateTime - beginTime;
        if (elapsed >= runLimit)
            break;

        var autoEndReason = GetAutoEndReason(NavalGameState.Instance);
        if (autoEndReason != null)
        {
            stopReason = autoEndReason;
            break;
        }

        var remainingSeconds = (float)Math.Max(0.001, (runLimit - elapsed).TotalSeconds);
        NavalGameState.Instance.Step(Math.Min(stepSeconds, remainingSeconds));
        steps++;

        if (lastProgressRender.ElapsedMilliseconds >= 100)
        {
            RenderProgress(beginTime, runLimit, steps, force: false);
            lastProgressRender.Restart();
        }
    }

    var finalAutoEndReason = GetAutoEndReason(NavalGameState.Instance);
    if (finalAutoEndReason != null)
        stopReason = finalAutoEndReason;

    RenderProgress(beginTime, runLimit, steps, force: true);
    Console.WriteLine();

    wallClock.Stop();
    var simulatedSeconds = Math.Max(0, (NavalGameState.Instance.scenarioState.dateTime - beginTime).TotalSeconds);
    var averageAcceleration = wallClock.Elapsed.TotalSeconds > 0
        ? simulatedSeconds / wallClock.Elapsed.TotalSeconds
        : 0;

    Console.WriteLine($"Stop:     {stopReason}");
    Console.WriteLine($"End:      {NavalGameState.Instance.scenarioState.dateTime:u}");
    Console.WriteLine($"Steps:    {steps}");
    Console.WriteLine($"Wall:     {FormatDuration(wallClock.Elapsed)}");
    Console.WriteLine($"Sim:      {FormatDuration(TimeSpan.FromSeconds(simulatedSeconds))}");
    Console.WriteLine($"Avg x:    {averageAcceleration:0.##}");
    Console.WriteLine();

    PrintVictoryTables(VictoryStatus.Generate(NavalGameState.Instance));

    if (!string.IsNullOrWhiteSpace(options.Output))
    {
        loadedState.navalGameState = NavalGameState.Instance;
        var outputPath = Path.GetFullPath(options.Output);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllText(outputPath, loadedState.ToXML());
        Console.WriteLine();
        Console.WriteLine($"Saved: {outputPath}");
    }
}

static TimeSpan ResolveRunLimit(ScenarioState scenarioState, RunnerOptions options)
{
    if (options.DurationSeconds > 0)
        return TimeSpan.FromSeconds(options.DurationSeconds);

    if (scenarioState.hasEndDateTime && scenarioState.endDateTime > scenarioState.dateTime)
        return scenarioState.endDateTime - scenarioState.dateTime;

    return TimeSpan.FromHours(1);
}

static void SetTopLevelGroupsAutomatic(NavalGameState gameState)
{
    foreach (var group in gameState.shipGroups.Where(group => group?.parentObjectId == null))
    {
        group.doctrine ??= new Doctrine();
        group.doctrine.maneuverAutomaticType.isInherited = false;
        group.doctrine.maneuverAutomaticType.value = AutomaticType.Automatic;
        group.doctrine.fireAutomaticType.isInherited = false;
        group.doctrine.fireAutomaticType.value = AutomaticType.Automatic;
        group.doctrine.searchlightAutomaticType.isInherited = false;
        group.doctrine.searchlightAutomaticType.value = AutomaticType.Automatic;
    }
}

static string GetAutoEndReason(NavalGameState gameState)
{
    var state = gameState.scenarioState;
    if (state.hasEndDateTime && state.dateTime >= state.endDateTime)
        return "scenario end time";

    var operationalRootGroups = GetOperationalRootGroups(gameState);
    if (operationalRootGroups.Count <= 1)
        return "only one operational fleet remains";

    var operationalRootGroupMembers = operationalRootGroups
        .Select(group => (IShipGroupMember)(group.FirstOrDefault() as IShipGroupMember)?.GetRootParent())
        .Where(group => group != null)
        .ToList();
    if (gameState.AreOperationalRootGroupsDisengaged(operationalRootGroupMembers))
        return "fleets disengaged";

    return null;
}

static List<List<ShipLog>> GetOperationalRootGroups(NavalGameState gameState)
{
    var rootGroups = gameState.shipGroups.Where(group => group?.parentObjectId == null).ToList();
    if (rootGroups.Count == 0)
    {
        return gameState.shipLogs
            .Where(IsAutoEndOperationalShip)
            .GroupBy(ship => ((IShipGroupMember)ship).GetRootParent()?.objectId ?? ship.objectId)
            .Select(group => group.ToList())
            .ToList();
    }

    return rootGroups
        .Select(group => group.Walk<ShipLog>().Where(IsAutoEndOperationalShip).ToList())
        .Where(ships => ships.Count > 0)
        .ToList();
}

static bool IsAutoEndOperationalShip(ShipLog shipLog)
{
    return shipLog != null
        && shipLog.mapState == MapState.Deployed
        && shipLog.operationalState == ShipOperationalState.Operational
        && (
            shipLog.GetMaxSpeedKnots() > 0
            || (shipLog.IsLandBattery() && shipLog.isLandTarget)
        );
}

static void RenderProgress(DateTime beginTime, TimeSpan runLimit, long steps, bool force)
{
    var currentTime = NavalGameState.Instance.scenarioState.dateTime;
    var elapsed = currentTime - beginTime;
    var totalSeconds = Math.Max(0.001, runLimit.TotalSeconds);
    var ratio = Math.Clamp(elapsed.TotalSeconds / totalSeconds, 0, 1);
    const int width = 32;
    var filled = (int)Math.Round(ratio * width);
    var bar = new string('#', filled) + new string('-', width - filled);
    var counts = string.Join(" ", GetOperationalCountsByRootGroup(NavalGameState.Instance)
        .Select(entry => $"{entry.name}:{entry.count}"));
    var line = $"[{bar}] {ratio * 100,6:0.0}% {currentTime:u} steps={steps} movable={counts}";

    Console.Write('\r');
    var windowWidth = GetConsoleWindowWidth();
    Console.Write(line.PadRight(windowWidth > 0 ? Math.Max(0, windowWidth - 1) : line.Length));
}

static int GetConsoleWindowWidth()
{
    try
    {
        return Console.WindowWidth;
    }
    catch (IOException)
    {
        return 0;
    }
}

static List<(string name, int count)> GetOperationalCountsByRootGroup(NavalGameState gameState)
{
    var rootGroups = gameState.shipGroups.Where(group => group?.parentObjectId == null).ToList();
    if (rootGroups.Count == 0)
    {
        return new()
        {
            ("ungrouped", gameState.shipLogs.Count(IsAutoEndOperationalShip))
        };
    }

    return rootGroups
        .Select(group => (name: group.GetMemberName(), count: group.Walk<ShipLog>().Count(IsAutoEndOperationalShip)))
        .ToList();
}

static void PrintVictoryTables(VictoryStatus victoryStatus)
{
    Console.WriteLine("Victory Summary");
    PrintTable(
        new[] { "Side", "Level", "VP", "LossVP", "CommitVP", "Loss%" },
        victoryStatus.sideVictoryStatuses.Select(side => new[]
        {
            side.name,
            side.victoryLevel.ToString(),
            side.victoryPoint.ToString("0.##"),
            side.lossVictoryPoint.ToString("0.##"),
            side.commitVictoryPoint.ToString("0.##"),
            (side.selfLossRatio * 100).ToString("0.##")
        }));

    foreach (var side in victoryStatus.sideVictoryStatuses)
    {
        Console.WriteLine();
        Console.WriteLine($"{side.name} Ship Type Losses");
        PrintTable(
            new[] { "Type", "Undmg", "Light", "Medium", "Heavy", "Sunk", "LossVP", "CommitVP" },
            side.shipTypeLossItems.Select(item => new[]
            {
                item.shipType.ToString(),
                item.undamaged.ToString(),
                item.light.ToString(),
                item.medium.ToString(),
                item.heavy.ToString(),
                item.sunk.ToString(),
                item.lossVictoryPoint.ToString("0.##"),
                item.commitVictroyPoint.ToString("0.##")
            }));
    }
}

static void PrintTable(string[] headers, IEnumerable<string[]> rows)
{
    var table = new List<string[]> { headers };
    table.AddRange(rows);
    var widths = Enumerable.Range(0, headers.Length)
        .Select(i => table.Max(row => (row.ElementAtOrDefault(i) ?? "").Length))
        .ToArray();

    foreach (var row in table)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (i > 0)
                Console.Write("  ");
            Console.Write((row.ElementAtOrDefault(i) ?? "").PadRight(widths[i]));
        }
        Console.WriteLine();

        if (row == headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                if (i > 0)
                    Console.Write("  ");
                Console.Write(new string('-', widths[i]));
            }
            Console.WriteLine();
        }
    }
}

static string FormatDuration(TimeSpan duration)
{
    if (duration.TotalHours >= 1)
        return $"{duration.TotalHours:0.##}h";
    if (duration.TotalMinutes >= 1)
        return $"{duration.TotalMinutes:0.##}m";
    return $"{duration.TotalSeconds:0.##}s";
}

static void RunSmokeProbe()
{
    var checks = new List<(string Name, Func<string> Probe)>
    {
        ("service defaults", ProbeServiceDefaults),
        ("sample index", ProbeSampleIndex),
        ("minimal ship reset", ProbeMinimalShipReset),
        ("firepower evaluation", ProbeFirepowerEvaluation),
        ("mask fallback", ProbeMaskFallback),
    };

    Console.WriteLine("NavalCombatHeadless smoke probe");

    var failed = false;
    foreach (var (name, probe) in checks)
    {
        try
        {
            var detail = probe();
            Console.WriteLine($"[OK] {name}: {detail}");
        }
        catch (Exception ex)
        {
            failed = true;
            Console.Error.WriteLine($"[FAIL] {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    Environment.ExitCode = failed ? 1 : 0;
}

static string ProbeServiceDefaults()
{
    var logger = ServiceLocator.Get<ILoggerService>() ?? throw new InvalidOperationException("ILoggerService is not registered.");
    var localize = ServiceLocator.Get<ILocalizeService>() ?? throw new InvalidOperationException("ILocalizeService is not registered.");
    logger.Log("Headless logger is available.");
    return $"{logger.GetType().Name}, {localize.GetType().Name}";
}

static string ProbeSampleIndex()
{
    var index = RandomUtils.SampleIndex(new[] { 0d, 0d, 1d });
    if (index != 2)
        throw new InvalidOperationException($"Expected deterministic weighted sample index 2, got {index}.");

    return $"index={index}";
}

static string ProbeMinimalShipReset()
{
    var shipLog = BuildAndRegisterMinimalShipLog();
    shipLog.ResetDamageExpenditureState(new ResetDamageExpenditureStateContext());

    if (shipLog.batteryStatus.Count != 1)
        throw new InvalidOperationException($"Expected one battery status, got {shipLog.batteryStatus.Count}.");
    if (shipLog.batteryStatus[0].mountStatus.Count != 2)
        throw new InvalidOperationException($"Expected two battery mounts, got {shipLog.batteryStatus[0].mountStatus.Count}.");
    if (shipLog.batteryStatus[0].fireControlSystemStatusRecords.Count != 1)
        throw new InvalidOperationException($"Expected one fire-control status, got {shipLog.batteryStatus[0].fireControlSystemStatusRecords.Count}.");
    if (shipLog.rapidFiringStatus.Count != 1)
        throw new InvalidOperationException($"Expected one rapid-fire status, got {shipLog.rapidFiringStatus.Count}.");

    var mountLocation = shipLog.batteryStatus[0].mountStatus[0].GetMountLocation();
    if (mountLocation != MountLocation.Forward)
        throw new InvalidOperationException($"Expected first mount location Forward, got {mountLocation}.");

    return $"batteryMounts={shipLog.batteryStatus[0].mountStatus.Count}, rf={shipLog.rapidFiringStatus.Count}";
}

static string ProbeFirepowerEvaluation()
{
    var shipLog = BuildAndRegisterMinimalShipLog();
    shipLog.ResetDamageExpenditureState(new ResetDamageExpenditureStateContext());

    var batteryScore = shipLog.batteryStatus[0].EvaluateFirepowerScore();
    var batteryDirectionalScore = shipLog.batteryStatus[0].EvaluateFirepowerScore(3000, TargetAspect.Broad, 10, 0);
    var rapidFireScore = shipLog.rapidFiringStatus[0].EvaluateFirepowerScore();

    if (batteryScore <= 0)
        throw new InvalidOperationException($"Expected positive battery score, got {batteryScore}.");
    if (batteryDirectionalScore <= 0)
        throw new InvalidOperationException($"Expected positive directional battery score, got {batteryDirectionalScore}.");
    if (rapidFireScore <= 0)
        throw new InvalidOperationException($"Expected positive rapid-fire score, got {rapidFireScore}.");

    return $"battery={batteryScore:0.##}, directional={batteryDirectionalScore:0.##}, rf={rapidFireScore:0.##}";
}

static string ProbeMaskFallback()
{
    var mask = NavalCombatServices.GetMaskCheckService();
    var result = mask.Check(new LatLon(39, 121), new LatLon(39.1f, 121.1f));
    if (result == null)
        throw new InvalidOperationException("Mask fallback returned null.");
    if (result.isMasked)
        throw new InvalidOperationException("Fallback mask checker should not mask a line of sight.");

    return mask.GetType().Name;
}

static ShipLog BuildAndRegisterMinimalShipLog()
{
    EntityManager.Instance.Reset();
    NavalGameState.ClearInstance();

    var shipClass = CreateMinimalShipClass();
    shipClass.objectId = "headless-smoke-class";

    var namedShip = new NamedShip
    {
        objectId = "headless-smoke-named-ship",
        shipClassObjectId = shipClass.objectId,
        name = new GlobalString { english = "Headless Smoke Ship" },
        crewRating = 0
    };

    var shipLog = new ShipLog
    {
        objectId = "headless-smoke-ship-log",
        namedShipObjectId = namedShip.objectId,
        mapState = MapState.Deployed,
        position = new LatLon(39, 121),
        speedKnots = 10,
        desiredSpeedKnots = 10,
        headingDeg = 0,
        desiredHeadingDeg = 0
    };

    NavalGameState.UpdateInstance(new NavalGameState
    {
        shipClasses = new List<ShipClass> { shipClass },
        namedShips = new List<NamedShip> { namedShip },
        shipLogs = new List<ShipLog> { shipLog },
        scenarioState = new ScenarioState()
    });

    return shipLog;
}

static ShipClass CreateMinimalShipClass()
{
    var forwardMount = new MountLocationRecord
    {
        mountLocation = MountLocation.Forward,
        barrels = 1,
        mounts = 2,
        mountArcsPattern = MountArcsPattern.Normal
    };
    forwardMount.SyncDefaultMountArcs();

    return new ShipClass
    {
        name = new GlobalString { english = "Headless Smoke Class" },
        type = ShipType.LightCruiser,
        extraShipType = ExtraShipType.ProtectedCruiser,
        country = Country.General,
        displacementTons = 1000,
        damagePoint = 100,
        speedKnots = 18,
        speedKnotsEngineRoomsLevels = new List<float> { 18 },
        speedKnotsPropulsionShaftLevels = new List<float> { 18 },
        speedKnotsBoilerRooms = new List<float> { 18 },
        standardTurnDegPer2Min = 45,
        emergencyTurnDegPer2Min = 90,
        damageControlRatingUnmodified = 5,
        batteryRecords = new List<BatteryRecord>
        {
            new()
            {
                name = new GlobalString { english = "Smoke Battery" },
                shellSizeInch = 6,
                shellWeightPounds = 100,
                damageRating = 10,
                rangeYards = 8000,
                ammunitionCapacity = 40,
                fireControlPositions = 1,
                fireControlTableRecords = new List<FireControlTableRecord>
                {
                    new()
                    {
                        speedThresholdKnot = 99,
                        shortBroad = 1,
                        shortNarrow = 1,
                        mediumBroad = 1,
                        mediumNarrow = 1,
                        longBroad = 1,
                        longNarrow = 1,
                        extremeBroad = 1,
                        extremeNarrow = 1
                    }
                },
                penetrationTableRecords = new List<PenetrationTableRecord>
                {
                    new()
                    {
                        distanceYards = 8000,
                        rateOfFire = 1,
                        rangeBand = RangeBand.Short,
                        horizontalPenetrationInchs = 1,
                        verticalPenetrationInchs = 1
                    }
                },
                mountLocationRecords = new List<MountLocationRecord> { forwardMount }
            }
        },
        rapidFireBatteryRecords = new List<RapidFireBatteryRecord>
        {
            new()
            {
                name = new GlobalString { english = "Smoke RF Battery" },
                maxRangeYards = 4000,
                effectiveRangeYards = 2000,
                damageFactor = 1,
                barrelsLevelPort = new List<int> { 2, 1, 0 },
                barrelsLevelStarboard = new List<int> { 2, 1, 0 },
                fireControlRecords = new List<RapidFireBatteryFireControlLevelRecord>
                {
                    new()
                    {
                        fireControlEffectiveRange = 1,
                        fireControlMaxRange = 1
                    }
                }
            }
        }
    };
}

sealed class HeadlessLogger : ILoggerService
{
    readonly bool showWarnings;

    public HeadlessLogger(bool showWarnings)
    {
        this.showWarnings = showWarnings;
    }

    public void Log(string message)
    {
    }

    public void LogWarning(string message)
    {
        if (showWarnings)
            Console.Error.WriteLine("[Warn] " + message);
    }

    public void LogError(string message)
    {
        Console.Error.WriteLine("[Error] " + message);
    }
}

sealed class RunnerOptions
{
    public string Scenario;
    public string Output;
    public double DurationSeconds;
    public float StepSeconds = 1f;
    public bool ShowWarnings;
    public bool ShowHelp;
    public bool RunSmoke;

    public static RunnerOptions Parse(string[] args)
    {
        var options = new RunnerOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "smoke":
                case "--smoke":
                    options.RunSmoke = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "--scenario":
                case "-s":
                    options.Scenario = RequireValue(args, ref i, arg);
                    break;
                case "--output":
                case "-o":
                    options.Output = RequireValue(args, ref i, arg);
                    break;
                case "--duration-seconds":
                    options.DurationSeconds = double.Parse(RequireValue(args, ref i, arg));
                    break;
                case "--duration-minutes":
                case "-m":
                    options.DurationSeconds = double.Parse(RequireValue(args, ref i, arg)) * 60;
                    break;
                case "--step-seconds":
                    options.StepSeconds = Math.Max(0.001f, float.Parse(RequireValue(args, ref i, arg)));
                    break;
                case "--warnings":
                    options.ShowWarnings = true;
                    break;
                default:
                    if (string.IsNullOrWhiteSpace(options.Scenario) && !arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        options.Scenario = arg;
                        break;
                    }

                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return options;
    }

    static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{name} requires a value.");
        index++;
        return args[index];
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
        NavalCombatHeadless

        Usage:
          dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -- --scenario <path-or-scenario-name> [options]
          dotnet run --project Tools/NavalCombatHeadless/NavalCombatHeadless.csproj -- smoke

        Options:
          -s, --scenario <path-or-name>   Scenario XML path, or a file name under Assets/StreamingAssets/Scenarios.
          -m, --duration-minutes <n>      Maximum simulated minutes. Defaults to scenario end time, or 60 minutes.
              --duration-seconds <n>      Maximum simulated seconds.
              --step-seconds <n>          Simulation step size. Default: 1.
          -o, --output <path>             Save final headless FullState XML.
              --warnings                  Show warnings from Core services. Errors are always shown.
          -h, --help                      Show this help.
        """);
    }
}

[XmlRoot("FullState")]
public class HeadlessFullState
{
    [XmlIgnore]
    public string SourcePath;

    public HeadlessStreamingAssetReference streamingAssetReference = new();
    public NavalGameState navalGameState;

    public string ToXML() => XmlUtils.ToXML(this);
}

public class HeadlessStreamingAssetReference
{
    public string leadersPath = "Leaders.xml";
    public string shipClassesPath = "ShipClasses.xml";
    public string namedShipsPath = "NamedShips.xml";
}

static class HeadlessScenarioLoader
{
    public static HeadlessFullState Load(string scenario)
    {
        var scenarioPath = ResolveScenarioPath(scenario);
        var xml = File.ReadAllText(scenarioPath);
        var state = XmlUtils.FromXML<HeadlessFullState>(xml);
        state.SourcePath = scenarioPath;
        state.streamingAssetReference ??= new HeadlessStreamingAssetReference();
        state.navalGameState ??= new NavalGameState();

        CompleteFromStreamingAssetReference(state, Path.GetDirectoryName(scenarioPath) ?? Directory.GetCurrentDirectory());
        return state;
    }

    static string ResolveScenarioPath(string scenario)
    {
        if (File.Exists(scenario))
            return Path.GetFullPath(scenario);

        var scenarioRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "Scenarios");
        var candidate = Path.Combine(scenarioRoot, scenario);
        if (File.Exists(candidate))
            return candidate;

        if (!scenario.EndsWith(".scen.xml", StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.Combine(scenarioRoot, scenario + ".scen.xml");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Scenario not found: {scenario}");
    }

    static void CompleteFromStreamingAssetReference(HeadlessFullState state, string scenarioRoot)
    {
        var gameState = state.navalGameState;
        var reference = state.streamingAssetReference;

        if (!gameState.leadersBuiltin && CanFill(gameState.leaders) && !string.IsNullOrWhiteSpace(reference.leadersPath))
            gameState.LeadersFromXML(File.ReadAllText(ResolveReferencePath(scenarioRoot, reference.leadersPath)));

        if (!gameState.shipClassesBuiltin && CanFill(gameState.shipClasses) && !string.IsNullOrWhiteSpace(reference.shipClassesPath))
            gameState.ShipClassesFromXML(File.ReadAllText(ResolveReferencePath(scenarioRoot, reference.shipClassesPath)));

        if (!gameState.namedShipsBuiltin && CanFill(gameState.namedShips) && !string.IsNullOrWhiteSpace(reference.namedShipsPath))
            gameState.NamedShipsFromXML(File.ReadAllText(ResolveReferencePath(scenarioRoot, reference.namedShipsPath)));
    }

    static bool CanFill<T>(List<T> list) => list == null || list.Count == 0;

    static string ResolveReferencePath(string scenarioRoot, string path)
    {
        if (Path.IsPathFullyQualified(path) && File.Exists(path))
            return path;

        var candidate = Path.Combine(scenarioRoot, path);
        if (File.Exists(candidate))
            return candidate;

        throw new FileNotFoundException($"Referenced scenario file not found: {path}", candidate);
    }
}
