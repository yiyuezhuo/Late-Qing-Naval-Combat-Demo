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
                // var r = rapidFireBatteryRecord;
                // if (r == null)
                //     return "Not Valid";

                // var portClass = r.barrelsLevelPort.Count == 0 ? 0 : r.barrelsLevelPort[0];
                // var starboardClass = r.barrelsLevelStarboard.Count == 0 ? 0 : r.barrelsLevelStarboard[0];

                // var portCurrent = r.barrelsLevelPort.Count == 0 ? 0 : rapidFireBatteryRecord.barrelsLevelPort[Math.Min(portMountHits, rapidFireBatteryRecord.barrelsLevelPort.Count - 1)];
                // var starboardCurrent = r.barrelsLevelStarboard.Count == 0 ? 0 : rapidFireBatteryRecord.barrelsLevelStarboard[Math.Min(starboardMountHits, rapidFireBatteryRecord.barrelsLevelStarboard.Count - 1)];

                // return $"{portClass}({portCurrent}) / {starboardClass}({starboardCurrent}) {r.name.mergedName}";
            }
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
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(torpedoMountStatusMultiColumnListView, () =>
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
            IOManager.Instance.textLoaded += OnShipLogsXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var setShipClassButton = root.Q<Button>("SetShipClassButton");
        setShipClassButton.clicked += () =>
        {
            var el = shipClassSelectorDialogDocument.CloneTree();

            var shipClassListView = el.Q<ListView>("ShipClassListView");
            var confirmButton = el.Q<Button>("ConfirmButton");
            var cancelButton = el.Q<Button>("CancelButton");

            Utils.BindItemsSourceRecursive(el);

            root.Add(el);

            cancelButton.clicked += () =>
            {
                root.Remove(el);
            };
            confirmButton.clicked += () =>
            {
                root.Remove(el);

                var selectedShipLog = GameManager.Instance.selectedShipLog;
                var selectedShipClass = shipClassListView.selectedItem as ShipClass;
                if (selectedShipLog != null && selectedShipClass != null)
                {
                    selectedShipLog.shipClassObjectId = selectedShipClass.objectId;
                }
            };
            el.style.position = Position.Absolute;
            el.style.left = new Length(50, LengthUnit.Percent);
            el.style.top = new Length(50, LengthUnit.Percent);
            el.style.translate = new StyleTranslate(
                new Translate(
                    new Length(-50, LengthUnit.Percent),
                    new Length(-50, LengthUnit.Percent)
                )
            );
        };
    }

    void OnShipLogsXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipLogsXMLLoaded;

        GameManager.Instance.navalGameState.ShipLogsFromXml(text);
    }
}