using System.Collections.Generic;
using System.Linq;


namespace NavalCombatCore
{
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


        // public IEnumerable<T> GetSubState<T>(SubState subState)
        // {
        //     if (subState is T t)
        //     {
        //         yield return t;
        //     }

        //     foreach (var childSubState in subState.children)
        //     {
        //         foreach (var tt in GetSubState<T>(childSubState))
        //         {
        //             yield return tt;
        //         }
        //     }
        // }

        public IEnumerable<T> GetSubStates<T>() // Upward, E.X a status modifer defined in ShipLog will effect all battery' mount, while a status modieifer defined on mount just effect a mount.
        {
            // foreach (var subState in subStates)
            // {
            //     foreach (var t in GetSubState<T>(subState))
            //         yield return t;
            // }

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
    }
}