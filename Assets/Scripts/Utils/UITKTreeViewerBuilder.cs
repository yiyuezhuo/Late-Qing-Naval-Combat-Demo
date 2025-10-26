

using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;
using System;

public interface ITree<IndexT, DataT>
{
    IndexT GetParent(IndexT node);
    IEnumerable<IndexT> GetChildren(IndexT node);
    DataT GetData(IndexT node);
}

public class UITKTreeViewBuilder<IndexT, DataT>
{
    public ITree<IndexT, DataT> tree;
    // public Func<IndexT, DataT> f;

    // public UITKTreeViewerBuilder(ITree<IndexT, DataT> tree)
    // {
    //     this.tree = tree;
    // }

    public List<TreeViewItemData<DataT>> CreateTreeViewRootItems(IEnumerable<IndexT> flattenTreeNodesIndexes)
    {
        var items = new List<TreeViewItemData<DataT>>();
        var idx = 0;

        foreach (var nodeIndex in flattenTreeNodesIndexes.Where(nodeIndex => tree.GetParent(nodeIndex) == null))
        {
            var subItems = CreateTreeViewItemsForGroup(nodeIndex, ref idx);
            var d = new TreeViewItemData<DataT>(idx, tree.GetData(nodeIndex), subItems);
            idx++;
            items.Add(d);
        }

        return items;
    }

    List<TreeViewItemData<DataT>> CreateTreeViewItemsForGroup(IndexT index, ref int idx)
    {
        var ret = new List<TreeViewItemData<DataT>>();

        foreach (var childrenIndex in tree.GetChildren(index))
        {
            // var childGroup = EntityManager.Instance.Get<ShipGroup>(childrenObjectId);
            var childrenIndexChildrenIndexes = tree.GetChildren(childrenIndex).ToList();
            var isLeaf = childrenIndexChildrenIndexes.Count == 0;
            if (!isLeaf)
            {
                var childItems = CreateTreeViewItemsForGroup(childrenIndex, ref idx);
                ret.Add(new TreeViewItemData<DataT>(idx, tree.GetData(childrenIndex), childItems));
                idx++;
            }
            else // ShipLog or null
            {
                ret.Add(new TreeViewItemData<DataT>(idx, tree.GetData(childrenIndex)));
                idx++;
            }
        }
        return ret;
    }

}