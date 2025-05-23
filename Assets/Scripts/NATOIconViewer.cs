
using NavalCombatCore;
using UnityEngine;
using TMPro;

public class NATOIconViewer : MonoBehaviour
{
    public ShipLog shipLog; // TODO: Use interface to decouple?
    public TMP_Text text;

    public void Update()
    {
        // Update acronym

        text.text = shipLog.shipClass.GetAcronym();

        // Update base
    }
}