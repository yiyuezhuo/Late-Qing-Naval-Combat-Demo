using CoreUtils;
using StrategicCombatCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NavalCombatCore
{
    public partial class ShipLog : IStrategicGroupMemberReferenceable, ISupplyNetworkNode
    {
        // public string strategicGroupId;
        public StrategicGroupReference parentGroupReference { get; set; } = new();
        public StrategicGroupReference detachedFromGroupReference { get; set; } = new();
        public bool enableAutoReattach { get; set; }
        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);
        public double supplyTons;
        
        // Move to strategic group's posture and restored hour
        // public float fixedHours; // Fixed by Tactical Combat Resolution
        //                          // GetDepot

        public SupplyTransferState supplyTransferState = new();

        public int GetStrengthMen() => shipClass?.complementMen ?? 0;
        public float GetShipTons() => shipClass?.displacementTons ?? 0; // Contain only deployed ship?
        // public float GetCombatShipTons()
        // {
        //     var _shipClass = shipClass;
        //     if(mapState == MapState.Deployed && _shipClass.IsCombatShip())
        //         return _shipClass.displacementTons;
        //     return 0;
        // }
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

        public static float supplyDisplacementPercentTranport = 0.5f;
        public static float supplyDisplacementPercentNormal = 0.1f;

        public float GetSupplyCapTons()
        {
            var meShipClass = shipClass;
            if (meShipClass == null)
                return 0;
            
            // if (meShipClass.type == ShipType.Transport)
            //     return shipClass.displacementTons / 2; // 50% of displacement for supply (coal + load) for transport ship
            // return shipClass.displacementTons / 10; // 10% of displacement for supply (coal only) for combat ship

            // 50% of displacement for supply (fuel + load) for transport ship
            // 10% of displacement for supply (fuel only) for combat ship
            var fuelAndCargoSupplyTonsCoef = meShipClass.type == ShipType.Transport ? supplyDisplacementPercentTranport : supplyDisplacementPercentNormal;
            var fuelAndCargoSupplyTons = shipClass.displacementTons * fuelAndCargoSupplyTonsCoef;
            
            var ammoGapPounds = GetGapAmmoWeightsPounds();
            var ammoGapTons = ammoGapPounds / 2204.623f;

            return fuelAndCargoSupplyTons + ammoGapTons;
        }

        // public float GetSupplyPercent() => (float)supplyTons / GetSupplyCapTons();

        // public static float shipEnduranceDays = 14;
        public static float shipEnduranceDays = 15;

        public float GetSupplyCostTonsPerDay()
        {
            return (shipClass?.displacementTons ?? 0) * supplyDisplacementPercentNormal / shipEnduranceDays; // ~0.75% of displacement of supply is consumed per day
        }

        public float GetSupplyCostTonsPerHour() => GetSupplyCostTonsPerDay() / 24;
        public float GetEnduranceHours() => (float)supplyTons / GetSupplyCostTonsPerHour();
        public double GetSupplyPercent() => supplyTons / GetSupplyCapTons();

        GlobalString ISupplyNetworkNode.GetName() => namedShip?.name;
        public double GetSupplyTons() => supplyTons;
        public void SetSupplyTons(double value) => supplyTons = value;
        public SupplyTransferState GetSupplyTransferState() => supplyTransferState;
        public Cell cell => parentGroupReference.GetCell();
        public SideState side => parentGroupReference.GetSide();
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
            foreach (var bty in batteryStatus)
            {
                bty.TrimMissHitLogs();
            }
        }

        public void ClearLogs() // TODO: Move to NavalCombatCore
        {
            logs.Clear();

            foreach (var bty in batteryStatus)
            {
                foreach (var btyMnt in bty.mountStatus)
                {
                    // btyMnt.ClearLogs();
                    btyMnt.logs.Clear();
                }
            }

            foreach (var rf in rapidFiringStatus)
            {
                // rf.ClearLogs();
                rf.logs.Clear();
            }
        }

        public void InsertLogs(ShipLog other) // TODO: Move to NavalCombatCore
        {
            // logs.AddRange(other.logs);
            logs.InsertRange(0, other.logs);

            foreach (var (selfBty, otherBty) in batteryStatus.Zip(other.batteryStatus, (x, y) => (x, y)))
            {
                foreach (var (selfMnt, otherMnt) in selfBty.mountStatus.Zip(otherBty.mountStatus, (x, y) => (x, y)))
                {
                    // selfMnt.logs.AddRange(otherMnt.logs);
                    selfMnt.logs.InsertRange(0, otherMnt.logs);
                }
            }

            foreach (var (selfRf, otherRf) in rapidFiringStatus.Zip(other.rapidFiringStatus, (x, y) => (x, y)))
            {
                // selfRf.logs.AddRange(otherRf.logs);
                selfRf.logs.InsertRange(0, otherRf.logs);
            }
        }
    }
}
