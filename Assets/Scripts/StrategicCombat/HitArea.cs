using UnityEngine;
using TMPro;
using CoreUtils;
using StrategicCombatCore;

public class HitArea : MonoBehaviour
{
    public TMP_Text locationLabelText;

    public string hitAreaObjectId;
    public string areaCellObjectId;
    Color defaultTextColor;
    bool defaultTextColorCached;
    bool influenceOverlayActive;
    float influenceOverlayValue;
    float influenceOverlayMaxAbs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    TMP_Text EnsureLocationLabelText()
    {
        if (locationLabelText == null)
        {
            locationLabelText = GetComponentInChildren<TMP_Text>(true);
        }

        return locationLabelText;
    }

    public void SyncLabel()
    {
        var labelText = EnsureLocationLabelText();
        if (labelText == null)
            return;

        if (!defaultTextColorCached)
        {
            defaultTextColor = labelText.color;
            defaultTextColorCached = true;
        }

        if (influenceOverlayActive)
        {
            labelText.text = StrategicInfluenceMapUtility.FormatValue(influenceOverlayValue);
            labelText.color = StrategicInfluenceMapUtility.GetValueColor(influenceOverlayValue, influenceOverlayMaxAbs);
            return;
        }

        if(areaCellObjectId != null)
        {
            var areaCell = EntityManager.Instance.Get<Cell>(areaCellObjectId);
            labelText.text = areaCell?.Label?.GetShortName() ?? "";
            labelText.color = defaultTextColor;
        }
        else
        {
            labelText.color = defaultTextColor;
        }
    }

    public void SetInfluenceOverlay(float value, float maxAbs)
    {
        influenceOverlayActive = true;
        influenceOverlayValue = value;
        influenceOverlayMaxAbs = maxAbs;
        SyncLabel();
    }

    public void ClearInfluenceOverlay()
    {
        influenceOverlayActive = false;
        influenceOverlayValue = 0f;
        influenceOverlayMaxAbs = 0f;
        SyncLabel();
    }
}
