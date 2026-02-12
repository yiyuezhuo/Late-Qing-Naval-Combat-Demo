
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

    public enum RangeRingDisplayMode
    {
        Circle,
        MergedArcs,
        DistinctArcs
    }

    public RangeRingDisplayMode rangeRingDisplayMode = RangeRingDisplayMode.MergedArcs;
    
    [CreateProperty]
    public PortraitViewer.Mode shipPortraitViewMode
    {
        get => PortraitViewer.mode;
        set => PortraitViewer.mode = value;
    }

    public float pulseLengthSeconds = 1; // 2; // 1;
    public float simulationRateRatio = 120; // 1s real time => 120s simulation time (similar to RTW's default advance speed)
    // public float simulationRateRatioAuto = 30; // 1s real time => 10s simulation time (x10 is similar to JTS's max speed, but feels too slow though)
    public float simulationRateRatioAuto = 10; // 1s real time => 10s simulation time (x10 is similar to JTS's max speed, but feels too slow though)

    // public LanguageType shortLabelLanguageType = LanguageType.English;
    // public LanguageType longLabelLanguageType = LanguageType.All;
    public bool showUnitLabel = true;
    public bool showDamagePointBar = true;

    public float dayAdvanceHourIntervalSeconds = 0.05f;

    // bool _earthDarkThemeSetup = false;
    // bool _earthDarkTheme;

    [CreateProperty]
    public bool earthDarkTheme
    {
        get => SphereController.earthDarkTheme;
        set => SphereController.earthDarkTheme = value;
        // get
        // {
        //     if (_earthDarkThemeSetup || !SuperGameState.Instance.IsInNavalGame())
        //     {
        //         return _earthDarkTheme;
        //     }

        //     _earthDarkThemeSetup = true;
        //     _earthDarkTheme = SphereController.Instance?.shaderEarthDarkTheme ?? false;
        //     return _earthDarkTheme;
        // }
        // set
        // {
        //     // if (!SuperGameState.Instance.IsInNavalGame())
        //     //     return;

        //     _earthDarkTheme = value;

        //     var sphereController = SphereController.Instance;
        //     if(sphereController != null)
        //     {
        //         sphereController.shaderEarthDarkTheme = _earthDarkTheme;
        //     }
        // }
    }

    [CreateProperty]
    public bool earthUseSeaTexture
    {
        get => SphereController.useSeaTexture;
        set => SphereController.useSeaTexture = value;
    }

    // [CreateProperty]
    // public bool earthDarkThemeEnabled => SuperGameState.Instance.IsInNavalGame();

    public bool forcedNavalCombatResolution = true;
    public bool showAIDialog = true;

    [CreateProperty]
    public float shipPortraitModelScale
    {
        get => PortraitViewer.modelScale;
        set => PortraitViewer.modelScale = value;
    }

    [CreateProperty]
    public float textScaleFactor
    {
        get => PortraitViewer.textScaleFactor;
        set => PortraitViewer.textScaleFactor = value;
    }

    [CreateProperty]
    public float iconBeamScale
    {
        get => PortraitViewer.iconBeamScale;
        set => PortraitViewer.iconBeamScale = value;
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
    public float fetchInfoDisplayAccSecondsThreshold
    {
        get => UnityWebRequestImageReaderShower.displaybusyAccSecondsThreshold;
        set => UnityWebRequestImageReaderShower.displaybusyAccSecondsThreshold = value;
    }

    [CreateProperty]
    public CoreParameter navalCombatCoreParameter => CoreParameter.Instance;

    // bool _isInEditMode;
    // bool _isInEditMode = true; // TODO: Enable edit mode in the default setting to reduce potension confusion.
    bool _isInEditMode = false;

    [CreateProperty]
    public bool isInEditMode
    {
        get => _isInEditMode;
        set
        {
            if (_isInEditMode != value)
            {
                _isInEditMode = value;
                isInEditModeChanged?.Invoke(this, value);
            }
        }
    }

    [CreateProperty]
    public bool showSunkShips
    {
        get => GameManager.showSunkShips;
        set => GameManager.showSunkShips = value;
    }

    bool _enableAudio = true;
    float _audioVolume = 1;

    [CreateProperty]
    public bool enableAudio
    {
        get => _enableAudio;
        set
        {
            if (_enableAudio != value)
            {
                _enableAudio = value;
                AudioListener.pause = !_enableAudio;
            }
        }
    }

    [CreateProperty]
    public float audioVolume
    {
        get => _audioVolume;
        set
        {
            if (_audioVolume != value)
            {
                _audioVolume = value;
                AudioListener.volume = _audioVolume;
            }
        }
    }


    // Helpers

    public event EventHandler<bool> isInEditModeChanged;

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
        p.simulationRateRatio = PlayerPrefs.GetFloat("simulationRateRaio", 120);
        // p.isInEditMode = PlayerPrefs.GetInt("isInEditMode", 0) == 1;
        p.isInEditMode = PlayerPrefs.GetInt("isInEditMode", 1) == 1;
        p.rangeRingDisplayMode = (RangeRingDisplayMode)PlayerPrefs.GetInt("rangeRingDisplayMode", (int)RangeRingDisplayMode.MergedArcs);
    }

    public void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt("forcedNavalCombatResolution", forcedNavalCombatResolution ? 1 : 0);
        PlayerPrefs.SetInt("showAIDialog", showAIDialog ? 1 : 0);
        PlayerPrefs.SetFloat("simulationRateRaio", simulationRateRatio);
        PlayerPrefs.SetInt("isInEditMode", isInEditMode ? 1 : 0);
        PlayerPrefs.SetInt("rangeRingDisplayMode", (int)rangeRingDisplayMode);

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

        [CreateProperty]
        public bool disableBatteryAmmunitionCost
        {
            get => MountStatusRecord.disableAmmunitionCost;
            set => MountStatusRecord.disableAmmunitionCost = value;
        }

        [CreateProperty]
        public bool disableRapidFiringBatteryAmmunitionCost
        {
            get => RapidFiringStatus.disableAmmunitionCost;
            set => RapidFiringStatus.disableAmmunitionCost = value;
        }

        [CreateProperty]
        public LaunchedTorpedo.FriendlyCollisionProcessMode torpedoFriendlyCollisionProcessMode
        {
            get => LaunchedTorpedo.friendlyCollisionProcessMode;
            set => LaunchedTorpedo.friendlyCollisionProcessMode = value;
        }

        [CreateProperty]
        public float torpedoFiringAngleErrorDeg
        {
            get => TorpedoMountStatusRecord.torpedoFiringAngleErrorDeg;
            set => TorpedoMountStatusRecord.torpedoFiringAngleErrorDeg = value;
        }

        [CreateProperty]
        public bool disableTorpedoReload
        {
            get => TorpedoMountStatusRecord.disableTorpedoReload;
            set => TorpedoMountStatusRecord.disableTorpedoReload = value;
        }

        // [CreateProperty]
        // public float obstacleAvoidCheckerStepDeg
        // {
        //     get => ObstacleAvoidChecker.stepDeg;
        //     set => ObstacleAvoidChecker.stepDeg = value;
        // }

        // [CreateProperty]
        // public float obstacleAvoidCheckerBoundDeg
        // {
        //     get => ObstacleAvoidChecker.boundDeg;
        //     set => ObstacleAvoidChecker.boundDeg = value;
        // }

        // [CreateProperty]
        // public float obstacleAvoidCheckerExtrapolateSecondsLow
        // {
        //     get => ObstacleAvoidChecker.extrapolateSecondsLow;
        //     set => ObstacleAvoidChecker.extrapolateSecondsLow = value;
        // }

        // [CreateProperty]
        // public float obstacleAvoidCheckerExtrapolateMinHigh
        // {
        //     get => ObstacleAvoidChecker.extrapolateMinHigh;
        //     set => ObstacleAvoidChecker.extrapolateMinHigh = value;
        // }

        // [CreateProperty]
        // public float obstacleAvoidCheckerExtrapolateMinStep
        // {
        //     get => ObstacleAvoidChecker.extrapolateMinStep;
        //     set => ObstacleAvoidChecker.extrapolateMinStep = value;
        // }
    }
}
