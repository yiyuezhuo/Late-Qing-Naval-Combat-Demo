using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public partial class LocalNavalCombatBuilder
    {
        // required parameter
        public PendingNavalCombat pendingNavalCombat;

        List<ShipGroup> shipGroups = new();
        List<ShipLog> shipLogs = new();
        // Dictionary<StrategicGroup, ShipGroup> strateicGroupToShipGroup = new();
        // HashSet<string> objectIds = new();
        EntityManager localEntityManager = new();

        public List<ShipGroup> rootShipGroups = new();

        void Scan(ShipGroup parentShipGroup, StrategicGroup strategicGroup)
        {
            var shipGroup = new ShipGroup()
            {
                // objectId // resolved by EntityManager
                parentObjectId = parentShipGroup.objectId,
                name = strategicGroup.name.Clone(),
                leaderReference = new()
                {
                    referenceObjectId = strategicGroup.leaderReference.referenceObjectId
                }
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
                    shipLogCloned.ClearLogs(); // Detach old logs for sandboxing
                    
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
            var shipGroup = new ShipGroup() // Top Ship Group
            {
                // objectId // resolved by EntityManager
                parentObjectId = null,
                name = side.name.Add(fleetNameSuffix),
            };
            shipGroups.Add(shipGroup);
            rootShipGroups.Add(shipGroup);
            localEntityManager.Register(shipGroup, null);

            foreach (var strategicGroup in strategicGroups)
            {
                Scan(shipGroup, strategicGroup);
            }

            if(strategicGroups.Count > 0)
            {
                var combatShipTons = strategicGroups.Select(g => g.GetCombatShipTons()).ToList();
                var maxIdx = combatShipTons.IndexOf(combatShipTons.Max());
                var mostPowerfulgroup = strategicGroups[maxIdx];
                shipGroup.leaderReference.referenceObjectId = mostPowerfulgroup.leaderReference.referenceObjectId;
            }
        }

        // void ScanStrategicGroups(List<StrategicGroup> strategicGroups)
        // {
        //     var groupings = strategicGroups.GroupBy(g => g.side).ToList();

        //     foreach (var grouping in groupings)
        //     {
        //         StartScan(grouping.Key, grouping.ToList());
        //     }
        // }

        // public FullState BuildFullState(Cell cell)
        public FullState BuildFullState()
        {
            var gameState = StrategicGameState.Instance;

            // var strategicGroups = gameState.hexInfoMap.GetValueOrDefault((cell.x, cell.y))?.strategicGroupReferences?.Select(r => r.Get())?.ToList();
            // var strategicGroups = cell.StrategicGroupReferences?.Select(r => r.Get())?.ToList();
            // strategicGroups = strategicGroups ?? new List<StrategicGroup>();

            // ScanStrategicGroups(strategicGroups);
            StartScan(pendingNavalCombat.sideState0.side, pendingNavalCombat.sideState0.GetGroups());
            StartScan(pendingNavalCombat.sideState1.side, pendingNavalCombat.sideState1.GetGroups());

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
                    xRotation = pendingNavalCombat.cell.latitude,
                    yRotation = 360 - pendingNavalCombat.cell.longitude,
                    orthographicSize = 20
                }
            };
        }
    }
}