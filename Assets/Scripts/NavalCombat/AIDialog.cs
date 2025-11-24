using System;
using System.Collections.Generic;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

public class AIDialogItem
{
    public ShipGroup topGroup;

    [CreateProperty]
    public string name => topGroup?.name?.GetMergedName() ?? "[Not defined or invalid]";

    [CreateProperty]
    public int movementAutomaticalIndex
    {
        get
        {
            var type = topGroup.doctrine.maneuverAutomaticType;
            var isAutomatic = !type.isInherited && type.value == AutomaticType.Automatic;
            return isAutomatic ? 1 : 0;
        }
        set
        {
            var automaticType = value switch
            {
                0 => AutomaticType.Manual,
                1 => AutomaticType.Automatic,
                _ => AutomaticType.Manual
            };
            topGroup.doctrine.maneuverAutomaticType.value = automaticType;

            var type = topGroup.doctrine.maneuverAutomaticType;
            if(value == 1)
            {
                type.isInherited = false;
                type.value = AutomaticType.Automatic;
            }
            else
            {
                type.isInherited = true;
                type.value = AutomaticType.Manual;
            }
        }
    }
}

public class AIDialog
{
    public List<AIDialogItem> items = new();
}