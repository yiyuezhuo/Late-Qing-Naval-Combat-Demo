using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using GeographicLib;

namespace NavalCombatCore
{
    public class AutoDeployment
    {
        public enum ControlGroupLayoutType
        {
            Parallel,
            Column
        }

        public ControlGroupLayoutType controlGroupLayoutType = ControlGroupLayoutType.Parallel;

        public float distanceYards = 12000;
        public float angleDeg = 22.5f;
        public LatLon initialAnchor = new LatLon(){LatDeg=37.5f, LonDeg=123.5f};

        public class ControlShipLog
        {
            // original reference
            public ShipLog shipLog;
            // frozen states
            public float maxSpeedKnots;
            public ShipType shipType;
            public float score;
            // dynamic value
            public LatLon position;

            public static ControlShipLog Create(ShipLog shipLog)
            {
                return new()
                {
                    shipLog = shipLog,
                    maxSpeedKnots = shipLog.GetMaxSpeedKnots(),
                    shipType = shipLog?.namedShip?.shipClass?.type ?? ShipType.LightCruiser,
                    score = shipLog?.namedShip?.shipClass?.EvaluateGeneralScore() ?? 0
                };
            }

            public bool IsLandCollisionTestPassed()
            {
                return ElevationService.Instance.GetElevation(position) <= 0;
            }

            public void Apply(float leaderHeadingDeg, float speedKnots)
            {
                shipLog.position = position;
                shipLog.headingDeg = leaderHeadingDeg;
                shipLog.desiredHeadingDeg = leaderHeadingDeg;
                shipLog.speedKnots = speedKnots;
                shipLog.desiredSpeedKnots = speedKnots;
                shipLog.mapState = MapState.Deployed;
            }
        }


        public class ControlGroup
        {
            public List<ControlShipLog> controlShipLogs = new();

            static List<HashSet<ShipType>> shipTypeSets = new()
            {
                // new(){ShipType.Battleship, ShipType.Cruiser, ShipType.ArmoredCruiser, ShipType.LightCruiser, ShipType.Cruiser, ShipType.PatrolGunboat},
                new(){ShipType.Battleship, ShipType.ArmoredCruiser, ShipType.LightCruiser, ShipType.PatrolGunboat},
                new(){ShipType.TorpedoBoat},
                // Other is treated as an independent category (aux.)
            };

            static int GetTypeIdx(ShipType shipType) => shipTypeSets.FindIndex(set => set.Contains(shipType));

            public IEnumerable<ControlGroup> SplitByCategory()
            {
                foreach(var g in controlShipLogs.GroupBy(c => GetTypeIdx(c.shipType)))
                {
                    yield return new(){controlShipLogs=g.ToList()};
                }
            }

            static int referenceSize = 5; // Taken from RTW

            public IEnumerable<ControlGroup> SplitByReferenceSize()
            {
                controlShipLogs.Sort((x, y) => -x.maxSpeedKnots.CompareTo(y.maxSpeedKnots));

                var groupCount = (int)Math.Round((float)(controlShipLogs.Count) / referenceSize);
                groupCount = Math.Max(1, groupCount);
                var controlShipLogSize = (int)Math.Ceiling((float)(controlShipLogs.Count) / groupCount);

                while(controlShipLogs.Count > 0)
                {
                    yield return new(){controlShipLogs = controlShipLogs.Take(controlShipLogSize).ToList()};
                    controlShipLogs = controlShipLogs.Skip(controlShipLogSize).ToList();
                }
            }

            public void SortByPower()
            {
                controlShipLogs.Sort((a, b) => -a.score.CompareTo(b.score));
            }

            static float innerDistM = MeasureUtils.yardToMeter * 500;

