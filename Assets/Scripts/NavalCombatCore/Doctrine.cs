namespace NavalCombatCore
{
    public enum AutomaticType
    {
        Manual, // Human
        Automatic // AI
    }

    public class Inheriable<T> where T: new()
    {
        public bool isInherited = true;
        public T value = new();
    }

    public class Unspecifiable<T>
    {
        public bool isSpecified = false;
        public T value;
    }

    // public class InheriableUnspecifiable<T>
    // {
    //     public bool isInherited = true;
    //     public Unspecifiable<T> unspecified = new();
    // }

    // public class InheriableAutomaticType: Inheriable<AutomaticType> // Help UITK binding
    // {
    // }

    // public class InheriableUnspecifiableFloat: InheriableUnspecifiable<float> // Help UITK binding
    // {
    // }
    
    public class UnspecifiableFloat : Unspecifiable<float> // UITK binding workaround
    {}

    // public class InheriableUnspecifiableFloat : Inheriable<UnspecifiableFloat> // UITK binding workaround
    // { }
    public class InheriableUnspecifiableFloat : Inheriable<Unspecifiable<float>> // UITK binding workaround
    { }


    public class Doctrine : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public Inheriable<AutomaticType> maneuverAutomaticType = new() { value = AutomaticType.Manual };
        public Inheriable<AutomaticType> fireAutomaticType = new() { value = AutomaticType.Automatic };
        public Inheriable<bool> ammunitionFallbackable = new() { value = true };
        public Inheriable<AutomaticType> ammunitionSwitchAutomaticType = new() { value = AutomaticType.Automatic };
        public InheriableUnspecifiableFloat maximumFiringDistanceYardsFor200mmPlus = new();
        public InheriableUnspecifiableFloat maximumFiringDistanceYardsFor100mmTo200mm = new();
        // public Inheriable<Unspecifiable<float>> maximumFiringDistanceYardsFor100mmTo200mm = new();

        public Doctrine GetParentDocrine()
        {
            var member = EntityManager.Instance.GetParent<IShipGroupMember>(this);
            if (member == null)
                return null;
            var parent = member.GetParentGroup();
            return parent?.doctrine;
        }

        public AutomaticType GetAmmunitionSwitchAutomaticType()
        {
            if (!ammunitionSwitchAutomaticType.isInherited)
                return ammunitionSwitchAutomaticType.value;
            return GetParentDocrine()?.GetAmmunitionSwitchAutomaticType() ?? AutomaticType.Automatic;
        }

        public bool GetAmmunitionFallbackable()
        {
            if (!ammunitionFallbackable.isInherited)
                return ammunitionFallbackable.value;
            return GetParentDocrine()?.GetAmmunitionFallbackable() ?? true;
        }

        public AutomaticType GetManeuverAutomaticType() // CMO-like method, look for a better way thought
        {
            if (!maneuverAutomaticType.isInherited)
                return maneuverAutomaticType.value;
            return GetParentDocrine()?.GetManeuverAutomaticType() ?? AutomaticType.Manual;
        }

        public AutomaticType GetFireAutomaticType()
        {
            if (!fireAutomaticType.isInherited)
                return fireAutomaticType.value;
            return GetParentDocrine()?.GetFireAutomaticType() ?? AutomaticType.Automatic;
        }

        public Unspecifiable<float> GetMaximumFiringDistanceYardsFor200mmPlus()
        {
            if (!maximumFiringDistanceYardsFor200mmPlus.isInherited)
                return maximumFiringDistanceYardsFor200mmPlus.value;
            return GetParentDocrine()?.GetMaximumFiringDistanceYardsFor200mmPlus() ?? new Unspecifiable<float>();
        }

        public Unspecifiable<float> GetMaximumFiringDistanceYardsFor100mmTo200mm()
        {
            if (!maximumFiringDistanceYardsFor100mmTo200mm.isInherited)
                return maximumFiringDistanceYardsFor100mmTo200mm.value;
            return GetParentDocrine()?.GetMaximumFiringDistanceYardsFor100mmTo200mm() ?? new Unspecifiable<float>();
        }
    }
}