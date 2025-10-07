using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ.PathFinding;

namespace StrategicCombatCore
{
    public partial class SupplyFlowRecord
    {
        public string otherObjectId;
        public float targetSupplyTons; // requested 
        public float flowSupplyTons;
        public float cost;
        public LandUnit GetOther()
        {
            return EntityManager.Instance.Get<LandUnit>(otherObjectId);
        }
        public void Clear()
        {
            otherObjectId = null;
            targetSupplyTons = 0;
            flowSupplyTons = 0;
            cost = 0;
        }
    }

    public class SupplyTransferState
    {
        public SupplyFlowRecord requestRecord = new();
        public List<SupplyFlowRecord> requestedRecords = new();
        public void Clear()
        {
            requestRecord.Clear();
            requestedRecords.Clear();
        }
        public float GetUnresolvedRequestedTons()
        {
            return requestedRecords.Sum(r => r.targetSupplyTons - r.flowSupplyTons);
        }
        public void DoFlow(float flowTons)
        {
            var satifyPercent = flowTons / GetUnresolvedRequestedTons();
            foreach (var r in requestedRecords)
            {
                r.flowSupplyTons += (r.targetSupplyTons - r.flowSupplyTons) * satifyPercent;
            }
        }
    }

    public class LandSupplyNetworkResolver
    {
        Dictionary<(SideState, Cell, Cell), AStarResult<Cell>> pathfindingCache = new ();

        public class Bundle
        {
            public LandUnit unit;
            public LandUnitTemplate template;
            public bool isDepot;
            public LandUnit depot;
            public float supplyCapTons;
            // public float virtualAssignedFlowTons;
            public float GetDeficit() => supplyCapTons - unit.supplyTons;
            public float GetDeficitAfterRequest()
            {
                var requestTons = unit.supplyTransferState.requestRecord.targetSupplyTons;
                var requestedTons = unit.supplyTransferState.requestedRecords.Sum(r => r.targetSupplyTons);
                return GetDeficit() + (requestedTons - requestTons);
            }
        }

        static int maxIterations = 100;

        public void Resolve()
        {
            var gameState = StrategicGameState.Instance;

            // Clear states
            // foreach (var landUnit in gameState.landUnits)
            // {
            //     landUnit.supplyTransferState.Clear();
            // }

            // Collect and freeze related information
            var bundleMap = new Dictionary<string, Bundle>();
            foreach (var landUnit in gameState.landUnits)
            {
                var template = landUnit.GetLandUnitTemplate();
                if (template == null)
                    continue;

                var depot = ((IStrategicGroupMemberReferenceable)landUnit).GetCurrentSourceDepot();

                // Handle PathFinding here

                bundleMap[landUnit.objectId] = new()
                {
                    unit = landUnit,
                    template = template,
                    isDepot = template.unitType == LandUnitType.Supply,
                    depot = depot,
                    supplyCapTons = landUnit.GetSupplyCapTons()
                };
            }

            // Clear states
            foreach (var bundle in bundleMap.Values)
            {
                bundle.unit.supplyTransferState.Clear();
            }

            // Non-depot units request supply
            foreach (var bundle in bundleMap.Values.Where(b => !b.isDepot && b.depot != null))
            {
                TryToAddSupplyRequestTarget(bundle.unit, bundle.depot, bundle.GetDeficit());
            }

            // Depot request chain iteration
            var depotBundles = bundleMap.Values.Where(b => b.isDepot).ToList();

            // while (true)
            for (int i = 0; i < depotBundles.Count; i++)
            {
                var updateAny = false;

                foreach (var depotBundle in depotBundles)
                {
                    var deficit = depotBundle.GetDeficitAfterRequest();
                    if (deficit > 1e-3 && depotBundle.depot != null)
                    {
                        updateAny = updateAny || TryToAddSupplyRequestTarget(depotBundle.unit, depotBundle.depot, deficit);
                        // updateAny = true;
                    }
                }

                if (!updateAny)
                    break;

                // Debug
                if (i == depotBundles.Count - 1)
                {
                    ServiceLocator.Get<ILoggerService>().LogWarning($"Potential infinite loop in Depot request chain iteration");
                }
            }

            // Virtual Supply Distribution flow iteration
            // while (true)
            for (int i = 0; i < depotBundles.Count; i++)
            {
                var updateAny = false;

                foreach (var depotBundle in depotBundles)
                {
                    if (depotBundle.unit.supplyTons <= 1e-3)
                        continue;

                    var unresolvedTons = depotBundle.unit.supplyTransferState.GetUnresolvedRequestedTons();
                    if (unresolvedTons <= 1e-3)
                        continue;

                    var flow = Math.Min(depotBundle.unit.supplyTons, unresolvedTons);
                    // TODO: Handle supply priority here
                    depotBundle.unit.supplyTransferState.DoFlow(flow);

                    updateAny = true;
                }

                if (!updateAny)
                    break;

                // Debug
                if (i == depotBundles.Count - 1)
                {
                    ServiceLocator.Get<ILoggerService>().LogWarning($"Potential infinite loop in Virtual Supply Distribution flow iteration");
                }
            }

            // Apply real flow
            foreach (var depotBundle in depotBundles)
            {
                foreach (var requestedRecord in depotBundle.unit.supplyTransferState.requestedRecords)
                {
                    var requestUnit = requestedRecord.GetOther();
                    if (requestUnit != null)
                    {
                        var flowTons = requestedRecord.flowSupplyTons;
                        requestUnit.supplyTons += flowTons;
                        depotBundle.unit.supplyTons -= flowTons; // In the process, it may be negative temporarily.

                        requestUnit.supplyTransferState.requestRecord.flowSupplyTons += flowTons;
                    }
                }
            }
        }

