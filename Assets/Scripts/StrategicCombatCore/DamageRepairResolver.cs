using System;
using System.Collections;
using NavalCombatCore;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using YYZ;

namespace StrategicCombatCore
{

    public class DamageRepairRecord
    {
        // Callback or a dedicated structure to reference a repair? (EX: engineRoomHits -=1 for a ship)
        // public float manualPriority; // Human given, for example, high priority may be given to a ship
        public enum Type
        {
            ShipOperationalState, // Major, Abandon Ship, Flooding Obstruction => Operational
            MaxSpeedOffset,
            AccelerationOffset,
            EngineRoomHits,
            EngineRoomFlooding, // Major
            PropulsionShaftHis, // Major
            BoilerRoomHits,
            BoilerRoomFlooding, // Major
            DamageControlRatingHit,
            PortSearchlightHits,
            StarboardSearchlightHits,
            SmokeGeneratorDisabled,
            SubState,
            BatteryMountStatus,
            FiringControlSystemState,
            TorpedoMountStatus,
            RapidFireBatteryPortMountHits,
            RapidFireBatteryStartboardMountHits,
            RapidFireBatteryFireControlHits,
            PureDamagePoint // placeholder
        }

        static float baseCost = 2000; // 1 SK5 Flooding Hit ~= 20 WITP Flooding Damage (20% Flooding Damage, 100% will sink the ship)

        public Type type;
        public ShipLog shipLog;
        public float manualPriority => shipLog.repairPriority;
        public float autoPriority; // Generated
        public float repairPointCost = baseCost;
        public bool major; // major can only be repaired in the repair shipyard meeting displacement requirement
        public Action callback;

        public float mappedDamagePoints;

        public override string ToString()
        {
            return $"DamageRepairRecord({type}, {shipLog?.namedShip?.name.GetMergedName()}, Priority=({manualPriority}, {autoPriority}), repairPointCost={repairPointCost}, major={major}, mappedDamagePoints={mappedDamagePoints})";
        }

        public LazyLocalizedString GetLazyLocalizedDesc()
        {
            return LazyLocalizedString.MakeTemplate(
                "Damage Repair({0}, {1}, Priority=({2}, {3}), Repair Point Cost={4}, Major={5}, Mapped Damage Points={6})",
                LazyLocalizedString.MakeEnum(type),
                LazyLocalizedString.MakeGlobalStringLong(shipLog?.namedShip?.name),
                LazyLocalizedString.MakeRaw(manualPriority),
                LazyLocalizedString.MakeRaw(autoPriority),
                LazyLocalizedString.MakeRaw(repairPointCost),
                LazyLocalizedString.MakeLocalizedRequired(major.ToString()),
                LazyLocalizedString.MakeRaw(mappedDamagePoints)
            );
        }

        public static int CompareTo(DamageRepairRecord left, DamageRepairRecord right)
        {
            if (left.manualPriority != right.manualPriority)
            {
                return left.manualPriority.CompareTo(right.manualPriority);
            }
            return left.autoPriority.CompareTo(right.autoPriority);
        }

        public static List<DamageRepairRecord> Extract(ShipLog shipLog)
        {
            var records = _Extract(shipLog).ToList();
            if(records.Count == 0 && shipLog.damagePoint > 0)
            {
                records.Add(new()
                {
                    type = Type.PureDamagePoint,
                    shipLog = shipLog,
                    autoPriority = 1,
                    repairPointCost = 1,
                    callback = () => { } // Damage Point would be cleared by "mapped" mechnanism
                });
            }

            var costSum = records.Sum(r => r.repairPointCost);
            var damagePoint = shipLog.damagePoint;
            foreach (var record in records)
            {
                record.mappedDamagePoints = costSum > 0 ? damagePoint * (record.repairPointCost / costSum) : 0;
            }
            return records;
        }

