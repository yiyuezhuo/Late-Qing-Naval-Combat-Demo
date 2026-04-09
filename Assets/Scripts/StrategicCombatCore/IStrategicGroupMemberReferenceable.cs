using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public interface IStrategicGroupMemberReferenceable : IObjectIdLabeled
    {
        StrategicGroupReference parentGroupReference { get; set; }
        StrategicGroupReference detachedFromGroupReference { get; set; }
        bool enableAutoReattach { get; set; }
        public float GetShipTons();
        // public float GetCombatShipTons();
        public int GetStrengthMen();
        public int GetSubUnitSize();
        public float GetCombinedPowerPoint(bool isTop);
        // public float GetEffectiveShipTons(); // Not Deployed & Destroyed would be cancelled. (Deploy state need to be set in the auto shiplog generator script)
        // public int GetEffectiveStrengthMen();
        // public int GetEffectiveUnitSize();

        void SetStrategicGroupReference(StrategicGroup group);

        // group == null => Unset
        static void SetStrategicGroupReference(IStrategicGroupMemberReferenceable self, StrategicGroup group)
        {
            PermanentTransferTo(self, group);
        }

        static void SetDetachedFromGroupReference(IStrategicGroupMemberReferenceable self, StrategicGroup group)
        {
            if (self == null)
                return;

            self.detachedFromGroupReference ??= new();
            self.detachedFromGroupReference.referenceId = group?.objectId;
        }

        static void ClearDetachedFromGroupState(IStrategicGroupMemberReferenceable self)
        {
            if (self == null)
                return;

            SetDetachedFromGroupReference(self, null);
            self.enableAutoReattach = false;
            if (self is ShipLog shipLog)
            {
                shipLog.autoReattachAfterRepair = false;
            }
        }

        static void PermanentTransferTo(IStrategicGroupMemberReferenceable self, StrategicGroup group)
        {
            if (self == null)
                return;

            StrategicGroup.ReassignMember(self, group);
            ClearDetachedFromGroupState(self);
        }

        static void TemporaryAttachTo(IStrategicGroupMemberReferenceable self, StrategicGroup group)
        {
            if (self == null)
                return;

            var oldParentGroup = self.parentGroupReference.Get();
            if (self.detachedFromGroupReference == null)
            {
                self.detachedFromGroupReference = new();
            }

            if (self.detachedFromGroupReference.Get() == null &&
                oldParentGroup != null &&
                oldParentGroup != group)
            {
                self.detachedFromGroupReference.referenceId = oldParentGroup.objectId;
            }

            StrategicGroup.ReassignMember(self, group);
        }

        static bool TryReattachToDetachedFromGroup(IStrategicGroupMemberReferenceable self, bool force = false)
        {
            if (self == null)
                return false;

            var detachedFromGroup = self.GetDetachedFromGroup();
            if (detachedFromGroup == null)
            {
                ClearDetachedFromGroupState(self);
                return false;
            }

            if (!force && !self.IsOnSameCellWithDetachedFromGroup())
                return false;

            var previousParentGroup = self.parentGroupReference.Get();
            StrategicGroup.ReassignMember(self, detachedFromGroup);
            ClearDetachedFromGroupState(self);
            // Reattach moves only this member back. If its temporary parent is empty afterwards
            // (for example a one-ship task force), TryDestroyGroupIfEmptyRecursive removes that parent.
            StrategicGameState.Instance?.TryDestroyGroupIfEmptyRecursive(previousParentGroup);
            return true;
        }

        public LandUnit GetCurrentSourceDepot()
        {
            if (this is StrategicGroup group)
                return group.GetCurrentSourceDepot();

            return parentGroupReference.Get()?.GetCurrentSourceDepot();
        }

        public StrategicGroup GetDetachedFromGroup() => detachedFromGroupReference?.Get();

        public Cell GetCurrentCell()
        {
            if (this is StrategicGroup group)
                return group.cell;
            if (this is LandUnit landUnit)
                return landUnit.cell;
            if (this is ShipLog shipLog)
                return shipLog.cell;
            return parentGroupReference.GetCell();
        }

        public bool IsOnSameCellWithDetachedFromGroup()
        {
            var detachedFromGroup = GetDetachedFromGroup();
            var currentCell = GetCurrentCell();
            return detachedFromGroup != null &&
                currentCell != null &&
                detachedFromGroup.cell == currentCell;
        }

        public string GetParentName() => parentGroupReference.Get()?.name?.mergedName ?? "[Undefined or Invalid]";
        public string GetCurrentSourceDepotName() => GetCurrentSourceDepot()?.name?.mergedName ?? "[Not Defined]";
        public string GetDetachedFromGroupName() => GetDetachedFromGroup()?.name?.mergedName ?? "[Not Detached]";
    }
}

