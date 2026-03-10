using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;
using System;

using CoreUtils;
using NavalCombatCore;


public class OOBEditor : HideableDocument<OOBEditor>
{
    const string TreeDragObjectIdsDataKey = "OOBEditor.TreeDragObjectIds";
    const string ShipGroupRemarkTextFieldName = "ShipGroupRemarkTextField";

    public TreeView oobTreeView;

    // IShipGroupMember currentSelectedGroupMember;
    string currentSelectedObjectId;

    [CreateProperty]
    public ShipGroup currentSelectedShipGroup
    {
        // get => currentSelectedGroupMember as ShipGroup;
        get => EntityManager.Instance.Get<ShipGroup>(currentSelectedObjectId);
    }

    [CreateProperty]
    public ShipLog currentSelectedShipLog
    {
        // get => currentSelectedGroupMember as ShipLog;
        get => EntityManager.Instance.Get<ShipLog>(currentSelectedObjectId);
    }

    [CreateProperty]
    public bool currentSelectedObjectTakeCommand
    {
        get => GameManager.Instance.takeCommandIdSet.Contains(currentSelectedObjectId);
        set
        {
            if(currentSelectedObjectTakeCommand != value)
            {
                var gmr = GameManager.Instance;
                if (value)
                {
                    gmr.takeCommandIdSet.Add(currentSelectedObjectId);
                }
                else
                {
                    if(gmr.takeCommandIdSet.Contains(currentSelectedObjectId))
                    {
                        gmr.takeCommandIdSet.Remove(currentSelectedObjectId);
                    }
                }

                // Send Command
                gmr.DoSubmitTakeCommand();
            }
        }
    }

    [CreateProperty]
    public bool currentSelectedAny => currentSelectedObjectId != null;

    public enum State
    {
        Idle,
        Attaching
    }
    public State state = State.Idle;

    Dictionary<int, string> treeViewIdxToObjectId = new();
    Dictionary<string, int> objectidToTreeViewIdx = new();

    bool linkRegistered = false;