        float DoPathFinding(LandUnit requestUnit, LandUnit requestedUnit)
        {
            var srcCell = requestUnit.cell;
            var dstCell = requestedUnit.cell;
            var sideState = requestUnit.side;

            if (srcCell != null && dstCell != null && sideState != null)
            {
                var key = (sideState, srcCell, dstCell);
                if (!pathfindingCache.TryGetValue(key, out var result))
                {
                    var graph = new DynamicLandSupplyNetworkingGraph() { side = sideState };
                    pathfindingCache[key] = result = PathFinding<Cell>.AStar3(graph, srcCell, dstCell);
                }
                var cost = result.Cost;
                return cost;
            }
            return float.PositiveInfinity;
        }

        bool TryToAddSupplyRequestTarget(LandUnit requestUnit, LandUnit requestedUnit, float supplyTons)
        {
            var sourceDepotRecord = requestUnit.supplyTransferState.requestRecord;
            if (sourceDepotRecord.otherObjectId != requestedUnit.objectId)
            {
                var cost = DoPathFinding(requestUnit, requestedUnit); // Not create record
                if (cost == float.PositiveInfinity)
                    return false;

                sourceDepotRecord.otherObjectId = requestedUnit.objectId;
                sourceDepotRecord.targetSupplyTons = supplyTons;
                sourceDepotRecord.flowSupplyTons = 0;
                sourceDepotRecord.cost = cost;
            }
            else
            {
                sourceDepotRecord.targetSupplyTons += supplyTons;
            }

            var matchedRecord = requestedUnit.supplyTransferState.requestedRecords.FirstOrDefault(r => r.otherObjectId == requestUnit.objectId);
            if (matchedRecord == null)
            {
                var cost = DoPathFinding(requestUnit, requestedUnit);
                if (cost == float.PositiveInfinity)
                    return false;

                var requestedRecord = new SupplyFlowRecord()
                {
                    otherObjectId = requestUnit.objectId,
                    targetSupplyTons = supplyTons,
                    flowSupplyTons = 0,
                    cost = cost
                };
                requestedUnit.supplyTransferState.requestedRecords.Add(requestedRecord);
            }
            else
            {
                matchedRecord.targetSupplyTons += supplyTons;
            }

            return true;
        }
    }

}