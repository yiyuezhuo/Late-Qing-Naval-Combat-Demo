using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public class LocalNavalCombatBuilder
    {
        List<ShipGroup> shipGroups = new();
        List<ShipLog> shipLogs = new();
        // Dictionary<StrategicGroup, ShipGroup> strateicGroupToShipGroup = new();
        // HashSet<string> objectIds = new();
        EntityManager localEntityManager = new();

        void Scan(ShipGroup parentShipGroup, StrategicGroup strategicGroup)
        {
            var shipGroup = new ShipGroup()
            {
                // objectId // resolved by EntityManager
                parentObjectId = parentShipGroup.objectId,
                name = strategicGroup.name.Clone(),
            };

            localEntityManager.Register(shipGroup, null);
            shipGroups.Add(shipGroup);

            shipGroup.parentObjectId = parentShipGroup.objectId;
            parentShipGroup.childrenObjectIds.Add(shipGroup.objectId);

            foreach (var subordinateRef in strategicGroup.subordinatesCombined)
            {
                var subordinate = subordinateRef.Get();
                var shipLog = subordinate as ShipLog;
                if (shipLog != null)
                {
                    var shipLogCloned = XmlUtils.FromXML<ShipLog>(XmlUtils.ToXML(shipLog));
                    
                    localEntityManager.Register(shipLogCloned, null);
                    shipLogs.Add(shipLogCloned);

                    shipLogCloned.parentObjectId = shipGroup.objectId;
                    shipGroup.childrenObjectIds.Add(shipLogCloned.objectId);
                    // TODO: Other setup for ShipLog in tactical?
                }
                var subStrategicGroup = subordinate as StrategicGroup;
                if (subStrategicGroup != null && subStrategicGroup.deployState == StrategicGroup.DeployState.Combined && subStrategicGroup.type == StrategicGroup.Type.Fleet)
                {
                    Scan(shipGroup, subStrategicGroup);
                }
            }
        }

        static GlobalString fleetNameSuffix = new()
        {
            english = " Fleet",
            japanese = "艦隊",
            chineseSimplified = "舰队",
            chineseTraditional = "艦隊"
        };

        void StartScan(SideState side, List<StrategicGroup> strategicGroups)
        {
            var shipGroup = new ShipGroup()
            {
                // objectId // resolved by EntityManager
                parentObjectId = null,
                name = side.name.Add(fleetNameSuffix),
            };
            shipGroups.Add(shipGroup);
            localEntityManager.Register(shipGroup, null);

            foreach (var strategicGroup in strategicGroups)
            {
                Scan(shipGroup, strategicGroup);
            }
        }

        void ScanStrategicGroups(List<StrategicGroup> strategicGroups)
        {
            var groupings = strategicGroups.GroupBy(g => g.side).ToList();
            // if (groupings.Count != 2)
            // {
            //     ServiceLocator.Get<ILoggerService>().LogWarning($"Side restriction check failed: grouping.Count={groupings.Count}");
            //     return null;
            // }
            foreach (var grouping in groupings)
            {
                StartScan(grouping.Key, grouping.ToList());
            }
            // shipGroups, shipLogs, localEntityManager should be collected.
            // return new NavalGameState()
            // {
            //     shipGroups = shipGroups,
            //     shipLogs = shipLogs,
            // };
        }

        public FullState BuildFullState(Cell cell)
        {
            var gameState = StrategicGameState.Instance;

            var strategicGroups = gameState.hexInfoMap.GetValueOrDefault((cell.x, cell.y))?.strategicGroupReferences?.Select(r => r.Get())?.ToList();
            strategicGroups = strategicGroups ?? new List<StrategicGroup>();

            // if (gameState.hexInfoMap.TryGetValue((cell.x, cell.y), out var cellInfo))
            // {
            //     var strategicGroups = cellInfo.strategicGroupReferences.Select(r => r.Get()).ToList();
            ScanStrategicGroups(strategicGroups);

            return new FullState()
            {
                streamingAssetReference = StreamingAssetReference.Instance, // Copy?
                navalGameState = new()
                {
                    shipGroups = shipGroups,
                    shipLogs = shipLogs,
                    scenarioState = new()
                    {
                        dateTime = gameState.scenarioState.dateTime // TODO: Add noise?
                    }
                },
                viewState = new()
                {
                    xRotation = cell.latitude,
                    yRotation = 360 - cell.longitude,
                    orthographicSize = 20
                }
            };
            // }
            // return null;
        }

        // public void TryGotoTacticalNavalCombat(Cell cell)
        // {
        //     var fullState = BuildFullState(cell);
        //     if (fullState != null)
        //     {
        //         GameManager.startupConfig = new()
        //         {
        //             fullState = fullState,
        //             mode = GameManager.StartupConfig.Mode.FullState
        //         };

        //         StrategicGameManager.Instance.PrepareReturnFromNavalGame();
        //         SceneManager.LoadScene("Naval Game");
        //     }
        // }

        // public void TryToSwitch(Cell cell)
        // {
        //     var gameState = StrategicGameState.Instance;
        //     if (gameState.hexInfoMap.TryGetValue((cell.x, cell.y), out var cellInfo))
        //     {
        //         var sideGroupGrouping = cellInfo.strategicGroupReferences.Select(r => r.Get()).GroupBy(g => g.side).ToList();
        //         if (sideGroupGrouping.Count != 2)
        //             return;

        //         var sideToShipLogs = sideGroupGrouping.ToDictionary(g => g.Key, g => g.Select(CollectShips).SelectMany(x => x).ToList());
        //         var shipLogs = sideToShipLogs.Values.SelectMany(x => x).ToList();


        //         var fullState = new FullState()
        //         {
        //             streamingAssetReference = StreamingAssetReference.Instance, // Copy?
        //             navalGameState = new()
        //             {
        //                 shipLogs = shipLogs,
        //                 scenarioState = new()
        //                 {
        //                     dateTime = gameState.scenarioState.dateTime // TODO: Add noise?
        //                 }
        //             },
        //             viewState = new()
        //             {
        //                 xRotation = cell.latitude,
        //                 yRotation = 360 - cell.longitude,
        //                 orthographicSize = 20
        //             }
        //         };

        //         GameManager.startupConfig = new()
        //         {
        //             fullState = fullState,
        //             mode = GameManager.StartupConfig.Mode.FullState
        //         };

        //         SceneManager.LoadScene("Naval Game");
        //     }
        // }

        // public static IEnumerable<ShipLog> CollectShips(StrategicGroup group)
        // {
        //     foreach (var subordinateRef in group.subordinatesCombined)
        //     {
        //         var subordinate = subordinateRef.Get();
        //         var shipLog = subordinate as ShipLog;
        //         if (shipLog != null)
        //         {
        //             yield return shipLog;
        //         }
        //         var subGroup = subordinate as StrategicGroup;
        //         if (subGroup != null && subGroup.deployState == StrategicGroup.DeployState.Combined)
        //         {
        //             foreach (var _shipLog in CollectShips(subGroup))
        //             {
        //                 yield return _shipLog;
        //             }
        //         }
        //     }
        // }
    }
}