            public void SetPositions(LatLon anchor, float leaderHeadingDeg)
            {
                controlShipLogs[0].position = anchor;

                var h = MeasureUtils.NormalizeAngle(leaderHeadingDeg + 180);
                // Geodesic.WGS84.Direct(groupAnchor.LatDeg, groupAnchor.LonDeg, groupHeadingDegree, innerDist, out double lat2, out double lon2);
                for(int i=1; i<controlShipLogs.Count; i++)
                {
                    var controlShipLog = controlShipLogs[i];
                    Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, h, innerDistM * i, out double lat2, out double lon2);
                    controlShipLog.position = new LatLon((float)lat2, (float)lon2);
                }
            }

            public bool IsLandCollisionTestPassed()
            {
                return controlShipLogs.All(s => s.IsLandCollisionTestPassed());
            }

            public void Apply(float leaderHeadingDeg)
            {
                var maxSpeedKnots = controlShipLogs.Min(s => s.maxSpeedKnots);
                var speedKnots = Math.Max(0, maxSpeedKnots - 2);

                foreach(var controlShipLog in controlShipLogs)
                {
                    controlShipLog.Apply(leaderHeadingDeg, speedKnots);
                }

                for(var i=1; i<controlShipLogs.Count; i++)
                {
                    var front = controlShipLogs[i - 1];
                    var after = controlShipLogs[i];

                    after.shipLog.controlMode = ControlMode.FollowTarget;
                    after.shipLog.followedTargetObjectId = front.shipLog.objectId;
                    after.shipLog.followDistanceYards = 500;
                }
            }
        }

        public class ControlSide
        {
            public List<ControlGroup> controlGroups = new();

            public static ControlSide BuildInitial(ShipGroup rootShipGroup)
            {
                var controlGroups = new List<ControlGroup>();
                CollectBaseControlGroups(rootShipGroup, ref controlGroups);
                return new ControlSide(){controlGroups=controlGroups};
            }

            static void CollectBaseControlGroups(ShipGroup rootShipGroup, ref List<ControlGroup> controlGroups)
            {
                // Determined by given ShipGroup Hierarchy, it would be "split" according ship and type though.

                var entityManager = EntityManager.Instance;
                var childrenObjects = rootShipGroup.childrenObjectIds.Select(id => entityManager.Get<IShipGroupMember>(id)).ToList();
                var controlShipLogs = childrenObjects.Where(obj => obj is ShipLog).Select(obj => ControlShipLog.Create(obj as ShipLog)).ToList();
                controlGroups.Add(new ControlGroup(){controlShipLogs=controlShipLogs});

                foreach(var subShipGroup in childrenObjects.Where(obj => obj is ShipGroup))
                {
                    CollectBaseControlGroups(subShipGroup as ShipGroup, ref controlGroups);
                }
            }

            void Split()
            {
                controlGroups = controlGroups.SelectMany(c => c.SplitByCategory()).ToList();
                controlGroups = controlGroups.SelectMany(c => c.SplitByReferenceSize()).ToList();
            }

            public void SortByPower()
            {
                foreach(var controlGroup in controlGroups)
                    controlGroup.SortByPower();
            }

            public static ControlSide Build(ShipGroup rootShipGroup)
            {
                var controlSide = BuildInitial(rootShipGroup);
                controlSide.Split();
                controlSide.SortByPower();

                return controlSide;
            }

            static float innerDistM = MeasureUtils.yardToMeter * 500;

