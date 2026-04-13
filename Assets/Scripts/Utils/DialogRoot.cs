using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using Unity.Properties;
using UnityEngine.Localization.Settings;
using System.Collections;

using NavalCombatCore;
using StrategicCombatCore;
using CoreUtils;
using NavalCombat;
using UnityEngine.Localization;
using GeographicLib;
using YYZ;

public class ScenarioPickerDialog // ScenarioPicker's root data source
{
    public List<string> scenarioNames = new();

    public string currentDescription;
    public Action<string> callbackOnceScenarioNameGet;
    public NavalGameState currentGameState;

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

    public void Bind(TempDialog tempDialog)
    {
        tempDialog.onCreated += (sender, root) =>
        {
            // var root = tempDialog.root;
            Utils.BindItemsSourceRecursive(root);

            var scenarioListView = root.Q<ListView>("ScenarioListView");
            scenarioListView.selectionChanged += (IEnumerable<object> objects) =>
            {
                Debug.Log("scenarioListView.selectionChanged");

                var scenarioPath = objects.FirstOrDefault() as string;
                if (scenarioPath != null)
                {
                    var scenarioName = scenarioPath.Split("/").Last();
                    // Update information
                    // GameManager.Instance.StartLoadScenarioCoroutine(scenarioName);
                    currentDescription = "Fetching Preview... " + scenarioName; // TODO: Show more informative data like side's deployed units.
                    DialogRoot.Instance.StartCoroutine(
                        StreamingAssetReference.Instance.FetchScenarioFile(scenarioName, fullStateStr =>
                        {
                            var fullState = FullState.FromXML(fullStateStr);
                            var shipCount = fullState.navalGameState.shipLogs.Count(s => s.mapState == MapState.Deployed);
                            var dateTimeUTC = fullState.navalGameState.scenarioState.dateTime;
                            // var dateTimeLocal = fullState.viewState.
                            // TODO: Fetch class to find country info

                            var centerLat = fullState.viewState.GetCenterLatitude();
                            var centerLon = fullState.viewState.GetCenterLongitude();

                            // var dateTimeLocal = fullState.navalGameState.scenarioState.GetLocalDateTime(centerLon);
                            var lines = new List<string>()
                            {
                                scenarioName,
                                Localize("Begin UTC Time: {0}", dateTimeUTC),
                                Localize("Begin Local DateTime: {0}", ScenarioState.GetLocalDateTimeOffset(centerLon, dateTimeUTC)),
                            };
                            if(fullState.navalGameState.scenarioState.hasEndDateTime)
                            {
                                var endDateTimeUTC = fullState.navalGameState.scenarioState.endDateTime;
                                lines.AddRange(new List<string>()
                                {
                                    Localize("End UTC DateTime: {0}", endDateTimeUTC),
                                    Localize("End Local DateTime: {0}", ScenarioState.GetLocalDateTimeOffset(centerLon, endDateTimeUTC)),
                                });
                            }
                            lines.AddRange(new List<string>()
                            {
                                Localize("Ship Count (On Map): {0}", shipCount),
                                Localize("Latitude: {0}, Longtitude: {1}", centerLat, centerLon),
                                Localize("Visibility: {0}", LocalizeEnum(fullState.navalGameState.scenarioState.visibility)),
                                Localize("Sea State (Beaufort): {0}", fullState.navalGameState.scenarioState.seaStateBeaufort),
                                Localize("Description:"),
                                // fullState.navalGameState.scenarioState.description
                                fullState.navalGameState.scenarioState.globalDescription.GetShortName()
                            });
                            currentDescription = string.Join("\n", lines);

                            // currentBackground = fullState.navalGameState.scenarioState.backgroundPictureReference.pictureStyleBackground;
                            currentGameState = fullState.navalGameState;
                        })
                    );
                }
            };
        };
        tempDialog.onConfirmed += (obj, root) =>
        {
            var scenarioListView = root.Q<ListView>("ScenarioListView");
            var scenarioName = scenarioListView.selectedItem as string;
            if (scenarioName != null)
            {
                // GameManager.Instance.StartLoadScenarioCoroutine(scenarioName);
                callbackOnceScenarioNameGet(scenarioName);
            }
        };
    }
}

public class SectorArcIndicatorBinder
{
    // VisualElement root;
    Dictionary<MountLocation, BatteryArcIndicator> uiMap;

    public void BindUI(VisualElement root)
    {

        uiMap = new Dictionary<MountLocation, BatteryArcIndicator>()
        {
            {MountLocation.PortForward, root.Q<BatteryArcIndicator>("PortForward")},
            {MountLocation.Forward, root.Q<BatteryArcIndicator>("Forward")},
            {MountLocation.StarboardForward, root.Q<BatteryArcIndicator>("StarboardForward")},
            {MountLocation.PortMidship, root.Q<BatteryArcIndicator>("PortMidship")},
            {MountLocation.Midship, root.Q<BatteryArcIndicator>("Midship")},
            {MountLocation.StarboardMidship, root.Q<BatteryArcIndicator>("StarboardMidship")},
            {MountLocation.PortAfter, root.Q<BatteryArcIndicator>("PortAfter")},
            {MountLocation.After, root.Q<BatteryArcIndicator>("After")},
            {MountLocation.StarboardAfter, root.Q<BatteryArcIndicator>("StarboardAfter")},
        };
    }

    public void BindBatteryData(ShipClass shipClass)
    {
        BindMountLocationRecords(shipClass?.batteryRecords?.SelectMany(btyRec => btyRec.mountLocationRecords));
    }

    public void BindBatteryData(BatteryRecord batteryRecord)
    {
        BindMountLocationRecords(batteryRecord?.mountLocationRecords);
    }

    public void BindTorpedoData(ShipClass shipClass)
    {
        BindMountLocationRecords(shipClass?.torpedoSector?.mountLocationRecords);
    }

    void BindMountLocationRecords(IEnumerable<MountLocationRecord> mountLocationRecords)
    {
        var updatedSet = new HashSet<MountLocation>();
        foreach (var grouping in (mountLocationRecords ?? Enumerable.Empty<MountLocationRecord>()).GroupBy(mntRec => mntRec.mountLocation))
        {
            if (uiMap.TryGetValue(grouping.Key, out var ui))
            {
                updatedSet.Add(grouping.Key);
                var startEndTopZeroCWAngles = grouping.SelectMany(g => g.mountArcs)
                    .Select(arcRec => (arcRec.startDeg, arcRec.startDeg + arcRec.CoverageDeg))
                    .ToList();
                ui.UpdateStartEndTopZeroCWAngles(startEndTopZeroCWAngles);
            }
        }
        foreach (var (mntLoc, ui) in uiMap)
        {
            if (!updatedSet.Contains(mntLoc))
            {
                ui.UpdateStartEndTopZeroCWAngles(new());
            }
        }
    }
}

public class PlotTrajectoryViewModel
{
    public string shipLogObjectId;

    [CreateProperty]
    public string shipLogName => EntityManager.Instance.Get<ShipLog>(shipLogObjectId)?.namedShip.name.GetMergedName();

    public Color32 color;

    [CreateProperty]
    public int red
    {
        get => color.r;
        set => color = new Color32((byte)value, color.g, color.b, 255);
    }

    [CreateProperty]
    public int green
    {
        get => color.g;
        set => color = new Color32(color.r, (byte)value, color.b, 255);
    }

    [CreateProperty]
    public int blue
    {
        get => color.b;
        set => color = new Color32(color.r, color.g, (byte)value, 255);
    }

    public bool plotTimestamp = true;
    public int timestampIntervalMinutes = 15;
}

public class FollowFormationDialogModel
{
    [CreateProperty]
    public float followDistanceYards { get; set; } = 500f;
}

public enum RelativeFormationMode
{
    KeepCurrentPosition,
    LineAbreast,
    LineOfBearing,
}

public class RelativeFormationDialogModel
{
    [CreateProperty]
    public int modeValue { get; set; } = (int)RelativeFormationMode.KeepCurrentPosition;

    [CreateProperty]
    public float angleDeg { get; set; } = 90f;

    [CreateProperty]
    public float distanceYards { get; set; } = 250f;

    [CreateProperty]
    public bool isSymmetric { get; set; }

    [CreateProperty]
    public bool absolute { get; set; }

    public RelativeFormationMode mode => (RelativeFormationMode)modeValue;
}

public sealed class StrategicViewerSideQuickPickerDialogOption
{
    public string sideObjectId;
    public bool isObserverEditorMode;
    public string displayName;
    public string topGroupName;
    public string flagPath;

    [CreateProperty]
    public StyleBackground flagBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(flagPath);

}

public sealed class StrategicViewerSideQuickPickerDialogModel
{
    public List<StrategicViewerSideQuickPickerDialogOption> options { get; }

    public StrategicViewerSideQuickPickerDialogModel()
    {
        var gameState = StrategicGameManager.Instance.gameState;
        var sideStates = gameState?.sideStates ?? new List<SideState>();
        var strategicGroups = gameState?.strategicGroups ?? new List<StrategicGroup>();

        options = sideStates
            .Where(sideState => sideState != null && sideState.recommended)
            .Select(sideState => BuildSideOption(sideState, strategicGroups))
            .OrderBy(option => option.displayName)
            .ToList();

        options.Add(new StrategicViewerSideQuickPickerDialogOption()
        {
            isObserverEditorMode = true,
            displayName = "Observer / Editor",
            topGroupName = "Free Observation",
        });
    }

    static StrategicViewerSideQuickPickerDialogOption BuildSideOption(SideState sideState, List<StrategicGroup> strategicGroups)
    {
        var topGroupName = strategicGroups
            .FirstOrDefault(group =>
                group != null &&
                group.side == sideState &&
                !group.parentGroupReference.isReferenceAny())
            ?.name?.GetMergedName();

        Country? firstCountry = sideState != null && sideState.countries.Count > 0
            ? sideState.countries[0]
            : null;

        return new StrategicViewerSideQuickPickerDialogOption()
        {
            sideObjectId = sideState?.objectId,
            displayName = sideState?.name?.GetMergedName() ?? "[Unnamed Side]",
            topGroupName = string.IsNullOrWhiteSpace(topGroupName) ? "[No Top Group]" : topGroupName,
            flagPath = firstCountry.HasValue ? Utils.GetCountryPath(firstCountry.Value) : null,
            // flagBackground = firstCountry.HasValue
            //     ? UnityWebRequestImageReader.Instance.FetchStyleBackground(Utils.GetCountryPath(firstCountry.Value))
            //     : default,
        };
    }
}

public class TorpedoInterceptSolutionDialogModel
{
    public string shooterObjectId;
    public string targetObjectId;
}

public sealed class TorpedoInterceptRandomModel
{
    public float minSpeedKnots;
    public float maxSpeedKnots;
    public float lowerHeadingOffsetDeg;
    public float upperHeadingOffsetDeg;
}

public enum TorpedoInterceptMountDiagnosticStatus
{
    Invalid,
    Disabled,
    NoTorpedoSetting,
    Reloading,
    NoAmmunition,
    DoctrineBlocked,
    Unsafe,
    NoSolution,
    OutOfArc,
    CanFire
}

public sealed class TorpedoInterceptVisualizerSolution
{
    public ShipLog shooter;
    public ShipLog target;
    public float currentDistanceYards;
    public bool isValid;
    public bool doctrineRespected;
    public bool isSafeToFire;
    public TorpedoInterceptFailureReason failureReason;
    public TorpedoSetting selectedSetting;
    public TorpedoAttackContext.ShipLogPairSupplementary selectedSupplementary;
    public InterceptionPointSolver.Result interceptionResult;
    public LatLon interceptionPoint;
    public bool hasSolution => selectedSupplementary != null && interceptionResult != null && interceptionResult.success;
}

public sealed class TorpedoInterceptProbabilityEstimate
{
    public bool isValid;
    public float hitProbability;
    public int headingDivisions;
    public int speedDivisions;
    public List<LatLon> futureRegionGridPoints = new();
    public List<LatLon> hitRegionVertices = new();
    public List<int> hitRegionTriangles = new();
}

public static class TorpedoInterceptSolutionDialogSupport
{
    static readonly float knotToMetersPerSecond = MeasureUtils.navalMileToMeter / 3600f;
    const float torpedoInterceptGeometryEpsilon = 0.0001f;

    struct TorpedoInterceptSpeedRange
    {
        public bool isValid;
        public float minSpeedKnots;
        public float maxSpeedKnots;
    }

    public static string GetShipDisplayName(ShipLog shipLog)
    {
        if (shipLog == null)
            return "[Invalid]";
        return shipLog.namedShip?.name?.GetShortName()
            ?? shipLog.namedShip?.name?.GetMergedName()
            ?? shipLog.objectId
            ?? "[Invalid]";
    }

    public static bool IsValidShooterTargetCombination(ShipLog shooter, ShipLog target)
    {
        if (shooter == null || target == null)
            return false;
        if (!shooter.IsOnMap() || !target.IsOnMap())
            return false;
        if (shooter == target)
            return false;
        return (shooter as IShipGroupMember)?.GetRootParent() != (target as IShipGroupMember)?.GetRootParent();
    }

    public static ShipLog GetDefaultTarget(ShipLog shooter)
    {
        if (shooter == null || !shooter.IsOnMap())
            return null;
        return new TorpedoBattery() { original = shooter }.GetCurrentFiringTarget() as ShipLog;
    }

    public static List<ShipLog> GetTargetCandidates(ShipLog shooter)
    {
        if (shooter == null || !shooter.IsOnMap())
            return new List<ShipLog>();

        var shooterRootParent = (shooter as IShipGroupMember)?.GetRootParent();
        return NavalGameState.Instance.shipLogsOnMap
            .Where(target => target != null && target != shooter && (target as IShipGroupMember)?.GetRootParent() != shooterRootParent)
            .OrderBy(target => GetCurrentDistanceYards(shooter, target))
            .ToList();
    }

    public static float GetCurrentDistanceYards(ShipLog shooter, ShipLog target)
    {
        if (shooter == null || target == null)
            return float.PositiveInfinity;
        var (distanceKm, _) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(
            shooter.position.LatDeg, shooter.position.LonDeg,
            target.position.LatDeg, target.position.LonDeg
        );
        return (float)(distanceKm * 1000 * MeasureUtils.meterToYard);
    }

    public static string BuildTargetChoiceLabel(ShipLog shooter, ShipLog target)
    {
        var distanceYards = GetCurrentDistanceYards(shooter, target);
        return $"{GetShipDisplayName(target)} ({distanceYards:0} yd)";
    }

    public static TorpedoInterceptRandomModel BuildDefaultRandomModel(ShipLog target)
    {
        var speedKnots = Mathf.Max(0f, target?.speedKnots ?? 0f);
        return new TorpedoInterceptRandomModel()
        {
            minSpeedKnots = speedKnots * 0.8f,
            maxSpeedKnots = speedKnots * 1.2f,
            lowerHeadingOffsetDeg = -30f,
            upperHeadingOffsetDeg = 30f
        };
    }

    public static TorpedoInterceptRandomModel SanitizeRandomModel(TorpedoInterceptRandomModel model)
    {
        if (model == null)
            return BuildDefaultRandomModel(null);

        var minSpeedKnots = Mathf.Max(0f, model.minSpeedKnots);
        var maxSpeedKnots = Mathf.Max(0f, model.maxSpeedKnots);
        if (minSpeedKnots > maxSpeedKnots)
            (minSpeedKnots, maxSpeedKnots) = (maxSpeedKnots, minSpeedKnots);

        var lowerHeadingOffsetDeg = model.lowerHeadingOffsetDeg;
        var upperHeadingOffsetDeg = model.upperHeadingOffsetDeg;
        if (lowerHeadingOffsetDeg > upperHeadingOffsetDeg)
            (lowerHeadingOffsetDeg, upperHeadingOffsetDeg) = (upperHeadingOffsetDeg, lowerHeadingOffsetDeg);

        return new TorpedoInterceptRandomModel()
        {
            minSpeedKnots = minSpeedKnots,
            maxSpeedKnots = maxSpeedKnots,
            lowerHeadingOffsetDeg = lowerHeadingOffsetDeg,
            upperHeadingOffsetDeg = upperHeadingOffsetDeg
        };
    }

    public static float GetTargetLengthMeters(ShipLog target)
    {
        if (target == null)
            return 1f;
        return Mathf.Max(1f, target.GetLengthFoot() * MeasureUtils.footToYard * MeasureUtils.yardToMeter);
    }

    public static float GetTargetBeamMeters(ShipLog target)
    {
        if (target == null)
            return 1f;
        return Mathf.Max(1f, target.GetBeamFoot() * MeasureUtils.footToYard * MeasureUtils.yardToMeter);
    }

    public static LatLon OffsetLatLon(LatLon origin, float eastMeters, float northMeters)
    {
        var distanceMeters = Mathf.Sqrt(eastMeters * eastMeters + northMeters * northMeters);
        if (distanceMeters <= 0.001f)
            return origin;

        var azimuthDeg = MeasureUtils.NormalizeAngle(Mathf.Atan2(eastMeters, northMeters) * Mathf.Rad2Deg);
        Geodesic.WGS84.Direct(origin.LatDeg, origin.LonDeg, azimuthDeg, distanceMeters, out var lat2, out var lon2);
        return new LatLon((float)lat2, (float)lon2);
    }

    public static Vector2 ProjectRelativeMeters(LatLon origin, LatLon point)
    {
        var (distanceKm, bearingDeg) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(
            origin.LatDeg, origin.LonDeg,
            point.LatDeg, point.LonDeg
        );
        var distanceMeters = (float)(distanceKm * 1000);
        var bearingRad = (float)(bearingDeg * Mathf.Deg2Rad);
        return new Vector2(
            distanceMeters * Mathf.Sin(bearingRad),
            distanceMeters * Mathf.Cos(bearingRad)
        );
    }

