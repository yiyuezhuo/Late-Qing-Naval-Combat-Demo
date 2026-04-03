using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using StrategicCombatCore;
using Unity.Properties;
using UnityEngine;
using YYZ.PathFinding;

public enum StrategicInfluenceMapType
{
    Power,
    Control,
}

public static class StrategicInfluenceMapDefaults
{
    public const float LinearRangeCost = 240f;
    public const float ExponentialDecayLengthCost = 120f;
    public const float InverseHalfEffectDistanceCost = 120f;
    public const float GaussianSigmaCost = 120f;
}

public sealed class StrategicInfluenceMapRequest
{
    public StrategicInfluenceMapType mapType;
    public bool forceRefresh = true;
    public InfluenceMapFalloffAlgorithm falloffAlgorithm = InfluenceMapFalloffAlgorithm.Linear;
    public string side1ObjectId;
    public string side2ObjectId;
    public float linearRangeCost = StrategicInfluenceMapDefaults.LinearRangeCost;
    public float exponentialDecayLengthCost = StrategicInfluenceMapDefaults.ExponentialDecayLengthCost;
    public float inverseHalfEffectDistanceCost = StrategicInfluenceMapDefaults.InverseHalfEffectDistanceCost;
    public float gaussianSigmaCost = StrategicInfluenceMapDefaults.GaussianSigmaCost;
}

public sealed class StrategicInfluenceMapDialogModel
{
    [CreateProperty]
    public int mapTypeValue { get; set; } = (int)StrategicInfluenceMapType.Power;

    [CreateProperty]
    public bool forceRefresh { get; set; } = true;

    [CreateProperty]
    public int falloffAlgorithmValue { get; set; } = (int)InfluenceMapFalloffAlgorithm.Linear;

    [CreateProperty]
    public float linearRangeCost { get; set; } = StrategicInfluenceMapDefaults.LinearRangeCost;

    [CreateProperty]
    public float exponentialDecayLengthCost { get; set; } = StrategicInfluenceMapDefaults.ExponentialDecayLengthCost;

    [CreateProperty]
    public float inverseHalfEffectDistanceCost { get; set; } = StrategicInfluenceMapDefaults.InverseHalfEffectDistanceCost;

    [CreateProperty]
    public float gaussianSigmaCost { get; set; } = StrategicInfluenceMapDefaults.GaussianSigmaCost;

    public string side1ObjectId;
    public string side2ObjectId;

    public StrategicInfluenceMapType mapType => (StrategicInfluenceMapType)mapTypeValue;
    public InfluenceMapFalloffAlgorithm falloffAlgorithm => (InfluenceMapFalloffAlgorithm)falloffAlgorithmValue;
}

public sealed class StrategicInfluenceMapFieldData
{
    public readonly Dictionary<(int x, int y), float> gridValues = new();
    public readonly Dictionary<string, float> areaValues = new();
    public float maxAbs;
}

public sealed class StrategicInfluenceMovementGraph : IGraphEnumerable<Cell>
{
    readonly StrategicGameState state;

    public StrategicInfluenceMovementGraph(StrategicGameState state)
    {
        this.state = state;
    }

    public IEnumerable<Cell> Nodes()
    {
        if (state == null)
            yield break;

        foreach (var cell in state.IterCells())
        {
            if (cell != null && cell.IsArmyPassable())
                yield return cell;
        }
    }

    public IEnumerable<Cell> Neighbors(Cell pos)
    {
        if (pos == null)
            yield break;

        foreach (var neighbor in pos.GetNeighbors())
        {
            if (neighbor != null && neighbor.IsArmyPassable())
                yield return neighbor;
        }
    }

    public float EstimateCost(Cell src, Cell dst)
    {
        if (src == null || dst == null)
            return 0f;

        if (src.IsGridCell() && dst.IsGridCell())
            return (Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y)) * 25f;