            public void SetPositions(LatLon anchor, float leaderHeadingDeg, ControlGroupLayoutType controlGroupLayoutType)
            {
                controlGroups[0].SetPositions(anchor, leaderHeadingDeg);

                if(controlGroupLayoutType == ControlGroupLayoutType.Parallel)
                {
                    for(int i=1; i<controlGroups.Count; i++)
                    {
                        var controlGroup = controlGroups[i];
                        var h = MeasureUtils.NormalizeAngle(leaderHeadingDeg + 90);
                        Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, h, innerDistM * i, out double lat2, out double lon2);
                        controlGroup.SetPositions(new LatLon((float)lat2, (float)lon2), leaderHeadingDeg);
                    }
                }
                else if(controlGroupLayoutType == ControlGroupLayoutType.Column)
                {
                    var cumCount = controlGroups[0].controlShipLogs.Count;
                    for(int i=1; i<controlGroups.Count; i++)
                    {
                        var controlGroup = controlGroups[i];
                        var h = MeasureUtils.NormalizeAngle(leaderHeadingDeg + 180);
                        Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, h, innerDistM * cumCount, out double lat2, out double lon2);
                        controlGroup.SetPositions(new LatLon((float)lat2, (float)lon2), leaderHeadingDeg);

                        cumCount += controlGroup.controlShipLogs.Count;
                    }
                }
            }

            public bool IsLandCollisionTestPassed()
            {
                return controlGroups.All(g => g.IsLandCollisionTestPassed());
            }

            public void Apply(float leaderHeadingDeg)
            {
                foreach(var g in controlGroups)
                    g.Apply(leaderHeadingDeg);
            }
        }


        
        public ControlSide controlSide0;
        public ControlSide controlSide1;

        public void Build()
        {
            var gameState = NavalGameState.Instance;

            var topShipGroups = gameState.shipGroups.Where(g => g.parentObjectId == null).ToList();
            if(topShipGroups.Count >= 1)
            {
                controlSide0 = ControlSide.Build(topShipGroups[0]);
            }
            if(topShipGroups.Count >= 2)
            {
                controlSide1 = ControlSide.Build(topShipGroups[1]);
            }
        }

        public void SetPositions(LatLon anchor)
        {
            if(controlSide0 == null && controlSide1 == null)
                return;
            
            if(controlSide1 == null)
            {
                controlSide0.SetPositions(anchor, angleDeg, controlGroupLayoutType);
                return;
            }

            var distFromAnchorM = distanceYards * MeasureUtils.yardToMeter / 2;
            var supAngleDeg = MeasureUtils.NormalizeAngle(angleDeg + 180);
            Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, angleDeg, distFromAnchorM, out double lat2, out double lon2);
            controlSide0.SetPositions(new LatLon((float)lat2, (float)lon2), supAngleDeg, controlGroupLayoutType);

            Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, supAngleDeg, distFromAnchorM, out double lat3, out double lon3);
            controlSide1.SetPositions(new LatLon((float)lat3, (float)lon3), angleDeg, controlGroupLayoutType);
        }

        public bool IsLandCollisionTestPassed()
        {
            if(controlSide0 != null && !controlSide0.IsLandCollisionTestPassed())
                return false;
            if(controlSide1 != null && !controlSide1.IsLandCollisionTestPassed())
                return false;
            return true;
        }

        public LatLon SearchValidPositions()
        {
            SetPositions(initialAnchor);
            if(IsLandCollisionTestPassed())
                return initialAnchor;
            
            for(float distNm=50; distNm <= 500; distNm += 50)
            {
                for(float angle=0; angle < 360; angle += 45)
                {
                    Geodesic.WGS84.Direct(initialAnchor.LatDeg, initialAnchor.LonDeg, angle, distNm * MeasureUtils.navalMileToMeter, out double lat2, out double lon2);
                    var testAnchor = new LatLon((float)lat2, (float)lon2);
                    SetPositions(testAnchor);
                    if(IsLandCollisionTestPassed())
                        return testAnchor;
                }
            }

            return null;
        }

        public void Apply()
        {
            var supAngleDeg = MeasureUtils.NormalizeAngle(angleDeg + 180);

            controlSide0?.Apply(supAngleDeg);
            controlSide1?.Apply(angleDeg);
        }

        public LatLon Execute()
        {
            Build();
            var resultAnchor = SearchValidPositions();
            if(resultAnchor != null)
            {
                Apply();
            }
            return resultAnchor;
        }
    }
}