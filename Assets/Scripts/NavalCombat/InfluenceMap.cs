using System;
using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine;

public enum InfluenceMapType
{
    Power,
    Firepower,
    Control,
}

public enum InfluenceMapFalloffAlgorithm
{
    Linear,
    Exponential,
    Inverse,
    Gaussian,
}

public static class InfluenceMapDefaults
{
    public const int SampleWidth = 96;
    public const int SampleHeight = 96;
    public const float BoundsPaddingRatio = 0.1f;
    public const float MinBoundsPaddingDeg = 0.05f;
    public const float LinearRangeYards = 36000f;
    public const float ExponentialDecayLengthYards = 12000f;
    public const float InverseHalfEffectDistanceYards = 12000f;
    public const float GaussianSigmaYards = 12000f;
}

public class InfluenceMapRequest
{
    public InfluenceMapType mapType;
    public InfluenceMapFalloffAlgorithm falloffAlgorithm = InfluenceMapFalloffAlgorithm.Linear;
    public bool fillEnabled = true;
    public string group1ObjectId;
    public string group2ObjectId;
    public float linearRangeYards = InfluenceMapDefaults.LinearRangeYards;
    public float exponentialDecayLengthYards = InfluenceMapDefaults.ExponentialDecayLengthYards;
    public float inverseHalfEffectDistanceYards = InfluenceMapDefaults.InverseHalfEffectDistanceYards;
    public float gaussianSigmaYards = InfluenceMapDefaults.GaussianSigmaYards;
    public int sampleWidth = InfluenceMapDefaults.SampleWidth;
    public int sampleHeight = InfluenceMapDefaults.SampleHeight;
    public float boundsPaddingRatio = InfluenceMapDefaults.BoundsPaddingRatio;
    public float minBoundsPaddingDeg = InfluenceMapDefaults.MinBoundsPaddingDeg;
}

public class InfluenceMapDialogModel
{
    [CreateProperty]
    public int mapTypeValue { get; set; } = (int)InfluenceMapType.Power;

    [CreateProperty]
    public int falloffAlgorithmValue { get; set; } = (int)InfluenceMapFalloffAlgorithm.Linear;

    [CreateProperty]
    public bool fillEnabled { get; set; } = true;

    [CreateProperty]
    public float linearRangeYards { get; set; } = InfluenceMapDefaults.LinearRangeYards;

    [CreateProperty]
    public float exponentialDecayLengthYards { get; set; } = InfluenceMapDefaults.ExponentialDecayLengthYards;

    [CreateProperty]
    public float inverseHalfEffectDistanceYards { get; set; } = InfluenceMapDefaults.InverseHalfEffectDistanceYards;

    [CreateProperty]
    public float gaussianSigmaYards { get; set; } = InfluenceMapDefaults.GaussianSigmaYards;

    [CreateProperty]
    public int sampleWidth { get; set; } = InfluenceMapDefaults.SampleWidth;

    [CreateProperty]
    public int sampleHeight { get; set; } = InfluenceMapDefaults.SampleHeight;

    [CreateProperty]
    public float boundsPaddingRatio { get; set; } = InfluenceMapDefaults.BoundsPaddingRatio;

    [CreateProperty]
    public float minBoundsPaddingDeg { get; set; } = InfluenceMapDefaults.MinBoundsPaddingDeg;

    public string group1ObjectId;
    public string group2ObjectId;

    public InfluenceMapType mapType => (InfluenceMapType)mapTypeValue;
    public InfluenceMapFalloffAlgorithm falloffAlgorithm => (InfluenceMapFalloffAlgorithm)falloffAlgorithmValue;
}

public readonly struct InfluenceMapBounds
{
    public readonly float minLat;
    public readonly float maxLat;
    public readonly float minLon;
    public readonly float maxLon;

    public InfluenceMapBounds(float minLat, float maxLat, float minLon, float maxLon)
    {
        this.minLat = minLat;
        this.maxLat = maxLat;
        this.minLon = minLon;
        this.maxLon = maxLon;
    }

    public float latSpan => maxLat - minLat;
    public float lonSpan => maxLon - minLon;

    public LatLon Lerp(float x01, float y01)
    {
        return new LatLon(
            Mathf.Lerp(minLat, maxLat, Mathf.Clamp01(y01)),
            Mathf.Lerp(minLon, maxLon, Mathf.Clamp01(x01))
        );
    }
}

