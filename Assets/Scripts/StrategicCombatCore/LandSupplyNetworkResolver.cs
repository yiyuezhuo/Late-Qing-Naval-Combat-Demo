using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ.PathFinding;
using YYZ;

namespace StrategicCombatCore
{
    public interface ISupplyNetworkNode
    {
        // SupplyTransferState supplyTransferState { get; }
        // float supplyTons { get; set; }
        // GlobalString name{ get; }
        GlobalString GetName();
        double GetSupplyTons(); // Move supplyTons to SupplyTransferState? (though if so, SupplyTransferState should be named to other thing)
        void SetSupplyTons(double value);
        SupplyTransferState GetSupplyTransferState();
        string objectId { get; set; }
        Cell cell { get; }
        SideState side { get; }
        bool IsDepotSameCellOnlySupply();
    }


    public partial class SupplyFlowRecord
    {
        public string otherObjectId;
        public double targetSupplyTons; // requested 
        public double flowSupplyTons;
        public float cost;
        public ISupplyNetworkNode GetOther()
        {
            return EntityManager.Instance.Get<ISupplyNetworkNode>(otherObjectId);
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
        public double GetUnresolvedRequestedTons()
        {
            return requestedRecords.Sum(r => r.targetSupplyTons - r.flowSupplyTons);
        }
        public void DoFlow(double flowTons)
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
        readonly SupplyNetworkCache cache = new();

        public class Bundle
        {
            // public LandUnit unit;
            public ISupplyNetworkNode unit;
            // public LandUnitTemplate template;
            public bool isDepot;
            public LandUnit depot;
            public float supplyCapTons;
            // public float virtualAssignedFlowTons;
            public double GetDeficit() => supplyCapTons - unit.GetSupplyTons();
            public double GetDeficitAfterRequest()
            {
                var requestTons = unit.GetSupplyTransferState().requestRecord.targetSupplyTons;
                var requestedTons = unit.GetSupplyTransferState().requestedRecords.Sum(r => r.targetSupplyTons);
                return GetDeficit() + (requestedTons - requestTons);
            }
        }

        static int maxIterations = 100;

        public void Resolve()
        {
            var gameState = StrategicGameState.Instance;

            // Collect and freeze related information
            var bundleMap = new Dictionary<string, Bundle>();
            foreach (var landUnit in gameState.landUnits)
            {
                var template = landUnit.GetLandUnitTemplate();
                if (template == null)
                    continue;

                var depot = GetCurrentSourceDepot((IStrategicGroupMemberReferenceable)landUnit);

                bundleMap[landUnit.objectId] = new()
                {
                    unit = landUnit,
                    isDepot = template.unitType == LandUnitType.Supply,
                    depot = depot,
                    supplyCapTons = landUnit.GetSupplyCapTons()
                };
            }
            foreach (var shipLog in gameState.shipLogs)
            {
                if(Math.Abs(shipLog.supplyTons - shipLog.GetSupplyCapTons()) > 1e-4)
                {
                    var depot = GetCurrentSourceDepot((IStrategicGroupMemberReferenceable)shipLog);

                    bundleMap[shipLog.objectId] = new()
                    {
                        unit = shipLog,
                        isDepot = false,
                        depot = depot,
                        supplyCapTons = shipLog.GetSupplyCapTons()
                    };
                }
            }

            // Clear states
            foreach (var bundle in bundleMap.Values)
            {
                bundle.unit.GetSupplyTransferState().Clear();
            }

            // Non-depot units request supply
            foreach (var bundle in bundleMap.Values.Where(b => !b.isDepot && b.depot != null))
            {
                TryToAddSupplyRequestTarget(bundle.unit, bundle.depot, bundle.GetDeficit());
            }

            // Depot request chain iteration
            var depotBundles = bundleMap.Values.Where(b => b.isDepot).ToList();

            // while (true)
            for (int i = 0; i < maxIterations; i++)
            {
                var updateAny = false;

                Bundle processingBundle = null; // debug purpose
                foreach (var depotBundle in depotBundles)
                {
                    var deficit = depotBundle.GetDeficitAfterRequest();
                    if (deficit > 1e-3 && depotBundle.depot != null)
                    {
                        var updateThisBundle = TryToAddSupplyRequestTarget(depotBundle.unit, depotBundle.depot, deficit);
                        updateAny = updateAny || updateThisBundle;
                        // updateAny = true;
                        if (updateThisBundle) // debug purpose
                            processingBundle = depotBundle;
                    }
                }

                if (!updateAny)
                    break;

                // Debug
                if (i == maxIterations - 1)
                {
                    ServiceLocator.Get<ILoggerService>().LogError($"Potential infinite loop in Depot request chain iteration");
                    var deficit1 = processingBundle.GetDeficitAfterRequest();
                    var ret1 = TryToAddSupplyRequestTarget(processingBundle.unit, processingBundle.depot, deficit1);
                    var deficit2 = processingBundle.GetDeficitAfterRequest();
                    var ret2 = TryToAddSupplyRequestTarget(processingBundle.unit, processingBundle.depot, deficit2);
                    var deficit3 = processingBundle.GetDeficitAfterRequest();
                }
            }

            // Virtual Supply Distribution flow iteration
            // while (true)
            for (int i = 0; i < maxIterations; i++)
            {
                var updateAny = false;

                foreach (var depotBundle in depotBundles)
                {
                    if (depotBundle.unit.GetSupplyTons() <= 1e-3)
                        continue;

                    var unresolvedTons = depotBundle.unit.GetSupplyTransferState().GetUnresolvedRequestedTons();
                    if (unresolvedTons <= 1e-3)
                        continue;

                    var flow = Math.Min(depotBundle.unit.GetSupplyTons(), unresolvedTons);
                    // TODO: Handle supply priority here
                    depotBundle.unit.GetSupplyTransferState().DoFlow(flow);

                    updateAny = true;
                }

                if (!updateAny)
                    break;

                // Debug
                if (i == maxIterations - 1)
                {
                    ServiceLocator.Get<ILoggerService>().LogError($"Potential infinite loop in Virtual Supply Distribution flow iteration");
                }
            }

            // Apply real flow
            foreach (var depotBundle in depotBundles)
            {
                foreach (var requestedRecord in depotBundle.unit.GetSupplyTransferState().requestedRecords)
                {
                    var requestUnit = requestedRecord.GetOther();
                    if (requestUnit != null)
                    {
                        var flowTons = requestedRecord.flowSupplyTons;
                        // requestUnit.supplyTons += flowTons;
                        // requestUnit.AddSupplyTons(flowTons);
                        requestUnit.SetSupplyTons(requestUnit.GetSupplyTons() + flowTons);
                        // depotBundle.unit.supplyTons -= flowTons; // In the process, it may be negative temporarily.
                        depotBundle.unit.SetSupplyTons(depotBundle.unit.GetSupplyTons() - flowTons);

                        requestUnit.GetSupplyTransferState().requestRecord.flowSupplyTons += flowTons;
                    }
                }
            }
        }

        float DoPathFinding(ISupplyNetworkNode requestUnit, ISupplyNetworkNode requestedUnit)
        {
            var srcCell = requestUnit.cell;
            var dstCell = requestedUnit.cell;
            var sideState = requestUnit.side;

            if (srcCell != null && dstCell != null && sideState != null)
            {
                var result = cache.GetLandSupplyPath(sideState, srcCell, dstCell);
                var cost = result.Cost;
                return cost;
            }
            return float.PositiveInfinity;
        }

        LandUnit GetCurrentSourceDepot(IStrategicGroupMemberReferenceable member)
        {
            if (member is StrategicGroup group)
                return group.GetCurrentSourceDepot(cache);

            return member.strategicGroupReference.Get()?.GetCurrentSourceDepot(cache);
        }

        bool TryToAddSupplyRequestTarget(ISupplyNetworkNode requestUnit, ISupplyNetworkNode requestedUnit, double supplyTons)
        {
            var sourceDepotRecord = requestUnit.GetSupplyTransferState().requestRecord;

            var cost = DoPathFinding(requestUnit, requestedUnit);
            if (cost == float.PositiveInfinity ||
                (requestUnit.IsDepotSameCellOnlySupply() && cost > 0))
            {
                return false;        
            }

            if (sourceDepotRecord.otherObjectId != requestedUnit.objectId)
            {
                // var cost = DoPathFinding(requestUnit, requestedUnit); // Not create record
                // if (cost == float.PositiveInfinity)
                //     return false;

                sourceDepotRecord.otherObjectId = requestedUnit.objectId;
                sourceDepotRecord.targetSupplyTons = supplyTons;
                sourceDepotRecord.flowSupplyTons = 0;
                sourceDepotRecord.cost = cost;
            }
            else
            {
                sourceDepotRecord.targetSupplyTons += supplyTons;
            }

            var matchedRecord = requestedUnit.GetSupplyTransferState().requestedRecords.FirstOrDefault(r => r.otherObjectId == requestUnit.objectId);
            if (matchedRecord == null)
            {
                // var cost = DoPathFinding(requestUnit, requestedUnit);
                // if (cost == float.PositiveInfinity)
                //     return false;

                var requestedRecord = new SupplyFlowRecord()
                {
                    otherObjectId = requestUnit.objectId,
                    targetSupplyTons = supplyTons,
                    flowSupplyTons = 0,
                    cost = cost
                };
                requestedUnit.GetSupplyTransferState().requestedRecords.Add(requestedRecord);
            }
            else
            {
                matchedRecord.targetSupplyTons += supplyTons;
            }

            return true;
        }
    }

}
