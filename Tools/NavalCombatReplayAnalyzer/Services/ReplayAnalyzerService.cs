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

        if (!string.IsNullOrWhiteSpace(sourcePath))
            CompleteFromStreamingAssetReference(state, Path.GetDirectoryName(sourcePath));

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
        var groupNameByShipId = new Dictionary<string, string>();
        foreach (var rootGroup in rootGroups)
        {
            foreach (var ship in rootGroup.Walk<ShipLog>())
            {
                if (!string.IsNullOrWhiteSpace(ship?.objectId))
                    groupNameByShipId[ship.objectId] = rootGroup.GetMemberName();
            }
        }

        var shipById = gameState.shipLogs
            .Where(ship => ship != null && !string.IsNullOrWhiteSpace(ship.objectId))
            .ToDictionary(ship => ship.objectId, ship => ship);

        foreach (var ship in gameState.shipLogs.Where(ship => ship != null))
        {
            var points = BuildTrack(ship, gameState).ToList();
            if (points.Count == 0)
                continue;

            var country = ship.shipClass?.country ?? Country.General;
            replay.ships.Add(new ReplayShip
            {
                id = ship.objectId,
                name = ship.GetMemberName(),
                groupName = groupNameByShipId.GetValueOrDefault(ship.objectId) ?? "Ungrouped",
                type = ship.shipClass?.type.ToString() ?? "Unknown",
                country = country.ToString(),
                color = ResolveColor(country, replay.ships.Count),
                isDestroyed = ship.mapState == MapState.Destroyed,
                finalDamagePoint = ship.damagePoint,
                maxDamagePoint = Math.Max(1, ship.shipClass?.damagePoint ?? 1),
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
        replay.events = BuildEvents(gameState, replay.ships).ToList();
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

        return points.OrderBy(point => point.time);
    }

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
                    targetId = target.objectId,
                    targetName = replayTarget.name,
                    weapon = weapon,
                    damagePoint = damagePoint,
                    shooterPoint = GetPointAtOrBefore(replayShooter.track, log.time),
                    targetPoint = GetPointAtOrBefore(replayTarget.track, log.time)
                };
            }
        }
    }

    static IEnumerable<ReplayEvent> BuildEvents(NavalGameState gameState, List<ReplayShip> replayShips)
    {
        var replayShipById = replayShips.ToDictionary(ship => ship.id, ship => ship);
        var emittedSunk = new HashSet<string>();

        foreach (var ship in gameState.shipLogs.Where(ship => ship != null))
        {
            foreach (var log in ship.logs ?? Enumerable.Empty<ShipLogLog>())
            {
                var description = log.SummaryContent();
                if (log is ShipLogBatteryHitLog or ShipLogRapidFiringGunHitLog or ShipLogTorpedoHitLog)
                {
                    yield return new ReplayEvent
                    {
                        time = log.time,
                        shipId = ship.objectId,
                        shipName = ship.GetMemberName(),
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
                        shipName = ship.GetMemberName(),
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
                    kind = "Sunk",
                    description = $"{replayShip.name} is destroyed/sunk."
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

    static void CompleteFromStreamingAssetReference(ReplayFullState state, string scenarioRoot)
    {
        if (string.IsNullOrWhiteSpace(scenarioRoot))
            return;

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

    static string ResolveScenarioOrSavePath(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        var scenarioRoot = Path.Combine(FindRepositoryRoot(), "Assets", "StreamingAssets", "Scenarios");
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

        throw new FileNotFoundException($"Referenced scenario file not found: {path}", candidate);
    }
}
