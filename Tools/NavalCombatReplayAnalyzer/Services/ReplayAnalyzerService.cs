using CoreUtils;
using NavalCombatCore;
using NavalCombatReplayAnalyzer.Models;
using YYZ;

namespace NavalCombatReplayAnalyzer.Services;

public class ReplayAnalyzerService
{
    static readonly string[] importantEventWords =
    {
        "sunk",
        "sink",
        "capsize",
        "destroyed",
        "catastrophic",
        "abandon"
    };

    static readonly Dictionary<Country, string> countryColors = new()
    {
        { Country.China, "#d63b2f" },
        { Country.Japan, "#2f6fd6" },
        { Country.Russia, "#64a1d8" },
        { Country.Britain, "#8d4bd6" },
        { Country.France, "#3a9d7c" },
        { Country.Germany, "#555555" },
        { Country.UnitedStates, "#4d8f3a" },
        { Country.Italy, "#8aa23f" },
        { Country.AustriaHungary, "#b17a32" },
        { Country.Portugal, "#2c8d6b" },
    };

    public ReplayFullState LoadFromXml(string xml, string sourcePath = null)
    {
        var state = XmlUtils.FromXML<ReplayFullState>(xml);
        state.SourcePath = sourcePath;
        state.streamingAssetReference ??= new ReplayStreamingAssetReference();
        state.navalGameState ??= new NavalGameState();

        CompleteFromStreamingAssetReference(state, ResolveScenarioRootForSource(sourcePath));

        NavalGameState.UpdateInstance(state.navalGameState);
        return state;
    }

    public ReplayFullState LoadFromPath(string path)
    {
        var fullPath = ResolveScenarioOrSavePath(path);
        return LoadFromXml(File.ReadAllText(fullPath), fullPath);
    }

    public ReplayViewModel BuildReplay(ReplayFullState state)
    {
        var gameState = state.navalGameState ?? throw new InvalidOperationException("No navalGameState loaded.");
        NavalGameState.UpdateInstance(gameState);

        var replay = new ReplayViewModel
        {
            sourceName = string.IsNullOrWhiteSpace(state.SourcePath) ? "Uploaded save" : Path.GetFileName(state.SourcePath),
        };

        var rootGroups = gameState.shipGroups.Where(group => group?.parentObjectId == null).ToList();
        var groupNameByShipId = new Dictionary<string, ReplayName>();
        foreach (var rootGroup in rootGroups)
        {
            var groupName = BuildName(rootGroup.name, rootGroup.GetMemberName());
            foreach (var ship in rootGroup.Walk<ShipLog>())
            {
                if (!string.IsNullOrWhiteSpace(ship?.objectId))
                    groupNameByShipId[ship.objectId] = groupName;
            }
        }

        var shipById = gameState.shipLogs
            .Where(ship => ship != null && !string.IsNullOrWhiteSpace(ship.objectId))
            .ToDictionary(ship => ship.objectId, ship => ship);
        var namedShipById = gameState.namedShips
            .Where(namedShip => namedShip != null && !string.IsNullOrWhiteSpace(namedShip.objectId))
            .ToDictionary(namedShip => namedShip.objectId, namedShip => namedShip);

        foreach (var ship in gameState.shipLogs.Where(ship => ship != null))
        {
            var points = BuildTrack(ship, gameState).ToList();
            if (points.Count == 0)
                continue;

            var namedShip = ResolveNamedShip(ship, namedShipById);
            var shipClass = ship.shipClass ?? namedShip?.shipClass;
            var country = shipClass?.country ?? Country.General;
            var shipName = ResolveShipName(ship, namedShip);
            var groupName = groupNameByShipId.GetValueOrDefault(ship.objectId) ?? BuildName(null, "Ungrouped");
            replay.ships.Add(new ReplayShip
            {
                id = ship.objectId,
                name = SelectName(shipName, "english"),
                nameVariants = shipName,
                groupName = SelectName(groupName, "english"),
                groupNameVariants = groupName,
                type = shipClass?.type.ToString() ?? "Unknown",
                country = country.ToString(),
                color = ResolveColor(country, replay.ships.Count),
                isDestroyed = ship.mapState == MapState.Destroyed,
                finalDamagePoint = ship.damagePoint,
                maxDamagePoint = Math.Max(1, shipClass?.damagePoint ?? 1),
                track = points
            });
        }

        replay.sampleTimes = replay.ships
            .SelectMany(ship => ship.track.Select(point => point.time))
            .Distinct()
            .OrderBy(time => time)
            .ToList();

        if (replay.sampleTimes.Count > 0)
        {
            replay.startTime = replay.sampleTimes.First();
            replay.endTime = replay.sampleTimes.Last();
        }
        else
        {
            replay.startTime = gameState.scenarioState?.beginDateTime ?? gameState.scenarioState?.dateTime ?? default;
            replay.endTime = gameState.scenarioState?.dateTime ?? replay.startTime;
        }

        replay.shots = BuildShots(gameState, shipById, replay.ships).ToList();
        replay.events = BuildEvents(gameState, replay.ships, namedShipById).ToList();
        return replay;
    }

