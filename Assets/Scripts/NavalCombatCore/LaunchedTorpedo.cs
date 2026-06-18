using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;
using System.Diagnostics;

using CoreUtils;
using YYZ;

namespace NavalCombatCore
{

    public enum LaunchedTorpedoEndgameType
    {
        Undetermined,
        SelfDestruction, // Collide with land and other things
        OutOfRange,
        Dud, // Similar to CMO Malfunction
        Hit
    }

    public partial class LaunchedTorpedo : IObjectIdLabeled, IDF4Model, ICollider
    {
        public string objectId { get; set; }
        public string shooterId;
        public ShipLog GetShooter() => EntityManager.Instance.Get<ShipLog>(shooterId); // shooter can sink
        public string desiredTargetObjectId;
        public ShipLog GetDesiredTarget() => EntityManager.Instance.Get<ShipLog>(desiredTargetObjectId); // torpedo may not attack its initial desired target but this can be labeled

        public LatLon position = new();
        public float speedKnots; // current speed vs "max" speed defined in the class
        public float headingDeg;
        public MapState mapState;
        public float lengthFoot = 11.58f;
        public float beamFoot = 1.3f;

        public float GetBeamFoot() => beamFoot;
        public float GetLengthFoot() => lengthFoot;
        public LatLon GetPosition() => position;

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;
        public float GetSpeedKnots() => speedKnots;

        public float maxRangeYards;
        public float movedDistanceYards;
        public TorpedoDamageClass damageClass;
        public GlobalString sourceName = new();

        // endgame
        public LaunchedTorpedoEndgameType endgameType = LaunchedTorpedoEndgameType.Undetermined;
        public string hitTargetObjectId;
        public ShipLog GetHitObject() => EntityManager.Instance.Get<ShipLog>(hitTargetObjectId);
        public float inflictDamagePoint;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public enum FriendlyCollisionProcessMode
        {
            Hit, 
            Passthrough,
            Dub
        }

        public static FriendlyCollisionProcessMode friendlyCollisionProcessMode = FriendlyCollisionProcessMode.Passthrough;

        public bool IsHostileTo(ShipLog shipLog)
        {
            return (GetShooter() as IShipGroupMember).GetRootParent() != (shipLog as IShipGroupMember).GetRootParent();
        }

