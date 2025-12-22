using System;
using System.Collections.Generic;
using CoreUtils;

namespace NavalCombatCore
{
    public class MaskCheckResult
    {
        public bool isMasked;
        public object maskedObject;
        public string message;
    }

    public class CollideCheckResult
    {
        public ShipLog collided;
        public ArmorLocation collideLocation;
        public float impactAngleDeg;
    }

    // IMaskProvider should look at NavalGameState's data, such as location of ships and size, to determine if LOS is masked.
    // The object which is at src location would not block LOS.
    public interface IMaskCheckService
    {
        MaskCheckResult Check(LatLon src, LatLon dst);
        MaskCheckResult Check(ShipLog observer, ShipLog target);
        CollideCheckResult CollideCheck(IObjectIdLabeled observer, float testDistanceYards);
        bool IsSafeToFireTorpedoAt(ShipLog shooter, ShipLog target);
    }


    public class FallbackMaskChecker : IMaskCheckService
    {
        public MaskCheckResult Check(LatLon src, LatLon dst) => new();
        public MaskCheckResult Check(ShipLog observer, ShipLog target) => new();
        public CollideCheckResult CollideCheck(IObjectIdLabeled observer, float testDistanceYards) => null;
        public bool IsSafeToFireTorpedoAt(ShipLog shooter, ShipLog target) => true;
    }

}