public sealed class InfluenceMapFieldData
{
    public InfluenceMapBounds bounds;
    public float[,] values;
    public int width;
    public int height;
    public float maxAbs;
}

public sealed class InfluenceMapContourPolyline
{
    public float level;
    public List<LatLon> points = new();
}

public static class InfluenceMapUtility
{
    public const int SampleWidth = InfluenceMapDefaults.SampleWidth;
    public const int SampleHeight = InfluenceMapDefaults.SampleHeight;
    public const float RangeYards = InfluenceMapDefaults.LinearRangeYards;
    public const float BoundsPaddingRatio = InfluenceMapDefaults.BoundsPaddingRatio;
    public const float MinBoundsPaddingDeg = InfluenceMapDefaults.MinBoundsPaddingDeg;

    readonly struct ContourSegment
    {
        public readonly LatLon start;
        public readonly LatLon end;

        public ContourSegment(LatLon start, LatLon end)
        {
            this.start = start;
            this.end = end;
        }
    }

    readonly struct ShipFieldSample
    {
        public readonly ShipLog shipLog;
        public readonly float xYards;
        public readonly float yYards;
        public readonly float headingDeg;
        public readonly float generalScore;

        public ShipFieldSample(ShipLog shipLog, MeasureUtils.LocalProjection projection)
        {
            this.shipLog = shipLog;
            xYards = projection.LongitudeToX(shipLog.position.LonDeg);
            yYards = projection.LatitudeToY(shipLog.position.LatDeg);
            headingDeg = shipLog.headingDeg;
            generalScore = shipLog.EvaluateGeneralScore();
        }
    }

    public static List<ShipGroup> GetShipGroupsInOobOrder(NavalGameState state)
    {
        return GetShipGroupsInOobOrder(state?.shipGroups, objectId => CoreUtils.EntityManager.Instance.Get<IShipGroupMember>(objectId));
    }

    public static List<ShipGroup> GetTopLevelShipGroupsInOobOrder(NavalGameState state)
    {
        return GetTopLevelShipGroupsInOobOrder(state?.shipGroups);
    }

    public static List<ShipGroup> GetTopLevelShipGroupsInOobOrder(IReadOnlyList<ShipGroup> shipGroups)
    {
        if (shipGroups == null)
            return new List<ShipGroup>();

        return shipGroups.Where(group => group != null && string.IsNullOrEmpty(group.parentObjectId)).ToList();
    }

    public static List<ShipGroup> GetShipGroupsInOobOrder(IReadOnlyList<ShipGroup> shipGroups, Func<string, IShipGroupMember> resolver)
    {
        var orderedGroups = new List<ShipGroup>();
        if (shipGroups == null || resolver == null)
            return orderedGroups;

        void Visit(ShipGroup group)
        {
            if (group == null)
                return;

            orderedGroups.Add(group);
            foreach (var childObjectId in group.childrenObjectIds ?? Enumerable.Empty<string>())
            {
                var childGroup = resolver(childObjectId) as ShipGroup;
                if (childGroup != null)
                    Visit(childGroup);
            }
        }

        foreach (var rootGroup in shipGroups.Where(group => group != null && string.IsNullOrEmpty(group.parentObjectId)))
        {
            Visit(rootGroup);
        }

        return orderedGroups;
    }

    public static List<ShipLog> GetDeployedShipsForGroup(ShipGroup group)
    {
        return GetDeployedShipsRecursive(group, objectId => CoreUtils.EntityManager.Instance.Get<IShipGroupMember>(objectId));
    }

    public static List<ShipLog> GetDeployedShipsRecursive(ShipGroup group, Func<string, IShipGroupMember> resolver)
    {
        var ships = new List<ShipLog>();
        if (group == null || resolver == null)
            return ships;

        void Visit(ShipGroup current)
        {
            foreach (var childObjectId in current.childrenObjectIds ?? Enumerable.Empty<string>())
            {
                var child = resolver(childObjectId);
                if (child is ShipLog shipLog)
                {
                    if (shipLog.mapState == MapState.Deployed)
                        ships.Add(shipLog);
                }
                else if (child is ShipGroup childGroup)
                {
                    Visit(childGroup);
                }
            }
        }

        Visit(group);
        return ships;
    }

    public static bool TryBuildBattleBounds(NavalGameState state, out InfluenceMapBounds bounds)
    {
        return TryBuildBattleBounds(state, null, out bounds);
    }

