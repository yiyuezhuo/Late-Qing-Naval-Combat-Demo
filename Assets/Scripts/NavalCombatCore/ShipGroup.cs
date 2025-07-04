using System.Collections.Generic;
// using System.Windows.Forms;

namespace NavalCombatCore
{
    public enum GroupType
    {
        General,
        Force,
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

    public enum GroupController
    {
        Inherited, // Root Default Human (can be set in Preference?)
        Manual, // Human
        Automatic // AI
    }

    public interface IShipGroupMember : IObjectIdLabeled // Include ShipGroup and ShipLog at times.
    {
        public string parentObjectId { get; set; }
        public string GetMemberName();
        public Doctrine doctrine{ get; set; }

        public ShipGroup GetParentGroup() => EntityManager.Instance.Get<ShipGroup>(parentObjectId);
        public IShipGroupMember GetRootParent()
        {
            var pt = this;
            while (pt.GetParentGroup() != null)
            {
                pt = pt.GetParentGroup();
            }
            return pt;
        }

        public void AttachTo(ShipGroup newParent)
        {
            var parent = GetParentGroup();
            if (parent != null)
            {
                parent.childrenObjectIds.Remove(objectId);
            }
            // else if(this is ShipGroup shipGroup) // ShipGroup is attached to null => Remove group (is it better to create detached group instead?)
            // {
            //     NavalGameState.Instance.shipGroups.Remove(shipGroup);
            // }

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

    public partial class ShipGroup : IShipGroupMember
    {
        public string objectId { get; set; }
        public string parentObjectId { get; set; }

        public List<string> childrenObjectIds = new();

        public IEnumerable<IShipGroupMember> GetChildren()
        {
            foreach (var childObjectId in childrenObjectIds)
            {
                yield return EntityManager.Instance.Get<IShipGroupMember>(childObjectId);
            }
        }
        public GlobalString name = new();
        // public GlobalString captain = new();

        public string leaderObjectId;
        public Leader leader
        {
            get => EntityManager.Instance.Get<Leader>(leaderObjectId);
        }

        public GroupType type;
        public FormationType formation;
        public Doctrine doctrine{ get; set; } = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield return doctrine;
        }

        public string GetMemberName() => name.mergedName;
        // public GroupController ResolveController()
        // {
        //     if (controller != GroupController.Inherited)
        //         return controller;
        //     var parent = (this as IShipGroupMember).GetParentGroup();
        //     if (parent == null)
        //         return GroupController.Manual; // Root default is Manual
        //     return parent.ResolveController();
        // }
        // public GroupController ResolveFireController()
        // {
        //     if (fireController != GroupController.Inherited)
        //         return fireController;
        //     var parent = (this as IShipGroupMember).GetParentGroup();
        //     if (parent == null)
        //         return GroupController.Automatic; // Root default is Automatic
        //     return parent.ResolveFireController();
        // }
    }
}