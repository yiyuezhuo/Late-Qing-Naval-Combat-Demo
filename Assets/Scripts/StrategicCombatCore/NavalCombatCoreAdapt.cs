using CoreUtils;
using StrategicCombatCore;
using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public partial class ShipLog : IStrategicGroupMemberReferenceable, ISupplyNetworkNode
    {
        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new();
        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);
        public double supplyTons;
        
        // Move to strategic group's posture and restored hour
        // public float fixedHours; // Fixed by Tactical Combat Resolution
        //                          // GetDepot

        public SupplyTransferState supplyTransferState = new();

        public int GetStrengthMen() => shipClass?.complementMen ?? 0;
        public float GetShipTons() => shipClass?.displacementTons ?? 0;
        public int GetSubUnitSize() => 1;
        public float GetCombinedPowerPoint(bool isTop)
        {
            if (mapState == MapState.Deployed)
            {
                var shipTons = shipClass?.displacementTons ?? 0;
                return shipTons / 1000;
            }
            return 0;
        }
        public float GetSupplyCapTons()
        {
            var meShipClass = shipClass;
            if (meShipClass == null)
                return 0;
            
            // if (meShipClass.type == ShipType.Transport)
            //     return shipClass.displacementTons / 2; // 50% of displacement for supply (coal + load) for transport ship
            // return shipClass.displacementTons / 10; // 10% of displacement for supply (coal only) for combat ship

            // 50% of displacement for supply (coal + load) for transport ship
            // 10% of displacement for supply (coal only) for combat ship
            var fuelAndCargoSupplyTonsCoef = meShipClass.type == ShipType.Transport ? 0.5f : 0.1f;
            var fuelAndCargoSupplyTons = shipClass.displacementTons * fuelAndCargoSupplyTonsCoef;
            
            var ammoGapPounds = GetGapAmmoWeightsPounds();
            var ammoGapTons = ammoGapPounds / 2204.623f;

            return fuelAndCargoSupplyTons + ammoGapTons;
        }

        public float GetSupplyCostTonsPerDay()
        {
            return (shipClass?.displacementTons ?? 0) / 10 / 7; // ~1.5% of displacement of supply is consumed per day
        }

        public double GetSupplyPercent() => supplyTons / GetSupplyCapTons();

        GlobalString ISupplyNetworkNode.GetName() => namedShip?.name;
        public double GetSupplyTons() => supplyTons;
        public void SetSupplyTons(double value) => supplyTons = value;
        public SupplyTransferState GetSupplyTransferState() => supplyTransferState;
        public Cell cell => strategicGroupReference.GetCell();
        public SideState side => strategicGroupReference.GetSide();
        public bool IsDepotSameCellOnlySupply() => true;
        public double GetTransferableWeightTons() => (shipClass?.displacementTons ?? 0) * 0.4; // 40%

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();
        public List<StrategicGroupMemberReference> loadedGroups = new();

        public float repairPriority;

        public void TacticalToStrategicPostHousekeeping()
        {
            timeLocLogs.Clear();

            // Truncate damage point to 100%
            damagePoint = Math.Min(damagePoint, shipClass.damagePoint);

            // Reset Battery Status
            foreach (var btyStatus in batteryStatus)
            {
                foreach (var btyMnt in btyStatus.mountStatus)
                {
                    btyMnt.ResetTargetting();
                }
                
                foreach(var fcRec in btyStatus.fireControlSystemStatusRecords)
                {
                    fcRec.ResetTargetting();
                }
            }

            // Reset Torpedo States
            foreach(var torpedoMnt in torpedoSectorStatus.mountStatus)
            {
                torpedoMnt.ResetTargetting();
            }

            // Reset Rapid Firing Status
            foreach (var rfStatus in rapidFiringStatus)
            {
                rfStatus.ResetTargetting();
            }

            TrimMissHitLogs();

            // Sub-states housekeeping
            var sunk = ApplyCampaignPersistenceEffectAndCheckSunk();
            if (sunk)
            {
                mapState = MapState.Destroyed; // TODO: Log and VP count?
            }
        }
        
        /// <summary>
        /// Trim logs, missing hit logs are removed. Hitting and hit records are reserved. (maybe generate a dedicated records?)
        /// </summary>
        public void TrimMissHitLogs()
        {
            foreach(var bty in batteryStatus)
            {
                bty.TrimMissHitLogs();
            }
        }

        // public SideState side => strategicGroupReference.Get()?.side;

    }
}