    // protected override void Awake()
    void OnEnable()
    {
        // base.Awake();

        NavalGameState.Instance.shipGroupsChanged -= OnShipGroupsChanged;
        NavalGameState.Instance.shipGroupsChanged += OnShipGroupsChanged;
        GamePreference.Instance.shortLabelLanguageTypeChanged -= OnShortLabelLanguageTypeChanged;
        GamePreference.Instance.shortLabelLanguageTypeChanged += OnShortLabelLanguageTypeChanged;

        root.dataSource = this;

        oobTreeView = root.Q<TreeView>("OOBTreeView");
        oobTreeView.selectionType = SelectionType.Single;
        oobTreeView.reorderable = true;

        // RegisterLinkTag
        // ShipLogNameLinkLabel
        if(!linkRegistered)
        {
            linkRegistered = true;
        }

        var namedShipLabel = root.Q<Label>("ShipLogNameLinkLabel"); // Open ShipLog or NamedShip??
        Utils.RegisterLinkTag(namedShipLabel, new()
        {
            {"namedShip", () => {
                if(Utils.TryResolveCurrentValueForBinding<ShipLog>(namedShipLabel, out var shipLog))
                {
                    SwitchCenter.Instance.SwitchToShipLogView(shipLog);
                }
            } }
        });

        oobTreeView.makeItem = () =>
        {
            var el = oobTreeView.itemTemplate.CloneTree();
            return el;
        };
        oobTreeView.bindItem = (e, i) =>
        {
            var item = oobTreeView.GetItemDataForIndex<string>(i);

            var label = e.Q<Label>();
            label.dataSource = item;
        };
        oobTreeView.canStartDrag -= OnTreeCanStartDrag;
        oobTreeView.setupDragAndDrop -= OnTreeSetupDragAndDrop;
        oobTreeView.dragAndDropUpdate -= OnTreeDragAndDropUpdate;
        oobTreeView.handleDrop -= OnTreeHandleDrop;
        oobTreeView.canStartDrag += OnTreeCanStartDrag;
        oobTreeView.setupDragAndDrop += OnTreeSetupDragAndDrop;
        oobTreeView.dragAndDropUpdate += OnTreeDragAndDropUpdate;
        oobTreeView.handleDrop += OnTreeHandleDrop;

        oobTreeView.selectionChanged += (selectedItems) =>
        {
            var newSelectedObjectId = selectedItems.FirstOrDefault() as string;

            if (state == State.Attaching)
            {
                state = State.Idle;

                if (currentSelectedObjectId != null && newSelectedObjectId != null)
                {
                    var currentSelectedGroupMember = EntityManager.Instance.Get<IShipGroupMember>(currentSelectedObjectId);
                    var newSelectedGroupMember = EntityManager.Instance.Get<ShipGroup>(newSelectedObjectId);

                    if (newSelectedGroupMember != null && currentSelectedGroupMember != null)
                    {
                        if (currentSelectedGroupMember.TryAttachTo(newSelectedGroupMember))
                        {
                            Sync();
                        }
                        else
                        {
                            Debug.LogWarning("Not attachable"); // TODO: raise notification?
                        }
                    }

                }
            }

            currentSelectedObjectId = newSelectedObjectId;
            RefreshSelectedGroupRemarkPreview();
        };

        var createGroupButton = root.Q<Button>("CreateGroupButton");
        var deleteGroupButton = root.Q<Button>("DeleteGroupButton");
        var attachButton = root.Q<Button>("AttachButton");
        var confirmButton = root.Q<Button>("ConfirmButton");
        var importButton = root.Q<Button>("ImportButton");
        var exportButton = root.Q<Button>("ExportButton");
        var expandButton = root.Q<Button>("ExpandButton");
        var collapseButton = root.Q<Button>("CollapseButton");
        var addShipButton = root.Q<Button>("AddShipButton");
        var removeShipButton = root.Q<Button>("RemoveShipButton");
        var repairButton = root.Q<Button>("RepairButton");
        var editRemarkButton = root.Q<Button>("EditRemarkButton");

        expandButton.clicked += () => oobTreeView.ExpandAll();
        collapseButton.clicked += () => oobTreeView.CollapseAll();

        exportButton.clicked += () =>
        {
            // var content = GameManager.Instance.navalGameState.ShipGroupsToXML();
            var content = NavalGameState.Instance.ShipGroupsToXML();
            // IOManager.Instance.SaveTextFile(content, "ShipGroups" + GameManager.scenarioSuffix, "xml");
            IOManager.Instance.SaveTextFile(content, "ShipGroups.xml", "xml");
        };

        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnRootShipGroupsXmlLoaded;
            IOManager.Instance.LoadTextFile(OnRootShipGroupsXmlLoaded, "xml");
        };

        confirmButton.clicked += Hide;
        createGroupButton.clicked += () =>
        {
            var group = new ShipGroup();
            EntityManager.Instance.Register(group, null); // Use parent defined in EntityManager to denote parent?
            // NavalGameState.Instance.rootShipGroups.Add(group);
            NavalGameState.Instance.shipGroups.Add(group);

            Sync();
        };
        deleteGroupButton.clicked += () =>
        {
            var shipGroup = EntityManager.Instance.Get<ShipGroup>(currentSelectedObjectId);
            // var shipGroup = currentSelectedGroupMember as ShipGroup;
            if (shipGroup != null)
            {
                var parentGroup = (shipGroup as IShipGroupMember).GetParentGroup();
                if (parentGroup != null)
                {
                    parentGroup.childrenObjectIds.Remove(shipGroup.objectId);
                }
                foreach (var child in shipGroup.GetChildren())
                {
                    child.parentObjectId = null;
                }

                NavalGameState.Instance.shipGroups.Remove(shipGroup);

                EntityManager.Instance.Unregister(shipGroup);

                // ResetAndRegisterAll
                NavalGameState.Instance.SyncShipLogParentWithGroupHierarchy();
            }
            else
            {
                Debug.LogWarning("Not deletable");
            }
            Sync();
        };

        attachButton.clicked += () =>
        {
            state = State.Attaching;
        };

