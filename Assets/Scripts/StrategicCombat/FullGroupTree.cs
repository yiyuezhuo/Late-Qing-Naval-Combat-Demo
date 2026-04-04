using System.Collections;
using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using UnityEngine.UIElements;
using StrategicCombatCore;
using UnityEngine;

public class FullGroupTree : ITree<IStrategicGroupMemberReferenceable, string>
{
    public IStrategicGroupMemberReferenceable GetParent(IStrategicGroupMemberReferenceable node)
    {
        return node.parentGroupReference.Get();
    }

    public IEnumerable<IStrategicGroupMemberReferenceable> GetChildren(IStrategicGroupMemberReferenceable node)
    {
        if (node is StrategicGroup group)
        {
            foreach (var obj in group.WalkDirectMembers())
            {
                yield return obj;
            }
        }
    }
    
    public string GetData(IStrategicGroupMemberReferenceable node)
    {
        return node switch
        {
            StrategicGroup group => group.name.GetMergedName(),
            LandUnit lu => lu.name.GetMergedName(),
            ShipLog ship => ship.namedShip.name.GetMergedName(),
            _ => "Unknown Item",
        };
    }
}

public class FullGroupTreeNameLink : ITree<IStrategicGroupMemberReferenceable, IStrategicGroupMemberReferenceable>
{
    static Leader GetLeader(IStrategicGroupMemberReferenceable node)
    {
        return node switch
        {
            StrategicGroup group => group.leaderReference.Get(),
            ShipLog ship => ship.leader,
            _ => null,
        };
    }

    public IStrategicGroupMemberReferenceable GetParent(IStrategicGroupMemberReferenceable node)
    {
        var parent = node.parentGroupReference.Get();
        while (parent is StrategicGroup group && group.type == StrategicGroup.Type.Base)
        {
            parent = parent.parentGroupReference.Get();
        }

        return parent;
    }

    public bool ShouldHideAsBaseRootDescendant(IStrategicGroupMemberReferenceable node)
    {
        var parent = node?.parentGroupReference?.Get();
        var skippedBase = false;
        while (parent is StrategicGroup group && group.type == StrategicGroup.Type.Base)
        {
            skippedBase = true;
            parent = parent.parentGroupReference.Get();
        }

        return skippedBase && parent == null;
    }

    public IEnumerable<IStrategicGroupMemberReferenceable> GetChildren(IStrategicGroupMemberReferenceable node)
    {
        if (node is StrategicGroup group)
        {
            foreach (var obj in group.WalkDirectMembers())
            {
                if (obj is StrategicGroup subGroup && subGroup.type == StrategicGroup.Type.Base)
                {
                    foreach (var visibleChild in GetChildren(subGroup))
                    {
                        yield return visibleChild;
                    }
                    continue;
                }

                yield return obj;
            }
        }
    }
    
    public IStrategicGroupMemberReferenceable GetData(IStrategicGroupMemberReferenceable node)
    {
        return node;
    }

    public void BindMakeItemBindItem(TreeView treeView)
    {
        treeView.makeItem = () =>
        {
            var el = treeView.itemTemplate.CloneTree();
            var nameLabel = el.Q<Label>("OOBNameLinkLabel");
            var leaderLabel = el.Q<Label>("OOBLeaderLinkLabel");

            Utils.RegisterLinkTag(nameLabel, new()
            {
                ["nameLink"] = () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding(el, out IStrategicGroupMemberReferenceable r))
                    {
                        Debug.Log($"Resolved: {r}");
                        SwitchCenter.Instance.SwitchByIStrategicGroupMemberReferenceable(r);
                    }
                }
            });

            Utils.RegisterLinkTag(leaderLabel, new()
            {
                ["leaderLink"] = () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out IStrategicGroupMemberReferenceable r))
                    {
                        var leader = GetLeader(r);
                        if (leader != null)
                        {
                            SwitchCenter.Instance.SwitchToLeaderView(leader);
                        }
                    }
                }
            });

            return el;
        };
        treeView.bindItem = (e, i) =>
        {
            var item = treeView.GetItemDataForIndex<IStrategicGroupMemberReferenceable>(i);

            e.dataSource = item;
            // var label = e.Q<Label>();
            // label.dataSource = item;
        };
    }
}