    static IEnumerable<ReplayPoint> BuildTrack(ShipLog ship, NavalGameState gameState)
    {
        var points = new List<ReplayPoint>();

        foreach (var log in ship.timeLocLogs ?? Enumerable.Empty<TimeLoc>())
        {
            points.Add(new ReplayPoint
            {
                time = log.time,
                lat = log.latDeg,
                lon = log.lonDeg,
                speedKnots = ship.speedKnots,
                headingDeg = ship.headingDeg,
                damagePoint = ship.damagePoint,
                operationalState = ship.operationalState.ToString(),
                mapState = ship.mapState.ToString()
            });
        }

        if (points.Count == 0 && ship.position != null)
        {
            var time = gameState.scenarioState?.dateTime ?? default;
            points.Add(new ReplayPoint
            {
                time = time,
                lat = ship.position.LatDeg,
                lon = ship.position.LonDeg,
                speedKnots = ship.speedKnots,
                headingDeg = ship.headingDeg,
                damagePoint = ship.damagePoint,
                operationalState = ship.operationalState.ToString(),
                mapState = ship.mapState.ToString()
            });
        }

        points = points.OrderBy(point => point.time).ToList();
        InferKinematics(points, ship.headingDeg, ship.speedKnots);
        return points;
    }

    static void InferKinematics(List<ReplayPoint> points, float fallbackHeadingDeg, float fallbackSpeedKnots)
    {
        if (points.Count == 0)
            return;

        for (var i = 0; i < points.Count; i++)
        {
            var previous = i > 0 ? points[i - 1] : null;
            var current = points[i];
            var next = i + 1 < points.Count ? points[i + 1] : null;

            var headingSegment = next != null && HasMovement(current, next)
                ? (current, next)
                : previous != null && HasMovement(previous, current)
                    ? (previous, current)
                    : ((ReplayPoint, ReplayPoint)?)null;

            if (headingSegment.HasValue)
                current.headingDeg = (float)InitialBearingDeg(headingSegment.Value.Item1, headingSegment.Value.Item2);
            else
                current.headingDeg = fallbackHeadingDeg;

            var speedSegment = next != null && next.time > current.time
                ? (current, next)
                : previous != null && current.time > previous.time
                    ? (previous, current)
                    : ((ReplayPoint, ReplayPoint)?)null;

            if (speedSegment.HasValue)
            {
                var hours = (speedSegment.Value.Item2.time - speedSegment.Value.Item1.time).TotalHours;
                var nauticalMiles = GreatCircleDistanceNm(speedSegment.Value.Item1, speedSegment.Value.Item2);
                current.speedKnots = hours > 0 ? (float)(nauticalMiles / hours) : fallbackSpeedKnots;
            }
            else
            {
                current.speedKnots = fallbackSpeedKnots;
            }
        }
    }

    static bool HasMovement(ReplayPoint a, ReplayPoint b)
    {
        return GreatCircleDistanceNm(a, b) >= 0.001;
    }