    static Vector2 HeadingToDirection(float headingDeg)
    {
        var headingRad = headingDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(headingRad), Mathf.Cos(headingRad));
    }

    static float Cross2D(Vector2 lhs, Vector2 rhs) => lhs.x * rhs.y - lhs.y * rhs.x;

    static LatLon GetFuturePositionAtArrival(ShipLog target, float headingDeg, float speedKnots, float arrivalSeconds)
    {
        var travelDistanceMeters = speedKnots * knotToMetersPerSecond * arrivalSeconds;
        var (futureLat, futureLon) = MeasureStats.Approximation.CalculateNewPosition(
            target.position.LatDeg,
            target.position.LonDeg,
            headingDeg,
            travelDistanceMeters
        );
        return new LatLon((float)futureLat, (float)futureLon);
    }

    static TorpedoInterceptSpeedRange EvaluateHeadingHitSpeedRange(
        TorpedoInterceptVisualizerSolution solution,
        TorpedoInterceptRandomModel sanitizedModel,
        float headingDeg,
        Vector2 shooterRelativeMeters,
        Vector2 torpedoDirection,
        float torpedoSpeedMetersPerSecond,
        float torpedoMaxTravelSeconds,
        float halfLengthMeters,
        float halfBeamMeters
    )
    {
        if (solution?.target == null || torpedoSpeedMetersPerSecond <= torpedoInterceptGeometryEpsilon)
            return default;

        var forward = HeadingToDirection(headingDeg);
        var determinant = Cross2D(torpedoDirection, forward);
        var absDeterminant = Mathf.Abs(determinant);
        if (absDeterminant <= torpedoInterceptGeometryEpsilon)
            return default;

        var targetOriginFromShooter = -shooterRelativeMeters;
        var torpedoDistanceMeters = Cross2D(targetOriginFromShooter, forward) / determinant;
        var targetAlongTrackMeters = Cross2D(targetOriginFromShooter, torpedoDirection) / determinant;
        if (torpedoDistanceMeters < 0f || targetAlongTrackMeters < 0f)
            return default;

        var torpedoArrivalSeconds = torpedoDistanceMeters / torpedoSpeedMetersPerSecond;
        if (torpedoArrivalSeconds < 0f || torpedoArrivalSeconds > torpedoMaxTravelSeconds)
            return default;

        var right = new Vector2(forward.y, -forward.x);
        var torpedoNormal = new Vector2(torpedoDirection.y, -torpedoDirection.x);
        var normalProjectionOnForward = Vector2.Dot(torpedoNormal, forward);
        if (Mathf.Abs(normalProjectionOnForward) <= torpedoInterceptGeometryEpsilon)
            return default;

        var halfExtentNormal = Mathf.Abs(Vector2.Dot(torpedoNormal, forward)) * halfLengthMeters
                               + Mathf.Abs(Vector2.Dot(torpedoNormal, right)) * halfBeamMeters;
        var deltaAlongTrackMeters = halfExtentNormal / Mathf.Abs(normalProjectionOnForward);
        var safeArrivalSeconds = Mathf.Max(torpedoArrivalSeconds, torpedoInterceptGeometryEpsilon);
        var minSpeedMetersPerSecond = Mathf.Max(0f, (targetAlongTrackMeters - deltaAlongTrackMeters) / safeArrivalSeconds);
        var maxSpeedMetersPerSecond = (targetAlongTrackMeters + deltaAlongTrackMeters) / safeArrivalSeconds;
        if (maxSpeedMetersPerSecond < 0f)
            return default;

        var minSpeedKnots = minSpeedMetersPerSecond / knotToMetersPerSecond;
        var maxSpeedKnots = maxSpeedMetersPerSecond / knotToMetersPerSecond;
        minSpeedKnots = Mathf.Max(sanitizedModel.minSpeedKnots, minSpeedKnots);
        maxSpeedKnots = Mathf.Min(sanitizedModel.maxSpeedKnots, maxSpeedKnots);
        if (maxSpeedKnots < minSpeedKnots)
            return default;

        return new TorpedoInterceptSpeedRange()
        {
            isValid = true,
            minSpeedKnots = minSpeedKnots,
            maxSpeedKnots = maxSpeedKnots
        };
    }

    public static TorpedoInterceptProbabilityEstimate EvaluateProbability(
        TorpedoInterceptVisualizerSolution solution,
        TorpedoInterceptRandomModel model,
        int speedSamples = 15,
        int headingSamples = 21
    )
    {
        var estimate = new TorpedoInterceptProbabilityEstimate()
        {
            isValid = solution != null && solution.hasSolution
        };
        if (!estimate.isValid)
            return estimate;

        var sanitizedModel = SanitizeRandomModel(model);
        var arrivalSeconds = Mathf.Max(0f, solution.interceptionResult.arrivalSeconds);
        estimate.headingDivisions = Mathf.Max(1, headingSamples);
        estimate.speedDivisions = Mathf.Max(1, speedSamples);

        var halfLengthMeters = GetTargetLengthMeters(solution.target) * 0.5f;
        var halfBeamMeters = GetTargetBeamMeters(solution.target) * 0.5f;
        var startHeadingDeg = solution.target.headingDeg + sanitizedModel.lowerHeadingOffsetDeg;
        var endHeadingDeg = solution.target.headingDeg + sanitizedModel.upperHeadingOffsetDeg;
        var safeSpeedSamples = estimate.speedDivisions;
        var safeHeadingSamples = estimate.headingDivisions;
        var shooterRelativeMeters = ProjectRelativeMeters(solution.target.position, solution.shooter.position);
        var torpedoDirection = HeadingToDirection(solution.interceptionResult.azimuth);
        var torpedoSpeedMetersPerSecond = Mathf.Max(
            torpedoInterceptGeometryEpsilon,
            (solution.selectedSetting?.speedKnots ?? 0f) * knotToMetersPerSecond
        );
        var torpedoMaxTravelSeconds = solution.selectedSetting != null
            ? solution.selectedSetting.rangeYards * MeasureUtils.yardToMeter / torpedoSpeedMetersPerSecond
            : 0f;
        var hitSamples = 0;
        var totalSamples = 0;

        for (var headingIdx = 0; headingIdx <= safeHeadingSamples; headingIdx++)
        {
            var headingT = safeHeadingSamples == 0 ? 0f : headingIdx / (float)safeHeadingSamples;
            var headingDeg = Mathf.Lerp(startHeadingDeg, endHeadingDeg, headingT);
            for (var speedIdx = 0; speedIdx <= safeSpeedSamples; speedIdx++)
            {
                var speedT = safeSpeedSamples == 0 ? 0f : speedIdx / (float)safeSpeedSamples;
                var speedKnots = Mathf.Lerp(sanitizedModel.minSpeedKnots, sanitizedModel.maxSpeedKnots, speedT);
                estimate.futureRegionGridPoints.Add(GetFuturePositionAtArrival(solution.target, headingDeg, speedKnots, arrivalSeconds));
            }
        }

        var headingLineDegs = new float[safeHeadingSamples + 1];
        var headingLineRanges = new TorpedoInterceptSpeedRange[safeHeadingSamples + 1];
        for (var headingIdx = 0; headingIdx <= safeHeadingSamples; headingIdx++)
        {
            var headingT = safeHeadingSamples == 0 ? 0f : headingIdx / (float)safeHeadingSamples;
            var headingDeg = Mathf.Lerp(startHeadingDeg, endHeadingDeg, headingT);
            headingLineDegs[headingIdx] = headingDeg;
            headingLineRanges[headingIdx] = EvaluateHeadingHitSpeedRange(
                solution,
                sanitizedModel,
                headingDeg,
                shooterRelativeMeters,
                torpedoDirection,
                torpedoSpeedMetersPerSecond,
                torpedoMaxTravelSeconds,
                halfLengthMeters,
                halfBeamMeters
            );
        }

        for (var headingIdx = 0; headingIdx < safeHeadingSamples; headingIdx++)
        {
            var headingT = (headingIdx + 0.5f) / safeHeadingSamples;
            var headingDeg = Mathf.Lerp(startHeadingDeg, endHeadingDeg, headingT);
            var hitSpeedRange = EvaluateHeadingHitSpeedRange(
                solution,
                sanitizedModel,
                headingDeg,
                shooterRelativeMeters,
                torpedoDirection,
                torpedoSpeedMetersPerSecond,
                torpedoMaxTravelSeconds,
                halfLengthMeters,
                halfBeamMeters
            );

            for (var speedIdx = 0; speedIdx < safeSpeedSamples; speedIdx++)
            {
                var speedT = (speedIdx + 0.5f) / safeSpeedSamples;
                var speedKnots = Mathf.Lerp(sanitizedModel.minSpeedKnots, sanitizedModel.maxSpeedKnots, speedT);
                var hit = hitSpeedRange.isValid
                          && speedKnots >= hitSpeedRange.minSpeedKnots - torpedoInterceptGeometryEpsilon
                          && speedKnots <= hitSpeedRange.maxSpeedKnots + torpedoInterceptGeometryEpsilon;
                if (hit)
                    hitSamples++;
                totalSamples++;
            }
        }

        int AddHitRegionVertex(float headingDeg, float speedKnots)
        {
            estimate.hitRegionVertices.Add(GetFuturePositionAtArrival(solution.target, headingDeg, speedKnots, arrivalSeconds));
            return estimate.hitRegionVertices.Count - 1;
        }

        void AddHitRegionTriangle(int a, int b, int c)
        {
            estimate.hitRegionTriangles.Add(a);
            estimate.hitRegionTriangles.Add(b);
            estimate.hitRegionTriangles.Add(c);
        }

        for (var headingIdx = 0; headingIdx < safeHeadingSamples; headingIdx++)
        {
            var currentRange = headingLineRanges[headingIdx];
            var nextRange = headingLineRanges[headingIdx + 1];
            var currentHeadingDeg = headingLineDegs[headingIdx];
            var nextHeadingDeg = headingLineDegs[headingIdx + 1];

            if (currentRange.isValid && nextRange.isValid)
            {
                var currentLower = AddHitRegionVertex(currentHeadingDeg, currentRange.minSpeedKnots);
                var nextLower = AddHitRegionVertex(nextHeadingDeg, nextRange.minSpeedKnots);
                var currentUpper = AddHitRegionVertex(currentHeadingDeg, currentRange.maxSpeedKnots);
                var nextUpper = AddHitRegionVertex(nextHeadingDeg, nextRange.maxSpeedKnots);
                AddHitRegionTriangle(currentLower, nextLower, currentUpper);
                AddHitRegionTriangle(currentUpper, nextLower, nextUpper);
                continue;
            }

            if (currentRange.isValid)
            {
                var apexSpeedKnots = Mathf.Clamp(
                    0.5f * (currentRange.minSpeedKnots + currentRange.maxSpeedKnots),
                    sanitizedModel.minSpeedKnots,
                    sanitizedModel.maxSpeedKnots
                );
                var currentLower = AddHitRegionVertex(currentHeadingDeg, currentRange.minSpeedKnots);
                var apex = AddHitRegionVertex(nextHeadingDeg, apexSpeedKnots);
                var currentUpper = AddHitRegionVertex(currentHeadingDeg, currentRange.maxSpeedKnots);
                AddHitRegionTriangle(currentLower, apex, currentUpper);
                continue;
            }

            if (nextRange.isValid)
            {
                var apexSpeedKnots = Mathf.Clamp(
                    0.5f * (nextRange.minSpeedKnots + nextRange.maxSpeedKnots),
                    sanitizedModel.minSpeedKnots,
                    sanitizedModel.maxSpeedKnots
                );
                var apex = AddHitRegionVertex(currentHeadingDeg, apexSpeedKnots);
                var nextLower = AddHitRegionVertex(nextHeadingDeg, nextRange.minSpeedKnots);
                var nextUpper = AddHitRegionVertex(nextHeadingDeg, nextRange.maxSpeedKnots);
                AddHitRegionTriangle(apex, nextLower, nextUpper);
            }
        }

        estimate.hitProbability = totalSamples > 0 ? hitSamples / (float)totalSamples : 0f;
        return estimate;
    }

    public static TorpedoInterceptVisualizerSolution EvaluateSolution(ShipLog shooter, ShipLog target, TorpedoAttackContext torpedoAttackContext)
    {
        var solution = new TorpedoInterceptVisualizerSolution()
        {
            shooter = shooter,
            target = target,
            isValid = IsValidShooterTargetCombination(shooter, target)
        };

        if (!solution.isValid)
            return solution;

        solution.currentDistanceYards = GetCurrentDistanceYards(shooter, target);
        solution.doctrineRespected = shooter.doctrine.GetMaximumFiringDistanceYardsForTorpedo().IsGreaterThanIfSpecified(solution.currentDistanceYards);

        var settings = shooter.shipClass?.torpedoSector?.torpedoSettings ?? new List<TorpedoSetting>();
        if (settings.Count == 0)
            return solution;

        TorpedoAttackContext.ShipLogPairSupplementary firstSupplementary = null;
        float minInterceptionDistanceYards = float.PositiveInfinity;

        foreach (var setting in settings)
        {
            var supplementary = torpedoAttackContext.GetOrCalculateFireComplexSupplementary(shooter, target, setting.speedKnots);
            firstSupplementary ??= supplementary;

            if (supplementary == null)
                continue;

            if (supplementary.failureReason == TorpedoInterceptFailureReason.Unsafe)
            {
                solution.isSafeToFire = false;
                solution.failureReason = TorpedoInterceptFailureReason.Unsafe;
            }
            else if (solution.failureReason == TorpedoInterceptFailureReason.None)
            {
                solution.isSafeToFire = supplementary.isSafeToFire;
                solution.failureReason = supplementary.failureReason;
            }

            var interceptionResult = supplementary.interceptionPointSolverResult;
            if (interceptionResult == null || !interceptionResult.success || interceptionResult.distanceYards >= setting.rangeYards)
                continue;

            if (interceptionResult.distanceYards < minInterceptionDistanceYards)
            {
                minInterceptionDistanceYards = interceptionResult.distanceYards;
                solution.selectedSetting = setting;
                solution.selectedSupplementary = supplementary;
                solution.interceptionResult = interceptionResult;
            }
        }

        if (firstSupplementary != null)
        {
            solution.isSafeToFire = firstSupplementary.isSafeToFire;
            if (!solution.hasSolution)
                solution.failureReason = firstSupplementary.failureReason;
        }

        if (solution.hasSolution)
        {
            solution.isSafeToFire = true;
            solution.failureReason = TorpedoInterceptFailureReason.None;
            Geodesic.WGS84.Direct(
                shooter.position.LatDeg, shooter.position.LonDeg,
                solution.interceptionResult.azimuth,
                solution.interceptionResult.distanceYards * MeasureUtils.yardToMeter,
                out var lat2,
                out var lon2
            );
            solution.interceptionPoint = new LatLon((float)lat2, (float)lon2);
        }

        return solution;
    }

    public static TorpedoInterceptMountDiagnosticStatus EvaluateMountStatus(TorpedoMountStatusRecord mount, TorpedoInterceptVisualizerSolution solution)
    {
        if (mount == null || solution == null || !solution.isValid)
            return TorpedoInterceptMountDiagnosticStatus.Invalid;

        if (!mount.IsOperational())
            return TorpedoInterceptMountDiagnosticStatus.Disabled;

        var shooter = solution.shooter;
        var classSector = shooter?.shipClass?.torpedoSector;
        if (classSector == null || classSector.torpedoSettings.Count == 0)
            return TorpedoInterceptMountDiagnosticStatus.NoTorpedoSetting;

        if (mount.currentLoad <= 0)
            return CanReload(mount, shooter)
                ? TorpedoInterceptMountDiagnosticStatus.Reloading
                : TorpedoInterceptMountDiagnosticStatus.NoAmmunition;

        if (!solution.doctrineRespected)
            return TorpedoInterceptMountDiagnosticStatus.DoctrineBlocked;

        if (!solution.isSafeToFire)
            return TorpedoInterceptMountDiagnosticStatus.Unsafe;

        if (!solution.hasSolution)
            return TorpedoInterceptMountDiagnosticStatus.NoSolution;

        var recordInfo = mount.GetTorpedoMountLocationRecordInfo();
        if (recordInfo?.record == null)
            return TorpedoInterceptMountDiagnosticStatus.Invalid;

        var bearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(solution.interceptionResult.azimuth - shooter.headingDeg);
        if (!recordInfo.record.IsInArc(bearingRelativeToBowDeg))
            return TorpedoInterceptMountDiagnosticStatus.OutOfArc;

        return TorpedoInterceptMountDiagnosticStatus.CanFire;
    }

    static bool CanReload(TorpedoMountStatusRecord mount, ShipLog shooter)
    {
        if (mount == null || shooter == null)
            return false;

        var recordInfo = mount.GetTorpedoMountLocationRecordInfo();
        if (recordInfo?.record == null)
            return false;

        var requested = mount.barrels - mount.currentLoad;
        var ammunitionCap = shooter.torpedoSectorStatus.ammunition;
        int reloadLimitCap;
        if (TorpedoMountStatusRecord.disableTorpedoReload)
        {
            reloadLimitCap = 0;
        }
        else
        {
            reloadLimitCap = recordInfo.record.reloadLimit == 0 ? int.MaxValue : recordInfo.record.reloadLimit - mount.reloadedLoad;
        }

        var transferred = Math.Min(reloadLimitCap, Math.Min(requested, ammunitionCap));
        return transferred > 0;
    }
}


enum TheaterSelectorDisplayMode
{
    Membership,
    Frontline,
    WeightRequested
}

