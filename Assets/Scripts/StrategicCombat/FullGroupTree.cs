using System.Collections;
using System.Collections.Generic;
using NavalCombatCore;

using StrategicCombatCore;

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