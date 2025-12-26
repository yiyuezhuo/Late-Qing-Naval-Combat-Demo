using System;
using System.Xml.Serialization;
using CoreUtils;
using StrategicCombatCore;
using System.Collections;
using System.Collections.Generic;
using NavalCombatCore;
using System.Linq;


namespace StrategicCombatCore
{

    public class NavalForceEstimation
    {

        public enum EsimationCategory
        {
            Other,
            BB,
            CA,
            CL,
            TB,
            DD,
        }

        public class EsimationCategoryConfig
        {
            // public EsimationCategory category;
            public Dictionary<EsimationCategory, float> confusionMap;
            public List<ShipType> shipTypes = new();

            // Use to simplify sample
            public List<EsimationCategory> confusionCategories = new();
            public List<float> confusionWeights = new();
            public float powerPoint;
        }

        public static Dictionary<EsimationCategory, EsimationCategoryConfig> esimateConfigMap = new()
        {
            [EsimationCategory.BB] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.BB] = 0.5f,
                    [EsimationCategory.CA] = 0.4f,
                    [EsimationCategory.CL] = 0.1f,
                },
                shipTypes = new(){ShipType.Battleship},
                powerPoint = 15 // Assume 15000 tons
            },
            [EsimationCategory.CA] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.BB] = 0.3f,
                    [EsimationCategory.CA] = 0.5f,
                    [EsimationCategory.CL] = 0.2f,
                },
                shipTypes = new(){ShipType.ArmoredCruiser, ShipType.Cruiser},
                powerPoint = 10, // Assume 10000 tons
            },
            [EsimationCategory.CL] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.BB] = 0.1f,
                    [EsimationCategory.CA] = 0.3f,
                    [EsimationCategory.CL] = 0.5f,
                    [EsimationCategory.TB] = 0.1f,
                    [EsimationCategory.DD] = 0.1f,
                },
                shipTypes = new(){ShipType.LightCruiser},
                powerPoint = 5, // Assume 5000 tons
            },
            [EsimationCategory.TB] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.CL] = 0.05f,
                    [EsimationCategory.TB] = 0.5f,
                    [EsimationCategory.DD] = 0.35f,
                    [EsimationCategory.Other] = 0.15f
                },
                shipTypes = new(){ShipType.TorpedoBoat},
                powerPoint = 1, // Assume <1000 tons
            },
            [EsimationCategory.DD] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.CL] = 0.05f,
                    [EsimationCategory.TB] = 0.35f,
                    [EsimationCategory.DD] = 0.5f,
                    [EsimationCategory.Other] = 0.15f
                },
                shipTypes = new(){ShipType.Destroyer},
                powerPoint = 1, // Assume <1000 tons
            },
            [EsimationCategory.Other] = new()
            {
                confusionMap = new()
                {
                    [EsimationCategory.CL] = 0.05f,
                    [EsimationCategory.TB] = 0.35f,
                    [EsimationCategory.DD] = 0.5f,
                    [EsimationCategory.Other] = 0.15f
                },
                // shipTypes = new(){ShipType.Destroyer}
                powerPoint = 2, // Assume 2000 tons (Though they're generally not considered to be "power")
            },
        };

        static Dictionary<ShipType, EsimationCategory> shipTypeToEsimationCategory = new();

        static NavalForceEstimation()
        {
            foreach(var (estimateCategory, config) in esimateConfigMap)
            {
                foreach(var shipType in config.shipTypes)
                {
                    shipTypeToEsimationCategory[shipType] = estimateCategory;
                }
                foreach(var (confusionCategory, confusionWeight) in config.confusionMap)
                {
                    config.confusionCategories.Add(confusionCategory);
                    config.confusionWeights.Add(confusionWeight);
                }
            }
        }

        public static EsimationCategory GetEstimateCategory(ShipType shipType)
        {
            return shipTypeToEsimationCategory.GetValueOrDefault(shipType);
        }

        public class EsimationRecord
        {
            public EsimationCategory estimateCategory;
            public int count;
        }

        public List<EsimationRecord> records = new()
        {
            new(){estimateCategory = EsimationCategory.BB}, // Enforce order
            new(){estimateCategory = EsimationCategory.CA},
            new(){estimateCategory = EsimationCategory.CL},
            new(){estimateCategory = EsimationCategory.DD},
            new(){estimateCategory = EsimationCategory.TB},
            new(){estimateCategory = EsimationCategory.Other},
        };

        public string GetEstimatateSummary() => string.Join(",", records.Where(r => r.count > 0).Select(r => $"{r.estimateCategory}: {r.count}"));

        public override string ToString()
        {
            return $"NavalForceEstimation({GetEstimatateSummary()})";
        }


        public void AddOne(EsimationCategory estimateCategory)
        {
            var record = records.FirstOrDefault(r => r.estimateCategory == estimateCategory);
            if(record == null)
            {
                record = new EsimationRecord()
                {
                    estimateCategory = estimateCategory,
                    count = 1,
                };
                records.Add(record);
            }
            else
            {
                record.count++;
            }
        }

        public void AddOne(ShipType shipType) => AddOne(GetEstimateCategory(shipType));
        public void AddOne(ShipLog shipLog) => AddOne(shipLog?.shipClass.type ?? ShipType.NotSpecified);

        public void AddByShipLogs(IEnumerable<ShipLog> shipLogs)
        {
            foreach(var shipLog in shipLogs)
            {
                AddOne(shipLog);
            }
        }

        public void ApplyQuantityNoise()
        {
            foreach(var record in records)
            {
                var count = record.count;
                for(int i=0; i<count; i++)
                {
                    var r = RandomUtils.NextFloat();
                    if(r < 0.33)
                    {
                        // recort.count--;
                        record.count = Math.Max(0, record.count - 1);
                    }
                    else if(r > 0.66)
                    {
                        record.count++;
                    }
                }
            }
        }

        public void ApplyConfusionMatrixNoise()
        {
            var test = new NavalForceEstimation();
            foreach(var record in records)
            {
                var config = esimateConfigMap[record.estimateCategory];
                for(int i=0; i<record.count; i++)
                {
                    var newCategory = RandomUtils.Sample(config.confusionCategories, config.confusionWeights);
                    test.AddOne(newCategory);
                }
            }
            records = test.records;
        }

        public void UpdateTo(IEnumerable<ShipLog> shipLogs)
        {
            // records.Clear();
            foreach(var r in records)
                r.count = 0;
            
            AddByShipLogs(shipLogs);
            ApplyQuantityNoise();
            ApplyConfusionMatrixNoise();
        }

        public float GetPowerPoint()
        {
            var totalPowerPoint = 0f;
            foreach(var record in records)
            {
                var config = esimateConfigMap[record.estimateCategory];
                totalPowerPoint += record.count * config.powerPoint;
            }
            return totalPowerPoint;
        }
    }

    public partial class NavalContactReport : IObjectIdLabeled
    {
        public string objectId{get;set;}
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public DateTime dateTime;
        public XY position;
        public string observerSideId;
        public string observedSideId;
        public NavalForceEstimation estimation = new();

        public SideState GetObserverSide() => EntityManager.Instance.Get<SideState>(observerSideId);
        public SideState GetObservedSide() => EntityManager.Instance.Get<SideState>(observedSideId);
        public Cell GetCell() => position.GetCell();

        public void UpdateTo(DateTime newDateTime, IEnumerable<ShipLog> shipLogs)
        {
            dateTime = newDateTime;
            estimation.UpdateTo(shipLogs);
        }

        public override string ToString()
        {
            return $"NavalContactReport({dateTime}, {position.GetAreaCellName()}, {GetObserverSide().name} => {GetObservedSide().name}, {estimation})";
        }

        public TimeSpan GetTimeSpanToCurrent()
        {
            var timeSpan = StrategicGameState.Instance.scenarioState.dateTime - dateTime;
            return timeSpan;
        }

        // public void Summary()
        // {
        //     var ds = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(dateTime);
        //     var es = estimation.GetEsimatateSummary();
        //     var ss = GetObservedSide().name.GetMergedName();
        // }
    }
}