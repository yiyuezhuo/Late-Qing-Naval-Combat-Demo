using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;

namespace NavalCombatCore
{
    public partial class ShipClass
    {
        [CreateProperty]
        public float armorScoreProp => EvaluateArmorScore();

        [CreateProperty]
        public float survivabilityProp => EvaluateSurvivability();

        [CreateProperty]
        public float batteryFirepowerProp => EvaluateBatteryFirepowerScore();

        [CreateProperty]
        public float torpedoThreatScoreProp => EvaluateTorpedoThreatScore();

        [CreateProperty]
        public float rapidFiringFirepowerProp => EvaluateRapidFiringFirepowerScore();

        [CreateProperty]
        public float firepoweScoreProp => EvaluateFirepowerScore();

        [CreateProperty]
        public float generalScoreProp => EvaluateGeneralScore();

        [CreateProperty]
        public float firepowerBowProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 0);

        [CreateProperty]
        public float firepowerStarboardProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 90);

        [CreateProperty]
        public float firepowerSternProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 180);

        [CreateProperty]
        public float firepowerPortProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 270);
    }

    public partial class LaunchedTorpedo : IPortraitViewerObservable
    {
        Country IPortraitViewerObservable.GetCountry() => GetShooter()?.shipClass?.country ?? Country.General;
        string IPortraitViewerObservable.GetPortraitTopCode() => "Schwartzkopff Torpedo_Top"; // TODO: use true name
        bool IPortraitViewerObservable.IsShowArrow() => false;
        GlobalString IPortraitViewerObservable.GetName() => sourceName;
        float IPortraitViewerObservable.GetDesiredHeadingDeg() => headingDeg;
        string IPortraitViewerObservable.GetAcronym() => "T";

        [CreateProperty]
        public string shooterDesc
        {
            get => GetShooter()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public string desiredTargetDesc
        {
            get => GetDesiredTarget()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public string sourceNameDesc
        {
            get => sourceName.GetMergedName();
        }

        [CreateProperty]
        public string hitObjectDesc
        {
            get => GetHitObject()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }
    }

    public partial class ShipLog : IPortraitViewerObservable
    {
        [CreateProperty]
        public ShipClass shipClassProperty
        {
            get => shipClass;
        }

        [CreateProperty]
        public Leader leaderProp
        {
            get => leader;
        }

        [CreateProperty]
        public NamedShip namedShipProp
        {
            get => namedShip;
        }

        [CreateProperty]
        public string namedShipDesc
        {
            get => namedShip?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public string namedShipDescLink
        {
            get
            {
                var name = namedShip?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                return $"<link=\"namedShip\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string shipClassDescLink
        {
            get
            {
                var name = shipClass?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                return $"Class: <link=\"shipClass\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string captainDesc
        {
            get => leader?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public string captainDescLink
        {
            get
            {
                var name = leader?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                return $"Captain: <link=\"captain\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string oobParentDesc
        {
            get
            {
                var member = (IShipGroupMember)this;
                var parentGroup = member.GetParentGroup();
                return parentGroup?.name.mergedName ?? "[Not Specified]";
            }
        }

        [CreateProperty]
        public string summary
        {
            get => Summary();
        }

        [CreateProperty]
        public string followedTargetDesc
        {
            get => followedTarget?.namedShip?.name.mergedName ?? "[Not Specified or Invalid]";
        }

        // [CreateProperty]
        // public DisplayStyle
        [CreateProperty]
        public ShipLog followedTargetProp
        {
            get => followedTarget;
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfControlModeIsFollowTarget
        {
            get => controlMode == ControlMode.FollowTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }

        [CreateProperty]
        public string relativeToTargetDesc
        {
            get => relativeToTarget?.namedShip?.name.mergedName ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfControlModeIsRelativeToTarget
        {
            get => controlMode == ControlMode.RelativeToTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Score: presentation & AI debug
        [CreateProperty]
        public float armorScoreProp => EvaluateArmorScore();

        [CreateProperty]
        public float survivabilityProp => EvaluateSurvivability();

        [CreateProperty]
        public float batteryFirepowerProp => EvaluateBatteryFirepowerScore();

        [CreateProperty]
        public float torpedoThreatScoreProp => EvaluateTorpedoThreatScore();

        [CreateProperty]
        public float rapidFiringFirepowerProp => EvaluateRapidFiringFirepowerScore();

        [CreateProperty]
        public float firepoweScoreProp => EvaluateFirepowerScore();

        [CreateProperty]
        public float generalScoreProp => EvaluateGeneralScore();

        [CreateProperty]
        public float firepowerBowProp => EvaluateBowFirepowerScore();

        [CreateProperty]
        public float firepowerStarboardProp => EvaluateStarboardFirepowerScore();

        [CreateProperty]
        public float firepowerSternProp => EvaluateSternFirepowerScore();

        [CreateProperty]
        public float firepowerPortProp => EvaluatePortFirepowerScore();

        [CreateProperty]
        public Doctrine doctrineProp => doctrine;

        [CreateProperty]
        public string damagePointProgrssDesc => $"DP Progress {(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1")} Damage Tier: {GetDamageTier()}";

        [CreateProperty]
        public float maxSpeedKnotsProp => GetMaxSpeedKnots();

        [CreateProperty]
        public float minSpeedKnotsProp => GetMinSpeedKnots();

        [CreateProperty]
        public int damageControlRatingProp => GetDamageControlRating();

        public string GetMapStatePrefix()
        {
            return mapState switch
            {
                MapState.NotDeployed => "_",
                MapState.Deployed => "",
                MapState.Destroyed => "+",
                _ => "Unknown"
            };
        }

        [CreateProperty]
        public string labelName => GetMapStatePrefix() + (namedShip?.name?.GetMergedName() ?? "[Named Ship not invalid or not specified]");

        // IPortraitViewerObservable
        string IPortraitViewerObservable.GetPortraitTopCode() => shipClass.portraitTopCode;
        Country IPortraitViewerObservable.GetCountry() => shipClass.country;
        GlobalString IPortraitViewerObservable.GetName() => namedShip?.name;
        bool IPortraitViewerObservable.IsShowArrow() => GetEffectiveControlMode() == ControlMode.Independent;
        string IPortraitViewerObservable.GetAcronym() => shipClass.GetAcronym();
        float IPortraitViewerObservable.GetDesiredHeadingDeg() => desiredHeadingDeg;
    }

    public partial class FireControlSystemStatusRecord
    {
        [CreateProperty]
        public string info => $"FCS #{GetSubIndex() + 1}";

        [CreateProperty]
        public string targetDesc => GetTarget()?.namedShip?.name.mergedName ?? "[Not Specified]";
    }

    public partial class BatteryStatus
    {
        [CreateProperty]
        public BatteryRecord batteryRecord
        {
            get => GetBatteryRecord();
        }
    }

    public partial class GlobalString
    {
        [CreateProperty]
        public string mergedName
        {
            get => GetMergedName();
        }
    }

    public partial class AbstractMountStatusRecord
    {
        [CreateProperty]
        public string firingTargetDesc
        {
            get => GetFiringTarget()?.namedShip?.name?.GetMergedName() ?? "[Not Specified]";
        }
    }

    public partial class MountStatusRecord
    {
        [CreateProperty]
        public MountLocationRecordInfo mountLocationRecordInfo
        {
            get => GetMountLocationRecordInfo();
        }

        [CreateProperty]
        public MountLocation mountLocation
        {
            get => mountLocationRecordInfo?.record?.mountLocation ?? MountLocation.NotSpecified;
        }

        [CreateProperty]
        public string mountLocationRecordSummary
        {
            get
            {
                // var r = mountLocationRecordInfo?.record;
                // if (r == null)
                //     return "Invalid";
                // return $"{r.mounts}x{r.barrels} {r.mountLocation}";
                return GetMountLocationRecordInfo()?.Summary() ?? "Invalid";
            }
        }
    }

    public partial class TorpedoMountStatusRecord
    {
        [CreateProperty]
        public MountLocationRecordInfo torpedoMountLocationRecordInfo
        {
            get => GetTorpedoMountLocationRecordInfo();
            // get => GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
        }

        [CreateProperty]
        public string torpedoMountLocationRecordSummary
        {
            get
            {
                // var r = torpedoMountLocationRecordInfo?.record;
                // if (r == null)
                //     return "Invalid";
                // return $"{r.mounts}x{r.barrels} {r.mountLocation}";
                return GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
            }
        }

        [CreateProperty]
        public MountLocation torpedoMountLocation
        {
            get => torpedoMountLocationRecordInfo?.record?.mountLocation ?? MountLocation.NotSpecified;
        }
    }

    public partial class RapidFiringStatus
    {
        [CreateProperty]
        public RapidFireBatteryRecord rapidFireBatteryRecord
        {
            get => GetRapidFireBatteryRecord();
        }

        [CreateProperty]
        public string info
        {
            get
            {
                return GetInfo();
            }
        }
    }

    public partial class ShipGroup
    {
        [CreateProperty]
        public Leader leaderProp => leader;

        [CreateProperty]
        public Doctrine doctrineProp => doctrine;
    }

    public partial class NamedShip
    {
        [CreateProperty]
        public Leader defaultLeaderProp => defaultLeader;

        [CreateProperty]
        public StyleBackground defaultLeaderStyleBackground
        {
            get
            {
                var leader = EntityManager.Instance.Get<Leader>(defaultLeaderObjectId);
                if (leader == null)
                    return null;
                return ResourceManager.GetLeaderPortraitSB(leader.portraitCode);
            }
        }

        [CreateProperty]
        public StyleBackground shipClassPortraitStyleBackground
        {
            get
            {
                var shipClass = EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
                var portraitCode = shipClass?.portraitCode;
                if (portraitCode == null)
                    return null;
                return ResourceManager.GetShipPortraitSB(portraitCode);
            }
        }

        [CreateProperty]
        public StyleBackground shipClassTopPortraitStyleBackground
        {
            get
            {
                var portraitCode = EntityManager.Instance.Get<ShipClass>(shipClassObjectId)?.portraitTopCode;
                if (portraitCode == null)
                    return null;
                return ResourceManager.GetShipPortraitSB(portraitCode);
            }
        }

        [CreateProperty]
        public Country shipClassCountry
        {
            get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId)?.country ?? Country.General;
        }

        [CreateProperty]
        public string shipClassDesc
        {
            get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId)?.name.mergedName ?? "[Not Specified]";
        }
    }

    public partial class RapidFiringTargettingStatus
    {
        [CreateProperty]
        public string targetDesc
        {
            get => GetTarget()?.namedShip?.name?.GetMergedName() ?? "[Not Specified or Invalid]";
        }
    }

    public partial class SubState
    {
        [CreateProperty]
        public string description => Describe();

        [CreateProperty]
        public bool damageControllableProp => damageControllable;
    }

    public partial class UnitModule
    {
        [CreateProperty]
        public List<SubState> subStatesDownward => GetSubStatesDownward().ToList();
    }
}