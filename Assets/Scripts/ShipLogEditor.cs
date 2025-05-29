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
using System.Threading.Tasks;
using System;

namespace NavalCombatCore
{
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

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        // foreach (var listView in root.Query<BaseListView>().ToList())
        // {
        //     listView.SetBinding("itemsSource", new DataBinding());
        // }
        Utils.BindItemsSourceRecursive(root);

        var shipLogListView = root.Q<ListView>("ShipLogListView");
        // shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);
        Utils.BindItemsAddedRemoved<ShipLog>(shipLogListView, () => null);

        shipLogListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        {
            var idx = ints.FirstOrDefault();
            GameManager.Instance.selectedShipLogIndex = idx;
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
            IOManager.Instance.SaveTextFile(content, "ShipLogs", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnShipLogsXmlLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var setShipClassButton = root.Q<Button>("SetShipClassButton");
        setShipClassButton.clicked += () =>
        {
            var tempDialog = new TempDialog()
            {
                root = root,
                template = shipClassSelectorDialogDocument,
                templateDataSource = GameManager.Instance
            };

            tempDialog.onConfirmed += (sender, el) =>
            {
                var shipClassListView = el.Q<ListView>("ShipClassListView");

                var selectedShipLog = GameManager.Instance.selectedShipLog;
                var selectedShipClass = shipClassListView.selectedItem as ShipClass;
                if (selectedShipLog != null && selectedShipClass != null)
                {
                    selectedShipLog.shipClassObjectId = selectedShipClass.objectId;
                }
            };

            tempDialog.Popup();
        };

        var setLeaderButton = root.Q<Button>("SetCaptainButton");
        setLeaderButton.clicked += DialogRoot.Instance.PopupLeaderSelectorDialogForSpecifyForShipLog;
    }

    void OnShipLogsXmlLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        GameManager.Instance.navalGameState.ShipLogsFromXML(text);
    }
}