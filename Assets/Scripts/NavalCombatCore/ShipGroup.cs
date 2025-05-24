using System.Collections.Generic;

namespace NavalCombatCore
{
    public enum GroupType
    {
        General,
        Fleet,
        Squadron,
        Flotilla, // For DD
        HalfFlotilla, // For DD
        Division,
    }

    public enum FormationType
    {
        General,
        LineAhead,
        LineAbreast,
        // LineOfBearing,
        // Column,
        // circularAndDiamond
    }

    public enum GroupRole
    {
        General,
        Screen,
    }

    public interface IShipGroupMember : IObjectIdLabeled // Include ShipGroup and ShipLog at times.
    {
        public string parentObjectId { get; set; }
        public ShipGroup GetParentGroup() => EntityManager.Instance.Get<ShipGroup>(parentObjectId);

        public void AttachTo(ShipGroup newParent)
        {
            var parent = GetParentGroup();
            if (parent != null)
            {
                parent.childrenObjectIds.Remove(objectId);
            }
            else if(this is ShipGroup shipGroup)
            {
                NavalGameState.Instance.rootShipGroups.Remove(shipGroup);
            }

            if (newParent != null)
            {
                newParent.childrenObjectIds.Add(objectId);
            }
            parentObjectId = newParent?.objectId;
        }

        public bool IsAttachToAble(ShipGroup newParent)
        {
            var p = newParent;
            while (p != null)
            {
                if (p == this)
                {
                    return false;
                }
                p = (p as IShipGroupMember).GetParentGroup(); // TODO: code smell
            }
            return true;
        }

        public bool TryAttachTo(ShipGroup newParent)
        {
            if (!IsAttachToAble(newParent))
                return false;

            AttachTo(newParent);
            return true;
        }
    }

    public class ShipGroup : IShipGroupMember
    {
        public string objectId { get; set; }
        public string parentObjectId{get;set;}

        public List<string> childrenObjectIds = new();
        
        public IEnumerable<IShipGroupMember> GetChildren()
        {
            foreach (var childObjectId in childrenObjectIds)
            {
                yield return EntityManager.Instance.Get<IShipGroupMember>(childObjectId);
            }
        }
        public GlobalString name = new();
        public GlobalString captain = new();
        public GroupType type;
        public FormationType formation;

    }
}