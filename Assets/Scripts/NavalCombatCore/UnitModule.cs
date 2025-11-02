using System;
using System.Collections.Generic;
using System.Linq;

using CoreUtils;

namespace NavalCombatCore
{
    public class RepairableSubStateRecord
    {
        public ISubject subject;
        public SubState subState;
        // TODO: would add cost & priority here if refining is required.
    }

    // Abstract class will prevent UITK binding hint so we switch back to concrete class at time.
    public partial class UnitModule : IObjectIdLabeled, ISubject
    {
        public string objectId { get; set; }
        public List<SubState> subStates = new();

        public void AddSubState(SubState state)
        {
            subStates.Add(state);
        }
        public void RemoveSubState(SubState state)
        {
            subStates.Remove(state);
        }

        public virtual IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var subState in subStates)
            {
                yield return subState;
            }
        }

        public virtual void StepDamageResolution(float deltaSeconds)
        {
            foreach (var subState in subStates.ToList()) // Shallow copy to prevent modification when iteration.
            {
                subState.Step(this, deltaSeconds);
            }

            foreach (var subobject in GetSubObjects())
            {
                if (subobject is UnitModule subUnitModule)
                {
                    subUnitModule.StepDamageResolution(deltaSeconds);
                }
            }
        }

        public IEnumerable<T> GetSubStates<T>() // Upward, E.X a status modifer defined in ShipLog will effect all battery' mount, while a status modieifer defined on mount just effect a mount.
        {
            foreach (var subState in subStates)
            {
                if (subState is T t)
                {
                    yield return t;
                }
            }

            var parent = EntityManager.Instance.GetParent<UnitModule>(this);
            if (parent != null)
            {
                foreach (var t in parent.GetSubStates<T>())
                {
                    yield return t;
                }
            }
        }

        public IEnumerable<SubState> GetSubStatesDownward()
        {
            foreach (var subState in subStates)
            {
                yield return subState;
            }

            foreach (var subObject in GetSubObjects())
            {
                if (subObject is UnitModule unitModule)
                {
                    foreach (var subState in unitModule.GetSubStatesDownward())
                    {
                        yield return subState;
                    }
                }
            }
        }

        public void ClearSubStates()
        {
            subStates.Clear();

            foreach (var subObject in GetSubObjects())
            {
                if (subObject is UnitModule unitModule)
                {
                    unitModule.ClearSubStates();
                }
            }
        }

        public bool ApplyCampaignPersistenceEffectAndCheckSunk()
        {
            foreach (var subState in subStates.ToList())
            {
                var campaignPersistence = subState.GetCampaignPersistence();
                if (campaignPersistence == CampaignPersistence.Clear)
                {
                    subStates.Remove(subState);
                }
                else if (campaignPersistence == CampaignPersistence.Volatile)
                {
                    if (subState.damageControllable)
                    {
                        subStates.Remove(subState); // TODO: Or use EndAt? But we may don't want sid-effect of it.
                    }
                }
                else if (campaignPersistence == CampaignPersistence.Maintained)
                {

                }
                else if (campaignPersistence == CampaignPersistence.DestinedSunk)
                {
                    return true;
                }
            }

            foreach (var subObject in GetSubObjects())
            {
                if (subObject is UnitModule unitModule)
                {
                    var sunk = unitModule.ApplyCampaignPersistenceEffectAndCheckSunk();

                    if (sunk)
                        return true;
                }
            }

            return false;
        }
        
        public IEnumerable<RepairableSubStateRecord> CollectRepairableSubStateRecords()
        {
            foreach (var subState in subStates)
            {
                yield return new()
                {
                    subject = this,
                    subState = subState
                };
            }
            
            foreach(var subObject in GetSubObjects())
            {
                if(subObject is UnitModule unitModule)
                {
                    foreach(var r in unitModule.CollectRepairableSubStateRecords())
                    {
                        yield return r;
                    }
                }
            }
        }
    }
}