using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


public class MainMenuGameManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Localization's async loading will prevent utilization of Localization system selector. May we add it to somewhat callback or just add it to concrete GameManager / StrategicGameManager ?
        // GamePreference.Instance.SetShortLabelLanguageTypeByLocale(LocalizationSettings.SelectedLocale);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
