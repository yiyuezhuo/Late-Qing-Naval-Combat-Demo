using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;

using NavalCombatCore;
using CoreUtils;
using System;
using YYZ;


public class ShipClassEditor : LeftObjectPickerRightEditor<ShipClassEditor, ShipClass>
{
    ListView batteryRecordsListView;
    VisualElement portraitTopPreview;
    VisualElement portraitIconPreview;
    VisualElement graphicTabContent;
    VisualElement sectorArcsTabContent;
    VisualElement batterySectorArcsContainer;
    Image defaultPlaceholderPreviewImage;
    Texture2D defaultPlaceholderPreviewTexture;
    string lastDefaultPlaceholderSignature;
    string lastDefaultPlaceholderShipObjectId;
    string lastSectorArcSignature;
    string lastSectorArcShipObjectId;

    SectorArcIndicatorBinder torpedoSectorArcIndicatorBinder = new();

    protected override string ObjectListViewElementName => "ShipClassListView";

    public ListView shipClassListView => objectListView;

    [CreateProperty]
    public ShipClass selectedShipClass => selectedObject;

    public ShipClass SelectedShipClassProvider()
    {
        return selectedObject;
    }

    // protected override void Awake()
    protected override void OnEnable()
    {
        base.OnEnable();

        torpedoSectorArcIndicatorBinder.BindUI(root.Q<VisualElement>("TorpedoSectorArcIndicator"));
        sectorArcsTabContent = root.Q<VisualElement>("SectorArcsTabContent");
        batterySectorArcsContainer = root.Q<VisualElement>("BatterySectorArcsContainer");
        sectorArcsTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestSectorArcRefresh());

        shipClassListView.selectionChanged += (objs) =>
        {
            // Debug.Log($"selectionChanged: {objs}");
            var currentShipClass = objs.FirstOrDefault() as ShipClass;
            if (currentShipClass != null)
            {
                Debug.Log($"currentShipClass: {currentShipClass}");
            }

            RequestSectorArcRefresh(currentShipClass, true);
            RequestDefaultPlaceholderPreviewRefresh(currentShipClass, true);
        };