    public static bool TryBuildBattleBounds(NavalGameState state, InfluenceMapRequest request, out InfluenceMapBounds bounds)
    {
        bounds = default;
        if (state == null)
            return false;

        return TryBuildBattleBounds(
            state.shipLogsOnMap,
            GetBoundsPaddingRatio(request),
            GetMinBoundsPaddingDeg(request),
            out bounds
        );
    }

    public static bool TryBuildBattleBounds(IEnumerable<ShipLog> deployedShips, float boundsPaddingRatio, float minBoundsPaddingDeg, out InfluenceMapBounds bounds)
    {
        var latitudes = new List<float>();
        var longitudes = new List<float>();

        if (deployedShips != null)
        {
            foreach (var ship in deployedShips)
            {
                if (ship == null || ship.mapState != MapState.Deployed)
                    continue;

                latitudes.Add(ship.position.LatDeg);
                longitudes.Add(ship.position.LonDeg);
            }
        }

        if (latitudes.Count == 0 || longitudes.Count == 0)
        {
            bounds = default;
            return false;
        }

        var minLat = latitudes.Min();
        var maxLat = latitudes.Max();
        var minLon = longitudes.Min();
        var maxLon = longitudes.Max();

        var safePaddingRatio = Mathf.Max(0f, boundsPaddingRatio);
        var safeMinPaddingDeg = Mathf.Max(0f, minBoundsPaddingDeg);
        var latPadding = Mathf.Max((maxLat - minLat) * safePaddingRatio, safeMinPaddingDeg);
        var lonPadding = Mathf.Max((maxLon - minLon) * safePaddingRatio, safeMinPaddingDeg);

        bounds = new InfluenceMapBounds(
            minLat - latPadding,
            maxLat + latPadding,
            minLon - lonPadding,
            maxLon + lonPadding
        );
        return true;
    }

    public static float EvaluateDistanceAttenuation(float distanceYards)
    {
        return EvaluateDistanceAttenuation(distanceYards, InfluenceMapFalloffAlgorithm.Linear, InfluenceMapDefaults.LinearRangeYards);
    }

    public static float EvaluateDistanceAttenuation(float distanceYards, InfluenceMapRequest request)
    {
        return request == null
            ? EvaluateDistanceAttenuation(distanceYards)
            : EvaluateDistanceAttenuation(distanceYards, request.falloffAlgorithm, GetPrimaryDistanceParameterYards(request));
    }

    public static float EvaluateDistanceAttenuation(float distanceYards, InfluenceMapFalloffAlgorithm algorithm, float parameterYards)
    {
        var safeParameterYards = Mathf.Max(1f, parameterYards);
        return algorithm switch
        {
            InfluenceMapFalloffAlgorithm.Linear => Mathf.Max(0f, (safeParameterYards - distanceYards) / safeParameterYards),
            InfluenceMapFalloffAlgorithm.Exponential => Mathf.Exp(-distanceYards / safeParameterYards),
            InfluenceMapFalloffAlgorithm.Inverse => safeParameterYards / (safeParameterYards + Mathf.Max(0f, distanceYards)),
            InfluenceMapFalloffAlgorithm.Gaussian => Mathf.Exp(-0.5f * Square(distanceYards / safeParameterYards)),
            _ => 0f,
        };
    }

    public static float EvaluatePowerContribution(ShipLog shipLog, LatLon point)
    {
        if (shipLog == null || point == null)
            return 0f;

        var distanceYards = (float)MeasureStats.Approximation.HaversineDistanceYards(shipLog.position, point);
        return EvaluatePowerContribution(shipLog.EvaluateGeneralScore(), distanceYards);
    }

    public static float EvaluatePowerContribution(float score, float distanceYards)
    {
        return score * EvaluateDistanceAttenuation(distanceYards);
    }

    public static float EvaluatePowerContribution(float score, float distanceYards, InfluenceMapRequest request)
    {
        return score * EvaluateDistanceAttenuation(distanceYards, request);
    }

    public static float EvaluateFirepowerContribution(ShipLog shipLog, LatLon point)
    {
        if (shipLog == null || point == null)
            return 0f;

        var distanceYards = (float)MeasureStats.Approximation.HaversineDistanceYards(shipLog.position, point);
        var initialBearingDeg = (float)MeasureStats.Approximation.CalculateInitialBearing(
            shipLog.position.LatDeg, shipLog.position.LonDeg, point.LatDeg, point.LonDeg
        );
        var relativeBearingDeg = MeasureUtils.GetPositiveAngleDifference(initialBearingDeg, shipLog.headingDeg);
        return EvaluateDisplayedFirepowerContribution(shipLog, distanceYards, relativeBearingDeg);
    }