        return 0f;
    }

    public float MoveCost(Cell src, Cell dst)
    {
        if (src == null || dst == null)
            return float.PositiveInfinity;

        var distanceKm = src.GetDistanceUnsafe(dst);
        var speedKmPerHour = Mathf.Max(0.01f, StrategicGroup.GetSpeedKmPerHour(src, dst));
        return distanceKm / speedKmPerHour;
    }
}

public static class StrategicInfluenceMapUtility
{
    sealed class SourceSample
    {
        public Cell cell;
        public float lethality;
    }

    public static List<SideState> GetAvailableSides(StrategicGameState state)
    {
        return state?.sideStates?.Where(side => side != null).ToList() ?? new List<SideState>();
    }

    public static string GetDefaultSide1ObjectId(StrategicGameState state, string preferredSideObjectId)
    {
        var sides = GetAvailableSides(state);
        if (!string.IsNullOrWhiteSpace(preferredSideObjectId) &&
            sides.Any(side => side.objectId == preferredSideObjectId))
        {
            return preferredSideObjectId;
        }

        return sides.FirstOrDefault()?.objectId;
    }

    public static string GetDefaultSide2ObjectId(StrategicGameState state, string side1ObjectId)
    {
        return GetAvailableSides(state).FirstOrDefault(side => side.objectId != side1ObjectId)?.objectId ?? side1ObjectId;
    }

    public static float GetPrimaryDistanceParameter(StrategicInfluenceMapRequest request)
    {
        if (request == null)
            return StrategicInfluenceMapDefaults.LinearRangeCost;

        return request.falloffAlgorithm switch
        {
            InfluenceMapFalloffAlgorithm.Linear => request.linearRangeCost,
            InfluenceMapFalloffAlgorithm.Exponential => request.exponentialDecayLengthCost,
            InfluenceMapFalloffAlgorithm.Inverse => request.inverseHalfEffectDistanceCost,
            InfluenceMapFalloffAlgorithm.Gaussian => request.gaussianSigmaCost,
            _ => StrategicInfluenceMapDefaults.LinearRangeCost,
        };
    }

    public static float EvaluateDistanceAttenuation(float distanceCost, StrategicInfluenceMapRequest request)
    {
        return EvaluateDistanceAttenuation(
            distanceCost,
            request?.falloffAlgorithm ?? InfluenceMapFalloffAlgorithm.Linear,
            GetPrimaryDistanceParameter(request)
        );
    }

    public static float EvaluateDistanceAttenuation(float distanceCost, InfluenceMapFalloffAlgorithm algorithm, float parameter)
    {
        var safeParameter = Mathf.Max(0.01f, parameter);
        return algorithm switch
        {
            InfluenceMapFalloffAlgorithm.Linear => Mathf.Max(0f, (safeParameter - distanceCost) / safeParameter),
            InfluenceMapFalloffAlgorithm.Exponential => Mathf.Exp(-distanceCost / safeParameter),
            InfluenceMapFalloffAlgorithm.Inverse => safeParameter / (safeParameter + Mathf.Max(0f, distanceCost)),
            InfluenceMapFalloffAlgorithm.Gaussian => Mathf.Exp(-0.5f * Mathf.Pow(distanceCost / safeParameter, 2f)),
            _ => 0f
        };
    }

    public static float GetEffectiveCutoffCost(StrategicInfluenceMapRequest request)
    {
        var parameter = Mathf.Max(0.01f, GetPrimaryDistanceParameter(request));
        return (request?.falloffAlgorithm ?? InfluenceMapFalloffAlgorithm.Linear) switch
        {
            InfluenceMapFalloffAlgorithm.Linear => parameter,
            InfluenceMapFalloffAlgorithm.Exponential => parameter * 6f,
            InfluenceMapFalloffAlgorithm.Inverse => parameter * 64f,
            InfluenceMapFalloffAlgorithm.Gaussian => parameter * 4f,
            _ => float.PositiveInfinity
        };
    }