    static double InitialBearingDeg(ReplayPoint from, ReplayPoint to)
    {
        var lat1 = DegreesToRadians(from.lat);
        var lat2 = DegreesToRadians(to.lat);
        var dLon = DegreesToRadians(to.lon - from.lon);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (RadiansToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    static double GreatCircleDistanceNm(ReplayPoint a, ReplayPoint b)
    {
        const double earthRadiusNm = 3440.065;
        var lat1 = DegreesToRadians(a.lat);
        var lat2 = DegreesToRadians(b.lat);
        var dLat = lat2 - lat1;
        var dLon = DegreesToRadians(b.lon - a.lon);
        var sinLat = Math.Sin(dLat / 2);
        var sinLon = Math.Sin(dLon / 2);
        var h = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;
        return 2 * earthRadiusNm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    static IEnumerable<ReplayShot> BuildShots(NavalGameState gameState, Dictionary<string, ShipLog> shipById, List<ReplayShip> replayShips)
    {
        var replayShipById = replayShips.ToDictionary(ship => ship.id, ship => ship);

        foreach (var target in gameState.shipLogs.Where(ship => ship != null))
        {
            foreach (var log in target.logs ?? Enumerable.Empty<ShipLogLog>())
            {
                var (shooterId, weapon, damagePoint) = log switch
                {
                    ShipLogBatteryHitLog battery => (battery.shooterId, "Battery", battery.damagePoint),
                    ShipLogRapidFiringGunHitLog rapid => (rapid.shooterId, "Rapid Fire", rapid.damagePoint),
                    _ => (null, null, 0f)
                };

                if (string.IsNullOrWhiteSpace(shooterId) || weapon == null)
                    continue;
                if (!shipById.TryGetValue(shooterId, out var shooter))
                    continue;
                if (!replayShipById.TryGetValue(shooter.objectId, out var replayShooter))
                    continue;
                if (!replayShipById.TryGetValue(target.objectId, out var replayTarget))
                    continue;

                yield return new ReplayShot
                {
                    time = log.time,
                    shooterId = shooter.objectId,
                    shooterName = replayShooter.name,
                    shooterNameVariants = replayShooter.nameVariants,
                    targetId = target.objectId,
                    targetName = replayTarget.name,
                    targetNameVariants = replayTarget.nameVariants,
                    weapon = weapon,
                    damagePoint = damagePoint,
                    shooterPoint = GetPointAtOrBefore(replayShooter.track, log.time),
                    targetPoint = GetPointAtOrBefore(replayTarget.track, log.time)
                };
            }
        }
    }

    static IEnumerable<ReplayEvent> BuildEvents(NavalGameState gameState, List<ReplayShip> replayShips, Dictionary<string, NamedShip> namedShipById)
    {
        var replayShipById = replayShips.ToDictionary(ship => ship.id, ship => ship);
        var emittedSunk = new HashSet<string>();

        foreach (var ship in gameState.shipLogs.Where(ship => ship != null))
        {
            var shipName = replayShipById.TryGetValue(ship.objectId, out var replayShipForName)
                ? replayShipForName.nameVariants
                : ResolveShipName(ship, ResolveNamedShip(ship, namedShipById));

            foreach (var log in ship.logs ?? Enumerable.Empty<ShipLogLog>())
            {
                var description = log.SummaryContent();
                if (log is ShipLogBatteryHitLog or ShipLogRapidFiringGunHitLog or ShipLogTorpedoHitLog)
                {
                    yield return new ReplayEvent
                    {
                        time = log.time,
                        shipId = ship.objectId,
                        shipName = SelectName(shipName, "english"),
                        shipNameVariants = shipName,
                        kind = "Hit",
                        description = description
                    };
                    continue;
                }

                if (description != null && importantEventWords.Any(word => description.Contains(word, StringComparison.OrdinalIgnoreCase)))
                {
                    if (description.Contains("sunk", StringComparison.OrdinalIgnoreCase))
                        emittedSunk.Add(ship.objectId);

                    yield return new ReplayEvent
                    {
                        time = log.time,
                        shipId = ship.objectId,
                        shipName = SelectName(shipName, "english"),
                        shipNameVariants = shipName,
                        kind = "Major",
                        description = description
                    };
                }
            }

            if (ship.mapState == MapState.Destroyed && !emittedSunk.Contains(ship.objectId) && replayShipById.TryGetValue(ship.objectId, out var replayShip))
            {
                yield return new ReplayEvent
                {
                    time = replayShip.track.LastOrDefault()?.time ?? gameState.scenarioState.dateTime,
                    shipId = ship.objectId,
                    shipName = replayShip.name,
                    shipNameVariants = replayShip.nameVariants,
                    kind = "Sunk",
                    description = "Destroyed/Sunk"
                };
            }
        }
    }

    public static ReplayPoint GetPointAtOrBefore(IReadOnlyList<ReplayPoint> points, DateTime time)
    {
        if (points == null || points.Count == 0)
            return null;

        ReplayPoint last = points[0];
        foreach (var point in points)
        {
            if (point.time > time)
                break;
            last = point;
        }
        return last;
    }

    static string ResolveColor(Country country, int index)
    {
        if (countryColors.TryGetValue(country, out var color))
            return color;

        var fallback = new[] { "#e4572e", "#4c78a8", "#59a14f", "#b279a2", "#f28e2b", "#76b7b2" };
        return fallback[index % fallback.Length];
    }

    static NamedShip ResolveNamedShip(ShipLog ship, Dictionary<string, NamedShip> namedShipById)
    {
        if (ship == null)
            return null;

        var namedShip = ship.namedShip;
        if (namedShip != null)
            return namedShip;

        return !string.IsNullOrWhiteSpace(ship.namedShipObjectId) && namedShipById.TryGetValue(ship.namedShipObjectId, out namedShip)
            ? namedShip
            : null;
    }

    public static string SelectName(ReplayName name, string language, string fallback = null)
    {
        if (name == null)
            return fallback ?? "";

        var selected = language switch
        {
            "japanese" => name.japanese,
            "chineseSimplified" => name.chineseSimplified,
            "chineseTraditional" => name.chineseTraditional,
            _ => name.english
        };

        return FirstValidName(selected, name.english, name.japanese, name.chineseSimplified, name.chineseTraditional, fallback)
            ?? "";
    }

    static ReplayName ResolveShipName(ShipLog ship, NamedShip namedShip)
    {
        var name = BuildName(namedShip?.name, null);
        if (!IsMissingName(SelectName(name, "english", null)))
            return name;

        name = BuildName(ship?.namedShip?.name, null);
        if (!IsMissingName(SelectName(name, "english", null)))
            return name;

        var fallback = ship?.GetMemberName();
        if (!IsMissingName(fallback))
            return BuildName(null, fallback);

        return BuildName(null, ship?.namedShipObjectId ?? ship?.objectId ?? "Unnamed ship");
    }

    static bool IsMissingName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            || name == "[Not Specified]"
            || name == "none";
    }

    static ReplayName BuildName(GlobalString name, string fallback)
    {
        return new ReplayName
        {
            english = FirstValidName(name?.english, fallback),
            japanese = FirstValidName(name?.japanese, name?.english, fallback),
            chineseSimplified = FirstValidName(name?.chineseSimplified, name?.english, fallback),
            chineseTraditional = FirstValidName(name?.chineseTraditional, name?.chineseSimplified, name?.english, fallback)
        };
    }

    static string FirstValidName(params string[] values)
    {
        foreach (var value in values)
        {
            if (!IsMissingName(value))
                return value;
        }

        return null;
    }

    static void CompleteFromStreamingAssetReference(ReplayFullState state, string scenarioRoot)
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

    static string ResolveScenarioRootForSource(string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            var directory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }

        return GetDefaultScenarioRoot();
    }

    static string ResolveScenarioOrSavePath(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        var scenarioRoot = GetDefaultScenarioRoot();
        var candidate = Path.Combine(scenarioRoot, path);
        if (File.Exists(candidate))
            return candidate;

        if (!path.EndsWith(".scen.xml", StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.Combine(scenarioRoot, path + ".scen.xml");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Save/scenario not found: {path}");
    }

    static string GetDefaultScenarioRoot()
    {
        return Path.Combine(FindRepositoryRoot(), "Assets", "StreamingAssets", "Scenarios");
    }

    static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var scenarioDir = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Scenarios");
            if (Directory.Exists(scenarioDir))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    static string ResolveReferencePath(string scenarioRoot, string path)
    {
        if (Path.IsPathFullyQualified(path) && File.Exists(path))
            return path;

        var candidate = Path.Combine(scenarioRoot, path);
        if (File.Exists(candidate))
            return candidate;

        var defaultCandidate = Path.Combine(GetDefaultScenarioRoot(), path);
        if (File.Exists(defaultCandidate))
            return defaultCandidate;

        throw new FileNotFoundException($"Referenced scenario file not found: {path}", candidate);
    }
}
