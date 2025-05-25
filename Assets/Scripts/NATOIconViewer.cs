
using NavalCombatCore;
using UnityEngine;
using TMPro;
using System.Linq;

public class NATOIconViewer : MonoBehaviour
{
    public string shipLogObjectId;
    public ShipLog shipLog{ get => EntityManager.Instance.Get<ShipLog>(shipLogObjectId); } // TODO: Use interface to decouple?
    public TMP_Text text;

    MeshRenderer iconRenderer;

    void Awake()
    {
        iconRenderer = GetComponent<MeshRenderer>();   
    }

    public void Update()
    {
        // Update acronym

        text.text = shipLog.shipClass.GetAcronym();

        // Update base
    }

    public void SyncPostureType(PostureType postureType)
    {
        var mat = GameManager.Instance.postureMaterialMap.FirstOrDefault(r => r.postureType == postureType).material;
        iconRenderer.material = mat;
    }
}