public class DialogRoot : SingletonDocument<DialogRoot>
{
    public VisualTreeAsset shipLogSelectorDocument;
    public VisualTreeAsset leaderSelectorDocument;
    public VisualTreeAsset shipClassSelectorDocument;
    public VisualTreeAsset namedShipSelectorDocument;
    public VisualTreeAsset messageDialogDocument;
    public VisualTreeAsset confirmDialogDocument;
    public VisualTreeAsset followFormationDialogDocument;
    public VisualTreeAsset shipClassPlaceholderGeneratorDialogDocument;
    public VisualTreeAsset relativeFormationDialogDocument;
    public VisualTreeAsset preScenarioDamageDialogDocument;
    public VisualTreeAsset streamingAssetReferenceDialogDocument;
    public VisualTreeAsset scenarioPickerDialogDocument;
    public VisualTreeAsset victoryStatusDocument;
    public VisualTreeAsset strategicVictoryStatusDialogDocument;
    public VisualTreeAsset helpDialogDocument;
    public VisualTreeAsset aboutDialogDocument;
    public VisualTreeAsset faqDialogDocument;
    public VisualTreeAsset locationLabelDialogDocument;
    public VisualTreeAsset navalLocationLabelEditorDialogDocument;
    public VisualTreeAsset shipGroupRemarkDialogDocument;
    public VisualTreeAsset locationLabelsEditorDialogDocument;
    public VisualTreeAsset subordinatePickerDialogDocument;
    public VisualTreeAsset strategicGroupTransferDialogDocument;
    public VisualTreeAsset strategicGroupPickerDialogDocument;
    public VisualTreeAsset gamePreferenceDialogDocument;
    public VisualTreeAsset batteryArcIndicatorDialogDocument;
    public VisualTreeAsset plotTrajectoryDialogDocument;
    public VisualTreeAsset influenceMapDialogDocument;
    public VisualTreeAsset wtaSolverInspectorDialogDocument;
    public VisualTreeAsset torpedoInterceptSolutionVisualizerDialogDocument;
    public VisualTreeAsset strategicInfluenceMapDialogDocument;
    public VisualTreeAsset shipTimeLocDialogDocument;
    public VisualTreeAsset eventStateEditorDialogDocument;
    public VisualTreeAsset weaponPickerDialogDocument;
    public VisualTreeAsset sideStatePickerDialogDocument;
    public VisualTreeAsset strategicViewerSideQuickPickerDialogDocument;
    public VisualTreeAsset strategicViewerSideQuickPickerOptionDocument;
    public VisualTreeAsset currentSideAutomationDialogDocument;
    public VisualTreeAsset landUnitTemplateDialogDocument;
    public VisualTreeAsset subStrategicCombatDialogDocument;
    public VisualTreeAsset cellEditorDialogDocument;
    public VisualTreeAsset pendingNavalCombatDialogDocument;
    public VisualTreeAsset navalCombatResolverDialogDocument;
    public VisualTreeAsset oobTreeDialogDocument;
    public VisualTreeAsset landBattleDialogDocument;
    public VisualTreeAsset aiDialogDocument;
    public VisualTreeAsset insertShipComplexDialogDocument;
    public VisualTreeAsset forceBuilderDialogDocument;
    public VisualTreeAsset autoDeploymentDialogDocument;
    public VisualTreeAsset batteryRecordSelectorDialogDocument;
    public VisualTreeAsset rapidFireBatteryRecordSelectorDialogDocument;
    public VisualTreeAsset torpedoSectorSelectorDialogDocument;
    public VisualTreeAsset scenarioStateEditorDialogDocument;
    public VisualTreeAsset vladivostokSquadronRaidingSideSelectorDialogDocument;
    public VisualTreeAsset strategicScenarioStateEditorDialogDocument;
    public VisualTreeAsset unbindHitAreaDialogDocument;
    public VisualTreeAsset strategicGroupDialogDocument;
    public VisualTreeAsset strategicReinforcementDialogDocument;
    public VisualTreeAsset strategicReleaseDialogDocument;
    public VisualTreeAsset shipLogDialogDocument;
    public VisualTreeAsset landUnitDialogDocument;
    public VisualTreeAsset strategicMissionSelectorDialogDocument;
    public VisualTreeAsset createMissionDialogDocument;
    public VisualTreeAsset hostDialogDocument;
    public VisualTreeAsset clientDialogDocument;
    public VisualTreeAsset pointListEditorDialogDocument;
    public VisualTreeAsset rectangleEditorDialogDocument;
    public VisualTreeAsset strategicStartupDialogDocument;
    public VisualTreeAsset theaterSelectorDialogDocument;
    public VisualTreeAsset theaterDetailDialogDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PopupStrategicStartupDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicStartupDialogDocument,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            MainMenu.RegisterStrategicStartup(el);
        };

        tempDialog.Popup();
    }

    public void PopupRectangleEditorDialog(Action callback)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=rectangleEditorDialogDocument,
            templateDataSource=StrategicGameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ResetButton").clicked += () =>
            {
                // StrategicGameManager.Instance.currentEditingPointList?.Clear();
                var rect = StrategicGameManager.Instance.currentEditingRect;
                if(rect != null)
                {
                    rect.xy1 = null;
                    rect.xy2 = null;
                }
            };
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            callback();
        };

        tempDialog.Popup();
    }

    public void PopupPointListEditorDialog(Action callback)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=pointListEditorDialogDocument,
            templateDataSource=StrategicGameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ResetButton").clicked += () =>
            {
                StrategicGameManager.Instance.currentEditingPointList?.Clear();
            };
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            callback();
        };

        tempDialog.Popup();
    }

    public void PopupClientDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=clientDialogDocument,
            templateDataSource=GameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            GameManager.Instance.networkingName = GameManager.Instance.networkingName == GameManager.defaultNetworkingName ? "Client" : GameManager.Instance.networkingName;
        
            el.Q<Button>("ConnectButton").clicked += GameManager.Instance.DoConnect;
            el.Q<Button>("DisconnectButton").clicked += GameManager.Instance.DoDisconnect;

            var listView = el.Q<ListView>();
            listView.makeItem = () =>
            {
                var ret = listView.itemTemplate.CloneTree();
                Utils.BindItemsSourceRecursive(ret);
                return ret;
            };
        };

        tempDialog.Popup();
    }

    public void PopupHostDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=hostDialogDocument,
            templateDataSource=GameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            GameManager.Instance.networkingName = GameManager.Instance.networkingName == GameManager.defaultNetworkingName ? "Host" : GameManager.Instance.networkingName;

            el.Q<Button>("StartHostButton").clicked += GameManager.Instance.DoStartHost;
            el.Q<Button>("StopHostButton").clicked += GameManager.Instance.DoDisconnect;

            var listView = el.Q<ListView>();
            listView.makeItem = () =>
            {
                var ret = listView.itemTemplate.CloneTree();
                Utils.BindItemsSourceRecursive(ret);
                return ret;
            };
        };

        tempDialog.Popup();
    }

    public void PopupCreateMissionDialog(Action<StrategicMission> callback)
    {
        var createMissionDialog = new CreateMissionDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=createMissionDialogDocument,
            templateDataSource=createMissionDialog
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            createMissionDialog.OnConfirm();
        };

        tempDialog.Popup();
    }


    public TempDialog PopupLandUnitDialog(LandUnit landUnit)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=landUnitDialogDocument,
            templateDataSource=landUnit
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new LandUnitView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public TempDialog PopupShipLogDialog(ShipLog shipLog)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=shipLogDialogDocument,
            templateDataSource=shipLog
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new ShipLogView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    // public TempDialog BuildShipLogDialog(ShipLog shipLog)
    // {
    //     var tempDialog = new TempDialog()
    //     {
    //         root=root,
    //         template=shipLogDialogDocument,
    //         templateDataSource=shipLog
    //     };

    //     tempDialog.onCreated += (sender, el) =>
    //     {
    //         var binder = new ShipLogView(){root=el};
    //         binder.Bind();
    //     };

    //     return tempDialog;
    // }

    public TempDialog PopupStrategicGroupDialog(StrategicGroup strategicGroup)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicGroupDialogDocument,
            templateDataSource=strategicGroup
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new StrategicGroupView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public void PopupUnbindHitAreaDialog(HitArea hitArea)
    {
        var unbindHitAreaDialog = new UnbindHitAreaDialog()
        {
            currentHitArea=hitArea
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=unbindHitAreaDialogDocument,
            templateDataSource=unbindHitAreaDialog
        };

        tempDialog.onCreated += unbindHitAreaDialog.OnCreated;
        tempDialog.onConfirmed += unbindHitAreaDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupStrategicScenarioStateEditorDialog()
    {
        var strategicScenarioStateEditor = new StrategicScenarioStateEditor();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicScenarioStateEditorDialogDocument,
            templateDataSource=strategicScenarioStateEditor
        };

        tempDialog.onCreated += strategicScenarioStateEditor.OnCreated;
        tempDialog.onConfirmed += strategicScenarioStateEditor.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupVladivostokSquadronRaidingSideSelectorDialog()
    {
        var vladivostokSquadronRaidingSideSelector = new VladivostokSquadronRaidingSideSelector();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=vladivostokSquadronRaidingSideSelectorDialogDocument,
            templateDataSource=vladivostokSquadronRaidingSideSelector
        };

        // tempDialog.onCreated += torpedoSectorSelectorDialog.OnCreated;
        // tempDialog.onConfirmed += torpedoSectorSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupStrategicMissionSelectorDialogDocument(Action<StrategicMission> callback, StrategicMission parentMission)
    {
        var selectorDialog = new NamedSelector<StrategicMission>()
        {
            fullObjects = StrategicGameState.Instance.missions.Where(m => m.parentMissionRef.Get() == null && parentMission != m).ToList(),
            callback = callback
        };
        selectorDialog.RefreshFilter();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicMissionSelectorDialogDocument,
            templateDataSource=selectorDialog
        };

        // tempDialog.onCreated += selectorDialog.OnCreated;
        tempDialog.onConfirmed += selectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupTheaterSelectorDialog()
    {
        if (theaterSelectorDialogDocument == null)
        {
            PopupMessageDialog("TheaterSelectorDialog is not configured.");
            return;
        }

        var state = StrategicGameState.Instance;
        state.RefreshTheaters();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = theaterSelectorDialogDocument,
            templateDataSource = state
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var displayModeDropdownField = el.Q<DropdownField>("DisplayModeDropdownField");
            var theaterListView = el.Q<ListView>("TheaterListView");
            List<Theater> orderedTheaters = null;
            var selectedDisplayMode = TheaterSelectorDisplayMode.Membership;
            theaterListView.makeItem = () =>
            {
                var item = theaterListView.itemTemplate.CloneTree();
                var detailButton = item.Q<Button>("DetailButton");
                detailButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(item, out Theater theater))
                    {
                        PopupTheaterDetailDialog(theater);
                    }
                };
                return item;
            };

            void RefreshDisplayModeDropdown()
            {
                if (displayModeDropdownField == null)
                    return;

                displayModeDropdownField.choices = new()
                {
                    Localize("Membership"),
                    Localize("Frontline"),
                    Localize("Weight Requested")
                };
                displayModeDropdownField.SetValueWithoutNotify(displayModeDropdownField.choices[(int)selectedDisplayMode]);
            }

            void RefreshOrderedTheaters()
            {
                orderedTheaters = (state.theaters ?? new List<Theater>())
                    .Select((theater, index) => new { theater, index })
                    .Where(x => x.theater != null)
                    .OrderByDescending(x => x.theater.cells?.Count ?? 0)
                    .ThenBy(x => x.index)
                    .Select(x => x.theater)
                    .ToList();
                theaterListView.itemsSource = orderedTheaters;
                theaterListView.Rebuild();
            }

            void UpdateTheaterOverlay(Theater theater)
            {
                if (theater == null)
                {
                    HexMapShower.Instance?.ClearTheaterOverlay();
                    return;
                }

                if (selectedDisplayMode == TheaterSelectorDisplayMode.Frontline)
                {
                    HexMapShower.Instance?.SetTheaterOverlayTexts(state.BuildTheaterFrontlineOverlayTexts(theater));
                    return;
                }

                if (selectedDisplayMode == TheaterSelectorDisplayMode.WeightRequested)
                {
                    HexMapShower.Instance?.SetTheaterOverlayTexts(state.BuildTheaterFrontlineWeightRequestedOverlayTexts(theater));
                    return;
                }

                HexMapShower.Instance?.SetTheaterOverlay(theater.cells);
            }

            void SyncSelectionByObjectId(string objectId)
            {
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    theaterListView.ClearSelection();
                    HexMapShower.Instance?.ClearTheaterOverlay();
                    return;
                }

                var selectedIndex = orderedTheaters?.FindIndex(theater => theater?.objectId == objectId) ?? -1;
                if (selectedIndex < 0)
                {
                    theaterListView.ClearSelection();
                    HexMapShower.Instance?.ClearTheaterOverlay();
                    return;
                }

                UpdateTheaterOverlay(orderedTheaters[selectedIndex]);
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(theaterListView, selectedIndex);
            }

            theaterListView.selectionChanged += objects =>
            {
                var theater = objects.FirstOrDefault() as Theater;
                UpdateTheaterOverlay(theater);
            };

            displayModeDropdownField?.RegisterValueChangedCallback(_ =>
            {
                selectedDisplayMode = displayModeDropdownField.index switch
                {
                    (int)TheaterSelectorDisplayMode.Frontline => TheaterSelectorDisplayMode.Frontline,
                    (int)TheaterSelectorDisplayMode.WeightRequested => TheaterSelectorDisplayMode.WeightRequested,
                    _ => TheaterSelectorDisplayMode.Membership
                };
                UpdateTheaterOverlay(theaterListView.selectedItem as Theater);
            });

            el.Q<Button>("RefreshButton").clicked += () =>
            {
                var selectedTheater = theaterListView.selectedItem as Theater;
                var selectedId = selectedTheater?.objectId;
                state.RefreshTheaters();
                RefreshOrderedTheaters();
                SyncSelectionByObjectId(selectedId);
            };

            el.Q<Button>("ClearButton").clicked += () =>
            {
                state.ClearTheaters();
                RefreshOrderedTheaters();
                theaterListView.ClearSelection();
                HexMapShower.Instance?.ClearTheaterOverlay();
            };

            RefreshDisplayModeDropdown();
            RefreshOrderedTheaters();
            SyncSelectionByObjectId(orderedTheaters.FirstOrDefault()?.objectId);
        };

        tempDialog.onClosed += (_, _) => HexMapShower.Instance?.ClearTheaterOverlay();
        tempDialog.Popup();
    }

    public void PopupTheaterDetailDialog(Theater theater)
    {
        if (theaterDetailDialogDocument == null)
        {
            PopupMessageDialog("TheaterDetailDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = theaterDetailDialogDocument,
            templateDataSource = theater
        };

        tempDialog.onCreated += (_, el) =>
        {
            var state = StrategicGameState.Instance;
            var stats = state.GetTheaterFrontlineWeightRequestedStats(theater);

            var frontlineSummaryLabel = el.Q<Label>("FrontlineCellSummaryLabel");
            var weightMinLabel = el.Q<Label>("FrontlineWeightMinLabel");
            var weightMaxLabel = el.Q<Label>("FrontlineWeightMaxLabel");
            var weightAverageLabel = el.Q<Label>("FrontlineWeightAverageLabel");
            var weightStdDevLabel = el.Q<Label>("FrontlineWeightStdDevLabel");

            if (frontlineSummaryLabel != null)
                frontlineSummaryLabel.text = stats.frontlineSummary;
            if (weightMinLabel != null)
                weightMinLabel.text = StrategicInfluenceMapUtility.FormatValue(stats.min);
            if (weightMaxLabel != null)
                weightMaxLabel.text = StrategicInfluenceMapUtility.FormatValue(stats.max);
            if (weightAverageLabel != null)
                weightAverageLabel.text = StrategicInfluenceMapUtility.FormatValue(stats.average);
            if (weightStdDevLabel != null)
                weightStdDevLabel.text = StrategicInfluenceMapUtility.FormatValue(stats.standardDeviation);
        };

        tempDialog.Popup();
    }


    public void PopupTorpedoSectorSelectorDialog(Action<ShipClass> callback)
    {
        var torpedoSectorSelectorDialog = new TorpedoSectorSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=torpedoSectorSelectorDialogDocument,
            templateDataSource=torpedoSectorSelectorDialog
        };

        tempDialog.onCreated += torpedoSectorSelectorDialog.OnCreated;
        tempDialog.onConfirmed += torpedoSectorSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupRapidFireBatteryRecordSelectorDialog(Action<RapidFireBatteryRecord> callback)
    {
        var rapidFireBatteryRecordSelectorDialog = new RapidFireBatteryRecordSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=rapidFireBatteryRecordSelectorDialogDocument,
            templateDataSource=rapidFireBatteryRecordSelectorDialog
        };

        tempDialog.onCreated += rapidFireBatteryRecordSelectorDialog.OnCreated;
        tempDialog.onConfirmed += rapidFireBatteryRecordSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupScenarioStateEditor()
    {
        var scenarioStateEditor = new ScenarioStateEditor()
        {
            timeZoneOffset = GameManager.Instance.GetTimeZoneOffsetByLatestHoveringLocation()
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=scenarioStateEditorDialogDocument,
            templateDataSource=scenarioStateEditor
        };

        tempDialog.onCreated += scenarioStateEditor.OnCreated;

        tempDialog.Popup();
    }

    public void PopupBatteryRecordSelectorDialog(Action<BatteryRecord> callback)
    {
        var batteryRecordSelectorDialog = new BatteryRecordSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=batteryRecordSelectorDialogDocument,
            templateDataSource=batteryRecordSelectorDialog
        };

        tempDialog.onCreated += batteryRecordSelectorDialog.OnCreated;
        tempDialog.onConfirmed += batteryRecordSelectorDialog.OnConfirm;

        tempDialog.Popup();

    }

    public void PopupAutoDeploymentDialog()
    {
        var autoDeploymentDialog = new AutoDeploymentDialog();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=autoDeploymentDialogDocument,
            templateDataSource=autoDeploymentDialog
        };

        tempDialog.onCreated += autoDeploymentDialog.OnCreated;
        tempDialog.onConfirmed += autoDeploymentDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupForceBuilderDialog()
    {
        var forceBuilder = new ForceBuilder();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=forceBuilderDialogDocument,
            templateDataSource=forceBuilder,
        };

        tempDialog.onCreated += forceBuilder.OnCreated;
        tempDialog.onConfirmed += forceBuilder.OnConfirm;
        tempDialog.confirmCheck = forceBuilder.ConfirmCheck;
        
        tempDialog.Popup();
    }

    public void PopupAIDialog()
    {
        var topShipGroups = NavalGameState.Instance.shipGroups.Where(g => g.parentObjectId == null);
        var items = topShipGroups.Select(g => new AIDialogItem(){topGroup=g}).ToList();
        var aiDialog = new AIDialog()
        {
            items = items
        };
        var tempDialog = new TempDialog()
        {
            root=root,
            template=aiDialogDocument,
            templateDataSource=aiDialog,
        };
        // tempDialog.onCreated += aiDialog.OnCreated;
        
        tempDialog.Popup();
    }

    public void PopupLandBattleDialog(LandBattle landBattle)
    {
        // var landBattleDialog = new LandBattleDialog()
        // {
        //     landBattle = landBattle,
        //     attacker = landBattle.GetAttackerDynamic(),
        //     defender = landBattle.GetDefenderDynamic(),
        // };
        var landBattleDialogDynamic = new LandBattleDialogLazy()
        {
            landBattleId = landBattle.objectId
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = landBattleDialogDocument,
            templateDataSource = landBattleDialogDynamic,
        };

        // tempDialog.onCreated += LandBattleDialog.OnCreated;

        // FIXME: Code smell

        var attacker = landBattle.GetAttackerDynamic();
        var defender = landBattle.GetDefenderDynamic();

        tempDialog.onCreated += (sender, root) => {

            LandBattleDialog.OnCreated(sender, root);

            if (attacker?.battleLeader?.portraitReference != null)
            {
                attacker.battleLeader.portraitReference.RequestIfNotRequestedYetOtherwiseExecuteDirectly(styleBackground =>
                {
                    var state = root.Q<VisualElement>("AttackerState");
                    var el = state?.Q<VisualElement>("LeaderPortrait");
                    if (el != null)
                    {
                        el.style.backgroundImage = styleBackground;
                    }
                });
            }

            if (defender?.battleLeader?.portraitReference != null)
            {
                defender.battleLeader.portraitReference.RequestIfNotRequestedYetOtherwiseExecuteDirectly(styleBackground =>
                {
                    var state = root.Q<VisualElement>("DefenderState");
                    var el = state?.Q<VisualElement>("LeaderPortrait");
                    if (el != null)
                    {
                        el.style.backgroundImage = styleBackground;
                    }
                });
            }
        };

        tempDialog.Popup();
    }

    public void PopupOOBTreeDialog(List<StrategicGroup> viewableGroups)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = oobTreeDialogDocument,
            templateDataSource = null,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var oobTreeView = el.Q<TreeView>("OOBTreeView");

            // var tree = new FullGroupTree();
            // var treeViewerBuilder = new UITKTreeViewBuilder<IStrategicGroupMemberReferenceable, string>()
            // {
            //     tree=tree
            // };

            // var viewableGroups = StrategicGameState.Instance.strategicGroups;

            var tree = new FullGroupTreeNameLink();
            var treeViewerBuilder = new UITKTreeViewBuilder<IStrategicGroupMemberReferenceable, IStrategicGroupMemberReferenceable>()
            {
                tree=tree
            };
            var rootItems = treeViewerBuilder.CreateTreeViewRootItems(
                viewableGroups.Where(group =>
                    group.type != StrategicGroup.Type.Base &&
                    !tree.ShouldHideAsBaseRootDescendant(group)));
            oobTreeView.SetRootItems(rootItems);

            tree.BindMakeItemBindItem(oobTreeView);

            oobTreeView.Rebuild();
            // oobTreeView.ExpandAll();
        };

        tempDialog.Popup();
    }

    public TempDialog PopupNavalCombatResolverDialog(PendingNavalCombat pendingNavalCombat)
    {
        // TODO: Very bad code smell (tangle) here, try to improve when I have enough spare time
        var resolver = new NavalCombatResolver()
        {
            root = null, // defer
            // cell = StrategicGameState.Instance.cellMatrix[pendingNavalCombat.xy.x, pendingNavalCombat.xy.y]
            pendingNavalCombat = pendingNavalCombat,
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalCombatResolverDialogDocument,
            templateDataSource = resolver,
            // draggable = false
        };

        resolver.closed += (sender, args) => tempDialog.Close();

        tempDialog.onCreated += (sender, el) =>
        {
            resolver.root = el;
            resolver.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public void PopupPendingNavalCombatDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = pendingNavalCombatDialogDocument,
            templateDataSource = StrategicGameState.Instance,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ClearButton").clicked += () =>
            {
                StrategicGameState.Instance.pendingNavalCombats.Clear();
                tempDialog.root.Remove(el);
            };

            var pendingNavalCombatsListView = el.Q<ListView>("PendingNavalCombatsListView");
            pendingNavalCombatsListView.makeItem = () =>
            {
                var el = pendingNavalCombatsListView.itemTemplate.CloneTree();

                el.Q<Button>().clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out PendingNavalCombat pendingNavalCombat))
                    {
                        var resolverDialog = PopupNavalCombatResolverDialog(pendingNavalCombat);
                        resolverDialog.onClosed += (sender, resolverEl) =>
                        {
                            if(StrategicGameState.Instance.pendingNavalCombats.Count == 0)
                            {
                                tempDialog.Close();
                            }
                        };
                    }
                };

                return el;
            };
        };
        
        tempDialog.Popup();
    }

    public void PopupStrategicReinforcementDialog()
    {
        var strategicReinforcements = StrategicGameState.Instance.strategicGroups
            .Where(group => group.arriveState != null && !group.arriveState.arrived)
            .OrderBy(group => group.arriveState.arriveTime)
            .ThenBy(group => group.name.GetMergedName())
            .ToList();

        if (strategicReinforcementDialogDocument == null)
        {
            PopupMessageDialog("ReinforcementDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicReinforcementDialogDocument,
            templateDataSource = StrategicGameState.Instance
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var listView = el.Q<ListView>("ReinforcementListView");
            listView.selectionType = SelectionType.None;
            listView.itemsSource = strategicReinforcements;
            listView.fixedItemHeight = 28;

            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minHeight = 28;
                row.style.paddingLeft = 0;
                row.style.paddingRight = 0;
                row.style.marginLeft = 0;
                row.style.marginRight = 0;

                var nameLabel = new Label
                {
                    name = "ReinforcementGroupLabel",
                };
                nameLabel.style.flexGrow = 1;
                nameLabel.style.minWidth = 0;
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                Utils.RegisterLinkTag(nameLabel, new()
                {
                    ["nameLink"] = () =>
                    {
                        if (Utils.TryResolveCurrentValueForBinding(row, out StrategicGroup group))
                        {
                            SwitchCenter.Instance.SwitchToStrategicGroupView(group);
                        }
                    }
                });

                row.Add(nameLabel);
                return row;
            };

            listView.bindItem = (item, index) =>
            {
                if (index < 0 || index >= strategicReinforcements.Count)
                    return;

                var group = strategicReinforcements[index];
                item.dataSource = group;

                var nameLabel = item.Q<Label>("ReinforcementGroupLabel");
                if (nameLabel != null)
                {
                    nameLabel.text = group.reinforcementDisplayText;
                }
            };
        };

        tempDialog.Popup();
    }

    public void PopupStrategicReleaseDialog()
    {
        var strategicReleases = StrategicGameState.Instance.strategicGroups
            .Where(group => group.fixedState != null && !group.fixedState.released && group.fixedState.enableReleaseTime)
            .OrderBy(group => group.fixedState.releaseTime)
            .ThenBy(group => group.name.GetMergedName())
            .ToList();

        if (strategicReleaseDialogDocument == null)
        {
            PopupMessageDialog("ReleaseDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicReleaseDialogDocument,
            templateDataSource = StrategicGameState.Instance
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var listView = el.Q<ListView>("ReleaseListView");
            listView.selectionType = SelectionType.None;
            listView.itemsSource = strategicReleases;
            listView.fixedItemHeight = 28;

            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minHeight = 28;
                row.style.paddingLeft = 0;
                row.style.paddingRight = 0;
                row.style.marginLeft = 0;
                row.style.marginRight = 0;

                var nameLabel = new Label
                {
                    name = "ReleaseGroupLabel",
                };
                nameLabel.style.flexGrow = 1;
                nameLabel.style.minWidth = 0;
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                Utils.RegisterLinkTag(nameLabel, new()
                {
                    ["nameLink"] = () =>
                    {
                        if (Utils.TryResolveCurrentValueForBinding(row, out StrategicGroup group))
                        {
                            SwitchCenter.Instance.SwitchToStrategicGroupView(group);
                        }
                    }
                });

                row.Add(nameLabel);
                return row;
            };

            listView.bindItem = (item, index) =>
            {
                if (index < 0 || index >= strategicReleases.Count)
                    return;

                var group = strategicReleases[index];
                item.dataSource = group;

                var nameLabel = item.Q<Label>("ReleaseGroupLabel");
                if (nameLabel != null)
                {
                    nameLabel.text = group.releaseDisplayText;
                }
            };
        };

        tempDialog.Popup();
    }

    public void PopupCellEditorDialog(Cell cell)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = cellEditorDialogDocument,
            templateDataSource = cell,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("SideObjectIdHexButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdHex = sideState.objectId);
            el.Q<Button>("SideObjectIdTopButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTop = sideState.objectId);
            el.Q<Button>("SideObjectIdTopRightButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTopRight = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomRightButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottomRight = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottom = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomLeftButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottomLeft = sideState.objectId);
            el.Q<Button>("SideObjectIdTopLeftButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTopLeft = sideState.objectId);

            var cellConnectionsMultiColumnListView = el.Q<MultiColumnListView>("CellConnectionsMultiColumnListView");
            
            // It's easier to write following compared to "Data Binding Gymnastics" and hack Add Removed callback
            var addConnectionButton = el.Q<Button>("AddConnectionButton");
            var deleteConnectionButton = el.Q<Button>("DeleteConnectionButton");

            addConnectionButton.clicked += () =>
            {
                StrategicGameManager.Instance.ScheduleOneshotCellClickCallback(otherCell =>
                {
                    StrategicGameManager.Instance.mapEditMode = StrategicMapEditMode.Select;

                    // otherCell.CellConnections.FirstOrDefault(c => c.GetOther() == cell);
                    var selfMatched = cell.CellConnections.FirstOrDefault(c => c.GetOther() == otherCell);
                    if(selfMatched == null)
                    {
                        cell.CellConnections.Add(new()
                        {
                            self=cell.ToXY(),
                            other=otherCell.ToXY(),
                        });
                        otherCell.CellConnections.Add(new()
                        {
                            self=otherCell.ToXY(),
                            other=cell.ToXY(),
                        });
                    }
                });
            };

            deleteConnectionButton.clicked += () =>
            {
                if(cellConnectionsMultiColumnListView.selectedItem is CellConnection cellConnection && cellConnection != null)
                {
                    cell.CellConnections.Remove(cellConnection);
                    var otherCell = cellConnection.GetOther();
                    var otherConnection = otherCell.CellConnections.FirstOrDefault(conn => conn.GetOther() == cell);
                    // var otherConnection = cellConnection.GetOtherConnectionToSelf();
                    if(otherConnection != null)
                    {
                        otherCell.CellConnections.Remove(otherConnection);
                    }
                }
            };

            el.Q<Button>("RecalculateCostButton").clicked += () =>
            {
                // TODO: Add grid system's calculation
                foreach(var areaCell in StrategicGameState.Instance.areaCells)
                {
                    foreach(var conn in areaCell.CellConnections)
                    {
                        // conn.costCoef = 1;
                        var otherCell = conn.GetOther();

                        Geodesic.WGS84.Inverse(areaCell.latitude, areaCell.longitude, otherCell.latitude, otherCell.longitude, out double distanceM);
                        var distanceKm = (float)distanceM / 1000; // Consistent with 50km/hex scale.
                        //  * MeasureUtils.kilometerToNavalMile;
                        conn.cost = distanceKm * conn.costCoef;
                    }
                }
            };

            var cellSideInforsListView = el.Q<ListView>("CellSideInforsListView");
            Utils.BindItemsAddedRemoved<CellSideInfo>(cellSideInforsListView, () => null);
            cellSideInforsListView.makeItem = () =>
            {
                var item = cellSideInforsListView.itemTemplate.CloneTree();
                var setButton = item.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<CellSideInfo>(setButton, out var cellSideInfo))
                    {
                        PopupSideStatePickerDialog(side =>
                        {
                            cellSideInfo.sideObjectId = side.objectId; 
                        });
                    }
                };
                return item;
            };
            // PopupSideStatePickerDialog
        };

        // tempDialog.onConfirmed += (sender, args) => StrategicGameState.Instance.InvokeMapCellUpdated(cell.x, cell.y);
        tempDialog.onConfirmed += (sender, args) => StrategicGameState.Instance.InvokeMapCellUpdated(cell);

        tempDialog.Popup();
    }

    public void PopupSubStrategicCombatDialog(SubStrategicCombat combat)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = subStrategicCombatDialogDocument,
            templateDataSource = combat,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            // var listViews = new ListView[] { el.Q<ListView>("AttackerListView"), el.Q<ListView>("DefenderListView") };
            var listViews = el.Query<ListView>("CombatItemListView").ToList();
            foreach (var listView in listViews)
            {
                Utils.BindItemsAddedRemoved<SubStrategicCombatItem>(listView, () => null);
                listView.makeItem = () =>
                {
                    var item = listView.itemTemplate.CloneTree();

                    var setButton = item.Q<Button>("SetButton");
                    setButton.clicked += () =>
                    {
                        if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
                        {
                            PopupSubordinatePickerDialog(selectedReferenceables =>
                            {
                                var selectedReferenceable = selectedReferenceables.FirstOrDefault();
                                if (selectedReferenceable != null)
                                {
                                    fieldReference.referenceId = selectedReferenceable.objectId;
                                }
                            }, SubordinatePickerDialog.Mode.Free);
                        }
                    };

                    // Utils.BindGotoButton(item, null); // TODO: Remove strange reference of StrategicGroupEditor
                    Utils.BindGotoButton(item);

                    return item;
                };
            }
        };

        tempDialog.Popup();
    }

    public class EventStateEditorDialogDataSource
    {
        public EventState eventState;
        public EventItem currentEventItem;

        [CreateProperty]
        public bool isCurrentSelectionValid => currentEventItem != null;
    }

    public void PopupEventStateEditorDialog()
    {
        var dataSource = new EventStateEditorDialogDataSource()
        {
            eventState = EventState.Instance,
            currentEventItem = null
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = eventStateEditorDialogDocument,
            templateDataSource = dataSource,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            Utils.BindItemsSourceRecursive(el);

            var objectListView = el.Q<ListView>("ObjectListView");
            Utils.BindItemsAddedRemoved<EventItem>(objectListView, () => null);

            objectListView.selectionChanged += (IEnumerable<object> objects) =>
            {
                Debug.Log("LeftObjectPickerRightEditorStrategic.selectionChanged");

                var obj = objects.FirstOrDefault() as EventItem;
                dataSource.currentEventItem = obj;
                // selectedId = obj?.objectId;
            };

            // el.Q<Button>("RefreshButton").clicked += TextReference.ClearCache; // TODO: switch local
            el.Q<Button>("RefreshAllButton").clicked += () =>
            {
                EventState.Instance.RefreshAll();
            };

            var refreshButton = el.Q<Button>("RefreshButton");
            refreshButton.clicked += () =>
            {
                Debug.Log(EventState.Instance.eventItems);
                if (Utils.TryResolveCurrentValueForBinding(refreshButton, out EventItem eventItem))
                {
                    BehaviourUtils.Instance.StartCoroutine(eventItem.Refresh());
                }
            };

            var pathField = el.Q<VisualElement>("PathField");
            PathReferenceBinder.BindJSReference(pathField);
            PathReferenceBinder.AddCallback(pathField, () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(refreshButton, out EventItem eventItem)) // TODO: Temp Hack
                {
                    BehaviourUtils.Instance.StartCoroutine(eventItem.Refresh());
                }
            });
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            BehaviourUtils.Instance.StartCoroutine(EventState.Instance.SyncAndRegister());
        };

        tempDialog.Popup();
    }

    public void PopupPlotTrajectoryDialog(ShipLog shipLog)
    {
        var model = new PlotTrajectoryViewModel()
        {
            shipLogObjectId = shipLog.objectId,
            color = GetDefaultTrajectoryColor(shipLog),
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = plotTrajectoryDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += (sender, root) =>
        {
            Debug.Log("PopupPlotTrajectoryDialog Confirm");

            GameManager.Instance.PlotShipLogTrajectory(EntityManager.Instance.Get<ShipLog>(model.shipLogObjectId), model.color, model.plotTimestamp, model.timestampIntervalMinutes);
        };

        tempDialog.Popup();
    }

    public void PopupInfluenceMapDialog()
    {
        if (influenceMapDialogDocument == null)
        {
            PopupMessageDialog("InfluenceMapDialog is not configured.");
            return;
        }

        var model = new InfluenceMapDialogModel();
        var tempDialog = new TempDialog()
        {
            root = root,
            template = influenceMapDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (_, el) =>
        {
            var orderedGroups = InfluenceMapUtility.GetShipGroupsInOobOrder(NavalGameState.Instance);
            var topGroups = InfluenceMapUtility.GetTopLevelShipGroupsInOobOrder(NavalGameState.Instance);
            var groupNames = orderedGroups.Select(group => group.name.GetMergedName()).ToList();

            var group1DropdownField = el.Q<DropdownField>("Group1DropdownField");
            var group2DropdownField = el.Q<DropdownField>("Group2DropdownField");
            var plotButton = el.Q<Button>("PlotButton");
            var clearButton = el.Q<Button>("ClearButton");
            var mapTypeField = el.Q<LocalizedEnumField>("MapTypeField");
            var falloffAlgorithmField = el.Q<LocalizedEnumField>("FalloffAlgorithmField");
            var linearParameterRow = el.Q<VisualElement>("LinearParameterRow");
            var exponentialParameterRow = el.Q<VisualElement>("ExponentialParameterRow");
            var inverseParameterRow = el.Q<VisualElement>("InverseParameterRow");
            var gaussianParameterRow = el.Q<VisualElement>("GaussianParameterRow");

            void SetGroupSelection(DropdownField dropdownField, Action<string> setObjectId, string objectId, int fallbackIndex = 0)
            {
                if (dropdownField == null)
                    return;

                var groups = dropdownField.userData as List<ShipGroup>;
                if (groups == null || groups.Count == 0)
                {
                    dropdownField.index = -1;
                    setObjectId(null);
                    return;
                }

                var selectedIndex = !string.IsNullOrEmpty(objectId)
                    ? groups.FindIndex(group => group.objectId == objectId)
                    : -1;
                if (selectedIndex < 0)
                    selectedIndex = Mathf.Clamp(fallbackIndex, 0, groups.Count - 1);

                dropdownField.index = selectedIndex;
                setObjectId(groups[selectedIndex].objectId);
            }

            void SyncGroupSelection(DropdownField dropdownField, Action<string> setObjectId, int defaultIndex)
            {
                if (dropdownField == null)
                    return;

                dropdownField.choices = groupNames;
                dropdownField.userData = orderedGroups;
                if (orderedGroups.Count == 0)
                {
                    dropdownField.index = -1;
                    setObjectId(null);
                    return;
                }

                SetGroupSelection(dropdownField, setObjectId, null, defaultIndex);
                dropdownField.RegisterValueChangedCallback(_ =>
                {
                    var groups = dropdownField.userData as List<ShipGroup>;
                    if (groups == null || dropdownField.index < 0 || dropdownField.index >= groups.Count)
                    {
                        setObjectId(null);
                        return;
                    }

                    setObjectId(groups[dropdownField.index].objectId);
                });
            }

            void SyncFalloffParameterState(InfluenceMapFalloffAlgorithm algorithm)
            {
                if (linearParameterRow != null)
                    linearParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Linear ? DisplayStyle.Flex : DisplayStyle.None;
                if (exponentialParameterRow != null)
                    exponentialParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Exponential ? DisplayStyle.Flex : DisplayStyle.None;
                if (inverseParameterRow != null)
                    inverseParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Inverse ? DisplayStyle.Flex : DisplayStyle.None;
                if (gaussianParameterRow != null)
                    gaussianParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Gaussian ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void ApplyDefaultControlGroups()
            {
                if (topGroups.Count == 0)
                    return;

                SetGroupSelection(group1DropdownField, objectId => model.group1ObjectId = objectId, topGroups[0].objectId, 0);
                var secondGroup = topGroups.Count > 1 ? topGroups[1] : topGroups[0];
                SetGroupSelection(group2DropdownField, objectId => model.group2ObjectId = objectId, secondGroup.objectId, 0);
            }

            void SyncGroup2State(InfluenceMapType mapType)
            {
                var group2Enabled = mapType == InfluenceMapType.Control;
                group2DropdownField?.SetEnabled(group2Enabled);
            }

            SyncGroupSelection(group1DropdownField, objectId => model.group1ObjectId = objectId, 0);
            SyncGroupSelection(group2DropdownField, objectId => model.group2ObjectId = objectId, orderedGroups.Count > 1 ? 1 : 0);

            mapTypeField?.RegisterValueChangedCallback(evt =>
            {
                var mapType = (InfluenceMapType)evt.newValue;
                SyncGroup2State(mapType);
                if (mapType == InfluenceMapType.Control)
                    ApplyDefaultControlGroups();
            });
            SyncGroup2State(mapTypeField != null ? (InfluenceMapType)mapTypeField.value : model.mapType);
            falloffAlgorithmField?.RegisterValueChangedCallback(evt => SyncFalloffParameterState((InfluenceMapFalloffAlgorithm)evt.newValue));
            SyncFalloffParameterState(falloffAlgorithmField != null ? (InfluenceMapFalloffAlgorithm)falloffAlgorithmField.value : model.falloffAlgorithm);

            if (plotButton != null)
            {
                plotButton.clicked += () =>
                {
                    GameManager.Instance.PlotInfluenceMap(new InfluenceMapRequest
                    {
                        mapType = model.mapType,
                        falloffAlgorithm = model.falloffAlgorithm,
                        fillEnabled = model.fillEnabled,
                        group1ObjectId = model.group1ObjectId,
                        group2ObjectId = model.group2ObjectId,
                        linearRangeYards = model.linearRangeYards,
                        exponentialDecayLengthYards = model.exponentialDecayLengthYards,
                        inverseHalfEffectDistanceYards = model.inverseHalfEffectDistanceYards,
                        gaussianSigmaYards = model.gaussianSigmaYards,
                        sampleWidth = model.sampleWidth,
                        sampleHeight = model.sampleHeight,
                        boundsPaddingRatio = model.boundsPaddingRatio,
                        minBoundsPaddingDeg = model.minBoundsPaddingDeg,
                    });
                };
            }

            if (clearButton != null)
            {
                clearButton.clicked += GameManager.Instance.ClearInfluenceMap;
            }
        };

        tempDialog.Popup();
    }

    public void PopupTorpedoInterceptSolutionVisualizerDialog()
    {
        if (torpedoInterceptSolutionVisualizerDialogDocument == null)
        {
            PopupMessageDialog("TorpedoInterceptSolutionVisualizerDialog is not configured.");
            return;
        }

        var selectedShipLog = GameManager.Instance.selectedShipLog;
        if (selectedShipLog != null && !selectedShipLog.IsOnMap())
            selectedShipLog = null;

        var model = new TorpedoInterceptSolutionDialogModel()
        {
            shooterObjectId = selectedShipLog?.objectId,
            targetObjectId = TorpedoInterceptSolutionDialogSupport.GetDefaultTarget(selectedShipLog)?.objectId
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = torpedoInterceptSolutionVisualizerDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (_, el) =>
        {
            var shooterValueLabel = el.Q<Label>("ShooterValueLabel");
            var targetDropdownField = el.Q<DropdownField>("TargetDropdownField");
            var solutionLabel = el.Q<Label>("SolutionLabel");
            var probabilityLabel = el.Q<Label>("ProbabilityLabel");
            var minSpeedField = el.Q<FloatField>("MinSpeedField");
            var maxSpeedField = el.Q<FloatField>("MaxSpeedField");
            var lowerOffsetField = el.Q<FloatField>("LowerOffsetField");
            var upperOffsetField = el.Q<FloatField>("UpperOffsetField");
            var mountStatusScrollView = el.Q<ScrollView>("MountStatusScrollView");

            LineRenderer interceptLineRenderer = null;
            GameObject overlayRoot = null;
            GameObject hitRegionOverlayObject = null;
            GameObject futureRegionOverlayObject = null;
            IVisualElementScheduledItem refreshItem = null;
            bool suppressCallbacks = false;

            LineRenderer GetOrCreateInterceptLineRenderer()
            {
                if (interceptLineRenderer != null)
                    return interceptLineRenderer;

                var gameManager = GameManager.Instance;
                if (gameManager.shipLogTrajectoryPrefab != null)
                {
                    var lineObject = Instantiate(gameManager.shipLogTrajectoryPrefab, gameManager.transform);
                    lineObject.name = "TorpedoInterceptSolutionVisualizerLine";
                    interceptLineRenderer = lineObject.GetComponent<LineRenderer>();
                }

                if (interceptLineRenderer == null)
                {
                    var lineObject = new GameObject("TorpedoInterceptSolutionVisualizerLine");
                    lineObject.transform.SetParent(GameManager.Instance.transform, false);
                    interceptLineRenderer = lineObject.AddComponent<LineRenderer>();
                    var shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                        interceptLineRenderer.material = new Material(shader);
                    interceptLineRenderer.widthMultiplier = 0.0005f;
                    interceptLineRenderer.useWorldSpace = true;
                }
                interceptLineRenderer.widthMultiplier = interceptLineRenderer.widthMultiplier > 0
                    ? interceptLineRenderer.widthMultiplier / 3f
                    : 0.0005f / 3f;

                var transparentMagenta = new Color(1f, 0f, 1f, 0.35f);
                interceptLineRenderer.startColor = transparentMagenta;
                interceptLineRenderer.endColor = transparentMagenta;
                interceptLineRenderer.enabled = false;
                interceptLineRenderer.positionCount = 0;
                return interceptLineRenderer;
            }

            void HideInterceptLine()
            {
                if (interceptLineRenderer == null)
                    return;
                interceptLineRenderer.positionCount = 0;
                interceptLineRenderer.enabled = false;
            }

            void DrawInterceptLine(TorpedoInterceptVisualizerSolution solution)
            {
                if (solution == null || !solution.hasSolution)
                {
                    HideInterceptLine();
                    return;
                }

                var lineRenderer = GetOrCreateInterceptLineRenderer();
                var inverseLine = Geodesic.WGS84.InverseLine(
                    solution.shooter.position.LatDeg, solution.shooter.position.LonDeg,
                    solution.interceptionPoint.LatDeg, solution.interceptionPoint.LonDeg
                );
                var distanceMeters = inverseLine.Distance;
                const int segments = 16;
                var positions = new Vector3[segments + 1];
                for (var i = 0; i <= segments; i++)
                {
                    var p = segments == 0 ? 0f : (float)i / segments;
                    var pos = inverseLine.Position(distanceMeters * p);
                    positions[i] = Utils.LatitudeLongitudeDegHeightFootToVector3((float)pos.Latitude, (float)pos.Longitude, 40);
                }

                var transparentMagenta = new Color(1f, 0f, 1f, 0.35f);
                lineRenderer.startColor = transparentMagenta;
                lineRenderer.endColor = transparentMagenta;
                lineRenderer.positionCount = positions.Length;
                lineRenderer.SetPositions(positions);
                lineRenderer.enabled = true;
            }

            GameObject GetOrCreateOverlayRoot()
            {
                if (overlayRoot != null)
                    return overlayRoot;

                var parent = GameManager.Instance.earthTransform != null
                    ? GameManager.Instance.earthTransform
                    : GameManager.Instance.transform;
                overlayRoot = new GameObject("TorpedoInterceptSolutionVisualizerOverlay");
                overlayRoot.transform.SetParent(parent, false);
                return overlayRoot;
            }

            Material CreateOverlayMaterial(string materialName, Color color)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                return new Material(shader)
                {
                    name = materialName,
                    color = color
                };
            }

            GameObject GetOrCreateOverlayObject(ref GameObject overlayObject, string name, Color color)
            {
                if (overlayObject != null)
                    return overlayObject;

                overlayObject = new GameObject(name);
                overlayObject.transform.SetParent(GetOrCreateOverlayRoot().transform, false);
                var meshFilter = overlayObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = new Mesh { name = $"{name}Mesh" };
                var meshRenderer = overlayObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = CreateOverlayMaterial($"{name}Material", color);
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                meshRenderer.enabled = false;
                return overlayObject;
            }

            void HideOverlay(GameObject overlayObject)
            {
                if (overlayObject == null)
                    return;

                var meshFilter = overlayObject.GetComponent<MeshFilter>();
                if (meshFilter?.sharedMesh != null)
                    meshFilter.sharedMesh.Clear();

                var meshRenderer = overlayObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
            }

            void DrawGridOverlay(
                ref GameObject overlayObject,
                string name,
                IReadOnlyList<LatLon> gridPoints,
                int headingDivisions,
                int speedDivisions,
                IReadOnlyList<bool> cellMask,
                Color color,
                float heightFoot
            )
            {
                if (gridPoints == null || headingDivisions <= 0 || speedDivisions <= 0)
                {
                    HideOverlay(overlayObject);
                    return;
                }

                var expectedGridPointCount = (headingDivisions + 1) * (speedDivisions + 1);
                if (gridPoints.Count != expectedGridPointCount)
                {
                    HideOverlay(overlayObject);
                    return;
                }

                var overlay = GetOrCreateOverlayObject(ref overlayObject, name, color);
                var meshFilter = overlay.GetComponent<MeshFilter>();
                var meshRenderer = overlay.GetComponent<MeshRenderer>();
                var mesh = meshFilter.sharedMesh ??= new Mesh { name = $"{name}Mesh" };
                mesh.Clear();

                var vertices = gridPoints
                    .Select(point => Utils.LatitudeLongitudeDegHeightFootToVector3(point.LatDeg, point.LonDeg, heightFoot))
                    .ToArray();
                var triangles = new List<int>();

                for (var headingIdx = 0; headingIdx < headingDivisions; headingIdx++)
                {
                    for (var speedIdx = 0; speedIdx < speedDivisions; speedIdx++)
                    {
                        var cellIndex = headingIdx * speedDivisions + speedIdx;
                        if (cellMask != null && (cellIndex < 0 || cellIndex >= cellMask.Count || !cellMask[cellIndex]))
                            continue;

                        var v00 = headingIdx * (speedDivisions + 1) + speedIdx;
                        var v10 = v00 + 1;
                        var v01 = v00 + speedDivisions + 1;
                        var v11 = v01 + 1;

                        triangles.Add(v00);
                        triangles.Add(v01);
                        triangles.Add(v10);
                        triangles.Add(v10);
                        triangles.Add(v01);
                        triangles.Add(v11);
                    }
                }

                if (triangles.Count == 0)
                {
                    HideOverlay(overlayObject);
                    return;
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles.ToArray();
                mesh.RecalculateBounds();
                meshRenderer.enabled = true;
            }

            void DrawMeshOverlay(
                ref GameObject overlayObject,
                string name,
                IReadOnlyList<LatLon> verticesLatLon,
                IReadOnlyList<int> triangles,
                Color color,
                float heightFoot
            )
            {
                if (verticesLatLon == null || triangles == null || verticesLatLon.Count == 0 || triangles.Count < 3)
                {
                    HideOverlay(overlayObject);
                    return;
                }

                var overlay = GetOrCreateOverlayObject(ref overlayObject, name, color);
                var meshFilter = overlay.GetComponent<MeshFilter>();
                var meshRenderer = overlay.GetComponent<MeshRenderer>();
                var mesh = meshFilter.sharedMesh ??= new Mesh { name = $"{name}Mesh" };
                mesh.Clear();

                mesh.vertices = verticesLatLon
                    .Select(point => Utils.LatitudeLongitudeDegHeightFootToVector3(point.LatDeg, point.LonDeg, heightFoot))
                    .ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.RecalculateBounds();
                meshRenderer.enabled = true;
            }

            void HideProbabilityOverlays()
            {
                HideOverlay(hitRegionOverlayObject);
                HideOverlay(futureRegionOverlayObject);
            }

            void DestroyOverlayObject(ref GameObject overlayObject)
            {
                if (overlayObject == null)
                    return;

                var meshFilter = overlayObject.GetComponent<MeshFilter>();
                if (meshFilter?.sharedMesh != null)
                    Destroy(meshFilter.sharedMesh);

                var meshRenderer = overlayObject.GetComponent<MeshRenderer>();
                if (meshRenderer?.sharedMaterial != null)
                    Destroy(meshRenderer.sharedMaterial);

                Destroy(overlayObject);
                overlayObject = null;
            }

            void CleanupProbabilityOverlays()
            {
                DestroyOverlayObject(ref hitRegionOverlayObject);
                DestroyOverlayObject(ref futureRegionOverlayObject);
                if (overlayRoot != null)
                {
                    Destroy(overlayRoot);
                    overlayRoot = null;
                }
            }

            void ApplyRandomModelDefaults(ShipLog target)
            {
                var defaultModel = TorpedoInterceptSolutionDialogSupport.BuildDefaultRandomModel(target);
                suppressCallbacks = true;
                if (minSpeedField != null)
                    minSpeedField.value = defaultModel.minSpeedKnots;
                if (maxSpeedField != null)
                    maxSpeedField.value = defaultModel.maxSpeedKnots;
                if (lowerOffsetField != null)
                    lowerOffsetField.value = defaultModel.lowerHeadingOffsetDeg;
                if (upperOffsetField != null)
                    upperOffsetField.value = defaultModel.upperHeadingOffsetDeg;
                suppressCallbacks = false;
            }

            TorpedoInterceptRandomModel ReadRandomModel()
            {
                var target = EntityManager.Instance.Get<ShipLog>(model.targetObjectId);
                var defaultModel = TorpedoInterceptSolutionDialogSupport.BuildDefaultRandomModel(target);
                return TorpedoInterceptSolutionDialogSupport.SanitizeRandomModel(new TorpedoInterceptRandomModel()
                {
                    minSpeedKnots = minSpeedField != null ? minSpeedField.value : defaultModel.minSpeedKnots,
                    maxSpeedKnots = maxSpeedField != null ? maxSpeedField.value : defaultModel.maxSpeedKnots,
                    lowerHeadingOffsetDeg = lowerOffsetField != null ? lowerOffsetField.value : defaultModel.lowerHeadingOffsetDeg,
                    upperHeadingOffsetDeg = upperOffsetField != null ? upperOffsetField.value : defaultModel.upperHeadingOffsetDeg,
                });
            }

            ShipLog GetCurrentShooter()
            {
                var shooter = GameManager.Instance.selectedShipLog;
                return shooter != null && shooter.IsOnMap() ? shooter : null;
            }

            void SetDropdownSelection(DropdownField dropdownField, List<ShipLog> ships, string objectId)
            {
                suppressCallbacks = true;
                dropdownField.userData = ships;
                dropdownField.choices = ships.Select(ship =>
                    TorpedoInterceptSolutionDialogSupport.BuildTargetChoiceLabel(EntityManager.Instance.Get<ShipLog>(model.shooterObjectId), ship)).ToList();

                if (ships.Count == 0)
                {
                    dropdownField.index = -1;
                    suppressCallbacks = false;
                    return;
                }

                var idx = !string.IsNullOrWhiteSpace(objectId)
                    ? ships.FindIndex(ship => ship.objectId == objectId)
                    : -1;
                dropdownField.index = idx >= 0 ? idx : -1;
                suppressCallbacks = false;
            }

            bool SyncShooterLabelAndModel()
            {
                var shooter = GetCurrentShooter();
                var newShooterObjectId = shooter?.objectId;
                var shooterChanged = !string.Equals(model.shooterObjectId, newShooterObjectId, StringComparison.Ordinal);
                model.shooterObjectId = newShooterObjectId;
                if (shooterValueLabel != null)
                    shooterValueLabel.text = shooter != null
                        ? TorpedoInterceptSolutionDialogSupport.GetShipDisplayName(shooter)
                        : Localize("Invalid");
                return shooterChanged;
            }

            void SyncTargetDropdown(bool resetToDefaultTarget)
            {
                var shooter = EntityManager.Instance.Get<ShipLog>(model.shooterObjectId);
                var targets = TorpedoInterceptSolutionDialogSupport.GetTargetCandidates(shooter);

                if (resetToDefaultTarget)
                    model.targetObjectId = TorpedoInterceptSolutionDialogSupport.GetDefaultTarget(shooter)?.objectId;

                if (model.targetObjectId != null && targets.All(ship => ship.objectId != model.targetObjectId))
                    model.targetObjectId = null;

                SetDropdownSelection(targetDropdownField, targets, model.targetObjectId);
            }

            string GetMountStatusText(TorpedoInterceptMountDiagnosticStatus status, TorpedoMountStatusRecord mount)
            {
                return status switch
                {
                    TorpedoInterceptMountDiagnosticStatus.Invalid => Localize("Invalid"),
                    TorpedoInterceptMountDiagnosticStatus.Disabled => Localize("Disabled"),
                    TorpedoInterceptMountDiagnosticStatus.NoTorpedoSetting => Localize("No Torpedo Setting"),
                    TorpedoInterceptMountDiagnosticStatus.Reloading => $"{Localize("Reloading")} ({mount.reloadingSeconds:0}s / 360s)",
                    TorpedoInterceptMountDiagnosticStatus.NoAmmunition => Localize("No Ammunition"),
                    TorpedoInterceptMountDiagnosticStatus.DoctrineBlocked => Localize("Doctrine Blocked"),
                    TorpedoInterceptMountDiagnosticStatus.Unsafe => Localize("Unsafe"),
                    TorpedoInterceptMountDiagnosticStatus.NoSolution => Localize("No Solution"),
                    TorpedoInterceptMountDiagnosticStatus.OutOfArc => Localize("Out Of Arc"),
                    TorpedoInterceptMountDiagnosticStatus.CanFire => Localize("Can Fire"),
                    _ => Localize("Invalid")
                };
            }

            void RefreshMountStatuses(TorpedoInterceptVisualizerSolution solution)
            {
                mountStatusScrollView.contentContainer.Clear();

                var shooter = solution?.shooter;
                var mounts = shooter?.torpedoSectorStatus?.mountStatus ?? new List<TorpedoMountStatusRecord>();
                if (mounts.Count == 0)
                {
                    mountStatusScrollView.contentContainer.Add(new Label(solution != null && solution.isValid
                        ? Localize("No Torpedo Setting")
                        : Localize("Invalid")));
                    return;
                }

                foreach (var mount in mounts)
                {
                    var mountLabel = mount?.GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
                    var status = TorpedoInterceptSolutionDialogSupport.EvaluateMountStatus(mount, solution);
                    mountStatusScrollView.contentContainer.Add(new Label($"{mountLabel}: {GetMountStatusText(status, mount)}"));
                }
            }

            void RefreshProbabilityEstimate(TorpedoInterceptVisualizerSolution solution)
            {
                if (solution == null || !solution.hasSolution)
                {
                    if (probabilityLabel != null)
                    {
                        var statusText = solution != null && solution.isValid
                            ? Localize("No Solution")
                            : Localize("Invalid");
                        probabilityLabel.text = Localize("Hit Probability: {0}", statusText);
                    }

                    HideProbabilityOverlays();
                    return;
                }

                var estimate = TorpedoInterceptSolutionDialogSupport.EvaluateProbability(solution, ReadRandomModel());
                if (probabilityLabel != null)
                    probabilityLabel.text = Localize("Hit Probability: {0}", $"{estimate.hitProbability * 100f:0.0}%");

                DrawGridOverlay(
                    ref futureRegionOverlayObject,
                    "TorpedoInterceptFutureRegion",
                    estimate.futureRegionGridPoints,
                    estimate.headingDivisions,
                    estimate.speedDivisions,
                    null,
                    new Color(0.12f, 0.45f, 1f, 0.16f),
                    30f
                );
                DrawMeshOverlay(
                    ref hitRegionOverlayObject,
                    "TorpedoInterceptHitRegion",
                    estimate.hitRegionVertices,
                    estimate.hitRegionTriangles,
                    new Color(1f, 0.08f, 0.08f, 0.18f),
                    34f
                );
            }

            void RefreshAll(bool resetTargetToDefault = false)
            {
                var shooterChanged = SyncShooterLabelAndModel();
                var shouldResetTarget = resetTargetToDefault || shooterChanged;
                SyncTargetDropdown(shouldResetTarget);
                if (shooterChanged)
                    ApplyRandomModelDefaults(EntityManager.Instance.Get<ShipLog>(model.targetObjectId));

                var shooter = EntityManager.Instance.Get<ShipLog>(model.shooterObjectId);
                var target = EntityManager.Instance.Get<ShipLog>(model.targetObjectId);
                if (!TorpedoInterceptSolutionDialogSupport.IsValidShooterTargetCombination(shooter, target))
                {
                    solutionLabel.text = Localize("Invalid");
                    if (probabilityLabel != null)
                        probabilityLabel.text = Localize("Hit Probability: {0}", Localize("Invalid"));
                    HideInterceptLine();
                    HideProbabilityOverlays();
                    RefreshMountStatuses(new TorpedoInterceptVisualizerSolution()
                    {
                        shooter = shooter,
                        target = target,
                        isValid = false
                    });
                    return;
                }

                using (var torpedoAttackContext = TorpedoAttackContext.Begin())
                {
                    var solution = TorpedoInterceptSolutionDialogSupport.EvaluateSolution(shooter, target, torpedoAttackContext);
                    if (solution.hasSolution)
                    {
                        solutionLabel.text = Localize("Azimuth {0} deg, Distance {1} yd, Time {2}s @ {3} kts",
                            solution.interceptionResult.azimuth.ToString("0.0"),
                            solution.interceptionResult.distanceYards.ToString("0"),
                            solution.interceptionResult.arrivalSeconds.ToString("0.0"),
                            solution.selectedSetting?.speedKnots.ToString("0.#") ?? "?");
                        DrawInterceptLine(solution);
                    }
                    else
                    {
                        solutionLabel.text = Localize("No Solution");
                        HideInterceptLine();
                    }

                    RefreshProbabilityEstimate(solution);
                    RefreshMountStatuses(solution);
                }
            }

            targetDropdownField.RegisterValueChangedCallback(_ =>
            {
                if (suppressCallbacks)
                    return;

                var targets = targetDropdownField.userData as List<ShipLog>;
                model.targetObjectId = targets != null &&
                                       targetDropdownField.index >= 0 &&
                                       targetDropdownField.index < targets.Count
                    ? targets[targetDropdownField.index].objectId
                    : null;
                ApplyRandomModelDefaults(EntityManager.Instance.Get<ShipLog>(model.targetObjectId));
                RefreshAll();
            });

            void RegisterRandomModelCallback(FloatField field)
            {
                field?.RegisterValueChangedCallback(_ =>
                {
                    if (suppressCallbacks)
                        return;
                    RefreshAll();
                });
            }

            RegisterRandomModelCallback(minSpeedField);
            RegisterRandomModelCallback(maxSpeedField);
            RegisterRandomModelCallback(lowerOffsetField);
            RegisterRandomModelCallback(upperOffsetField);

            ApplyRandomModelDefaults(EntityManager.Instance.Get<ShipLog>(model.targetObjectId));
            RefreshAll();
            refreshItem = el.schedule.Execute(() => RefreshAll()).Every(250);

            tempDialog.onClosed += (_, _) =>
            {
                refreshItem?.Pause();
                HideInterceptLine();
                CleanupProbabilityOverlays();
                if (interceptLineRenderer != null)
                    Destroy(interceptLineRenderer.gameObject);
            };
        };

        tempDialog.Popup();
    }

    public void PopupWtaSolverInspectorDialog()
    {
        if (wtaSolverInspectorDialogDocument == null)
        {
            PopupMessageDialog("WtaSolverInspectorDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = wtaSolverInspectorDialogDocument,
        };

        var dialog = new WtaSolverInspectorDialog();
        tempDialog.onCreated += dialog.OnCreated;
        tempDialog.Popup();
    }

    public void PopupStrategicInfluenceMapDialog()
    {
        var template = strategicInfluenceMapDialogDocument != null
            ? strategicInfluenceMapDialogDocument
            : influenceMapDialogDocument;
        if (template == null)
        {
            PopupMessageDialog("StrategicInfluenceMapDialog is not configured.");
            return;
        }

        var scenarioState = StrategicGameState.Instance.scenarioState;
        var model = new StrategicInfluenceMapDialogModel
        {
            falloffAlgorithmValue = (int)scenarioState.falloffAlgorithmValue,
            linearRangeCost = scenarioState.linearRangeCost,
            exponentialDecayLengthCost = scenarioState.exponentialDecayLengthCost,
            inverseHalfEffectDistanceCost = scenarioState.inverseHalfEffectDistanceCost,
            gaussianSigmaCost = scenarioState.gaussianSigmaCost,
            forceRefresh = true
        };
        var tempDialog = new TempDialog()
        {
            root = root,
            template = template,
            templateDataSource = model,
        };

        tempDialog.onCreated += (_, el) =>
        {
            var state = StrategicGameState.Instance;
            var sides = StrategicInfluenceMapUtility.GetAvailableSides(state);
            var sideNames = sides.Select(side => side.name.GetMergedName()).ToList();

            var side1DropdownField = el.Q<DropdownField>("Side1DropdownField");
            var side2DropdownField = el.Q<DropdownField>("Side2DropdownField");
            var forceRefreshToggle = el.Q<Toggle>("ForceRefreshToggle");
            var plotButton = el.Q<Button>("PlotButton");
            var clearButton = el.Q<Button>("ClearButton");
            var mapTypeField = el.Q<LocalizedEnumField>("MapTypeField");
            var falloffAlgorithmField = el.Q<LocalizedEnumField>("FalloffAlgorithmField");
            var linearRangeField = el.Q<FloatField>("LinearRangeField");
            var exponentialDecayField = el.Q<FloatField>("ExponentialDecayField");
            var inverseHalfEffectField = el.Q<FloatField>("InverseHalfEffectField");
            var gaussianSigmaField = el.Q<FloatField>("GaussianSigmaField");
            var linearParameterRow = el.Q<VisualElement>("LinearParameterRow");
            var exponentialParameterRow = el.Q<VisualElement>("ExponentialParameterRow");
            var inverseParameterRow = el.Q<VisualElement>("InverseParameterRow");
            var gaussianParameterRow = el.Q<VisualElement>("GaussianParameterRow");

            void CopyScenarioPowerParametersToModel()
            {
                model.falloffAlgorithmValue = state.scenarioState.falloffAlgorithmValue;
                model.linearRangeCost = state.scenarioState.linearRangeCost;
                model.exponentialDecayLengthCost = state.scenarioState.exponentialDecayLengthCost;
                model.inverseHalfEffectDistanceCost = state.scenarioState.inverseHalfEffectDistanceCost;
                model.gaussianSigmaCost = state.scenarioState.gaussianSigmaCost;
            }

            void RefreshParameterFieldValuesFromModel()
            {
                falloffAlgorithmField?.SetValueWithoutNotify(model.falloffAlgorithmValue);
                linearRangeField?.SetValueWithoutNotify(model.linearRangeCost);
                exponentialDecayField?.SetValueWithoutNotify(model.exponentialDecayLengthCost);
                inverseHalfEffectField?.SetValueWithoutNotify(model.inverseHalfEffectDistanceCost);
                gaussianSigmaField?.SetValueWithoutNotify(model.gaussianSigmaCost);
            }

            void SyncScenarioStatePowerParameters()
            {
                state.scenarioState.falloffAlgorithmValue = model.falloffAlgorithmValue;
                state.scenarioState.linearRangeCost = model.linearRangeCost;
                state.scenarioState.exponentialDecayLengthCost = model.exponentialDecayLengthCost;
                state.scenarioState.inverseHalfEffectDistanceCost = model.inverseHalfEffectDistanceCost;
                state.scenarioState.gaussianSigmaCost = model.gaussianSigmaCost;
            }

            void SetSideSelection(DropdownField dropdownField, Action<string> setObjectId, string objectId, int fallbackIndex = 0)
            {
                if (dropdownField == null)
                    return;

                var dropdownSides = dropdownField.userData as List<SideState>;
                if (dropdownSides == null || dropdownSides.Count == 0)
                {
                    dropdownField.index = -1;
                    setObjectId(null);
                    return;
                }

                var selectedIndex = !string.IsNullOrEmpty(objectId)
                    ? dropdownSides.FindIndex(side => side.objectId == objectId)
                    : -1;
                if (selectedIndex < 0)
                    selectedIndex = Mathf.Clamp(fallbackIndex, 0, dropdownSides.Count - 1);

                dropdownField.index = selectedIndex;
                setObjectId(dropdownSides[selectedIndex].objectId);
            }

            void SyncSideSelection(DropdownField dropdownField, Action<string> setObjectId, string defaultObjectId, int defaultIndex = 0)
            {
                if (dropdownField == null)
                    return;

                dropdownField.choices = sideNames;
                dropdownField.userData = sides;
                if (sides.Count == 0)
                {
                    dropdownField.index = -1;
                    setObjectId(null);
                    return;
                }

                SetSideSelection(dropdownField, setObjectId, defaultObjectId, defaultIndex);
                dropdownField.RegisterValueChangedCallback(_ =>
                {
                    var dropdownSides = dropdownField.userData as List<SideState>;
                    if (dropdownSides == null || dropdownField.index < 0 || dropdownField.index >= dropdownSides.Count)
                    {
                        setObjectId(null);
                        return;
                    }

                    setObjectId(dropdownSides[dropdownField.index].objectId);
                });
            }

            void SyncFalloffParameterState(InfluenceMapFalloffAlgorithm algorithm)
            {
                if (linearParameterRow != null)
                    linearParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Linear ? DisplayStyle.Flex : DisplayStyle.None;
                if (exponentialParameterRow != null)
                    exponentialParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Exponential ? DisplayStyle.Flex : DisplayStyle.None;
                if (inverseParameterRow != null)
                    inverseParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Inverse ? DisplayStyle.Flex : DisplayStyle.None;
                if (gaussianParameterRow != null)
                    gaussianParameterRow.style.display = algorithm == InfluenceMapFalloffAlgorithm.Gaussian ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void SyncSide2State(StrategicInfluenceMapType mapType)
            {
                side2DropdownField?.SetEnabled(mapType == StrategicInfluenceMapType.Control);
            }

            void SyncPowerParameterEditability(StrategicInfluenceMapType mapType)
            {
                var isPower = mapType == StrategicInfluenceMapType.Power;
                falloffAlgorithmField?.SetEnabled(true);
                linearRangeField?.SetEnabled(true);
                exponentialDecayField?.SetEnabled(true);
                inverseHalfEffectField?.SetEnabled(true);
                gaussianSigmaField?.SetEnabled(true);

                if (isPower)
                {
                    CopyScenarioPowerParametersToModel();
                    RefreshParameterFieldValuesFromModel();
                }
            }

            void SyncForceRefreshState()
            {
                var isPower = model.mapType == StrategicInfluenceMapType.Power;
                if (forceRefreshToggle == null)
                    return;

                forceRefreshToggle.style.display = isPower ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isPower)
                {
                    model.forceRefresh = true;
                    forceRefreshToggle.SetValueWithoutNotify(true);
                    forceRefreshToggle.SetEnabled(false);
                    return;
                }

                var selectedSide = EntityManager.Instance.Get<SideState>(model.side1ObjectId);
                var hasValidCache = StrategicInfluenceMapUtility.HasValidPowerCache(selectedSide, state.scenarioState);
                if (hasValidCache)
                {
                    model.forceRefresh = false;
                    forceRefreshToggle.SetValueWithoutNotify(false);
                    forceRefreshToggle.SetEnabled(true);
                }
                else
                {
                    model.forceRefresh = true;
                    forceRefreshToggle.SetValueWithoutNotify(true);
                    forceRefreshToggle.SetEnabled(false);
                }
            }

            var defaultSide1ObjectId = StrategicInfluenceMapUtility.GetDefaultSide1ObjectId(state, StrategicGameManager.Instance.currentSideStateObjectId);
            var defaultSide2ObjectId = StrategicInfluenceMapUtility.GetDefaultSide2ObjectId(state, defaultSide1ObjectId);

            SyncSideSelection(side1DropdownField, objectId => model.side1ObjectId = objectId, defaultSide1ObjectId, 0);
            SyncSideSelection(side2DropdownField, objectId => model.side2ObjectId = objectId, defaultSide2ObjectId, sides.Count > 1 ? 1 : 0);

            mapTypeField?.RegisterValueChangedCallback(evt =>
            {
                model.mapTypeValue = evt.newValue;
                var mapType = (StrategicInfluenceMapType)evt.newValue;
                SyncSide2State(mapType);
                SyncPowerParameterEditability(mapType);
                SyncFalloffParameterState((InfluenceMapFalloffAlgorithm)falloffAlgorithmField.value);
                SyncForceRefreshState();
            });
            SyncSide2State(mapTypeField != null ? (StrategicInfluenceMapType)mapTypeField.value : model.mapType);

            falloffAlgorithmField?.RegisterValueChangedCallback(evt =>
            {
                model.falloffAlgorithmValue = evt.newValue;
                if (model.mapType == StrategicInfluenceMapType.Power)
                {
                    SyncScenarioStatePowerParameters();
                    SyncForceRefreshState();
                }

                SyncFalloffParameterState((InfluenceMapFalloffAlgorithm)evt.newValue);
            });

            linearRangeField?.RegisterValueChangedCallback(evt =>
            {
                model.linearRangeCost = evt.newValue;
                if (model.mapType == StrategicInfluenceMapType.Power)
                {
                    SyncScenarioStatePowerParameters();
                    SyncForceRefreshState();
                }
            });

            exponentialDecayField?.RegisterValueChangedCallback(evt =>
            {
                model.exponentialDecayLengthCost = evt.newValue;
                if (model.mapType == StrategicInfluenceMapType.Power)
                {
                    SyncScenarioStatePowerParameters();
                    SyncForceRefreshState();
                }
            });

            inverseHalfEffectField?.RegisterValueChangedCallback(evt =>
            {
                model.inverseHalfEffectDistanceCost = evt.newValue;
                if (model.mapType == StrategicInfluenceMapType.Power)
                {
                    SyncScenarioStatePowerParameters();
                    SyncForceRefreshState();
                }
            });

            gaussianSigmaField?.RegisterValueChangedCallback(evt =>
            {
                model.gaussianSigmaCost = evt.newValue;
                if (model.mapType == StrategicInfluenceMapType.Power)
                {
                    SyncScenarioStatePowerParameters();
                    SyncForceRefreshState();
                }
            });

            forceRefreshToggle?.RegisterValueChangedCallback(evt =>
            {
                model.forceRefresh = evt.newValue;
            });

            side1DropdownField?.RegisterValueChangedCallback(_ => SyncForceRefreshState());
            SyncFalloffParameterState(falloffAlgorithmField != null ? (InfluenceMapFalloffAlgorithm)falloffAlgorithmField.value : model.falloffAlgorithm);
            SyncPowerParameterEditability(model.mapType);
            SyncForceRefreshState();

            if (plotButton != null)
            {
                plotButton.clicked += () =>
                {
                    StrategicInfluenceMapRequest request;
                    if (model.mapType == StrategicInfluenceMapType.Power)
                    {
                        request = StrategicInfluenceMapUtility.BuildPowerRequest(state, model.side1ObjectId);
                        request.forceRefresh = model.forceRefresh;
                    }
                    else
                    {
                        request = new StrategicInfluenceMapRequest
                        {
                            mapType = model.mapType,
                            forceRefresh = true,
                            falloffAlgorithm = model.falloffAlgorithm,
                            side1ObjectId = model.side1ObjectId,
                            side2ObjectId = model.side2ObjectId,
                            linearRangeCost = model.linearRangeCost,
                            exponentialDecayLengthCost = model.exponentialDecayLengthCost,
                            inverseHalfEffectDistanceCost = model.inverseHalfEffectDistanceCost,
                            gaussianSigmaCost = model.gaussianSigmaCost,
                        };
                    }

                    StrategicGameManager.Instance.PlotStrategicInfluenceMap(request);
                    if (model.mapType == StrategicInfluenceMapType.Power)
                    {
                        SyncForceRefreshState();
                    }
                };
            }

            if (clearButton != null)
            {
                clearButton.clicked += StrategicGameManager.Instance.ClearStrategicInfluenceMap;
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipTimeLocDialog(ShipLog shipLog)
    {
        if (shipTimeLocDialogDocument == null)
        {
            PopupMessageDialog("ShipTimeLocDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipTimeLocDialogDocument,
            templateDataSource = shipLog
        };

        tempDialog.Popup();
    }

    public static Color GetDefaultTrajectoryColor(ShipLog shipLog)
    {
        if (shipLog?.shipClass?.country == Country.Japan)
            return Color.blue;
        return Color.red;
    }

    public void PopupBatteryArcIndicatorDialog(ShipClass shipClass)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = batteryArcIndicatorDialogDocument,
        };

        tempDialog.onCreated += (sender, root) =>
        {
            var binder = new SectorArcIndicatorBinder();
            binder.BindUI(root);
            binder.BindBatteryData(shipClass);
        };

        tempDialog.Popup();
    }

    public void PopupGamePreferenceDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = gamePreferenceDialogDocument,
            templateDataSource = GamePreference.Instance,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var localeDropdownField = el.Q<DropdownField>("LocaleDropdownField");
            StartCoroutine(GamePreference.Instance.SetupLocale(localeDropdownField));
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            GamePreference.Instance.SaveToPlayerPrefs();
        };

        tempDialog.Popup();
    }

    public void PopupLandUnitTemplatePickerDialog(Action<LandUnitTemplate> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = landUnitTemplateDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var landUnitTemplate = objectListView.selectedItem as LandUnitTemplate;
            callback(landUnitTemplate);
        };

        tempDialog.Popup();
    }

    public void PopupWeaponPickerDialog(Action<Weapon> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = weaponPickerDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var weapon = objectListView.selectedItem as Weapon;
            callback(weapon);
        };

        tempDialog.Popup();
    }

    public void PopupSideStatePickerDialog(Action<SideState> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = sideStatePickerDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var sideState = objectListView.selectedItem as SideState;
            callback(sideState);
        };

        tempDialog.Popup();
    }

    public void PopupStrategicGroupPickerDialog(Action<StrategicGroup> callback, Func<StrategicGroup, bool> filter = null)
    {
        var strategicGroups = StrategicGameManager.Instance.gameState.strategicGroups;
        if (filter != null)
        {
            strategicGroups = strategicGroups.Where(filter).ToList();
        }

        var selectorDialog = new NamedSelector<StrategicGroup>()
        {
            fullObjects = strategicGroups,
            callback = callback
        };
        selectorDialog.RefreshFilter();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicGroupPickerDialogDocument,
            templateDataSource = selectorDialog
        };

        tempDialog.onConfirmed += selectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupStrategicViewerSideQuickPickerDialog()
    {
        var model = new StrategicViewerSideQuickPickerDialogModel();
        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicViewerSideQuickPickerDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var optionsContainer = el.Q<VisualElement>("OptionsContainer");
            optionsContainer.Clear();

            foreach (var option in model.options)
            {
                var optionElement = strategicViewerSideQuickPickerOptionDocument.CloneTree();
                optionElement.dataSource = option;
                Utils.BindItemsSourceRecursive(optionElement);
                var button = optionElement.Q<Button>("OptionButton");
                var flagElement = optionElement.Q<VisualElement>("FlagElement");

                if (!string.IsNullOrWhiteSpace(option.flagPath))
                {
                    UnityWebRequestImageReader.Instance.RequestIfNotRequestedYetOtherwiseExecuteDirectly(new ImageFetchTask()
                    {
                        path = option.flagPath,
                        styleBackgroundCallbacks = new()
                        {
                            styleBackground =>
                            {
                                flagElement.style.backgroundImage = styleBackground;
                            }
                        }
                    });
                }

                button.clicked += () =>
                {
                    tempDialog.Close();

                    if (option.isObserverEditorMode)
                    {
                        StrategicGameManager.Instance.viewerSideId = null;
                        StrategicGameManager.Instance.isInEditMode = true;
                    }
                    else
                    {
                        StrategicGameManager.Instance.viewerSideId = option.sideObjectId;
                        StrategicGameManager.Instance.isInEditMode = false;
                        PopupCurrentSideAutomationDialog(StrategicGameManager.Instance.GetViewerSide());
                    }
                };

                optionsContainer.Add(optionElement);
            }
        };

        tempDialog.Popup();
    }

    public void PopupCurrentSideAutomationDialog(SideState sideState)
    {
        if (sideState == null)
            return;

        var tempDialog = new TempDialog()
        {
            root = root,
            template = currentSideAutomationDialogDocument,
            templateDataSource = sideState,
        };

        tempDialog.Popup();
    }

    public void PopupSubordinatePickerDialog(Action<List<IStrategicGroupMemberReferenceable>> confirmCallback, SubordinatePickerDialog.Mode mode)
    {
        var subordinatePickerDialog = new SubordinatePickerDialog()
        {
            confirmCallback = confirmCallback,
            mode = mode
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = subordinatePickerDialogDocument,
            templateDataSource = subordinatePickerDialog,
        };
        tempDialog.onCreated += subordinatePickerDialog.OnCreated;
        tempDialog.onConfirmed += subordinatePickerDialog.OnConfirmed;

        tempDialog.Popup();
    }

    public void PopupStrategicGroupTransferDialog(StrategicGroup initialGroup)
    {
        if (initialGroup == null)
        {
            PopupMessageDialog("No strategic group is selected.");
            return;
        }

        if (strategicGroupTransferDialogDocument == null)
        {
            PopupMessageDialog("StrategicGroupTransferDialog is not configured.");
            return;
        }

        var transferDialog = new StrategicGroupTransferDialog()
        {
            initialGroupObjectId = initialGroup.objectId,
        };

        if (!transferDialog.CanOpen(out var message))
        {
            PopupMessageDialog(message);
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicGroupTransferDialogDocument,
            templateDataSource = transferDialog,
        };
        tempDialog.onCreated += transferDialog.OnCreated;
        tempDialog.onConfirmed += transferDialog.OnConfirmed;

        tempDialog.Popup();
    }

    public void PopupStrategicGroupDetachDamagedDialog(StrategicGroup initialGroup)
    {
        if (initialGroup == null)
        {
            PopupMessageDialog("No strategic group is selected.");
            return;
        }

        if (initialGroup.cell == null)
        {
            PopupMessageDialog(Localize("Detach damaged requires the current group to resolve to a cell."));
            return;
        }

        var detachedShips = StrategicGroupSubGroupUtility.CollectCombinedHierarchyShipsNeedingDetach(initialGroup);
        if (detachedShips.Count == 0)
        {
            PopupMessageDialog(Localize("No ships require detach for repair."));
            return;
        }

        var shipList = StrategicGroupSubGroupUtility.BuildDetachDamagedShipList(detachedShips);
        PopupConfirmDialog(
            Localize("The following ships will be detached for repair:") + "\n" + shipList,
            () =>
            {
                StrategicGroupSubGroupUtility.DetachDamagedShipsForRepair(initialGroup, detachedShips);
            },
            Localize("Detach Damaged Ships")
        );
    }

    public void PopupLocationLabelDialog(StrategicLocationLabel label)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = locationLabelDialogDocument,
            // templateDataSource = StreamingAssetReference.Instance
            templateDataSource = label
        };

        tempDialog.Popup();
    }

    public void PopupNavalLocationLabelEditorDialogForCreate(LatLon latLon)
    {
        var model = LocationLabelEditDialogModel.ForCreate(latLon, label =>
        {
            NavalGameState.Instance.scenarioState.locationLabels ??= new();
            NavalGameState.Instance.scenarioState.locationLabels.Add(label);
        });

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalLocationLabelEditorDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += model.OnConfirm;
        tempDialog.Popup();
    }

    public void PopupNavalLocationLabelEditorDialog(LocationLabel label, Action afterConfirm = null)
    {
        var model = LocationLabelEditDialogModel.ForEdit(label, afterConfirm);

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalLocationLabelEditorDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += model.OnConfirm;
        tempDialog.Popup();
    }

    public void PopupShipGroupRemarkDialog(ShipGroup shipGroup, Action onClosed = null)
    {
        if (shipGroup == null)
            return;

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipGroupRemarkDialogDocument,
            templateDataSource = shipGroup.remark
        };

        if (onClosed != null)
        {
            tempDialog.onClosed += (sender, root) => onClosed();
        }

        tempDialog.Popup();
    }

    public void PopupLocationLabelsEditorDialog()
    {
        var dialog = new LocationLabelsEditorDialog();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = locationLabelsEditorDialogDocument,
            templateDataSource = dialog
        };

        tempDialog.onCreated += dialog.OnCreated;
        tempDialog.Popup();
    }

    public void PopupScenarioPickerDialogForScenarioSwitchInGame()
    {
        ManifestModelCache.Instance.CommitTask(manifestModel =>
        {
            var scenarioNames = manifestModel.scenarioFiles.Select(path => path.Split("/").Last()).ToList();
            var scenarioPickerDialog = new ScenarioPickerDialog()
            {
                scenarioNames = scenarioNames,
                callbackOnceScenarioNameGet = GameManager.Instance.StartLoadScenarioCoroutine
            };
            var tempDialog = new TempDialog()
            {
                root = root,
                template = scenarioPickerDialogDocument,
                templateDataSource = scenarioPickerDialog
            };
            scenarioPickerDialog.Bind(tempDialog);

            tempDialog.Popup();
        });
    }

    public void PopupScenarioPickerDialogForSwitchingSceneWithSelectedScenario()
    {
        ManifestModelCache.Instance.CommitTask(manifestModel =>
        {
            var scenarioNames = manifestModel.scenarioFiles.Select(path => path.Split("/").Last()).ToList();
            var scenarioPickerDialog = new ScenarioPickerDialog()
            {
                scenarioNames = scenarioNames,
                callbackOnceScenarioNameGet = scenarioName =>
                {
                    // GameManager.startupConfig.builtinScenName = scenarioName;
                    // GameManager.startupConfig.mode = GameManager.StartupConfig.Mode.BuiltinScenName;
                    GameManager.startupConfig = new()
                    {
                        builtinScenName = scenarioName,
                        mode = GameManager.StartupConfig.Mode.BuiltinScenName
                    };
                    SceneManager.LoadScene("Naval Game");
                }
            };
            var tempDialog = new TempDialog()
            {
                root = root,
                template = scenarioPickerDialogDocument,
                templateDataSource = scenarioPickerDialog,
                positionMode = TempDialog.PositionMode.None,
                fullScreen = true
            };
            scenarioPickerDialog.Bind(tempDialog);

            tempDialog.Popup();
        });
    }

    public void PopupStreamingAssetReferenceDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = streamingAssetReferenceDialogDocument,
            // templateDataSource = StreamingAssetReference.Instance
            templateDataSource = ReferenceManager.Instance
        };

        tempDialog.Popup();
    }

    public void PopupMessageDialog(string message, string title = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = messageDialogDocument,
            templateDataSource = null,
            // draggable = true
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var contentTextField = el.Q<TextField>("ContentTextField");

            contentTextField.SetValueWithoutNotify(message);
            if (title != null)
            {
                var titleLabel = el.Q<Label>("TitleLabel");
                titleLabel.text = title;
            }
        };

        tempDialog.Popup();
    }

    public TempDialog PopupCustomMessageContentDialog(string title, Func<VisualElement> contentFactory, float width = 900f, float height = 560f, string confirmButtonText = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = messageDialogDocument,
            templateDataSource = null,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var contentTextField = el.Q<TextField>("ContentTextField");
            var panel = contentTextField?.parent;
            if (panel != null)
            {
                panel.style.width = width;
                panel.style.height = height;
            }

            if (title != null)
            {
                var titleLabel = el.Q<Label>("TitleLabel");
                titleLabel.text = title;
            }

            if (!string.IsNullOrEmpty(confirmButtonText))
            {
                var confirmButton = el.Q<Button>("ConfirmButton");
                if (confirmButton != null)
                {
                    confirmButton.text = confirmButtonText;
                }
            }

            var customContent = contentFactory?.Invoke();
            if (contentTextField != null && customContent != null)
            {
                var parent = contentTextField.parent;
                var index = parent.IndexOf(contentTextField);
                contentTextField.RemoveFromHierarchy();
                customContent.style.flexGrow = 1;
                customContent.style.flexShrink = 1;
                parent.Insert(index, customContent);
            }
        };

        tempDialog.Popup();
        return tempDialog;
    }

    public void PopupConfirmDialog(string message, Action confirmCallback, string title = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = confirmDialogDocument,
            templateDataSource = null,
            // draggable = true
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var contentTextField = el.Q<TextField>("ContentTextField");

            contentTextField.SetValueWithoutNotify(message);
            if (title != null)
            {
                var titleLabel = el.Q<Label>("TitleLabel");
                titleLabel.text = title;
            }
        };

        tempDialog.onConfirmed += (sender, el) => confirmCallback();

        tempDialog.Popup();
    }

    public void PopupFollowFormationDialog(Action<float> confirmCallback, float initialFollowDistanceYards = 500f)
    {
        if (followFormationDialogDocument == null)
        {
            PopupMessageDialog("FollowFormationDialog is not configured.");
            return;
        }

        var model = new FollowFormationDialogModel()
        {
            followDistanceYards = initialFollowDistanceYards
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = followFormationDialogDocument,
            templateDataSource = model,
        };

        tempDialog.confirmCheck = _ =>
        {
            if (float.IsNaN(model.followDistanceYards) || float.IsInfinity(model.followDistanceYards) || model.followDistanceYards <= 0f)
            {
                PopupMessageDialog("Follow distance must be greater than 0 yards.");
                return false;
            }
            return true;
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            confirmCallback?.Invoke(model.followDistanceYards);
        };

        tempDialog.Popup();
    }

    public void PopupShipClassPlaceholderGeneratorDialog(ShipClass shipClass)
    {
        if (shipClassPlaceholderGeneratorDialogDocument == null)
        {
            PopupMessageDialog("ShipClassPlaceholderGeneratorDialog is not configured.");
            return;
        }

        var model = ShipClassPlaceholderImageGenerator.CreateDefaultDialogModel(shipClass);

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipClassPlaceholderGeneratorDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var titleLabel = el.Q<Label>("TitleLabel");
            var previewImage = el.Q<Image>("PreviewImage");
            var statusLabel = el.Q<Label>("StatusLabel");
            var generateButton = el.Q<Button>("GenerateButton");
            var saveTopButton = el.Q<Button>("SaveTopButton");
            var saveIconButton = el.Q<Button>("SaveIconButton");

            if (titleLabel != null && shipClass != null)
            {
                titleLabel.text = MyLocale.Get("Generate Placeholder Image - {0}", shipClass.name.GetMergedName());
            }

            void RefreshUi()
            {
                if (previewImage != null)
                {
                    previewImage.image = model.previewTexture;
                }
                if (statusLabel != null)
                {
                    statusLabel.text = model.statusText;
                }
                if (saveTopButton != null)
                {
                    saveTopButton.SetEnabled(model.hasGenerated);
                }
                if (saveIconButton != null)
                {
                    saveIconButton.SetEnabled(model.hasGenerated);
                }
            }

            model.TryGenerate();
            RefreshUi();

            if (generateButton != null)
            {
                generateButton.clicked += () =>
                {
                    model.TryGenerate();
                    RefreshUi();
                };
            }

            if (saveTopButton != null)
            {
                saveTopButton.clicked += () =>
                {
                    model.SaveTopImage();
                    RefreshUi();
                };
            }

            if (saveIconButton != null)
            {
                saveIconButton.clicked += () =>
                {
                    model.SaveIconImage();
                    RefreshUi();
                };
            }
        };

        tempDialog.onClosed += (_, _) => model.Dispose();
        tempDialog.Popup();
    }

    public void PopupRelativeFormationDialog(Action<RelativeFormationDialogModel> confirmCallback)
    {
        if (relativeFormationDialogDocument == null)
        {
            PopupMessageDialog("RelativeFormationDialog is not configured.");
            return;
        }

        var model = new RelativeFormationDialogModel();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = relativeFormationDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var modeField = el.Q<LocalizedEnumField>("ModeField");
            var angleField = el.Q<FloatField>("AngleField");
            modeField?.RegisterValueChangedCallback(evt =>
            {
                var mode = (RelativeFormationMode)evt.newValue;
                if (mode == RelativeFormationMode.LineAbreast)
                {
                    model.angleDeg = 90f;
                    angleField?.SetValueWithoutNotify(model.angleDeg);
                }
                else if (mode == RelativeFormationMode.LineOfBearing)
                {
                    model.angleDeg = 135f;
                    angleField?.SetValueWithoutNotify(model.angleDeg);
                }
            });
        };

        tempDialog.confirmCheck = _ =>
        {
            if (float.IsNaN(model.distanceYards) || float.IsInfinity(model.distanceYards) || model.distanceYards <= 0f)
            {
                PopupMessageDialog("Relative formation distance must be greater than 0 yards.");
                return false;
            }

            if (float.IsNaN(model.angleDeg) || float.IsInfinity(model.angleDeg))
            {
                PopupMessageDialog("Relative formation angle must be a valid number.");
                return false;
            }
            return true;
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            model.angleDeg = MeasureUtils.NormalizeAngle(model.angleDeg);
            confirmCallback?.Invoke(model);
        };

        tempDialog.Popup();
    }

    public void PopupPreScenarioDamageDialog(float initialDamageRatioPercent, Action<float> confirmCallback)
    {
        if (preScenarioDamageDialogDocument == null)
        {
            PopupMessageDialog("PreScenarioDamageDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = preScenarioDamageDialogDocument,
            templateDataSource = null,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var ratioSlider = el.Q<SliderInt>("TargetDamageRatioPercentSlider");
            ratioSlider?.SetValueWithoutNotify((int)Math.Round(Math.Clamp(initialDamageRatioPercent, 0, 100)));
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var ratioSlider = el.Q<SliderInt>("TargetDamageRatioPercentSlider");
            var targetRatioPercent = Math.Clamp(ratioSlider?.value ?? 0, 0, 100);
            confirmCallback?.Invoke(targetRatioPercent);
        };

        tempDialog.Popup();
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public void PopupConfirmOpenURLDialog(string url, string title = null)
    {
        PopupConfirmDialog(
            Localize("Confirm to open url {0} ?", url),
            () => Application.OpenURL(url),
            title
        );
    }

    public void PopupLeaderSelectorDialogForCallback(Action<Leader> callback)
    {
        var leaderSelector = new NamedSelector<Leader>()
        {
            fullObjects = SuperGameState.Instance.GetCurrentGameState().leaders,
            callback = callback
        };

        leaderSelector.RefreshFilter();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = leaderSelector
        };

        // tempDialog.onConfirmed += (sender, el) =>
        // {
        //     Debug.Log("tempDialog.onConfirmed");

        //     var leadersListView = el.Q<ListView>("LeadersListView");
        //     var leader = leadersListView.selectedItem as Leader;

        //     callback(leader);
        // };

        tempDialog.onConfirmed += leaderSelector.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForSpecifyForGroup()
    {
        PopupLeaderSelectorDialogForCallback(leader =>
        {
            var selectedGroup = OOBEditor.Instance.currentSelectedShipGroup;

            if (leader != null && selectedGroup != null)
            {
                // selectedGroup.leaderObjectId = leader.objectId;
                selectedGroup.leaderReference.referenceObjectId = leader.objectId;
            }
        });
    }

    public void PopupLeaderSelectorDialogForNamedShip()
    {
        PopupLeaderSelectorDialogForCallback(leader =>
        {
            var selectedNamedShip = NamedShipEditor.Instance.selectedObject;

            // if (leader != null && selectedNamedShip != null)
            // {
            //     selectedNamedShip.defaultLeaderReference.referenceObjectId = leader.objectId;
            // }
            if (selectedNamedShip != null)
            {
                selectedNamedShip.defaultLeaderReference.referenceObjectId = leader?.objectId;
            }
        });
    }

    public void PopupShipClassSelectorDialogForNamedShip()
    {
        var shipClassSelector = new ShipClassSelector()
        {
            fullShipClasses = SuperGameState.Instance.GetCurrentGameState().shipClasses
        };
        shipClassSelector.Refresh();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipClassSelectorDocument,
            templateDataSource = shipClassSelector
        };

        tempDialog.onConfirmed += shipClassSelector.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupNamedShipSelctorDialogForShipLog()
    {
        var namedShipSelector = new NamedShipSelector()
        {
            fullNamedShips = SuperGameState.Instance.GetCurrentGameState().namedShips
        };
        namedShipSelector.Refresh();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = namedShipSelectorDocument,
            templateDataSource = namedShipSelector // GameManager.Instance
        };

        tempDialog.onConfirmed += namedShipSelector.OnConfirm;

        // tempDialog.onConfirmed += (sender, el) =>
        // {
        //     // var selectedShipLog = GameManager.Instance.selectedShipLog;
        //     var selectedShipLog = ShipLogEditor.Instance.selectedShipLog;

        //     var namedShipListView = el.Q<ListView>("NamedShipListView");
        //     var namedShip = namedShipListView.selectedItem as NamedShip;
        //     if (selectedShipLog != null && namedShip != null)
        //     {
        //         selectedShipLog.namedShipObjectId = namedShip.objectId;
        //     }
        // };

        tempDialog.Popup();
    }

    public void PopupInsertShipComplexDialog()
    {
        var insertShipComplexDialog = new InsertShipComplexDialog();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = insertShipComplexDialogDocument,
            templateDataSource = insertShipComplexDialog
        };

        tempDialog.onConfirmed += insertShipComplexDialog.OnConfirm;
        tempDialog.onCreated += insertShipComplexDialog.OnCreated;
        tempDialog.confirmCheck = insertShipComplexDialog.ConfirmCheck;

        tempDialog.Popup();
    }

    public void PopupShipLogSelectorDialogForRedeploy()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;
            var latLon = GameManager.Instance.lastSelectedLatLon;
            if (selectedShipLog != null && latLon != null)
            {
                selectedShipLog.mapState = MapState.Deployed;
                selectedShipLog.position = latLon;
                selectedShipLog.MarkNonPhysicalPoseChanged();
                // Set Default heading?
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipLogSelectorDialogForAddShipLogToOOBItem()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var addToShipGroup = OOBEditor.Instance.currentSelectedShipGroup;
            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;

            if (addToShipGroup != null && selectedShipLog != null)
            {
                if (((IShipGroupMember)selectedShipLog).TryAttachTo(addToShipGroup))
                {
                    OOBEditor.Instance.Sync();
                }
                else
                {
                    Debug.LogWarning("Not attachable");
                }
            }
        };

        tempDialog.Popup();
    }

    void SetVictoryStatusSegmentWidth(VisualElement segment, float ratio)
    {
        if (segment == null)
            return;

        var clampedRatio = Mathf.Clamp01(ratio);
        segment.style.display = clampedRatio > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        segment.style.width = new Length(clampedRatio * 100f, LengthUnit.Percent);
    }

    void ConfigureVictoryStatusDetailListView(ListView listView)
    {
        if (listView == null)
            return;

        listView.makeItem = () =>
        {
            var item = listView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(item);
            return item;
        };

        listView.bindItem = (item, index) =>
        {
            if (listView.itemsSource is not List<ShipVictoryDetailItem> items ||
                index < 0 ||
                index >= items.Count)
                return;

            var shipDetailItem = items[index];
            item.dataSource = shipDetailItem;

            var shipIconContainer = item.Q<VisualElement>("ShipOverlayIconContainer");
            var shipIconImage = item.Q<VisualElement>("ShipOverlayIconImage");
            shipIconContainer?.EnableInClassList("victory-detail-ship-overlay-icon-container-sunk", shipDetailItem.isSunk);
            shipIconImage?.EnableInClassList("victory-detail-ship-overlay-icon-image-sunk", shipDetailItem.isSunk);

            SetVictoryStatusSegmentWidth(item.Q<VisualElement>("YellowSegment"), shipDetailItem.yellowRatio);
            SetVictoryStatusSegmentWidth(item.Q<VisualElement>("RedSegment"), shipDetailItem.redRatio);
            SetVictoryStatusSegmentWidth(item.Q<VisualElement>("TransparentSegment"), shipDetailItem.transparentRatio);
        };
    }

    void BindVictoryStatusDetailCard(VisualElement card, SideVictoryStatus sideVictoryStatus)
    {
        if (card == null)
            return;

        if (sideVictoryStatus == null)
        {
            card.style.display = DisplayStyle.None;
            return;
        }

        card.style.display = DisplayStyle.Flex;

        var groupNameLabel = card.Q<Label>(null, "victory-detail-group-title");
        if (groupNameLabel != null)
            groupNameLabel.text = sideVictoryStatus.name;

        var listView = card.Q<ListView>();
        if (listView != null)
        {
            listView.itemsSource = sideVictoryStatus.shipDetailItems;
            listView.Rebuild();
        }
    }

    public void PopupVictoryStatusDialog(VictoryStatus victoryStatus)
    {
        // StrategicGameManager.startupConfig.victoryStatus
        // var victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

        var tempDialog = new TempDialog()
        {
            root = root,
            template = victoryStatusDocument,
            templateDataSource = victoryStatus
        };

        tempDialog.onCreated += (sender, root) =>
        {
            // SideVictoryStatusesListView
            // ShipTypeLossItemsMultiColumnListView

            Utils.BindItemsSourceRecursive(root);

            var sideVictoryStatusesListView = root.Q<ListView>("SideVictoryStatusesListView");
            sideVictoryStatusesListView.makeItem = () =>
            {
                var el = sideVictoryStatusesListView.itemTemplate.CloneTree();

                Utils.BindItemsSourceRecursive(el);

                return el;
            };

            ConfigureVictoryStatusDetailListView(root.Q<ListView>("DetailGroup0ListView"));
            ConfigureVictoryStatusDetailListView(root.Q<ListView>("DetailGroup1ListView"));

            var detailSideVictoryStatuses = victoryStatus?.sideVictoryStatuses?.Take(2).ToList() ?? new();
            BindVictoryStatusDetailCard(root.Q<VisualElement>("DetailGroup0Card"), detailSideVictoryStatuses.ElementAtOrDefault(0));
            BindVictoryStatusDetailCard(root.Q<VisualElement>("DetailGroup1Card"), detailSideVictoryStatuses.ElementAtOrDefault(1));

            var summaryContainer = root.Q<VisualElement>("SummaryContainer");
            var detailContainer = root.Q<VisualElement>("DetailContainer");
            var modeToggleButton = root.Q<Button>("ModeToggleButton");

            void SetVictoryStatusMode(bool showDetail)
            {
                if (detailContainer != null)
                    detailContainer.style.display = showDetail ? DisplayStyle.Flex : DisplayStyle.None;
                if (summaryContainer != null)
                    summaryContainer.style.display = showDetail ? DisplayStyle.None : DisplayStyle.Flex;
                if (modeToggleButton != null)
                    modeToggleButton.text = Localize(showDetail ? "Summary" : "Detail");
            }

            SetVictoryStatusMode(showDetail: false);
            if (modeToggleButton != null)
            {
                modeToggleButton.clicked += () =>
                {
                    var showingDetail = detailContainer == null || detailContainer.style.display != DisplayStyle.None;
                    SetVictoryStatusMode(!showingDetail);
                };
            }
        };

        tempDialog.Popup();
    }

    public void PopupStrategicVictoryStatusDialog()
    {
        if (strategicVictoryStatusDialogDocument == null)
        {
            PopupMessageDialog("StrategicVictoryStatusDialog is not configured.");
            return;
        }

        var model = StrategicVictoryStatusDialogModel.Generate(StrategicGameState.Instance);
        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicVictoryStatusDialogDocument,
            templateDataSource = model
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var listView = el.Q<ListView>("StrategicVictoryStatusListView");
            if (listView == null)
                return;

            listView.makeItem = () =>
            {
                var row = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        paddingLeft = 4,
                        paddingRight = 4
                    }
                };

                Label MakeCell(string name, float basis)
                {
                    var label = new Label();
                    label.name = name;
                    label.style.flexBasis = basis;
                    label.style.flexShrink = 0;
                    return label;
                }

                row.Add(MakeCell("SideNameLabel", 220));
                row.Add(MakeCell("LandBattleLossLabel", 180));
                row.Add(MakeCell("DestroyedShipsLabel", 140));
                row.Add(MakeCell("LandBattleVictoryLabel", 120));
                row.Add(MakeCell("LandBattleDefeatLabel", 140));

                return row;
            };

            listView.bindItem = (item, index) =>
            {
                if (listView.itemsSource is not List<StrategicVictoryStatusRow> rows ||
                    index < 0 ||
                    index >= rows.Count)
                    return;

                var row = rows[index];
                item.Q<Label>("SideNameLabel").text = row.sideName;
                item.Q<Label>("LandBattleLossLabel").text = row.totalLandBattleLossMenText;
                item.Q<Label>("DestroyedShipsLabel").text = row.totalDestroyedShipCountText;
                item.Q<Label>("LandBattleVictoryLabel").text = row.landBattleVictoryCountText;
                item.Q<Label>("LandBattleDefeatLabel").text = row.landBattleDefeatCountText;
            };

            listView.itemsSource = model.rows;
            listView.Rebuild();
        };

        tempDialog.Popup();
    }

    public void PopupHelpDialogDocument()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = helpDialogDocument,
            templateDataSource = null,
            positionMode = TempDialog.PositionMode.None,
        };

        tempDialog.Popup();
    }

    public void PopupAboutDialogDocument()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = aboutDialogDocument,
            templateDataSource = null,
            positionMode = TempDialog.PositionMode.Centering,
        };

        tempDialog.onCreated += (_, el) =>
        {
            el.Q<Label>("TitleLabel").text = MyLocale.Get("About");
            el.Q<Label>("ProductNameLabel").text = MyLocale.Get("First Sino-Japanese War");
            el.Q<Label>("VersionLabel").text = $"Version: {Application.version}";
            el.Q<Label>("DeveloperLabel").text = MyLocale.Get("Developed by January Desk");
            el.Q<Label>("LicenseLabel").text = MyLocale.Get("Open-source under the MIT License");

            BindOpenUrlButton(el, "GitHubButton", "GitHub (Open Source Repository)", "https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
            BindOpenUrlButton(el, "GitHubReleaseButton", "GitHub Release (What's New)", "https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo/releases");
            BindOpenUrlButton(el, "SteamStoreButton", "Steam Store", "https://store.steampowered.com/app/3996220/First_SinoJapanese_War/");
            BindOpenUrlButton(el, "SteamDiscussionsButton", "Steam Discussions", "https://steamcommunity.com/app/3996220/discussions/");
            BindOpenUrlButton(el, "DiscordButton", "Discord", "https://discord.gg/2yqbyGwsdQ");
        };

        tempDialog.Popup();
    }
    
    public void PopupFAQDialogDocument()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = faqDialogDocument,
            templateDataSource = null,
            positionMode = TempDialog.PositionMode.None,
        };

        tempDialog.Popup();
    }

    void BindOpenUrlButton(VisualElement rootElement, string buttonName, string labelKey, string url)
    {
        var button = rootElement.Q<Button>(buttonName);
        if (button == null)
            return;

        button.text = MyLocale.Get(labelKey);
        button.clicked += () => PopupConfirmOpenURLDialog(url);
    }
}