        public void StepMoveToNewPosition(float deltaSeconds)
        {
            var distNm = speedKnots / 3600 * deltaSeconds;
            var distM = distNm * 1852;
            double arcLength = Geodesic.WGS84.Direct(position.LatDeg, position.LonDeg, headingDeg, distM, out double lat2, out double lon2);

            var newPosition = new LatLon((float)lat2, (float)lon2);

            var newPositionBlocked = false;
            if (CoreParameter.Instance.checkLandCollision)
            {
                newPositionBlocked = ElevationService.Instance.GetElevation(newPosition) > 0;
                if(newPositionBlocked)
                {
                    endgameType = LaunchedTorpedoEndgameType.SelfDestruction;
                }
            }

            if (!newPositionBlocked)
            {
                var maskCheckService = NavalCombatServices.GetMaskCheckService();
                var collideCheckResult = maskCheckService.CollideCheck(this, distNm * MeasureUtils.navalMileToYard + GetLengthFoot() / 2 * MeasureUtils.footToYard);
                var collidedShipLog = collideCheckResult?.collided;

                // var collidedShipLog = NavalGameState.Instance.shipLogsOnMap.FirstOrDefault(other => other.objectId != shooterId && CollideUtils.IsCollided(newPosition, this, other));
                // if (collidedShipLog != null)
                var shooter = GetShooter();

                if(collidedShipLog != null && shooter != collidedShipLog) // Torpedo is setup in the same place of the platform, suppress the collision with the platform.
                {
                    if(friendlyCollisionProcessMode == FriendlyCollisionProcessMode.Hit || IsHostileTo(collidedShipLog))
                    {
                        var classSector = shooter.shipClass.torpedoSector;
                        var damageClass = classSector.damageClass;
                        var pistolType = classSector.pistolType;
                        var dudProb = classSector.dudProbability;

                        newPositionBlocked = true;

                        if (RandomUtils.rand.NextDouble() <= dudProb)
                        {
                            endgameType = LaunchedTorpedoEndgameType.Dud;

                            var logger = ServiceLocator.Get<ILoggerService>();
                            logger.LogWarning($"Torpedo {objectId} collides ship {collidedShipLog.namedShip.name.GetMergedName()} Dud (Prob={dudProb})");
                        }
                        else
                        {
                            endgameType = LaunchedTorpedoEndgameType.Hit;

                            var torpedoDamage = RuleChart.RollTorpedoDamage(damageClass, pistolType);

                            var armorEffInch = 0f;
                            var p = RandomUtils.rand.NextDouble();
                            if (p <= 0.45)
                            {
                                armorEffInch = collidedShipLog.shipClass.armorRating.GetArmorEffectiveInch(ArmorLocation.MainBelt);
                            }
                            else if (p <= 0.75)
                            {
                                armorEffInch = collidedShipLog.shipClass.armorRating.GetArmorEffectiveInch(ArmorLocation.BeltEnd);
                            }

                            if (armorEffInch > 0)
                            {
                                // This modifier may be bad for ironclad though. Ironclad should not have no effective TDS for that.
                                var adjustment = RuleChart.GetArmorAdjustment(armorEffInch);
                                torpedoDamage = Math.Max(0, torpedoDamage - adjustment);
                            }

                            // collidedShipLog.damagePoint += torpedoDamage;
                            collidedShipLog.AddDamagePoint(torpedoDamage);

                            inflictDamagePoint = torpedoDamage;
                            hitTargetObjectId = collidedShipLog.objectId;

                            // Handle Damage Effect

                            var damageSchema = collidedShipLog.shipClass.GetDamageSchema();

                            var ctx = new DamageEffectContext()
                            {
                                subject = collidedShipLog,
                                baseDamagePoint = torpedoDamage,
                                damageSchema = damageSchema
                                // cause = DamageEffectCause.Torpedo,
                            };
                            if(damageSchema == DamageSchema.Warship)
                            {
                                ctx.cause = DamageEffectCause.Torpedo;
                            }
                            else if(damageSchema == DamageSchema.MerchantVessal)
                            {
                                var hitLocation = RuleChart.SampleHitLocationMerchantVesselTorpedo();
                                ctx.causeMerchantVessel = RuleChart.GetDamageEffectCauseMerchantVessel(hitLocation, collidedShipLog.cargoAreas);
                            }
                            else if (damageSchema == DamageSchema.LandBattery)
                            {
                                ctx.causeLandBattery = DamageEffectCauseLandBattery.Torpedo;
                            }

                            var damageEffectId = DamageEffectChart.AddNewDamageEffect(ctx);

                            var tgtLog = new ShipLogTorpedoHitLog()
                            {
                                torpedoObjectId = objectId,
                                time = NavalGameState.Instance.scenarioState.dateTime,
                                damagePoint = torpedoDamage,
                                damageEffectId = damageEffectId
                            };
                            collidedShipLog.AddLog(tgtLog);

                            collidedShipLog.startingExplosions += 1;

                            var logger = ServiceLocator.Get<ILoggerService>();
                            logger.LogWarning($"Torpedo {objectId} collides ship {collidedShipLog.namedShip.name.GetMergedName()} armorEffInch={armorEffInch} torpedoDamage={torpedoDamage}");
                        }
                    }
                    else if(friendlyCollisionProcessMode == FriendlyCollisionProcessMode.Passthrough)
                    {
                        // ignore
                    }
                    else if(friendlyCollisionProcessMode == FriendlyCollisionProcessMode.Dub)
                    {
                        newPositionBlocked = true;
                        endgameType = LaunchedTorpedoEndgameType.Dud;
                    }
                }
            }

            if (newPositionBlocked)
            {
                mapState = MapState.Destroyed;
            }
            else
            {
                position = newPosition;
            }
            movedDistanceYards += distM * MeasureUtils.meterToYard;
            if (movedDistanceYards > maxRangeYards)
            {
                mapState = MapState.Destroyed;
                endgameType = LaunchedTorpedoEndgameType.OutOfRange;
            }
        }
    }
}
