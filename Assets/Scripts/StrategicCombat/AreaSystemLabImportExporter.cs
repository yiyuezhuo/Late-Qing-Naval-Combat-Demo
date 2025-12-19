using CoreUtils;
using UnityEngine;
using StrategicCombatCore;

public class AreaSystemImportExporter : MonoBehaviour
{
    // public PictureReference pictureReference = new();
    public bool isBuiltin;
    public string path;
    
    public GameObject hitAreaPrefab;
    public Transform areasTransform;

    public string ToXml()
    {
        var areaSystem = new AreaSystem()
        {
            backgroundReference = new()
            {
                isBuiltin = isBuiltin,
                path = path
            }
        };
        foreach(Transform t in areasTransform.GetComponentInChildren<Transform>())
        {
            var areaState = new AreaState()
            {
                name = t.name,
                posX = t.localPosition.x,
                posY = t.localPosition.y,
                scaleX = t.localScale.x,
                scaleY = t.localScale.y
            };
            areaSystem.areaStates.Add(areaState);
        }
        return XmlUtils.ToXML(areaSystem);
    }

}