    public static float EvaluateFirepowerContribution(float smoothedFirepower, float distanceYards)
    {
        return smoothedFirepower * EvaluateDistanceAttenuation(distanceYards);
    }

    public static float EvaluateFirepowerContribution(float smoothedFirepower, float distanceYards, InfluenceMapRequest request)
    {
        return smoothedFirepower * EvaluateDistanceAttenuation(distanceYards, request);
    }

    public static float EvaluateDisplayedFirepowerContribution(ShipLog shipLog, float distanceYards, float relativeBearingDeg)
    {
        if (shipLog == null)
            return 0f;

        var batteryFirepower = shipLog.EvaluateBatteryFirepowerScore(distanceYards, TargetAspect.Broad, 0f, relativeBearingDeg);
        var rapidFirepower = shipLog.EvaluateRapidFiringFirepowerScore(distanceYards, relativeBearingDeg);
        var torpedoThreat = shipLog.EvaluateTorpedoThreatScore(distanceYards, relativeBearingDeg);
        return batteryFirepower + rapidFirepower + torpedoThreat;
    }

    public static float ComposeValue(InfluenceMapType mapType, float group1Power, float group1Firepower, float group2Power)
    {
        return mapType switch
        {
            InfluenceMapType.Power => group1Power,
            InfluenceMapType.Firepower => group1Firepower,
            InfluenceMapType.Control => group1Power - group2Power,
            _ => 0f,
        };
    }

    public static float EvaluateSmoothedFirepower(float bowFirepower, float starboardFirepower, float sternFirepower, float portFirepower, float relativeBearingDeg)
    {
        var angle = MeasureUtils.NormalizeAngle(relativeBearingDeg);
        if (angle <= 90f)
        {
            return bowFirepower * (90f - angle) / 90f + starboardFirepower * angle / 90f;
        }

        if (angle < 180f)
        {
            return starboardFirepower * (180f - angle) / 90f + sternFirepower * (angle - 90f) / 90f;
        }

        if (angle < 270f)
        {
            return sternFirepower * (270f - angle) / 90f + portFirepower * (angle - 180f) / 90f;
        }

        return portFirepower * (360f - angle) / 90f + bowFirepower * (angle - 270f) / 90f;
    }

    public static InfluenceMapFieldData BuildField(InfluenceMapBounds bounds, InfluenceMapRequest request, IReadOnlyList<ShipLog> group1Ships, IReadOnlyList<ShipLog> group2Ships, int width = SampleWidth, int height = SampleHeight)
    {
        var values = new float[width, height];
        var field = new InfluenceMapFieldData
        {
            bounds = bounds,
            values = values,
            width = width,
            height = height,
        };

        var projection = new MeasureUtils.LocalProjection(
            (bounds.minLat + bounds.maxLat) * 0.5f,
            (bounds.minLon + bounds.maxLon) * 0.5f
        );
        var xCoords = BuildSampleXCoordinates(bounds, projection, width);
        var yCoords = BuildSampleYCoordinates(bounds, projection, height);
        var group1Samples = BuildShipFieldSamples(group1Ships, projection);
        var group2Samples = request.mapType == InfluenceMapType.Control
            ? BuildShipFieldSamples(group2Ships, projection)
            : null;
        var cutoffDistanceYards = request.mapType == InfluenceMapType.Firepower
            ? GetEffectiveFirepowerCutoffDistanceYards(bounds, group1Ships)
            : GetEffectiveCutoffDistanceYards(request, bounds);
        var cutoffDistanceSquaredYards = cutoffDistanceYards <= 0f ? 0f : Square(cutoffDistanceYards);
        var maxAbs = 0f;
        for (var y = 0; y < height; y++)
        {
            var pointY = yCoords[y];
            for (var x = 0; x < width; x++)
            {
                var pointX = xCoords[x];
                var value = request.mapType switch
                {
                    InfluenceMapType.Power => EvaluatePowerAtPoint(group1Samples, pointX, pointY, request, cutoffDistanceSquaredYards),
                    InfluenceMapType.Firepower => EvaluateFirepowerAtPoint(group1Samples, pointX, pointY, request, cutoffDistanceSquaredYards),
                    InfluenceMapType.Control => EvaluatePowerAtPoint(group1Samples, pointX, pointY, request, cutoffDistanceSquaredYards)
                        - EvaluatePowerAtPoint(group2Samples, pointX, pointY, request, cutoffDistanceSquaredYards),
                    _ => 0f,
                };

                values[x, y] = value;
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(value));
            }
        }