    public static string FormatValue(float value)
    {
        return Mathf.Abs(value) < 0.05f ? "0" : value.ToString("0.#");
    }

    public static Color GetValueColor(float value, float maxAbs)
    {
        return InfluenceMapUtility.GetContourColor(value, maxAbs);
    }

    public static StrategicInfluenceMapRequest BuildPowerRequest(StrategicGameState state, string sideObjectId)
    {
        var scenarioState = state?.scenarioState;
        return new StrategicInfluenceMapRequest
        {
            mapType = StrategicInfluenceMapType.Power,
            forceRefresh = true,
            side1ObjectId = sideObjectId,
            falloffAlgorithm = scenarioState?.powerInfluenceFalloffAlgorithm ?? InfluenceMapFalloffAlgorithm.Linear,
            linearRangeCost = scenarioState?.powerInfluenceLinearRangeCost ?? StrategicInfluenceMapDefaults.LinearRangeCost,
            exponentialDecayLengthCost = scenarioState?.powerInfluenceExponentialDecayLengthCost ?? StrategicInfluenceMapDefaults.ExponentialDecayLengthCost,
            inverseHalfEffectDistanceCost = scenarioState?.powerInfluenceInverseHalfEffectDistanceCost ?? StrategicInfluenceMapDefaults.InverseHalfEffectDistanceCost,
            gaussianSigmaCost = scenarioState?.powerInfluenceGaussianSigmaCost ?? StrategicInfluenceMapDefaults.GaussianSigmaCost
        };
    }

    public static StrategicInfluenceMapFieldData BuildPowerField(StrategicGameState state, string sideObjectId)
    {
        return BuildField(state, BuildPowerRequest(state, sideObjectId));
    }

    public static StrategicPowerInfluenceMapCache BuildPowerCache(StrategicGameState state, SideState side)
    {
        if (state == null || side == null)
            return new StrategicPowerInfluenceMapCache();

        return StrategicPowerInfluenceMapCache.FromFieldData(
            BuildPowerField(state, side.objectId),
            StrategicPowerInfluenceParameterSnapshot.FromScenarioState(state.scenarioState),
            DateTime.UtcNow
        );
    }

    public static bool HasValidPowerCache(SideState side, StrategicScenarioState scenarioState)
    {
        return side?.powerInfluenceMapCache != null && side.powerInfluenceMapCache.Matches(scenarioState);
    }

    public static bool TryBuildFieldFromValidPowerCache(SideState side, StrategicScenarioState scenarioState, out StrategicInfluenceMapFieldData field)
    {
        if (HasValidPowerCache(side, scenarioState))
        {
            field = side.powerInfluenceMapCache.ToFieldData();
            return true;
        }

        field = null;
        return false;
    }

    public static float GetValueAtCell(StrategicInfluenceMapFieldData field, Cell cell)
    {
        return GetCellValue(field, cell);
    }