        var speedIncreaseMultiColumnListView = root.Q<MultiColumnListView>("SpeedIncreaseMultiColumnListView");
        // speedIncreaseMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView);
        Utils.BindItemsAddedRemoved<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView, SelectedShipClassProvider);

        batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
        // batteryRecordsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<BatteryRecord>(batteryRecordsListView);
        Utils.BindItemsAddedRemoved<BatteryRecord>(batteryRecordsListView, SelectedShipClassProvider);
        batteryRecordsListView.makeItem = () =>
        {
            var el = batteryRecordsListView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(el);

            var fireControlTableMultiColumnListView = el.Q<MultiColumnListView>("FireControlTableMultiColumnListView");
            var penetrationTableMultiColumnListView = el.Q<MultiColumnListView>("PenetrationTableMultiColumnListView");
            var mountsListView = el.Q<ListView>("MountsListView");

            // fireControlTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<FireControlTableRecord>(fireControlTableMultiColumnListView);
            // penetrationTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<PenetrationTableRecord>(penetrationTableMultiColumnListView);
            // mountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(mountsListView);
            Utils.BindItemsAddedRemoved<FireControlTableRecord>(fireControlTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<PenetrationTableRecord>(penetrationTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<MountLocationRecord>(mountsListView, SelectedShipClassProvider);

            mountsListView.makeItem = () =>
            {
                var el2 = mountsListView.itemTemplate.CloneTree();

                var mountsArcsMultiColumnsListView = el2.Q<MultiColumnListView>("MountArcsMultiColumnListView");
                // mountsArcsMultiColumnsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountsArcsMultiColumnsListView);
                Utils.BindItemsAddedRemoved<MountArcRecord>(mountsArcsMultiColumnsListView, SelectedShipClassProvider);

                Utils.BindItemsSourceRecursive(el2);

                return el2;
            };

            return el;
        };

        var torpedoSettingsMultiColumnListView = root.Q<MultiColumnListView>("TorpedoSettingsMultiColumnListView");
        // torpedoSettingsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<TorpedoSetting>(torpedoSettingsMultiColumnListView);
        Utils.BindItemsAddedRemoved<TorpedoSetting>(torpedoSettingsMultiColumnListView, SelectedShipClassProvider);

        var torpedoMountsListView = root.Q<ListView>("TorpedoMountsListView");
        // torpedoMountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(torpedoMountsListView);
        Utils.BindItemsAddedRemoved<MountLocationRecord>(torpedoMountsListView, SelectedShipClassProvider);
        torpedoMountsListView.makeItem = () =>
        {
            var el = torpedoMountsListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var mountArcsMultiColumnListView = el.Q<MultiColumnListView>("MountArcsMultiColumnListView");
            // mountArcsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountArcsMultiColumnListView);
            Utils.BindItemsAddedRemoved<MountArcRecord>(mountArcsMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var rapidFireBatteryListView = root.Q<ListView>("RapidFireBatteryListView");
        // rapidFireBatteryListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryRecord>(rapidFireBatteryListView);
        Utils.BindItemsAddedRemoved<RapidFireBatteryRecord>(rapidFireBatteryListView, SelectedShipClassProvider);

        rapidFireBatteryListView.makeItem = () =>
        {
            var el = rapidFireBatteryListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var fireControlLevelMultiColumnListView = el.Q<MultiColumnListView>("FireControlLevelMultiColumnListView");
            // fireControlLevelMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView);
            Utils.BindItemsAddedRemoved<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipClassesXMLLoaded;
            IOManager.Instance.LoadTextFile(OnShipClassesXMLLoaded, "xml");
        };

        var exportSelectedBatteryButton = root.Q<Button>("ExportSelectedBatteryButton");
        var importToSelectedBatteryButton = root.Q<Button>("ImportToSelectedBatteryButton");

        exportSelectedBatteryButton.clicked += () =>
        {
            var battryRecord = batteryRecordsListView.selectedItem as BatteryRecord;
            if (battryRecord != null)
            {
                var content = battryRecord.ToXML();
                IOManager.Instance.SaveTextFile(content, "battery", "xml");
            }
        };

        importToSelectedBatteryButton.clicked += () =>
        {
            var idx = batteryRecordsListView.selectedIndex;
            if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
            {
                // IOManager.Instance.textLoaded += OnBatteryXMLLoaded;
                IOManager.Instance.LoadTextFile(OnBatteryXMLLoaded, "xml");
            }
        };

        var setSelectedByBatterySelectorButton = root.Q<Button>("SetSelectedByBatterySelectorButton");
        setSelectedByBatterySelectorButton.clicked += () =>
        {
            // Debug.Log("setSelectedByBatterySelectorButton clicked");

            DialogRoot.Instance.PopupBatteryRecordSelectorDialog(_batteryRecord =>
            {
                var batteryRecord = XmlUtils.FromXML<BatteryRecord>(XmlUtils.ToXML(_batteryRecord));
                ((IObjectIdLabeled)batteryRecord).ResetObjectId();

                var idx = batteryRecordsListView.selectedIndex;
                if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    batteryRecordsListView.itemsSource[idx] = batteryRecord;
                }
                else
                {
                    batteryRecordsListView.itemsSource.Add(batteryRecord);
                }

                var gameState = SuperGameState.Instance.GetCurrentGameState();
                gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };

        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitTopReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitIconReferenceField"));
        portraitTopPreview = root.Q<VisualElement>("PortraitTopPreview");
        portraitIconPreview = root.Q<VisualElement>("PortraitIconPreview");
        graphicTabContent = root.Q<VisualElement>("GraphicTabContent");
        defaultPlaceholderPreviewImage = root.Q<Image>("DefaultPlaceholderPreviewImage");

        graphicTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestDefaultPlaceholderPreviewRefresh());

        root.Q<Button>("GeneratePlaceholderImageButton").clicked += () =>
        {
            if (selectedShipClass != null)
            {
                DialogRoot.Instance.PopupShipClassPlaceholderGeneratorDialog(selectedShipClass);
            }
        };

        root.Q<Button>("GeneratePlaceholderImageForAllPlaceholderButton").clicked += () =>
        {
            var placeholders = SuperGameState.Instance.GetCurrentGameState().shipClasses.Where(x => x.isGraphicPlaceholder).ToList();
            var count = placeholders.Count;
            if (count == 0)
            {
                DialogRoot.Instance.PopupMessageDialog("No ship class is marked as graphic placeholder.");
                return;
            }

            DialogRoot.Instance.PopupConfirmDialog(
                $"Generate placeholder images for {count} ship class? If confirm, {count} x 2 images would be generated in the game folder and binding would be reset to those image.\n\n Warning: This will modify files in the disk.",
                () =>
                {
                    var result = ShipClassPlaceholderImageGenerator.GenerateAndBindAllMarked(placeholders);
                    UnityWebRequestImageReader.Instance.Reset();
                    RefreshGraphicBindings();

                    var message = $"Generated placeholder images for {result.generatedShipClasses.Count} ship class.";
                    if (result.skippedMessages.Count > 0)
                    {
                        message += "\nSkipped:\n" + string.Join("\n", result.skippedMessages);
                    }
                    DialogRoot.Instance.PopupMessageDialog(message);
                });
        };

        var batteryArcIndicatorDialogButton = root.Q<Button>("BatteryArcIndicatorDialogButton");
        batteryArcIndicatorDialogButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(batteryArcIndicatorDialogButton, out ShipClass shipClass))
            {
                DialogRoot.Instance.PopupBatteryArcIndicatorDialog(shipClass);
            }
        };

        root.Q<Button>("SetSelectedByRapidFireBatterySelectorButton").clicked += () =>
        {
            Debug.Log("SetSelectedByRapidFireBatterySelectorButton clicked");

            DialogRoot.Instance.PopupRapidFireBatteryRecordSelectorDialog(_rapidFireBatteryRecord =>
            {
                var rapidFireBatteryRecord = XmlUtils.FromXML<RapidFireBatteryRecord>(XmlUtils.ToXML(_rapidFireBatteryRecord));
                // ((IObjectIdLabeled)rapidFireBatteryRecord).ResetObjectId();

                var idx = rapidFireBatteryListView.selectedIndex;
                if (idx >= 0 && idx < rapidFireBatteryListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    rapidFireBatteryListView.itemsSource[idx] = rapidFireBatteryRecord;
                }
                else
                {
                    rapidFireBatteryListView.itemsSource.Add(rapidFireBatteryRecord);
                }

                // var gameState = SuperGameState.Instance.GetCurrentGameState();
                // gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };
        
        var setByTorpedoSelectorButton = root.Q<Button>("SetByTorpedoSelectorButton");
        setByTorpedoSelectorButton.clicked += () =>
        {
            Debug.Log("SetByTorpedoSelectorButton clicked");

            DialogRoot.Instance.PopupTorpedoSectorSelectorDialog(_shipClass =>
            {
                var _torpedoSector = _shipClass.torpedoSector;
                var torpedoSector = XmlUtils.FromXML<TorpedoSector>(XmlUtils.ToXML(_torpedoSector));
                foreach (var mountLocationRecord in torpedoSector.mountLocationRecords)
                {
                    mountLocationRecord.objectId = null;
                }

                if(Utils.TryResolveCurrentValueForBinding<ShipClass>(setByTorpedoSelectorButton, out var shipClass))
                {
                    shipClass.torpedoSector = torpedoSector;
                    SuperGameState.Instance.GetCurrentGameState().ResetAndRegisterAll();
                }
            });
        };
    }

    void OnDisable()
    {
        ClearSectorArcState();
        DisposeDefaultPlaceholderPreviewTexture();
    }

    public EventHandler shown;
    public EventHandler hidden;

    protected override void OnShow()
    {
        RequestDefaultPlaceholderPreviewRefresh();
        shown?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnHidden()
    {
        ClearDefaultPlaceholderPreviewState();
        hidden?.Invoke(this, EventArgs.Empty);
    }

    public void OnBatteryXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnBatteryXMLLoaded;

        var idx = batteryRecordsListView.selectedIndex;
        if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
        {
            var battryRecord = BatteryRecord.FromXml(text);
            batteryRecordsListView.itemsSource[idx] = battryRecord;
        }

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ResetAndRegisterAll(); // re-duplicate object id // FIXME: Correctness is questionable though
    }

    public void OnShipClassesXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipClassesFromXML(text);
        gameState.ResetAndRegisterAll();
        GetFullObjects();
        RefreshFilter();
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, true);
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;

    protected override void GetFullObjects()
    {
        fullObjects = currentGameState.shipClasses;
    }

    protected override void ProcessRemovedOne(ShipClass removeObj)
    {
        EntityManager.Instance.Unregister(removeObj);
    }

    protected override void OnAddObjectButtonClicked()
    {
        var newObj = new ShipClass();
        EntityManager.Instance.Register(newObj, null);
        fullObjects.Add(newObj);

        ProcessAddedOne(newObj);

        RefreshFilter();
        SelectObject(newObj);
    }

    void RefreshGraphicBindings()
    {
        shipClassListView?.RefreshItems();
        if (selectedShipClass == null)
            return;

        RefreshPictureField(root.Q<VisualElement>("PortraitTopReferenceField"), selectedShipClass.portraitTopReference);
        RefreshPictureField(root.Q<VisualElement>("PortraitIconReferenceField"), selectedShipClass.portraitIconReference);

        if (portraitTopPreview != null)
            portraitTopPreview.style.backgroundImage = selectedShipClass.portraitTopReference.pictureStyleBackground;

        if (portraitIconPreview != null)
            portraitIconPreview.style.backgroundImage = selectedShipClass.portraitIconReference.pictureStyleBackground;

        RequestDefaultPlaceholderPreviewRefresh();
    }

    void RequestSectorArcRefresh(bool force = false)
    {
        RequestSectorArcRefresh(selectedShipClass, force);
    }

    void RequestSectorArcRefresh(ShipClass shipClass, bool force = false)
    {
        if (sectorArcsTabContent == null || batterySectorArcsContainer == null || !IsElementActuallyVisible(sectorArcsTabContent))
            return;

        if (shipClass == null)
        {
            ClearSectorArcState();
            return;
        }

        if (lastSectorArcShipObjectId != shipClass.objectId)
        {
            ClearSectorArcState();
            lastSectorArcShipObjectId = shipClass.objectId;
        }

        var signature = BuildSectorArcSignature(shipClass);
        if (!force && signature == lastSectorArcSignature)
            return;

        RebuildBatterySectorArcCards(shipClass);
        torpedoSectorArcIndicatorBinder.BindTorpedoData(shipClass);
        lastSectorArcShipObjectId = shipClass.objectId;
        lastSectorArcSignature = signature;
    }

    void RequestDefaultPlaceholderPreviewRefresh(bool force = false)
    {
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, force);
    }

    void RequestDefaultPlaceholderPreviewRefresh(ShipClass shipClass, bool force = false)
    {
        if (graphicTabContent == null || defaultPlaceholderPreviewImage == null || !IsElementActuallyVisible(graphicTabContent))
            return;

        if (shipClass == null)
        {
            ClearDefaultPlaceholderPreviewState();
            return;
        }

        if (lastDefaultPlaceholderShipObjectId != shipClass.objectId)
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        }

        var signature = ShipClassPlaceholderImageGenerator.BuildDefaultPreviewSignature(shipClass);
        if (!force && signature == lastDefaultPlaceholderSignature && defaultPlaceholderPreviewTexture != null)
            return;

        if (!ShipClassPlaceholderImageGenerator.TryRenderDefaultPreview(shipClass, out var renderResult))
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
            lastDefaultPlaceholderSignature = signature;
            return;
        }

        DisposeDefaultPlaceholderPreviewTexture();
        defaultPlaceholderPreviewTexture = renderResult.previewTexture;
        defaultPlaceholderPreviewImage.image = defaultPlaceholderPreviewTexture;
        lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        lastDefaultPlaceholderSignature = signature;

        if (renderResult.topTexture != null)
            Destroy(renderResult.topTexture);
        if (renderResult.iconTexture != null)
            Destroy(renderResult.iconTexture);
    }

    void RebuildBatterySectorArcCards(ShipClass shipClass)
    {
        batterySectorArcsContainer.Clear();

        if (shipClass?.batteryRecords == null)
            return;

        for (int i = 0; i < shipClass.batteryRecords.Count; i++)
        {
            batterySectorArcsContainer.Add(BuildBatterySectorArcCard(shipClass.batteryRecords[i], i));
        }
    }

    VisualElement BuildBatterySectorArcCard(BatteryRecord batteryRecord, int batteryIndex)
    {
        var card = new VisualElement();
        card.style.width = 220;
        card.style.minWidth = 220;
        card.style.marginRight = 8;
        card.style.marginBottom = 8;
        card.style.paddingTop = 6;
        card.style.paddingRight = 6;
        card.style.paddingBottom = 6;
        card.style.paddingLeft = 6;
        card.style.alignItems = Align.Center;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Color.black;
        card.style.borderRightColor = Color.black;
        card.style.borderBottomColor = Color.black;
        card.style.borderLeftColor = Color.black;

        var titleLabel = new Label(GetBatterySectorArcTitle(batteryRecord, batteryIndex));
        titleLabel.style.width = Length.Percent(100);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.marginBottom = 6;
        card.Add(titleLabel);

        var indicatorRoot = CreateSectorArcIndicatorLayout();
        var binder = new SectorArcIndicatorBinder();
        binder.BindUI(indicatorRoot);
        binder.BindBatteryData(batteryRecord);
        card.Add(indicatorRoot);

        return card;
    }

    VisualElement CreateSectorArcIndicatorLayout()
    {
        var indicatorRoot = new VisualElement();
        indicatorRoot.style.flexGrow = 0;
        indicatorRoot.style.alignItems = Align.Center;
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortForward", "Forward", "StarboardForward"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortMidship", "Midship", "StarboardMidship"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortAfter", "After", "StarboardAfter"));
        return indicatorRoot;
    }

    VisualElement CreateSectorArcIndicatorRow(params string[] indicatorNames)
    {
        var row = new VisualElement();
        row.style.flexGrow = 0;
        row.style.flexDirection = FlexDirection.Row;

        foreach (var indicatorName in indicatorNames)
        {
            var indicator = new BatteryArcIndicator();
            indicator.name = indicatorName;
            indicator.style.justifyContent = Justify.Center;
            row.Add(indicator);
        }

        return row;
    }

    void ClearSectorArcState()
    {
        batterySectorArcsContainer?.Clear();
        torpedoSectorArcIndicatorBinder.BindTorpedoData((ShipClass)null);
        lastSectorArcSignature = null;
        lastSectorArcShipObjectId = null;
    }

    string GetBatterySectorArcTitle(BatteryRecord batteryRecord, int batteryIndex)
    {
        var shortName = batteryRecord?.name?.GetShortName();
        return string.IsNullOrWhiteSpace(shortName) ? Localize("Battery {0}", batteryIndex + 1) : shortName;
    }

    string BuildSectorArcSignature(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        var batterySignature = string.Join(";",
            (shipClass.batteryRecords ?? new List<BatteryRecord>())
                .Select(batteryRecord => string.Join("~", new[]
                {
                    batteryRecord?.name?.GetShortName() ?? "",
                    BuildMountLocationSignature(batteryRecord?.mountLocationRecords)
                })));

        return string.Join("|", new[]
        {
            shipClass.objectId ?? "",
            batterySignature,
            BuildMountLocationSignature(shipClass.torpedoSector?.mountLocationRecords)
        });
    }

    static string BuildMountLocationSignature(IEnumerable<MountLocationRecord> mountLocationRecords)
    {
        return string.Join(";",
            (mountLocationRecords ?? Enumerable.Empty<MountLocationRecord>())
                .Select(record => string.Join(":", new[]
                {
                    record.mountLocation.ToString(),
                    BuildMountArcSignature(record.mountArcs)
                })));
    }

    static string BuildMountArcSignature(IEnumerable<MountArcRecord> mountArcs)
    {
        return string.Join(",",
            (mountArcs ?? Enumerable.Empty<MountArcRecord>())
                .Select(arc => $"{arc.startDeg:0.###}-{arc.CoverageDeg:0.###}"));
    }

    void ClearDefaultPlaceholderPreviewState()
    {
        DisposeDefaultPlaceholderPreviewTexture();
        lastDefaultPlaceholderSignature = null;
        lastDefaultPlaceholderShipObjectId = null;
    }

    void DisposeDefaultPlaceholderPreviewTexture()
    {
        if (defaultPlaceholderPreviewImage != null)
            defaultPlaceholderPreviewImage.image = null;

        if (defaultPlaceholderPreviewTexture != null)
        {
            Destroy(defaultPlaceholderPreviewTexture);
            defaultPlaceholderPreviewTexture = null;
        }
    }

    static bool IsElementActuallyVisible(VisualElement element)
    {
        return element != null
            && element.resolvedStyle.display != DisplayStyle.None
            && element.worldBound.width > 1f
            && element.worldBound.height > 1f;
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    static void RefreshPictureField(VisualElement fieldRoot, PictureReference pictureReference)
    {
        if (fieldRoot == null || pictureReference == null)
            return;

        var textField = fieldRoot.Q<TextField>();
        if (textField != null)
            textField.SetValueWithoutNotify(pictureReference.path);

        var toggle = fieldRoot.Q<Toggle>();
        if (toggle != null)
            toggle.SetValueWithoutNotify(pictureReference.isBuiltin);
    }
}
