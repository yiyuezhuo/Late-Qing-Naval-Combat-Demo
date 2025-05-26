using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

public class OOBEditor : HideableDocument<OOBEditor>
{
    public TreeView oobTreeView;

    IShipGroupMember currentSelectedGroupMember;

    [CreateProperty]
    public ShipGroup currentSelectedShipGroup
    {
        get => currentSelectedGroupMember as ShipGroup;
    }

    [CreateProperty]
    public ShipLog currentSelectedShipLog
    {
        get => currentSelectedGroupMember as ShipLog;
    }

    public enum State
    {
        Idle,
        Attaching
    }
    public State state = State.Idle;

    protected override void Awake()
    {
        base.Awake();

        NavalGameState.Instance.shipGroupsChanged -= OnShipGroupsChanged;
        NavalGameState.Instance.shipGroupsChanged += OnShipGroupsChanged;

        root.dataSource = this;

        oobTreeView = root.Q<TreeView>("OOBTreeView");

        // oobTreeView.makeItem = () => new Label();
        // oobTreeView.bindItem = (e, i) =>
        // {
        //     var item = oobTreeView.GetItemDataForIndex<IShipGroupMember>(i);
        //     var id = oobTreeView.GetIdForIndex(i);
        //     ((Label)e).text = item switch
        //     {
        //         ShipLog sl => sl.name.GetMergedName(),
        //         ShipGroup sg => sg.name.GetMergedName(),
        //         _ => "[not defined]"
        //     };
        // };

        oobTreeView.makeItem = () =>
        {
            var el = oobTreeView.itemTemplate.CloneTree();
            return el;
        };
        oobTreeView.bindItem = (e, i) =>
        {
            var item = oobTreeView.GetItemDataForIndex<IShipGroupMember>(i);

            var label = e.Q<Label>();
            label.dataSource = item.objectId;
        };

        // oobTreeView.Expand

        // oobTreeView.itemsChosen += (selectedItems) =>
        // {
        //     var selectedItem = selectedItems.FirstOrDefault();
        //     if (selectedItem != null)
        //     {
        //         currentSelectedGroupMember = selectedItem as IShipGroupMember;
        //         Debug.Log($"Items chosen: {selectedItem}");
        //     }
        // };

        oobTreeView.selectionChanged += (selectedItems) =>
        {
            var selectedItem = selectedItems.FirstOrDefault() as IShipGroupMember;

            if (state == State.Attaching)
            {
                state = State.Idle;

                if (currentSelectedGroupMember != null && (selectedItem is ShipGroup selectedShipGroup))
                {
                    if (currentSelectedGroupMember.TryAttachTo(selectedShipGroup))
                    {
                        Sync();
                    }
                    else
                    {
                        Debug.LogWarning("Not attachable"); // TODO: raise notification?
                    }
                }
            }

            currentSelectedGroupMember = selectedItem;
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

        expandButton.clicked += () => oobTreeView.ExpandAll();
        collapseButton.clicked += () => oobTreeView.CollapseAll();

        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipGroupsToXml();
            IOManager.Instance.SaveTextFile(content, "ShipGroups", "xml");
        };

        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnRootShipGroupsXmlLoaded;
            IOManager.Instance.LoadTextFile("xml");
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
            var shipGroup = currentSelectedGroupMember as ShipGroup;
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
        };
    }

    public void OnRootShipGroupsXmlLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnRootShipGroupsXmlLoaded;

        GameManager.Instance.navalGameState.ShipGroupsFromXml(text);

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
    }

    public override void OnShow()
    {
        Sync();
    }

    List<TreeViewItemData<IShipGroupMember>> CreateTreeViewRootItems() // Use List<string> (objectId based denoting?) However Tree Items is a volatile and temp so objectId and other lowered structure doesn't make a lot of senses. 
    {
        // Collect Estalished groups
        var items = new List<TreeViewItemData<IShipGroupMember>>();
        var idx = 0;

        var state = NavalGameState.Instance;
        // foreach (var group in state.rootShipGroups)
        foreach (var group in state.shipGroups.Where(g => g.parentObjectId == null))
        {
            var subItems = CreateTreeViewItemsForGroup(group, ref idx);
            var d = new TreeViewItemData<IShipGroupMember>(idx, group, subItems);
            idx++;
            items.Add(d);
        }

        // Collect un-grouped ships
        // foreach (var ship in state.shipLogs)
        // {
        //     var parent = (ship as IShipGroupMember).GetParentGroup();
        //     if (ship.parentObjectId == null)
        //     {
        //         var d = new TreeViewItemData<IShipGroupMember>(idx, ship);
        //         idx++;
        //         items.Add(d);
        //     }
        // }

        return items;
    }

    List<TreeViewItemData<IShipGroupMember>> CreateTreeViewItemsForGroup(ShipGroup group, ref int idx)
    {
        var ret = new List<TreeViewItemData<IShipGroupMember>>();
        foreach (var child in group.GetChildren())
        {
            if (child is ShipGroup childGroup)
            {
                var childGroupItems = CreateTreeViewItemsForGroup(childGroup, ref idx);
                ret.Add(new TreeViewItemData<IShipGroupMember>(idx, childGroup, childGroupItems));
                idx++;
            }
            if (child is ShipLog childShip)
            {
                ret.Add(new TreeViewItemData<IShipGroupMember>(idx, childShip));
                idx++;
            }
        }
        return ret;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            state = State.Idle;
        }   
    }
}