    public static StrategicInfluenceMapFieldData BuildField(StrategicGameState state, StrategicInfluenceMapRequest request)
    {
        var field = new StrategicInfluenceMapFieldData();
        if (state == null || request == null)
            return field;

        var side1 = EntityManager.Instance.Get<SideState>(request.side1ObjectId);
        var side2 = EntityManager.Instance.Get<SideState>(request.side2ObjectId);
        var graph = new StrategicInfluenceMovementGraph(state);

        var displayCells = new HashSet<Cell>();
        if (state.scenarioState.enableGridSystem && state.cellMatrix != null)
        {
            var width = state.GetMapWidth();
            var height = state.GetMapHeight();
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    displayCells.Add(state.cellMatrix[x, y]);
                }
            }
        }

        var side1Samples = BuildSourceSamples(state, side1);
        if (request.mapType == StrategicInfluenceMapType.Power && side1Samples.Count == 0)
            return field;

        AccumulateField(field, graph, displayCells, side1Samples, request, 1f);

        if (request.mapType == StrategicInfluenceMapType.Control)
        {
            var side2Samples = BuildSourceSamples(state, side2);
            if (side1Samples.Count == 0 && side2Samples.Count == 0)
                return field;

            AccumulateField(field, graph, displayCells, side2Samples, request, -1f);
        }

        return field;
    }

    static List<SourceSample> BuildSourceSamples(StrategicGameState state, SideState side)
    {
        var samples = new List<SourceSample>();
        if (state == null || side == null)
            return samples;

        foreach (var group in state.IterIndependentStrategicGroups())
        {
            if (group == null || !group.LandCombatable() || group.side != side)
                continue;

            foreach (var landUnit in group.WalkGroupMembers<LandUnit>())
            {
                var template = landUnit?.GetLandUnitTemplate();
                var cell = landUnit?.cell;
                if (landUnit == null || template == null || cell == null)
                    continue;

                if (landUnit.strength <= 0)
                    continue;

                if (template.unitType == LandUnitType.Supply || template.unitType == LandUnitType.Port)
                    continue;

                var lethality = landUnit.GetLethality();
                if (lethality <= 0f)
                    continue;

                samples.Add(new SourceSample
                {
                    cell = cell,
                    lethality = lethality
                });
            }
        }

        return samples;
    }

    static void AccumulateField(
        StrategicInfluenceMapFieldData field,
        StrategicInfluenceMovementGraph graph,
        HashSet<Cell> displayCells,
        List<SourceSample> samples,
        StrategicInfluenceMapRequest request,
        float sign)
    {
        if (field == null || graph == null || displayCells == null || samples == null || request == null)
            return;

        var samplesByCell = samples
            .Where(sample => sample?.cell != null)
            .GroupBy(sample => sample.cell)
            .ToDictionary(group => group.Key, group => group.Sum(sample => sample.lethality));
        var cutoffCost = GetEffectiveCutoffCost(request);

        foreach (var (sourceCell, totalLethality) in samplesByCell)
        {
            var dijkstra = PathFinding<Cell>.Dijkstra(
                graph,
                new[] { sourceCell },
                PathFinding<Cell>.DummyFalsePredicate,
                cutoffCost
            );

            foreach (var (targetCell, path) in dijkstra.nodeToPath)
            {
                if (targetCell == null)
                    continue;
                if (!displayCells.Contains(targetCell))
                    continue;

                var attenuation = EvaluateDistanceAttenuation(path.cost, request);
                if (attenuation <= 0f)
                    continue;

                var contribution = totalLethality * attenuation * sign;
                AddCellValue(field, targetCell, contribution);
            }
        }
    }

    static void SetCellValue(StrategicInfluenceMapFieldData field, Cell cell, float value)
    {
        if (cell == null)
            return;

        if (cell.IsAreaCell())
        {
            field.areaValues[cell.objectId] = value;
        }
        else
        {
            field.gridValues[(cell.x, cell.y)] = value;
        }
    }

    static void AddCellValue(StrategicInfluenceMapFieldData field, Cell cell, float delta)
    {
        if (cell == null)
            return;

        if (cell.IsAreaCell())
        {
            var value = field.areaValues.GetValueOrDefault(cell.objectId) + delta;
            field.areaValues[cell.objectId] = value;
            field.maxAbs = Mathf.Max(field.maxAbs, Mathf.Abs(value));
        }
        else
        {
            var key = (cell.x, cell.y);
            var value = field.gridValues.GetValueOrDefault(key) + delta;
            field.gridValues[key] = value;
            field.maxAbs = Mathf.Max(field.maxAbs, Mathf.Abs(value));
        }
    }

    static float GetCellValue(StrategicInfluenceMapFieldData field, Cell cell)
    {
        if (cell == null)
            return 0f;

        return cell.IsAreaCell()
            ? field.areaValues.GetValueOrDefault(cell.objectId)
            : field.gridValues.GetValueOrDefault((cell.x, cell.y));
    }
}