        field.maxAbs = maxAbs;
        return field;
    }

    public static float EvaluatePowerAtPoint(IEnumerable<ShipLog> ships, LatLon point)
    {
        if (ships == null)
            return 0f;

        var value = 0f;
        foreach (var ship in ships)
        {
            value += EvaluatePowerContribution(ship, point);
        }
        return value;
    }

    public static float EvaluateFirepowerAtPoint(IEnumerable<ShipLog> ships, LatLon point)
    {
        if (ships == null)
            return 0f;

        var value = 0f;
        foreach (var ship in ships)
        {
            value += EvaluateFirepowerContribution(ship, point);
        }
        return value;
    }

    public static List<float> BuildContourLevels(float maxAbs)
    {
        if (maxAbs <= 0f)
            return new List<float>();

        return new List<float>
        {
            -1f * maxAbs,
            -0.75f * maxAbs,
            -0.5f * maxAbs,
            -0.25f * maxAbs,
            0f,
            0.25f * maxAbs,
            0.5f * maxAbs,
            0.75f * maxAbs,
            1f * maxAbs,
        };
    }

    public static int GetFillBandIndex(IReadOnlyList<float> levels, float value)
    {
        if (levels == null || levels.Count < 2)
            return -1;

        if (value <= levels[0])
            return 0;

        for (var i = 0; i < levels.Count - 1; i++)
        {
            if (value < levels[i + 1])
                return i;
        }

        return levels.Count - 2;
    }

    public static List<InfluenceMapContourPolyline> BuildContourPolylines(InfluenceMapFieldData field, float level)
    {
        var segments = BuildContourSegments(field, level);
        return StitchSegments(level, segments);
    }

    public static Color GetContourColor(float level, float maxAbs)
    {
        if (Mathf.Approximately(level, 0f) || maxAbs <= 0f)
            return new Color(0.72f, 0.72f, 0.72f, 1f);

        var t = Mathf.Clamp01(Mathf.Abs(level) / maxAbs);
        if (level > 0f)
            return Color.Lerp(new Color(0.48f, 0.76f, 1f, 1f), new Color(0.03f, 0.19f, 0.78f, 1f), t);

        return Color.Lerp(new Color(1f, 0.58f, 0.58f, 1f), new Color(0.73f, 0.07f, 0.07f, 1f), t);
    }

    public static string FormatContourLabel(float level)
    {
        if (Mathf.Abs(level) < 0.005f)
            return "0";
        return level.ToString("0.##");
    }

    public static LatLon GetLabelPosition(InfluenceMapContourPolyline polyline)
    {
        if (polyline == null || polyline.points == null || polyline.points.Count == 0)
            return null;

        if (polyline.points.Count == 1)
            return polyline.points[0];

        var segmentLengths = new List<float>(polyline.points.Count - 1);
        var totalLength = 0f;
        for (var i = 1; i < polyline.points.Count; i++)
        {
            var segmentLength = ApproximateDistanceYards(polyline.points[i - 1], polyline.points[i]);
            segmentLengths.Add(segmentLength);
            totalLength += segmentLength;
        }

        if (totalLength <= 0f)
            return polyline.points[polyline.points.Count / 2];

        var targetLength = totalLength * 0.5f;
        var accumulated = 0f;
        for (var i = 1; i < polyline.points.Count; i++)
        {
            var segmentLength = segmentLengths[i - 1];
            if (accumulated + segmentLength >= targetLength)
            {
                var t = segmentLength <= 0f ? 0f : (targetLength - accumulated) / segmentLength;
                return new LatLon(
                    Mathf.Lerp(polyline.points[i - 1].LatDeg, polyline.points[i].LatDeg, t),
                    Mathf.Lerp(polyline.points[i - 1].LonDeg, polyline.points[i].LonDeg, t)
                );
            }

            accumulated += segmentLength;
        }

        return polyline.points[polyline.points.Count / 2];
    }

    static float[] BuildSampleXCoordinates(InfluenceMapBounds bounds, MeasureUtils.LocalProjection projection, int width)
    {
        var coords = new float[width];
        for (var x = 0; x < width; x++)
        {
            var x01 = width <= 1 ? 0f : x / (float)(width - 1);
            coords[x] = projection.LongitudeToX(Mathf.Lerp(bounds.minLon, bounds.maxLon, x01));
        }

        return coords;
    }

    static float[] BuildSampleYCoordinates(InfluenceMapBounds bounds, MeasureUtils.LocalProjection projection, int height)
    {
        var coords = new float[height];
        for (var y = 0; y < height; y++)
        {
            var y01 = height <= 1 ? 0f : y / (float)(height - 1);
            coords[y] = projection.LatitudeToY(Mathf.Lerp(bounds.minLat, bounds.maxLat, y01));
        }

        return coords;
    }

    static List<ShipFieldSample> BuildShipFieldSamples(IReadOnlyList<ShipLog> ships, MeasureUtils.LocalProjection projection)
    {
        var samples = new List<ShipFieldSample>(ships?.Count ?? 0);
        if (ships == null)
            return samples;

        foreach (var ship in ships)
        {
            if (ship == null)
                continue;

            samples.Add(new ShipFieldSample(ship, projection));
        }

        return samples;
    }

    public static float GetPrimaryDistanceParameterYards(InfluenceMapRequest request)
    {
        if (request == null)
            return InfluenceMapDefaults.LinearRangeYards;

        return request.falloffAlgorithm switch
        {
            InfluenceMapFalloffAlgorithm.Linear => request.linearRangeYards,
            InfluenceMapFalloffAlgorithm.Exponential => request.exponentialDecayLengthYards,
            InfluenceMapFalloffAlgorithm.Inverse => request.inverseHalfEffectDistanceYards,
            InfluenceMapFalloffAlgorithm.Gaussian => request.gaussianSigmaYards,
            _ => InfluenceMapDefaults.LinearRangeYards,
        };
    }

    public static int GetSampleWidth(InfluenceMapRequest request)
    {
        return Mathf.Clamp(request?.sampleWidth ?? InfluenceMapDefaults.SampleWidth, 8, 512);
    }

    public static int GetSampleHeight(InfluenceMapRequest request)
    {
        return Mathf.Clamp(request?.sampleHeight ?? InfluenceMapDefaults.SampleHeight, 8, 512);
    }

    public static float GetBoundsPaddingRatio(InfluenceMapRequest request)
    {
        return Mathf.Max(0f, request?.boundsPaddingRatio ?? InfluenceMapDefaults.BoundsPaddingRatio);
    }

    public static float GetMinBoundsPaddingDeg(InfluenceMapRequest request)
    {
        return Mathf.Max(0f, request?.minBoundsPaddingDeg ?? InfluenceMapDefaults.MinBoundsPaddingDeg);
    }

    public static float GetEffectiveCutoffDistanceYards(InfluenceMapRequest request, InfluenceMapBounds bounds)
    {
        var diagonalDistanceYards = ApproximateDistanceYards(
            new LatLon(bounds.minLat, bounds.minLon),
            new LatLon(bounds.maxLat, bounds.maxLon)
        );
        var parameterYards = Mathf.Max(1f, GetPrimaryDistanceParameterYards(request));
        var algorithmCutoffYards = (request?.falloffAlgorithm ?? InfluenceMapFalloffAlgorithm.Linear) switch
        {
            InfluenceMapFalloffAlgorithm.Linear => parameterYards,
            InfluenceMapFalloffAlgorithm.Exponential => parameterYards * 6f,
            InfluenceMapFalloffAlgorithm.Inverse => parameterYards * 64f,
            InfluenceMapFalloffAlgorithm.Gaussian => parameterYards * 4f,
            _ => diagonalDistanceYards,
        };
        return Mathf.Min(diagonalDistanceYards, algorithmCutoffYards);
    }

    static float GetEffectiveFirepowerCutoffDistanceYards(InfluenceMapBounds bounds, IReadOnlyList<ShipLog> ships)
    {
        var diagonalDistanceYards = ApproximateDistanceYards(
            new LatLon(bounds.minLat, bounds.minLon),
            new LatLon(bounds.maxLat, bounds.maxLon)
        );
        var maxWeaponRangeYards = ships?
            .Where(ship => ship != null)
            .Select(GetDisplayedFirepowerCutoffDistanceYards)
            .DefaultIfEmpty(0f)
            .Max() ?? 0f;
        return Mathf.Min(diagonalDistanceYards, maxWeaponRangeYards <= 0f ? diagonalDistanceYards : maxWeaponRangeYards);
    }

    static float GetDisplayedFirepowerCutoffDistanceYards(ShipLog shipLog)
    {
        if (shipLog?.shipClass == null)
            return 0f;

        var batteryRange = shipLog.shipClass.batteryRecords.Select(record => record?.rangeYards ?? 0f).DefaultIfEmpty(0f).Max();
        var rapidRange = shipLog.shipClass.rapidFireBatteryRecords.Select(record => record?.maxRangeYards ?? 0f).DefaultIfEmpty(0f).Max();
        var torpedoRange = shipLog.shipClass.torpedoSector?.torpedoSettings.Select(setting => setting?.rangeYards ?? 0f).DefaultIfEmpty(0f).Max() ?? 0f;
        return Mathf.Max(batteryRange, rapidRange, torpedoRange);
    }

    static float EvaluatePowerAtPoint(IReadOnlyList<ShipFieldSample> ships, float pointX, float pointY, InfluenceMapRequest request, float cutoffDistanceSquaredYards)
    {
        if (ships == null)
            return 0f;

        var value = 0f;
        for (var i = 0; i < ships.Count; i++)
        {
            var sample = ships[i];
            var distanceSquaredYards = Square(pointX - sample.xYards) + Square(pointY - sample.yYards);
            if (distanceSquaredYards >= cutoffDistanceSquaredYards)
                continue;

            var distanceYards = Mathf.Sqrt(distanceSquaredYards);
            value += EvaluatePowerContribution(sample.generalScore, distanceYards, request);
        }

        return value;
    }

    static float EvaluateFirepowerAtPoint(IReadOnlyList<ShipFieldSample> ships, float pointX, float pointY, InfluenceMapRequest request, float cutoffDistanceSquaredYards)
    {
        if (ships == null)
            return 0f;

        var value = 0f;
        for (var i = 0; i < ships.Count; i++)
        {
            var sample = ships[i];
            var dx = pointX - sample.xYards;
            var dy = pointY - sample.yYards;
            var distanceSquaredYards = Square(dx) + Square(dy);
            if (distanceSquaredYards >= cutoffDistanceSquaredYards)
                continue;

            var distanceYards = Mathf.Sqrt(distanceSquaredYards);
            var initialBearingDeg = MeasureUtils.NormalizeAngle(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
            var relativeBearingDeg = MeasureUtils.NormalizeAngle(initialBearingDeg - sample.headingDeg);
            value += EvaluateDisplayedFirepowerContribution(sample.shipLog, distanceYards, relativeBearingDeg);
        }

        return value;
    }

    static float ApproximateDistanceYards(LatLon a, LatLon b)
    {
        return MeasureUtils.ApproximateDistanceYards(a, b);
    }

    static float Square(float value)
    {
        return value * value;
    }

    static List<ContourSegment> BuildContourSegments(InfluenceMapFieldData field, float level)
    {
        var segments = new List<ContourSegment>();
        var values = field.values;
        for (var y = 0; y < field.height - 1; y++)
        {
            var y01 = y / (float)(field.height - 1);
            var yTop01 = (y + 1) / (float)(field.height - 1);
            for (var x = 0; x < field.width - 1; x++)
            {
                var x01 = x / (float)(field.width - 1);
                var xRight01 = (x + 1) / (float)(field.width - 1);

                var p00 = field.bounds.Lerp(x01, y01);
                var p10 = field.bounds.Lerp(xRight01, y01);
                var p11 = field.bounds.Lerp(xRight01, yTop01);
                var p01 = field.bounds.Lerp(x01, yTop01);

                var v00 = values[x, y];
                var v10 = values[x + 1, y];
                var v11 = values[x + 1, y + 1];
                var v01 = values[x, y + 1];

                var index = 0;
                if (v00 >= level) index |= 1;
                if (v10 >= level) index |= 2;
                if (v11 >= level) index |= 4;
                if (v01 >= level) index |= 8;

                if (index == 0 || index == 15)
                    continue;

                var edgePoints = new LatLon[4];
                edgePoints[0] = InterpolatePoint(p00, p10, v00, v10, level);
                edgePoints[1] = InterpolatePoint(p10, p11, v10, v11, level);
                edgePoints[2] = InterpolatePoint(p01, p11, v01, v11, level);
                edgePoints[3] = InterpolatePoint(p00, p01, v00, v01, level);

                switch (index)
                {
                    case 1:
                    case 14:
                        AddSegment(segments, edgePoints[3], edgePoints[0]);
                        break;
                    case 2:
                    case 13:
                        AddSegment(segments, edgePoints[0], edgePoints[1]);
                        break;
                    case 3:
                    case 12:
                        AddSegment(segments, edgePoints[3], edgePoints[1]);
                        break;
                    case 4:
                    case 11:
                        AddSegment(segments, edgePoints[1], edgePoints[2]);
                        break;
                    case 5:
                        if (((v00 + v10 + v11 + v01) * 0.25f) >= level)
                        {
                            AddSegment(segments, edgePoints[3], edgePoints[2]);
                            AddSegment(segments, edgePoints[0], edgePoints[1]);
                        }
                        else
                        {
                            AddSegment(segments, edgePoints[3], edgePoints[0]);
                            AddSegment(segments, edgePoints[1], edgePoints[2]);
                        }
                        break;
                    case 6:
                    case 9:
                        AddSegment(segments, edgePoints[0], edgePoints[2]);
                        break;
                    case 7:
                    case 8:
                        AddSegment(segments, edgePoints[3], edgePoints[2]);
                        break;
                    case 10:
                        if (((v00 + v10 + v11 + v01) * 0.25f) >= level)
                        {
                            AddSegment(segments, edgePoints[3], edgePoints[0]);
                            AddSegment(segments, edgePoints[1], edgePoints[2]);
                        }
                        else
                        {
                            AddSegment(segments, edgePoints[3], edgePoints[2]);
                            AddSegment(segments, edgePoints[0], edgePoints[1]);
                        }
                        break;
                }
            }
        }

        return segments;
    }

    static LatLon InterpolatePoint(LatLon from, LatLon to, float fromValue, float toValue, float level)
    {
        var denom = toValue - fromValue;
        var t = Mathf.Approximately(denom, 0f) ? 0.5f : Mathf.Clamp01((level - fromValue) / denom);
        return new LatLon(
            Mathf.Lerp(from.LatDeg, to.LatDeg, t),
            Mathf.Lerp(from.LonDeg, to.LonDeg, t)
        );
    }

    static void AddSegment(List<ContourSegment> segments, LatLon start, LatLon end)
    {
        if (start == null || end == null)
            return;

        if (Mathf.Approximately(start.LatDeg, end.LatDeg) && Mathf.Approximately(start.LonDeg, end.LonDeg))
            return;

        segments.Add(new ContourSegment(start, end));
    }

    static List<InfluenceMapContourPolyline> StitchSegments(float level, List<ContourSegment> segments)
    {
        var polylines = new List<InfluenceMapContourPolyline>();
        var remaining = new List<ContourSegment>(segments);

        while (remaining.Count > 0)
        {
            var seed = remaining[remaining.Count - 1];
            remaining.RemoveAt(remaining.Count - 1);

            var points = new List<LatLon> { seed.start, seed.end };
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = remaining.Count - 1; i >= 0; i--)
                {
                    var segment = remaining[i];
                    if (TryAppend(points, segment))
                    {
                        remaining.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            polylines.Add(new InfluenceMapContourPolyline
            {
                level = level,
                points = points,
            });
        }

        return polylines;
    }

    static bool TryAppend(List<LatLon> points, ContourSegment segment)
    {
        var head = points[0];
        var tail = points[points.Count - 1];

        if (ApproximatelySame(head, segment.start))
        {
            points.Insert(0, segment.end);
            return true;
        }

        if (ApproximatelySame(head, segment.end))
        {
            points.Insert(0, segment.start);
            return true;
        }

        if (ApproximatelySame(tail, segment.start))
        {
            points.Add(segment.end);
            return true;
        }

        if (ApproximatelySame(tail, segment.end))
        {
            points.Add(segment.start);
            return true;
        }

        return false;
    }

    static bool ApproximatelySame(LatLon left, LatLon right)
    {
        if (left == null || right == null)
            return false;

        return Mathf.Abs(left.LatDeg - right.LatDeg) <= 0.0001f &&
            Mathf.Abs(left.LonDeg - right.LonDeg) <= 0.0001f;
    }
}
