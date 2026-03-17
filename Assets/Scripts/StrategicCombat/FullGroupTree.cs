using System.Collections;
using System.Collections.Generic;
using NavalCombatCore;
using UnityEngine.UIElements;
using StrategicCombatCore;
using UnityEngine;

public class FullGroupTree : ITree<IStrategicGroupMemberReferenceable, string>
{
    public IStrategicGroupMemberReferenceable GetParent(IStrategicGroupMemberReferenceable node)
    {
        return node.strategicGroupReference.Get();
    }

    public IEnumerable<IStrategicGroupMemberReferenceable> GetChildren(IStrategicGroupMemberReferenceable node)
    {
        if (node is StrategicGroup group)
        {
            foreach (var sub in group.subordinatesCombined)
            {
                var obj = sub.Get();
                if (obj != null)
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
    public IStrategicGroupMemberReferenceable GetParent(IStrategicGroupMemberReferenceable node)
    {
        var parent = node.strategicGroupReference.Get();
        while (parent is StrategicGroup group && group.type == StrategicGroup.Type.Base)
        {
            parent = parent.strategicGroupReference.Get();
        }

        return parent;
    }

    public IEnumerable<IStrategicGroupMemberReferenceable> GetChildren(IStrategicGroupMemberReferenceable node)
    {
        if (node is StrategicGroup group)
        {
            foreach (var sub in group.subordinatesCombined)
            {
                var obj = sub.Get();
                if (obj == null)
                {
                    continue;
                }

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
            // TODO: Bind link here.

            Utils.RegisterLinkTag(el.Q<Label>(), new()
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
