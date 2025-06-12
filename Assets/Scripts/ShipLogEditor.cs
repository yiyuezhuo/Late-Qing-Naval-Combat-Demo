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

    public partial class MountStatusRecord
    {
        [CreateProperty]
        public string firingTargetDesc
        {
            get => GetFiringTarget()?.namedShip?.name?.GetMergedName() ?? "[Not Specified]";
        }

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

        [CreateProperty]
        public MountLocationRecordInfo torpedoMountLocationRecordInfo
        {
            get => GetTorpedoMountLocationRecordInfo();
            // get => GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
        }

        [CreateProperty]
        public MountLocation torpedoMountLocation
        {
            get => torpedoMountLocationRecordInfo?.record?.mountLocation ?? MountLocation.NotSpecified;
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
            var batteryStatusElement = batteryStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(batteryStatusElement);

            var mountStatusMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("MountStatusMultiColumnListView");
            Utils.BindItemsAddedRemoved<MountStatusRecord>(mountStatusMultiColumnListView, () =>
            {
                var ctx = batteryStatusElement.GetHierarchicalDataSourceContext(); // 
                var isSucc = PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out NavalCombatCore.BatteryStatus bs);

                return bs;
            }); // TODO: Not always valid?

            var firingTargetColumn = mountStatusMultiColumnListView.columns["firingTarget"];
            firingTargetColumn.makeCell = () =>
            {
                var el = firingTargetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    var ctx = setButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                    {
                        GameManager.Instance.selectedMountStatusRecordObjectId = mountStatus.objectId;
                        GameManager.Instance.state = GameManager.State.SelectingFiringTarget;
                        Hide();
                    }
                };

                return el;
            };

            var detailColumn = mountStatusMultiColumnListView.columns["detail"];
            detailColumn.makeCell = () =>
            {
                var el = detailColumn.cellTemplate.CloneTree();

                var detailButton = el.Q<Button>("DetailButton");
                detailButton.clicked += () =>
                {
                    var ctx = detailButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                    {
                        // Debug.Log($"Detail Invoke: {mountStatus.objectId}");

                        DialogRoot.Instance.PopupMessageDialog(mountStatus.DescribeDetail(), "Mount Detail");
                    }
                };

                return el;
            };

            var fireControlSystemMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("FireControlSystemMultiColumnListView");
            Utils.BindItemsAddedRemoved<FireControlSystemStatusRecord>(
                fireControlSystemMultiColumnListView,
                Utils.MakeDynamicResolveProvider<NavalCombatCore.BatteryStatus>(batteryStatusElement)
            );

            var targetColumn = fireControlSystemMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();
                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out FireControlSystemStatusRecord r))
                    {
                        GameManager.Instance.selectedFireControlSystemStatusRecordObjectId = r.objectId;
                        GameManager.Instance.state = GameManager.State.SelectingFireControlSystemTarget;
                        Hide();
                    }
                };
                return el;
            };

            var batteryDetailButton = batteryStatusElement.Q<Button>("BatteryDetailButton");
            batteryDetailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(batteryDetailButton, out NavalCombatCore.BatteryStatus batteryStatus))
                {
                    DialogRoot.Instance.PopupMessageDialog(batteryStatus.DescribeDetail(), "Battery Detail");
                }
            };

            return batteryStatusElement;
        };

        var torpedoMountStatusMultiColumnListView = root.Q<MultiColumnListView>("TorpedoMountStatusMultiColumnListView");
        Utils.BindItemsAddedRemoved<MountStatusRecord>(torpedoMountStatusMultiColumnListView, () =>
        {
            return GameManager.Instance.selectedShipLog;
        });
        var torpedoMountStatusFiringTargetColumn = torpedoMountStatusMultiColumnListView.columns["firingTarget"];
        torpedoMountStatusFiringTargetColumn.makeCell = () =>
        {
            var el = torpedoMountStatusFiringTargetColumn.cellTemplate.CloneTree();

            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                var ctx = setButton.GetHierarchicalDataSourceContext();
                PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord torpedoMountStatus);
                Debug.Log(torpedoMountStatus);
            };

            return el;
        };

        // var rapidFiringStatusMultiColumnListView = root.Q<MultiColumnListView>("RapidFiringStatusMultiColumnListView");
        // Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusMultiColumnListView, () =>
        // {
        //     return GameManager.Instance.selectedShipLog;
        // });

        var rapidFiringStatusListView = root.Q<ListView>("RapidFiringStatusListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusListView, () => GameManager.Instance.selectedShipLog);
        rapidFiringStatusListView.makeItem = () =>
        {
            var el = rapidFiringStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);

            var detailButton = el.Q<Button>("DetailButton");
            detailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringStatus r))
                {
                    DialogRoot.Instance.PopupMessageDialog(r.DescribeDetail());
                }
            };

            var rapidFiringTargettingStatusMultiColumnListView = el.Q<MultiColumnListView>("RapidFiringTargettingStatusMultiColumnListView");

            Utils.BindItemsAddedRemoved<RapidFiringTargettingStatus>(
                rapidFiringTargettingStatusMultiColumnListView,
                Utils.MakeDynamicResolveProvider<RapidFiringStatus>(el)
            );

            var targetColumn = rapidFiringTargettingStatusMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringTargettingStatus r))
                    {
                        GameManager.Instance.selectedRapidFiringTargettingStatus = r;
                        GameManager.Instance.state = GameManager.State.SelectingRapidFiringTarget;
                        Hide();
                    }
                };

                return el;
            };

            return el;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipLogsToXML();
            IOManager.Instance.SaveTextFile(content, "ShipLogs" + GameManager.scenarioSuffix, "xml");
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