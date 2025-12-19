using UnityEngine;
using TMPro;
using CoreUtils;
using StrategicCombatCore;

public class HitArea : MonoBehaviour
{
    public TMP_Text locationLabelText;

    public string hitAreaObjectId;
    public string areaCellObjectId;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SyncLabel()
    {
        if(areaCellObjectId != null)
        {
            var areaCell = EntityManager.Instance.Get<Cell>(areaCellObjectId);
            locationLabelText.text = areaCell.Label?.GetShortName();
        }
    }
}
