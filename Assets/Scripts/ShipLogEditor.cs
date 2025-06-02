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
        public float batteryFirepowerProp => EvaluateBatteryFirepower();

        [CreateProperty]
        public float torpedoThreatScoreProp => EvaluateTorpedoThreatScore();

        [CreateProperty]
        public float rapidFiringFirepowerProp => EvaluateRapidFiringFirepower();

        [CreateProperty]
        public float firepoweScoreProp => EvaluateFirepowerScore();

        [CreateProperty]
        public float generalScoreProp => EvaluateGeneralScore();
    }

    public partial class ShipLog
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
            get => followedTarget?.namedShip?.name.mergedName ?? "[Not Specified]";
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
            get => relativeToTarget?.namedShip?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfControlModeIsRelativeToTarget
        {
            get => controlMode == ControlMode.RelativeToTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }
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

    public partial class MountStatusRecord
    {
        [CreateProperty]
        public MountLocationRecord mountLocationRecord
        {
            get => GetMountLocationRecord();
        }

        [CreateProperty]
        public MountLocation mountLocation
        {
            get => mountLocationRecord?.mountLocation ?? MountLocation.NotSpecified;
        }

        [CreateProperty]
        public string mountLocationRecordSummary
        {
            get
            {
                var r = mountLocationRecord;
                if (r == null)
                    return "Invalid";
                return $"{r.mounts}x{r.barrels} {r.mountLocation}";
            }
        }

        [CreateProperty]
        public MountLocationRecord torpedoMountLocationRecord
        {
            get => GetTorpedoMountLocationRecord();
        }

        [CreateProperty]
        public MountLocation torpedoMountLocation
        {
            get => torpedoMountLocationRecord?.mountLocation ?? MountLocation.NotSpecified;
        }

        [CreateProperty]
        public string torpedoMountLocationRecordSummary
        {
            get
            {
                var r = torpedoMountLocationRecord;
                if (r == null)
                    return "Invalid";
                return $"{r.mounts}x{r.barrels} {r.mountLocation}";
            }
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
}


public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    public VisualTreeAsset shipClassSelectorDialogDocument;
    public ListView shipLogListView;

    protected override void Awake()
    {
        base.Awake();

        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"ShipLogEditor sortingOrder={sortingOrder}");

        root.dataSource = GameManager.Instance;

        // foreach (var listView in root.Query<BaseListView>().ToList())
        // {
        //     listView.SetBinding("itemsSource", new DataBinding());
        // }
        Utils.BindItemsSourceRecursive(root);

        shipLogListView = root.Q<ListView>("ShipLogListView");
        // shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);
        Utils.BindItemsAddedRemoved<ShipLog>(shipLogListView, () => null);

        // shipLogListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        // {
        //     var idx = ints.FirstOrDefault();
        //     GameManager.Instance.selectedShipLogIndex = idx;
        // };

        shipLogListView.selectionChanged += (IEnumerable<object> objs) =>
        {
            var shipLog = objs.FirstOrDefault() as ShipLog;
            GameManager.Instance.selectedShipLogObjectId = shipLog.objectId;
        };

        var batteryStatusListView = root.Q<ListView>("BatteryStatusListView");
        Utils.BindItemsAddedRemoved<NavalCombatCore.BatteryStatus>(batteryStatusListView, () => GameManager.Instance.selectedShipLog);
        // MountStatusMultiColumnListView
        batteryStatusListView.makeItem = () =>
        {
            var el = batteryStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var mountStatusMultiColumnListView = el.Q<MultiColumnListView>("MountStatusMultiColumnListView");
            Utils.BindItemsAddedRemoved<MountStatusRecord>(mountStatusMultiColumnListView, () =>
            {
                var parent = mountStatusMultiColumnListView.parent; // FIXME: ugly hack
                var templateContainer = parent.parent.parent;
                var idx = templateContainer.parent.IndexOf(templateContainer);
                var sourceList = batteryStatusListView.itemsSource;
                return sourceList[idx];
                // return batteryStatusListView.selectedItem;
            }); // TODO: Not always valid?

            return el;
        };

        var torpedoMountStatusMultiColumnListView = root.Q<MultiColumnListView>("TorpedoMountStatusMultiColumnListView");
        Utils.BindItemsAddedRemoved<MountStatusRecord>(torpedoMountStatusMultiColumnListView, () =>
        {
            return GameManager.Instance.selectedShipLog;
        });

        var rapidFiringStatusMultiColumnListView = root.Q<MultiColumnListView>("RapidFiringStatusMultiColumnListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusMultiColumnListView, () =>
        {
            return GameManager.Instance.selectedShipLog;
        });

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipLogsToXML();
            IOManager.Instance.SaveTextFile(content, "ShipLogs" + GameManager.scenarioSuffex, "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnShipLogsXmlLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var setNamedShipButton = root.Q<Button>("SetNamedShipButton");
        setNamedShipButton.clicked += DialogRoot.Instance.PopupNamedShipSelctorDialogForShipLog;

        var resetDamageExpenditureStateButton = root.Q<Button>("ResetDamageExpenditureStateButton");
        resetDamageExpenditureStateButton.clicked += () =>
        {
            var selectedShipLog = GameManager.Instance.selectedShipLog;
            if (selectedShipLog == null)
                return;
            selectedShipLog.ResetDamageExpenditureState();
        };

        var gotoNamedShipButton = root.Q<Button>("GotoNamedShipButton");
        gotoNamedShipButton.clicked += () =>
        {
            var namedShip = GameManager.Instance.selectedShipLog?.namedShip;
            if (namedShip == null)
                return;
            var idx = NavalGameState.Instance.namedShips.IndexOf(namedShip);
            if (idx != -1)
            {
                Hide();
                NamedShipEditor.Instance.Show();
                NamedShipEditor.Instance.namedShipListView.SetSelection(idx);
            }
        };

        var resetAllStatesButton = root.Q<Button>("ResetAllStatesButton");
        resetAllStatesButton.clicked += () =>
        {
            foreach (var shipLog in NavalGameState.Instance.shipLogs)
            {
                shipLog.ResetDamageExpenditureState();
            }
        };
    }

    void OnShipLogsXmlLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        GameManager.Instance.navalGameState.ShipLogsFromXML(text);
    }

    public void PopupWithSelection(ShipLog shipLog)
    {
        var idx = NavalGameState.Instance.shipLogs.IndexOf(shipLog);
        if(shipLog != null && idx != -1)
        {
            Show();
            shipLogListView.SetSelection(idx);
        }
    }
}