
using NavalCombatCore;
using Unity.Properties;
using CoreUtils;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UIElements;
using UnityEngine;


public class GamePreference
{
    static GamePreference instance = new();
    public static GamePreference Instance => instance;

    public enum FiringLineDisplayMode
    {
        None,
        SelectedShip,
        SelectedGroup,
        SelectedRootGroup,
        All
    }

    public FiringLineDisplayMode firingLineDisplayMode = FiringLineDisplayMode.SelectedRootGroup;
    
    [CreateProperty]
    public PortraitViewer.Mode shipPortraitViewMode
    {
        get => PortraitViewer.mode;
        set => PortraitViewer.mode = value;
    }

    public float pulseLengthSeconds = 1; // 2; // 1;
    public float simulationRateRaio = 120; // 1s real time => 120s simulation time (similar to RTW's default advance speed)

    // public LanguageType shortLabelLanguageType = LanguageType.English;
    // public LanguageType longLabelLanguageType = LanguageType.All;
    public bool showUnitLabel = true;
    public bool showDamagePointBar = true;

    public float dayAdvanceHourIntervalSeconds = 0.05f;

    bool _earthDarkThemeSetup = false;
    bool _earthDarkTheme;

    [CreateProperty]
    public bool earthDarkTheme
    {
        get
        {
            if (_earthDarkThemeSetup || !SuperGameState.Instance.IsInNavalGame())
            {
                return _earthDarkTheme;
            }

            _earthDarkThemeSetup = true;
            _earthDarkTheme = SphereController.Instance?.earthDarkTheme ?? false;
            return _earthDarkTheme;
        }
        set
        {
            if (!SuperGameState.Instance.IsInNavalGame())
                return;

            _earthDarkTheme = value;
            SphereController.Instance.earthDarkTheme = _earthDarkTheme;
        }
    }

    [CreateProperty]
    public bool earthDarkThemeEnabled => SuperGameState.Instance.IsInNavalGame();

    public bool forcedNavalCombatResolution = true;
    public bool showAIDialog = true;

    [CreateProperty]
    public float shipPortraitModelScale
    {
        get => PortraitViewer.modelScale;
        set => PortraitViewer.modelScale = value;
    }

    public event EventHandler shortLabelLanguageTypeChanged;

    [CreateProperty]
    public LanguageType shortLabelLanguageType
    {
        get => GlobalString.shortMode;
        set
        {
            if (value != GlobalString.shortMode)
            {
                GlobalString.shortMode = value;
                shortLabelLanguageTypeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [CreateProperty]
    public LanguageType longLabelLanguageType
    {
        get => GlobalString.mergeMode;
        set => GlobalString.mergeMode = value;
    }

    [CreateProperty]
    public CoreParameter navalCombatCoreParameter => CoreParameter.Instance;

    public void SetShortLabelLanguageTypeByLocale(Locale locale)
    {
        shortLabelLanguageType = locale.Identifier.CultureInfo.Name switch
        {
            "en" => LanguageType.English,
            "ja" => LanguageType.Japanese,
            "zh-Hans" => LanguageType.ChineseSimplified,
            "zh-Hant" => LanguageType.ChineseTraditional,
            _ => LanguageType.English
        };
    }

    public void SwitchToLocaleByName(string s)
    {
        var selectedLocale = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(locale => LocaleToNativeName(locale) == s);

        if (selectedLocale != null)
        {
            LocalizationSettings.SelectedLocale = selectedLocale;
            SetShortLabelLanguageTypeByLocale(selectedLocale);
        }
    }

    static string LocaleToNativeName(UnityEngine.Localization.Locale locale)
    {
        var name = locale.Identifier.CultureInfo.Name;
        switch (name)
        {
            case "en":
                return "English";
            case "ja":
                return "日本語";
            case "zh-Hans":
                return "简体中文";
            case "zh-Hant":
                return "繁體中文";
            default:
                return name;
        }
    }
    
    public IEnumerator SetupLocale(DropdownField localeDropdownField)
    {
        yield return LocalizationSettings.InitializationOperation;

        localeDropdownField.choices = LocalizationSettings.AvailableLocales.Locales.Select(LocaleToNativeName).ToList();

        // LocalizationSettings.SelectedLocale.Identifier.CultureInfo.NativeName
        // en
        // ja
        // zh-Hans
        // zh-Hant

        var locales = LocalizationSettings.AvailableLocales.Locales;
        for (var i = 0; i < locales.Count; i++)
            if (LocaleToNativeName(locales[i]) == LocaleToNativeName(LocalizationSettings.SelectedLocale))
                localeDropdownField.index = i;

        localeDropdownField.RegisterValueChangedCallback(evt => SwitchToLocaleByName(evt.newValue));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Setup()
    {
        var p = GamePreference.Instance;
        p.forcedNavalCombatResolution = PlayerPrefs.GetInt("forcedNavalCombatResolution", 1) == 1;
        p.showAIDialog = PlayerPrefs.GetInt("showAIDialog", 1) == 1;
    }

    public void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt("forcedNavalCombatResolution", forcedNavalCombatResolution ? 1 : 0);
        PlayerPrefs.SetInt("showAIDialog", showAIDialog ? 1 : 0);

        PlayerPrefs.Save();
    }

    public void ResetPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}

namespace NavalCombatCore
{
    public partial class CoreParameter
    {
        [CreateProperty]
        public float expectedCombatRangeYardLow
        {
            get => LowLevelCoursePlanner.expectedCombatRangeYardLow;
            set => LowLevelCoursePlanner.expectedCombatRangeYardLow = value;
        }

        [CreateProperty]
        public float expectedCombatRangeYardHigh
        {
            get => LowLevelCoursePlanner.expectedCombatRangeYardHigh;
            set => LowLevelCoursePlanner.expectedCombatRangeYardHigh = value;
        }
    }
}