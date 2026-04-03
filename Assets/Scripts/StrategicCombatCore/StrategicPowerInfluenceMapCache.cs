using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace StrategicCombatCore
{
    [Serializable]
    public class StrategicPowerInfluenceParameterSnapshot
    {
        public InfluenceMapFalloffAlgorithm falloffAlgorithm = InfluenceMapFalloffAlgorithm.Linear;
        public float linearRangeCost = StrategicInfluenceMapDefaults.LinearRangeCost;
        public float exponentialDecayLengthCost = StrategicInfluenceMapDefaults.ExponentialDecayLengthCost;
        public float inverseHalfEffectDistanceCost = StrategicInfluenceMapDefaults.InverseHalfEffectDistanceCost;
        public float gaussianSigmaCost = StrategicInfluenceMapDefaults.GaussianSigmaCost;

        public bool Matches(StrategicScenarioState scenarioState)
        {
            if (scenarioState == null)
                return false;

            return falloffAlgorithm == scenarioState.powerInfluenceFalloffAlgorithm &&
                   Math.Abs(linearRangeCost - scenarioState.powerInfluenceLinearRangeCost) < 0.001f &&
                   Math.Abs(exponentialDecayLengthCost - scenarioState.powerInfluenceExponentialDecayLengthCost) < 0.001f &&
                   Math.Abs(inverseHalfEffectDistanceCost - scenarioState.powerInfluenceInverseHalfEffectDistanceCost) < 0.001f &&
                   Math.Abs(gaussianSigmaCost - scenarioState.powerInfluenceGaussianSigmaCost) < 0.001f;
        }

        public static StrategicPowerInfluenceParameterSnapshot FromScenarioState(StrategicScenarioState scenarioState)
        {
            if (scenarioState == null)
            {
                return new StrategicPowerInfluenceParameterSnapshot();
            }

            return new StrategicPowerInfluenceParameterSnapshot
            {
                falloffAlgorithm = scenarioState.powerInfluenceFalloffAlgorithm,
                linearRangeCost = scenarioState.powerInfluenceLinearRangeCost,
                exponentialDecayLengthCost = scenarioState.powerInfluenceExponentialDecayLengthCost,
                inverseHalfEffectDistanceCost = scenarioState.powerInfluenceInverseHalfEffectDistanceCost,
                gaussianSigmaCost = scenarioState.powerInfluenceGaussianSigmaCost
            };
        }
    }

    [Serializable]
    public class StrategicPowerInfluenceGridValue
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public float value;
    }

    [Serializable]
    public class StrategicPowerInfluenceAreaValue
    {
        [XmlAttribute]
        public string areaCellObjectId;

        [XmlAttribute]
        public float value;
    }

    [Serializable]
    public class StrategicPowerInfluenceMapCache
    {
        public long generatedAtTicks;
        public StrategicPowerInfluenceParameterSnapshot parameterSnapshot = new();
        public float maxAbs;
        public List<StrategicPowerInfluenceGridValue> gridValues = new();
        public List<StrategicPowerInfluenceAreaValue> areaValues = new();

        public bool Matches(StrategicScenarioState scenarioState)
        {
            return generatedAtTicks > 0 &&
                   parameterSnapshot != null &&
                   parameterSnapshot.Matches(scenarioState);
        }

        public StrategicInfluenceMapFieldData ToFieldData()
        {
            var field = new StrategicInfluenceMapFieldData
            {
                maxAbs = maxAbs
            };

            foreach (var gridValue in gridValues ?? Enumerable.Empty<StrategicPowerInfluenceGridValue>())
            {
                if (gridValue == null)
                    continue;

                field.gridValues[(gridValue.x, gridValue.y)] = gridValue.value;
            }

            foreach (var areaValue in areaValues ?? Enumerable.Empty<StrategicPowerInfluenceAreaValue>())
            {
                if (areaValue == null || string.IsNullOrWhiteSpace(areaValue.areaCellObjectId))
                    continue;

                field.areaValues[areaValue.areaCellObjectId] = areaValue.value;
            }

            return field;
        }

        public static StrategicPowerInfluenceMapCache FromFieldData(
            StrategicInfluenceMapFieldData field,
            StrategicPowerInfluenceParameterSnapshot parameterSnapshot,
            DateTime generatedAtUtc)
        {
            field ??= new StrategicInfluenceMapFieldData();

            return new StrategicPowerInfluenceMapCache
            {
                generatedAtTicks = generatedAtUtc.Ticks,
                parameterSnapshot = parameterSnapshot ?? new StrategicPowerInfluenceParameterSnapshot(),
                maxAbs = field.maxAbs,
                gridValues = field.gridValues
                    .Select(pair => new StrategicPowerInfluenceGridValue
                    {
                        x = pair.Key.x,
                        y = pair.Key.y,
                        value = pair.Value
                    })
                    .ToList(),
                areaValues = field.areaValues
                    .Select(pair => new StrategicPowerInfluenceAreaValue
                    {
                        areaCellObjectId = pair.Key,
                        value = pair.Value
                    })
                    .ToList()
            };
        }
    }
}