        static IEnumerable<DamageRepairRecord> _Extract(ShipLog shipLog)
        {
            if (shipLog.operationalState != ShipOperationalState.Operational)
            {
                yield return new()
                {
                    type = Type.ShipOperationalState,
                    shipLog = shipLog,
                    autoPriority = 100,
                    major = true,
                    callback = () =>
                    {
                        shipLog.operationalState = ShipOperationalState.Operational;
                    }
                };
            }

            if (shipLog.dynamicStatus.maxSpeedKnotsOffset < 0)
            {
                for (int i = 0; i < -shipLog.dynamicStatus.maxSpeedKnotsOffset; i++)
                {
                    yield return new()
                    {
                        type = Type.MaxSpeedOffset,
                        shipLog = shipLog,
                        autoPriority = 25,
                        callback = () =>
                        {
                            shipLog.dynamicStatus.maxSpeedKnotsOffset++;
                        }
                    };
                }
            }

            if (shipLog.dynamicStatus.accelerationOffset < 0)
            {
                for (int i = 0; i < -shipLog.dynamicStatus.accelerationOffset; i++)
                {
                    yield return new()
                    {
                        type = Type.AccelerationOffset,
                        shipLog = shipLog,
                        autoPriority = 25,
                        callback = () =>
                        {
                            shipLog.dynamicStatus.accelerationOffset++;
                        }
                    };
                }
            }

            for (int i = 0; i < shipLog.dynamicStatus.engineRoomHits; i++)
            {
                yield return new()
                {
                    type = Type.EngineRoomHits,
                    shipLog = shipLog,
                    autoPriority = 75,
                    callback = () =>
                    {
                        shipLog.dynamicStatus.engineRoomHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.dynamicStatus.engineRoomFloodingHits; i++)
            {
                yield return new()
                {
                    type = Type.EngineRoomFlooding,
                    shipLog = shipLog,
                    autoPriority = 100,
                    major = true,
                    callback = () =>
                    {
                        shipLog.dynamicStatus.engineRoomFloodingHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.dynamicStatus.propulsionShaftHits; i++)
            {
                yield return new()
                {
                    type = Type.PropulsionShaftHis,
                    shipLog = shipLog,
                    autoPriority = 100,
                    major = true,
                    callback = () =>
                    {
                        shipLog.dynamicStatus.propulsionShaftHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.dynamicStatus.boilerRoomHits; i++)
            {
                yield return new()
                {
                    type = Type.BoilerRoomHits,
                    shipLog = shipLog,
                    autoPriority = 75,
                    callback = () =>
                    {
                        shipLog.dynamicStatus.boilerRoomHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.dynamicStatus.boilerRoomFloodingHits; i++)
            {
                yield return new()
                {
                    type = Type.BoilerRoomFlooding,
                    shipLog = shipLog,
                    autoPriority = 100,
                    major = true,
                    callback = () =>
                    {
                        shipLog.dynamicStatus.boilerRoomFloodingHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.damageControlRatingHits; i++)
            {
                yield return new()
                {
                    type = Type.DamageControlRatingHit,
                    shipLog = shipLog,
                    autoPriority = 50,
                    callback = () =>
                    {
                        shipLog.damageControlRatingHits--;
                    }
                };
            }

            for (int i = 0; i < shipLog.searchLightHits.portHit; i++)
            {
                yield return new()
                {
                    type = Type.PortSearchlightHits,
                    shipLog = shipLog,
                    autoPriority = 1,
                    repairPointCost = baseCost * 0.05f,
                    callback = () =>
                    {
                        shipLog.searchLightHits.portHit--;
                    },
                };
            }

            for (int i = 0; i < shipLog.searchLightHits.starboardHit; i++)
            {
                yield return new()
                {
                    type = Type.StarboardSearchlightHits,
                    shipLog = shipLog,
                    autoPriority = 1,
                    repairPointCost = baseCost * 0.05f,
                    callback = () =>
                    {
                        shipLog.searchLightHits.starboardHit--;
                    },
                };
            }

            if (shipLog.smokeGeneratorDisabled)
            {
                yield return new()
                {
                    type = Type.SmokeGeneratorDisabled,
                    shipLog = shipLog,
                    autoPriority = 2,
                    repairPointCost = baseCost * 0.15f,
                    callback = () =>
                    {
                        shipLog.smokeGeneratorDisabled = false;
                    },
                };
            }

            foreach (var repairableSubStateRecord in shipLog.CollectRepairableSubStateRecords())
            {
                yield return new()
                {
                    type = Type.SubState,
                    shipLog = shipLog,
                    autoPriority = 75,
                    callback = () =>
                    {
                        repairableSubStateRecord.subject.RemoveSubState(repairableSubStateRecord.subState);
                    },
                };
            }

            // BatteryMountStatus
            foreach (var btyStatus in shipLog.batteryStatus)
            {
                foreach (var btyMnt in btyStatus.mountStatus)
                {
                    if (btyMnt.status != MountStatus.Operational)
                    {
                        yield return new()
                        {
                            type = Type.BatteryMountStatus,
                            shipLog = shipLog,
                            autoPriority = 25,
                            // TODO: Cost use WITP-like effect cost
                            callback = () =>
                            {
                                btyMnt.status = MountStatus.Operational;
                            }
                        };
                    }
                }

                foreach (var fcRec in btyStatus.fireControlSystemStatusRecords)
                {
                    if (fcRec.trackingState == TrackingSystemState.Destroyed)
                    {
                        yield return new()
                        {
                            type = Type.FiringControlSystemState,
                            shipLog = shipLog,
                            autoPriority = 20f,
                            repairPointCost = baseCost * 0.5f,
                            callback = () =>
                            {
                                fcRec.trackingState = TrackingSystemState.Idle;
                            }
                        };
                    }
                }
            }

            foreach (var torpedoMnt in shipLog.torpedoSectorStatus.mountStatus)
            {
                if (torpedoMnt.status != MountStatus.Operational)
                {
                    yield return new()
                    {
                        type = Type.TorpedoMountStatus,
                        shipLog = shipLog,
                        autoPriority = 15f,
                        callback = () =>
                        {
                            torpedoMnt.status = MountStatus.Operational;
                        }
                    };
                }
            }

            // RapidFireBatteryPortMountHits,
            // RapidFireBatteryStartboardMountHits,
            // RapidFireBatteryFireControlHits,
            foreach (var rfRec in shipLog.rapidFiringStatus)
            {
                for (int i = 0; i < rfRec.portMountHits; i++)
                {
                    yield return new()
                    {
                        type = Type.RapidFireBatteryPortMountHits,
                        shipLog = shipLog,
                        autoPriority = 25,
                        callback = () =>
                        {
                            rfRec.portMountHits -= 1;
                        }
                    };
                }

                for (int i = 0; i < rfRec.starboardMountHits; i++)
                {
                    yield return new()
                    {
                        type = Type.RapidFireBatteryStartboardMountHits,
                        shipLog = shipLog,
                        autoPriority = 25,
                        callback = () =>
                        {
                            rfRec.starboardMountHits -= 1;
                        }
                    };
                }

                for (int i = 0; i < rfRec.fireControlHits; i++)
                {
                    yield return new()
                    {
                        type = Type.RapidFireBatteryFireControlHits,
                        shipLog = shipLog,
                        autoPriority = 20f,
                        repairPointCost = baseCost * 0.5f,
                        callback = () =>
                        {
                            rfRec.fireControlHits -= 1;
                        }
                    };
                }
            }
            
            // TODO: Barrel loss repair
        }
    }

    public class DamageRepairResolver
    {
        const float repairPointsPerPortLevel = 100f;
        const float repairShipRepairPoints = repairPointsPerPortLevel;

        public class RepairCapacity
        {
            public float displacementUpperTons;
            public float repairPoints;
        }

        public class Bundle
        {
            public SideState sideState;
            public Cell cell;

            public List<LandUnit> ports = new();
            public List<RepairCapacity> repairCapacities = new();

            public List<ShipLog> shipLogs = new();
            public List<DamageRepairRecord> damageRepairRecords = new();

            public override string ToString()
            {
                return $"DamageRepairResolver.Bundle({sideState}, {cell}, #ports={ports.Count}, #repairCapacities={repairCapacities.Count}, #shipLogs={shipLogs.Count}, #damageRepairRecords={damageRepairRecords.Count})";
            }

            public LazyLocalizedString GetLazyLocalizedDesc()
            {
                return LazyLocalizedString.MakeTemplate(
                    "Repair Cell: ({0}, {1}, Requesting repair ships count={2}, Requested repair damages={3})",
                    LazyLocalizedString.MakeGlobalStringShort(sideState.name),
                    LazyLocalizedString.MakeRaw($"({cell.x}, {cell.y})"),
                    LazyLocalizedString.MakeRaw(repairCapacities.Count),
                    LazyLocalizedString.MakeRaw(damageRepairRecords.Count)
                );
            }

            public float GetRepairPointsForDisplacementTons(float displacementTons)
            {
                return repairCapacities.Where(r => displacementTons <= r.displacementUpperTons).Sum(r => r.repairPoints);
            }

            public bool IsRepairbleForDisplacementTons(float displacementTons, float requiredRepairPoints) => requiredRepairPoints <= GetRepairPointsForDisplacementTons(displacementTons);

            public bool TryToRepair(DamageRepairRecord r)
            {
                var displacementTons = r.shipLog?.shipClass?.displacementTons ?? 0;

                var unresolvedCost = r.repairPointCost;
                foreach (var cap in repairCapacities)
                {
                    if (unresolvedCost == 0)
                    {
                        break;
                    }

                    if (!r.major || displacementTons <= cap.displacementUpperTons)
                    {
                        var resolvedCost = Math.Min(unresolvedCost, cap.repairPoints);
                        unresolvedCost -= resolvedCost;
                        cap.repairPoints -= resolvedCost;
                    }
                }

                var failProb = unresolvedCost / r.repairPointCost;
                if (failProb < 1)
                {
                    if(RandomUtils.NextFloat() > failProb)
                    {
                        // succ
                        r.callback(); // Concreate repair, such as Sub state removed, max speed negative offset removed.
                        r.shipLog.damagePoint = Math.Max(0, r.shipLog.damagePoint - r.mappedDamagePoints); // Note for SK5 system, resolved damagePoint is somewhat a "good" thing (potension risk is reduced). The "bad" thing is damage effect caused by damage tier crossed or other side effect which usually happend at the same time.

                        ServiceLocator.Get<ILoggerService>().Log($"Repair (Succ, Prob={1 - failProb}): {r}");

                        StrategicGameState.Instance.AddLog(LazyLocalizedString.MakeTemplate(
                            "Repair (Succ, Prob={0}): {1}",
                            LazyLocalizedString.MakeRaw(1 - failProb),
                            r.GetLazyLocalizedDesc()
                        ), sideState);

                        return true;
                    }
                    else
                    {
                        ServiceLocator.Get<ILoggerService>().Log($"Repair (Failed, Prob={1 - failProb}: {r}");
                        
                        StrategicGameState.Instance.AddLog(LazyLocalizedString.MakeTemplate(
                            "Repair (Failed, Prob={0}): {1}",
                            LazyLocalizedString.MakeRaw(1 - failProb),
                            r.GetLazyLocalizedDesc()
                        ), sideState);
                    }
                }
                return false;
            }
            
            public void Resolve()
            {
                damageRepairRecords.Sort((left, right) => -DamageRepairRecord.CompareTo(left, right));
                repairCapacities.Sort((left, right) => left.displacementUpperTons.CompareTo(right.displacementUpperTons));

                foreach(var record in damageRepairRecords)
                {
                    TryToRepair(record);
                }
            }
        }

        public void Resolve()
        {
            // Collect cell containing friendly port or shipyard
            Dictionary<(SideState, Cell), Bundle> bundleMap = new();
            Bundle GetOrCreateBundle(SideState side, Cell cell)
            {
                var key = (side, cell);
                if (!bundleMap.TryGetValue(key, out var bundle))
                {
                    bundleMap[key] = bundle = new Bundle() { sideState = side, cell = cell };
                }

                return bundle;
            }

            foreach (var landUnit in StrategicGameState.Instance.landUnits)
            {
                if (landUnit.GetLandUnitTemplate()?.unitType == LandUnitType.Port && (landUnit.portLevel > 0 || landUnit.repairShipyardLevel > 0))
                {
                    var cell = landUnit.cell;
                    var side = landUnit.side;

                    if (cell != null && side != null)
                    {
                        var bundle = GetOrCreateBundle(side, cell);
                        bundle.ports.Add(landUnit);

                        if (landUnit.portLevel > 0)
                        {
                            bundle.repairCapacities.Add(new()
                            {
                                displacementUpperTons = 0,
                                repairPoints = landUnit.portLevel * repairPointsPerPortLevel
                            });
                        }
                        if (landUnit.repairShipyardLevel > 0)
                        {
                            bundle.repairCapacities.Add(new()
                            {
                                displacementUpperTons = landUnit.repairShipyardLevel * 1000,
                                repairPoints = landUnit.repairShipyardLevel * repairPointsPerPortLevel
                            });
                        }
                    }
                }
            }

            foreach (var group in StrategicGameState.Instance.IterIndependentStrategicGroups())
            {
                if (group.IsMovingStrategically || group.side == null || group.cell == null)
                    continue;

                var bundle = (Bundle)null;
                foreach (var shipLog in group.WalkGroupMembersDeployedShips())
                {
                    if (shipLog?.shipClass?.type != ShipType.Repair ||
                        shipLog.operationalState != ShipOperationalState.Operational)
                    {
                        continue;
                    }

                    bundle ??= GetOrCreateBundle(group.side, group.cell);
                    bundle.repairCapacities.Add(new()
                    {
                        displacementUpperTons = 0,
                        repairPoints = repairShipRepairPoints
                    });
                }
            }

            // Collect Repairable ships
            foreach (var group in StrategicGameState.Instance.IterIndependentStrategicGroups())
            {
                if (group.IsMovingStrategically)
                {
                    continue;
                }

                // If group is in a cell containing friendly port or shipyard.
                var key = (group.side, group.cell);
                if (bundleMap.TryGetValue(key, out var bundle))
                {
                    // bundle.shipLogs.AddRange(group.WalkGroupMembersDeployedShips());
                    foreach (var shipLog in group.WalkGroupMembersDeployedShips())
                    {
                        var damageRepairRecords = DamageRepairRecord.Extract(shipLog);
                        if(damageRepairRecords.Count > 0)
                        {
                            bundle.shipLogs.Add(shipLog);
                            bundle.damageRepairRecords.AddRange(damageRepairRecords);
                        }
                    }
                }
            }
            
            // Resolve
            foreach(var bundle in bundleMap.Values)
            {
                if(bundle.repairCapacities.Count > 0 && bundle.damageRepairRecords.Count > 0)
                {
                    ServiceLocator.Get<ILoggerService>().Log($"DamageRepairResolver.Bundle: {bundle}");

                    StrategicGameState.Instance.AddLog(bundle.GetLazyLocalizedDesc(), bundle.sideState);

                    bundle.Resolve();
                }
            }

            foreach (var shipLog in StrategicGameState.Instance.shipLogs)
            {
                if (shipLog?.detachedFromGroupReference?.Get() == null)
                    continue;

                if (StrategicGroupSubGroupUtility.NeedsDetachForRepair(shipLog))
                    continue;

                shipLog.enableAutoReattach = true;
            }
        }
    }
}