        addShipButton.clicked += () =>
        {
            Debug.Log("addShipButton.clicked");
            if (currentSelectedShipGroup != null)
            {
                DialogRoot.Instance.PopupShipLogSelectorDialogForAddShipLogToOOBItem();
            }
        };

        removeShipButton.clicked += () =>
        {
            Debug.Log("removeShipButton.clicked");

            if (currentSelectedShipLog != null)
            {
                ((IShipGroupMember)currentSelectedShipLog).AttachTo(null);
                Sync();
            }
            // else
            // {
            //     var obj = EntityManager.Instance.Get<object>(currentSelectedObjectId);
            //     if (obj == null)
            //     {

            //     }
            // }
        };

        editRemarkButton.clicked += () =>
        {
            if (currentSelectedShipGroup != null)
            {
                DialogRoot.Instance.PopupShipGroupRemarkDialog(currentSelectedShipGroup, RefreshSelectedGroupRemarkPreview);
            }
        };

        var setLeaderButton = root.Q<Button>("SetLeaderButton");
        setLeaderButton.clicked += DialogRoot.Instance.PopupLeaderSelectorDialogForSpecifyForGroup;

        repairButton.clicked += () =>
        {
            foreach (var shipGroup in NavalGameState.Instance.shipGroups)
            {
                foreach (var childObjectId in shipGroup.childrenObjectIds.ToList())
                {
                    var member = EntityManager.Instance.Get<IShipGroupMember>(childObjectId);
                    if (member == null)
                    {
                        shipGroup.childrenObjectIds.Remove(childObjectId);
                    }
                }
            }

            Sync();
        };

