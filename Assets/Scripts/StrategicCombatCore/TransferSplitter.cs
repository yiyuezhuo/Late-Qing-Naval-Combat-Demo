using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public class TransferSplitter
    {
        // public List<ShipLog> transports;
        // public List<StrategicGroup> waitLoadGroups;

        List<ShipLog> ships;
        // List<StrategicGroup> containers,
        List<IStrategicGroupMemberReferenceable> building = new(); // LandUnit or StrategicGroup, for current ship only
        // ShipLog currentShip;
        double currentTransferableWeightTons;
        double currentCostTons;

        bool isEnd;

        static GlobalString loadedSuffix = new()
        {
            english = " loaded",
            japanese = " 積載",
            chineseSimplified = " 装载",
            chineseTraditional = " 装载",
        };

        void ResolveBuilding(bool endCurrentShip)
        {
            // Handle Established groups
            foreach(var establishedGroup in building.OfType<StrategicGroup>())
            {
                // establishedGroup.autoCombinable = true;
                // establishedGroup.deployState = StrategicGroup.DeployState.NotDeployed;
                // establishedGroup.containerObjectId = ships[0].objectId;
                establishedGroup.LoadToShip(ships[0]);
            }

            // Handle LandUnits
            foreach(var g in building.OfType<LandUnit>().GroupBy(u => u.parentGroupReference.Get()))
            {
                var originalParent = g.Key;
                var subLandUnits = g.ToList();

                var newDissolvableGroup = new StrategicGroup()
                {
                    name = ships[0]?.namedShip.name.Add(loadedSuffix),
                    // type = originalParent.type,
                    // size = originalParent.size,
                    type = StrategicGroup.Type.General, // replace it with a naval transfer type?
                    size = StrategicUnitSize.Unspecified,
                    country = originalParent.country,
                    // autoCombinable = true,
                    dissolvable = true,
                    plannedPath = originalParent.plannedPath?.Select(xy => xy?.Clone()).Where(xy => xy != null).ToList() ?? new(),
                    embarkingLandingPairs = originalParent.embarkingLandingPairs?.Select(pair => pair?.Clone()).Where(pair => pair != null).ToList() ?? new(),
                    moveProgressionKm = originalParent.moveProgressionKm,
                    // containerObjectId = ships[0].objectId,
                };
                StrategicGameState.Instance.strategicGroups.Add(newDissolvableGroup);
                EntityManager.Instance.Register(newDissolvableGroup, null);

                newDissolvableGroup.AttachTo(originalParent);
                newDissolvableGroup.deployState = StrategicGroup.DeployState.Combined;
                newDissolvableGroup.LoadToShip(ships[0]);

                foreach (var subLandUnit in subLandUnits)
                {
                    // TODO: Refactor
                    originalParent.TransferLandUnit(subLandUnit, newDissolvableGroup);
                }

            // originalParent.directMemberReferences.Add(new() { referenceId = newDissolvableGroup.objectId });
            }


            if (endCurrentShip)
            {
                ships.RemoveAt(0);

                if (ships.Count == 0)
                {
                    isEnd = true;
                    return;
                }

                currentTransferableWeightTons = ships[0].GetTransferableWeightTons();
            }
            else
            {
                currentTransferableWeightTons -= currentCostTons;
            }

            building.Clear();
            currentCostTons = 0;
        }

        public void SplitLoadWalk(StrategicGroup root)
        {
            foreach (var refItem in root.directMemberReferences.ToList()) // New subordinate may be added but would not be considered in the iteration.
            {
                if (isEnd)
                    return;

                var item = refItem.Get();
                if (item is StrategicGroup subRoot && subRoot.deployState == StrategicGroup.DeployState.Combined)
                {
                    SplitLoadWalk(subRoot);
                }
                else if (item is LandUnit landUnit)
                {
                    var cost = landUnit.GetTransferWeightTons();
                    if (currentCostTons + cost <= currentTransferableWeightTons)
                    {
                        currentCostTons += cost;
                        building.Add(landUnit);
                    }
                    else
                    {
                        ResolveBuilding(true);
                    }
                }
            }

            if (!isEnd)
            {
            var idSet = root.directMemberReferences.Select(r => r.referenceId).ToHashSet();
                building.RemoveAll(el => idSet.Contains(el.objectId));
                building.Add(root);
            }
        }
        
        public static void SequenceSplit(List<ShipLog> transportShips, List<StrategicGroup> cargoGroups)
        {
            if (transportShips.Count == 0 || cargoGroups.Count == 0)
            {
                return;
            }
            
            var splitter = new TransferSplitter()
            {
                ships = transportShips,
                currentTransferableWeightTons = transportShips[0].GetTransferableWeightTons(),
            };

            foreach(var cargoGroup in cargoGroups)
            {
                splitter.SplitLoadWalk(cargoGroup);

                if (splitter.isEnd) // ships are "used up"
                    break;
                else
                {
                    splitter.ResolveBuilding(false);
                }
            }
        }
    }
}
