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

        public enum EstimationCategory
        {
            Other,
            BB,
            CA,
            CL,
            TB,
            DD,
        }

        public class EstimationCategoryConfig
        {
            // public EsimationCategory category;
            public Dictionary<EstimationCategory, float> confusionMap;
            public List<ShipType> shipTypes = new();

            // Used to simplify sampling procedure
            public List<EstimationCategory> confusionCategories = new();
            public List<float> confusionWeights = new();
            public float powerPoint;
        }

        public class Rule
        {
            // Parameters
            public Dictionary<EstimationCategory, EstimationCategoryConfig> estimateConfigMap = new();

            // Derived
            // public Dictionary<ShipType, EstimationCategory> shipTypeToEsimationCategory = new();

            public void Setup()
            {
                foreach(var (estimateCategory, config) in estimateConfigMap)
                {
                    // foreach(var shipType in config.shipTypes)
                    // {
                    //     shipTypeToEsimationCategory[shipType] = estimateCategory;
                    // }
                    foreach(var (confusionCategory, confusionWeight) in config.confusionMap)
                    {
                        config.confusionCategories.Add(confusionCategory);
                        config.confusionWeights.Add(confusionWeight);
                    }
                }
            }

            public static Rule russoJapaneseWar = new()
            {
                estimateConfigMap = new()
                {
                    [EstimationCategory.BB] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.5f,
                            [EstimationCategory.CA] = 0.4f,
                            [EstimationCategory.CL] = 0.1f,
                        },
                        powerPoint = 15 // Assume 15000 tons
                    },
                    [EstimationCategory.CA] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.3f,
                            [EstimationCategory.CA] = 0.5f,
                            [EstimationCategory.CL] = 0.2f,
                        },
                        powerPoint = 9, // Assume 9000 tons
                    },
                    [EstimationCategory.CL] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.1f,
                            [EstimationCategory.CA] = 0.3f,
                            [EstimationCategory.CL] = 0.5f,
                            [EstimationCategory.TB] = 0.1f,
                            [EstimationCategory.DD] = 0.1f,
                        },
                        powerPoint = 5, // Assume 5000 tons
                    },
                    [EstimationCategory.TB] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.CL] = 0.05f,
                            [EstimationCategory.TB] = 0.5f,
                            [EstimationCategory.DD] = 0.35f,
                            [EstimationCategory.Other] = 0.15f
                        },
                        powerPoint = 1, // Assume <1000 tons
                    },
                    [EstimationCategory.DD] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.CL] = 0.05f,
                            [EstimationCategory.TB] = 0.35f,
                            [EstimationCategory.DD] = 0.5f,
                            [EstimationCategory.Other] = 0.15f
                        },
                        powerPoint = 1, // Assume <1000 tons
                    },
                    [EstimationCategory.Other] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.CL] = 0.05f,
                            [EstimationCategory.TB] = 0.35f,
                            [EstimationCategory.DD] = 0.5f,
                            [EstimationCategory.Other] = 0.15f
                        },
                        // shipTypes = new(){ShipType.Destroyer}
                        powerPoint = 2, // Assume 2000 tons (Though they're generally not considered to be "power")
                    }
                }
            };

            public static Rule sinoJapaneseWar = new()
            {
                estimateConfigMap = new()
                {
                    [EstimationCategory.BB] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.5f,
                            [EstimationCategory.CA] = 0.4f,
                            [EstimationCategory.CL] = 0.1f,
                        },
                        powerPoint = 8 // Assume 8000 tons
                    },
                    [EstimationCategory.CA] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.3f,
                            [EstimationCategory.CA] = 0.5f,
                            [EstimationCategory.CL] = 0.2f,
                        },
                        powerPoint = 4, // Assume 4000 tons
                    },
                    [EstimationCategory.CL] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.BB] = 0.1f,
                            [EstimationCategory.CA] = 0.3f,
                            [EstimationCategory.CL] = 0.5f,
                            [EstimationCategory.TB] = 0.1f,
                        },
                        powerPoint = 3, // Assume 3000 tons
                    },
                    [EstimationCategory.DD] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.DD] = 1f,
                        },
                        powerPoint = 1, // Assume <1000 tons
                    },
                    [EstimationCategory.TB] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.CL] = 0.05f,
                            [EstimationCategory.TB] = 0.5f,
                            [EstimationCategory.Other] = 0.15f
                        },
                        powerPoint = 1, // Assume <1000 tons
                    },
                    [EstimationCategory.Other] = new()
                    {
                        confusionMap = new()
                        {
                            [EstimationCategory.CL] = 0.05f,
                            [EstimationCategory.TB] = 0.35f,
                            [EstimationCategory.Other] = 0.15f
                        },
                        // shipTypes = new(){ShipType.Destroyer}
                        powerPoint = 2, // Assume 2000 tons (Though they're generally not considered to be "power")
                    },
                }
            };

        static Rule()
        {
            russoJapaneseWar.Setup();
            sinoJapaneseWar.Setup();
        }

        [XmlType("NavalForceEstimationRuleMode")]
        public enum Mode
        {
            RussoJapaneseWar,
            SinoJapaneseWar,
            }

            public static Rule Get(Mode mode)
            {
                return mode switch
                {
                    Mode.RussoJapaneseWar => russoJapaneseWar,
                    Mode.SinoJapaneseWar => sinoJapaneseWar,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                };
            }
        }

        public static Dictionary<ShipType, EstimationCategory> shipTypeToEsimationCategory = new()
        {
            [ShipType.Battleship] = EstimationCategory.BB,
            // [ShipType.Cruiser] = EstimationCategory.CA,
            [ShipType.ArmoredCruiser] = EstimationCategory.CA,
            [ShipType.LightCruiser] = EstimationCategory.CL,
            [ShipType.Destroyer] = EstimationCategory.DD,
            [ShipType.TorpedoBoat] = EstimationCategory.TB,
        };


        public static EstimationCategory GetEstimateCategory(ShipType shipType)
        {
            return shipTypeToEsimationCategory.GetValueOrDefault(shipType);
        }

        public class EsimationRecord
        {
            public EstimationCategory estimateCategory;
            public int count;
        }

        public List<EsimationRecord> records = new()
        {
            new(){estimateCategory = EstimationCategory.BB}, // Enforce order
            new(){estimateCategory = EstimationCategory.CA},
            new(){estimateCategory = EstimationCategory.CL},
            new(){estimateCategory = EstimationCategory.DD},
            new(){estimateCategory = EstimationCategory.TB},
            new(){estimateCategory = EstimationCategory.Other},
        };

        public string GetEstimatateSummary() => string.Join(",", records.Where(r => r.count > 0).Select(r => $"{r.estimateCategory}: {r.count}"));

        public override string ToString()
        {
            return $"NavalForceEstimation({GetEstimatateSummary()})";
        }


        public void AddOne(EstimationCategory estimateCategory)
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
            var scenarioState = StrategicGameState.Instance.scenarioState;

            var test = new NavalForceEstimation();
            foreach(var record in records)
            {
                // var config = esimateConfigMap[record.estimateCategory];
                if(record.count > 0) // Skip DD for Sino-Japanese War
                {
                    var config = scenarioState.GetEstimationCategoryConfig(record.estimateCategory);
                    for(int i=0; i<record.count; i++)
                    {
                        var newCategory = RandomUtils.Sample(config.confusionCategories, config.confusionWeights);
                        test.AddOne(newCategory);
                    }
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
            var scenarioState = StrategicGameState.Instance.scenarioState;

            var totalPowerPoint = 0f;
            foreach(var record in records)
            {
                // var config = esimateConfigMap[record.estimateCategory];
                var config = scenarioState.GetEstimationCategoryConfig(record.estimateCategory);
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

        public LazyLocalizedString ToLazyLocalizedString()
        {
            var dateTimeOffsetString = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(dateTime);

            return LazyLocalizedString.MakeTemplate(
                "Contact Report: {0}, {1}, {2} -> {3}: {4}",
                LazyLocalizedString.MakeRaw(dateTimeOffsetString),
                LazyLocalizedString.MakeGlobalStringShort(position.GetAreaCellNameGlobalString()),
                LazyLocalizedString.MakeGlobalStringShort(GetObserverSide().name),
                LazyLocalizedString.MakeGlobalStringShort(GetObservedSide().name),
                LazyLocalizedString.MakeRaw(estimation.GetEstimatateSummary())
            );
        }

        public TimeSpan GetTimeSpanToCurrent()
        {
            var timeSpan = StrategicGameState.Instance.scenarioState.dateTime - dateTime;
            return timeSpan;
        }

        public float GetHoursToCurrent()
        {
            var timeSpan = GetTimeSpanToCurrent();
            return (float)timeSpan.TotalHours;
        }

        public static float threatMaintainedDays = 4;
        public static float threatMaintainedHours = threatMaintainedDays * 24;
        
        public float GetTimelinessCoef()
        {
            var decayCoef = Math.Min(1, GetHoursToCurrent() / threatMaintainedHours);
            return 1 - decayCoef;
        }
        public float GetThreatScore()
        {
            var basePowerpoint = estimation.GetPowerPoint();
            return Math.Max(1, basePowerpoint * GetTimelinessCoef());
        }

        // public void Summary()
        // {
        //     var ds = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(dateTime);
        //     var es = estimation.GetEsimatateSummary();
        //     var ss = GetObservedSide().name.GetMergedName();
        // }
    }
}