        RefreshSelectedGroupRemarkPreview();
    }

    void OnDisable()
    {
        if (NavalGameState.Instance != null)
        {
            NavalGameState.Instance.shipGroupsChanged -= OnShipGroupsChanged;
        }

        GamePreference.Instance.shortLabelLanguageTypeChanged -= OnShortLabelLanguageTypeChanged;
    }

    void OnShortLabelLanguageTypeChanged(object sender, EventArgs args)
    {
        RefreshSelectedGroupRemarkPreview();
    }

    void RefreshSelectedGroupRemarkPreview()
    {
        var remarkTextField = root?.Q<TextField>(ShipGroupRemarkTextFieldName);
        if (remarkTextField == null)
            return;

        var value = currentSelectedShipGroup?.remark?.shortName ?? string.Empty;
        remarkTextField.SetValueWithoutNotify(value);
    }

    public void OnRootShipGroupsXmlLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnRootShipGroupsXmlLoaded;

        // GameManager.Instance.navalGameState.ShipGroupsFromXML(text);
        NavalGameState.Instance.ShipGroupsFromXML(text);

        // oobTreeView.ExpandAll();
        // oobTreeView.CollapseAll();
    }

    public void OnShipGroupsChanged(object sender, List<ShipGroup> rootShipGroups)
    {
        Sync();
    }

    public void Sync()
    {
        var treeViewRootItems = CreateTreeViewRootItems();
        oobTreeView.SetRootItems(treeViewRootItems);
        oobTreeView.Rebuild();

        oobTreeView.ExpandAll(); // Set Default behaviour?
        RefreshSelectedGroupRemarkPreview();
    }

    public EventHandler shown;

    protected override void OnShow()
    {
        Sync();

        shown?.Invoke(this, EventArgs.Empty);
    }

    List<TreeViewItemData<string>> CreateTreeViewRootItems() // Use List<string> (objectId based denoting?) However Tree Items is a volatile and temp so objectId and other lowered structure doesn't make a lot of senses. 
    {
        treeViewIdxToObjectId.Clear();

        // Collect Established groups
        var items = new List<TreeViewItemData<string>>();
        var idx = 0;

        var state = NavalGameState.Instance;
        // foreach (var group in state.rootShipGroups)
        foreach (var group in state.shipGroups.Where(g => g.parentObjectId == null))
        {
            var subItems = CreateTreeViewItemsForGroup(group, ref idx);

            var d = new TreeViewItemData<string>(idx, group.objectId, subItems);
            items.Add(d);
            treeViewIdxToObjectId[idx] = group.objectId;

            idx++;
        }

        objectidToTreeViewIdx = treeViewIdxToObjectId.ToDictionary(p => p.Value, p => p.Key);

        return items;
    }

    List<TreeViewItemData<string>> CreateTreeViewItemsForGroup(ShipGroup group, ref int idx)
    {
        var ret = new List<TreeViewItemData<string>>();

        foreach (var childrenObjectId in group.childrenObjectIds)
        {
            var childGroup = EntityManager.Instance.Get<ShipGroup>(childrenObjectId);
            if (childGroup != null)
            {
                var childGroupItems = CreateTreeViewItemsForGroup(childGroup, ref idx);
                ret.Add(new TreeViewItemData<string>(idx, childGroup.objectId, childGroupItems));
                treeViewIdxToObjectId[idx] = childGroup.objectId;

                idx++;
            }
            else // ShipLog or null
            {
                ret.Add(new TreeViewItemData<string>(idx, childrenObjectId));
                treeViewIdxToObjectId[idx] = childrenObjectId;

                idx++;
            }
        }
        return ret;
    }

    public void TrySetSelection(string objectId)
    {
        if(objectidToTreeViewIdx.TryGetValue(objectId, out int treeViewIdx))
        {
            oobTreeView.SetSelectionById(treeViewIdx);
            oobTreeView.ScrollToItemById(treeViewIdx);
        }
    }

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            state = State.Idle;
        }
    }

    bool OnTreeCanStartDrag(CanStartDragArgs args)
    {
        if (!isInEditMode)
            return false;

        var selectedIds = args.selectedIds?.ToList();
        if (selectedIds == null || selectedIds.Count != 1)
            return false;

        return TryGetObjectIdByTreeItemId(selectedIds[0], out _);
    }

    StartDragArgs OnTreeSetupDragAndDrop(SetupDragAndDropArgs args)
    {
        var draggedObjectIds = args.selectedIds
            .Where(treeItemId => TryGetObjectIdByTreeItemId(treeItemId, out _))
            .Select(treeItemId => treeViewIdxToObjectId[treeItemId])
            .Distinct()
            .ToList();

        var dragTitle = draggedObjectIds.Count == 1
            ? draggedObjectIds[0]
            : $"Move {draggedObjectIds.Count} nodes";
        var startDragArgs = new StartDragArgs(dragTitle, DragVisualMode.Move);
        startDragArgs.SetGenericData(TreeDragObjectIdsDataKey, draggedObjectIds);
        return startDragArgs;
    }

    DragVisualMode OnTreeDragAndDropUpdate(HandleDragAndDropArgs args)
    {
        return TryBuildDropRequest(args, out _) ? DragVisualMode.Move : DragVisualMode.Rejected;
    }

    DragVisualMode OnTreeHandleDrop(HandleDragAndDropArgs args)
    {
        if (!TryBuildDropRequest(args, out var request))
            return DragVisualMode.Rejected;

        if (!ApplyDropRequest(request))
            return DragVisualMode.Rejected;

        Sync();
        TrySetSelection(request.draggedObjectId);
        return DragVisualMode.Move;
    }

    bool TryBuildDropRequest(HandleDragAndDropArgs args, out DropRequest request)
    {
        request = default;
        if (!isInEditMode)
            return false;

        var draggedObjectIds = GetDraggedObjectIds(args.dragAndDropData).ToList();
        if (draggedObjectIds.Count != 1)
            return false;

        var draggedObjectId = draggedObjectIds[0];
        var draggedMember = EntityManager.Instance.Get<IShipGroupMember>(draggedObjectId);
        if (draggedMember == null)
            return false;

        if (!TryResolveDropParent(args.parentId, out var dropParent))
            return false;

        if (dropParent == null && draggedMember is ShipLog)
            return false;

        if (!draggedMember.IsAttachToAble(dropParent))
            return false;

        request = new DropRequest
        {
            draggedObjectId = draggedObjectId,
            draggedMember = draggedMember,
            dropParent = dropParent,
            dropChildIndex = args.childIndex
        };

        return true;
    }

    bool ApplyDropRequest(DropRequest request)
    {
        var member = request.draggedMember;
        var oldParent = member.GetParentGroup();
        var newParent = request.dropParent;

        if (oldParent != newParent)
        {
            member.AttachTo(newParent);
        }

        if (newParent != null)
        {
            MoveObjectIdInList(newParent.childrenObjectIds, request.draggedObjectId, request.dropChildIndex, oldParent == newParent);
            return true;
        }

        if (member is not ShipGroup group)
            return false;

        MoveRootGroupToIndex(group, request.dropChildIndex, oldParent == null);
        return true;
    }

    void MoveRootGroupToIndex(ShipGroup group, int targetRootIndex, bool fromRoot)
    {
        var allGroups = NavalGameState.Instance.shipGroups;
        if (!allGroups.Contains(group))
            return;

        if (targetRootIndex < 0)
        {
            targetRootIndex = int.MaxValue;
        }

        if (fromRoot)
        {
            var rootsBeforeMove = allGroups.Where(g => g.parentObjectId == null).ToList();
            var oldRootIndex = rootsBeforeMove.IndexOf(group);
            if (oldRootIndex >= 0 && targetRootIndex > oldRootIndex)
            {
                targetRootIndex--;
            }
        }

        allGroups.Remove(group);

        var rootsAfterRemove = allGroups.Where(g => g.parentObjectId == null).ToList();
        var insertRootIndex = Mathf.Clamp(targetRootIndex, 0, rootsAfterRemove.Count);
        if (insertRootIndex >= rootsAfterRemove.Count)
        {
            allGroups.Add(group);
        }
        else
        {
            var insertionIndex = allGroups.IndexOf(rootsAfterRemove[insertRootIndex]);
            if (insertionIndex < 0)
            {
                allGroups.Add(group);
            }
            else
            {
                allGroups.Insert(insertionIndex, group);
            }
        }
    }

    static void MoveObjectIdInList(List<string> objectIds, string objectId, int targetIndex, bool sameContainer)
    {
        var oldIndex = objectIds.IndexOf(objectId);
        if (oldIndex < 0)
            return;

        if (targetIndex < 0)
        {
            targetIndex = int.MaxValue;
        }

        if (sameContainer && targetIndex > oldIndex)
        {
            targetIndex--;
        }

        objectIds.RemoveAt(oldIndex);
        targetIndex = Mathf.Clamp(targetIndex, 0, objectIds.Count);
        objectIds.Insert(targetIndex, objectId);
    }

    IEnumerable<string> GetDraggedObjectIds(DragAndDropData dragAndDropData)
    {
        if (dragAndDropData != null)
        {
            var genericData = dragAndDropData.GetGenericData(TreeDragObjectIdsDataKey);
            if (genericData is IEnumerable<string> draggedObjectIds)
            {
                return draggedObjectIds.Where(id => !string.IsNullOrEmpty(id));
            }
        }

        return oobTreeView.selectedIds
            .Where(treeItemId => TryGetObjectIdByTreeItemId(treeItemId, out _))
            .Select(treeItemId => treeViewIdxToObjectId[treeItemId]);
    }

    bool TryResolveDropParent(int parentTreeItemId, out ShipGroup parentGroup)
    {
        parentGroup = null;

        if (parentTreeItemId < 0)
            return true;

        if (!TryGetObjectIdByTreeItemId(parentTreeItemId, out var parentObjectId))
            return false;

        parentGroup = EntityManager.Instance.Get<ShipGroup>(parentObjectId);
        return parentGroup != null;
    }

    bool TryGetObjectIdByTreeItemId(int treeItemId, out string objectId)
    {
        return treeViewIdxToObjectId.TryGetValue(treeItemId, out objectId);
    }

    struct DropRequest
    {
        public string draggedObjectId;
        public IShipGroupMember draggedMember;
        public ShipGroup dropParent;
        public int dropChildIndex;
    }
}
