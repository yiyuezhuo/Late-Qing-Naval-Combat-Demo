using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;
using StrategicCombatCore;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public class StrategicGroupTransferDialogItem
{
    public string objectId;
    public string clickObjectId;
    public string displayName;
    public string displayDesc;
    public StyleBackground displayIcon;
    public bool hasDisplayIcon;
    public ItemOrigin origin;
    public bool isPreview;

    public enum ItemOrigin
    {
        Source,
        Target,
    }

    IStrategicGroupMemberReferenceable GetMember() => EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);

    static string GetMemberName(IStrategicGroupMemberReferenceable member)
    {
        if (member is ShipLog shipLog)
        {
            return shipLog?.namedShip?.name?.mergedName ?? "[Undefined or Invalid ShipLog]";
        }
        if (member is StrategicGroup group)
        {
            return group?.name?.mergedName ?? "[Undefined or Invalid StrategicGroup]";
        }
        if (member is LandUnit landUnit)
        {
            return landUnit?.name?.mergedName ?? "[Undefined or Invalid LandUnit]";
        }
        return "[Undefined or Invalid]";
    }

    static StyleBackground GetMemberIcon(IStrategicGroupMemberReferenceable member)
    {
        if (member is ShipLog shipLog)
        {
            return shipLog?.shipClass?.portraitStyleBackground ?? null;
        }
        if (member is StrategicGroup group)
        {
            return group.typeIcon;
        }
        if (member is LandUnit landUnit)
        {
            return landUnit.GetLandUnitTemplate()?.typeIcon ?? null;
        }
        return null;
    }

    static string GetMemberDescription(IStrategicGroupMemberReferenceable member)
    {
        if (member is ShipLog shipLog)
        {
            var tons = shipLog?.shipClass?.displacementTons ?? 0f;
            var type = shipLog?.shipClass?.type.ToString() ?? "";
            var crews = shipLog?.shipClass?.complementMen ?? 0;
            return $"{type}, {tons} tons, {crews} men";
        }
        if (member is StrategicGroup group)
        {
            var shipTons = group.GetShipTons();
            var shipTonsStr = shipTons == 0 ? "" : $", {shipTons} tons ships";
            return $"{group.type}, {group.combinedSubUnitSize} sub units, {group.GetStrengthMen()} men{shipTonsStr}";
        }
        if (member is LandUnit landUnit)
        {
            var unitType = landUnit.GetLandUnitTemplate()?.unitType;
            if (unitType == LandUnitType.Supply)
            {
                return $"Supply: {landUnit.supplyTons} tons";
            }
            if (unitType == LandUnitType.Port)
            {
                return $"Port: {landUnit.portLevel}, Repair Yard: {landUnit.repairShipyardLevel}";
            }
            return $"{landUnit.strength} men";
        }
        return "";
    }

    [CreateProperty]
    public string name
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            return GetMemberName(GetMember());
        }
    }

    [CreateProperty]
    public StyleBackground icon
    {
        get
        {
            if (hasDisplayIcon)
                return displayIcon;

            return GetMemberIcon(GetMember());
        }
    }

    [CreateProperty]
    public string desc
    {
        get
        {
            if (!string.IsNullOrEmpty(displayDesc))
                return displayDesc;

            return GetMemberDescription(GetMember());
        }
    }

    public string interactionObjectId => !string.IsNullOrEmpty(clickObjectId) ? clickObjectId : objectId;
    public bool IsSourceOrigin => origin == ItemOrigin.Source;

    public static StrategicGroupTransferDialogItem CreateLive(string objectId, ItemOrigin origin)
    {
        return new StrategicGroupTransferDialogItem()
        {
            objectId = objectId,
            clickObjectId = objectId,
            origin = origin,
        };
    }

    public static StrategicGroupTransferDialogItem CreatePreview(
        IStrategicGroupMemberReferenceable member,
        string clickObjectId,
        ItemOrigin origin,
        string desc)
    {
        return new StrategicGroupTransferDialogItem()
        {
            objectId = member?.objectId ?? clickObjectId,
            clickObjectId = clickObjectId,
            origin = origin,
            isPreview = true,
            displayName = GetMemberName(member),
            displayDesc = desc,
            displayIcon = GetMemberIcon(member),
            hasDisplayIcon = true,
        };
    }
}

public static class StrategicGroupSubGroupUtility
{
    static readonly Dictionary<string, LocalizedString> localizedStringMap = new();

    static string Localize(string key, params object[] args)
    {
        if (!localizedStringMap.TryGetValue(key, out var localizedString))
        {
            localizedString = new LocalizedString("Standard Table", key);
            localizedStringMap[key] = localizedString;
        }

        var result = localizedString.GetLocalizedString(args);
        if (result == null || result.StartsWith("No translation found"))
        {
            result = args.Length == 0 ? key : string.Format(key, args);
        }

        return result;
    }

    static string LocalizeDynamicKeyForLocale(string key, string localeCode, string fallback)
    {
        var locale = LocalizationSettings.AvailableLocales?.Locales?
            .FirstOrDefault(candidate => candidate?.Identifier.CultureInfo.Name == localeCode);
        if (locale == null)
            return fallback;

        var result = LocalizationSettings.StringDatabase.GetLocalizedString("Dynamic Table", key, locale);
        if (string.IsNullOrEmpty(result) || result.StartsWith("No translation found"))
            return fallback;

        return result;
    }

    static GlobalString LocalizeEnumGlobalString<T>(T value)
    {
        var fallback = value?.ToString() ?? string.Empty;
        return new GlobalString()
        {
            english = LocalizeEnumForLocale(typeof(T), value, "en", fallback),
            japanese = LocalizeEnumForLocale(typeof(T), value, "ja", fallback),
            chineseSimplified = LocalizeEnumForLocale(typeof(T), value, "zh-Hans", fallback),
            chineseTraditional = LocalizeEnumForLocale(typeof(T), value, "zh-Hant", fallback),
        };
    }

    static string LocalizeEnumForLocale(Type enumType, object value, string localeCode, string fallback)
    {
        foreach (var key in UnityLocalizationService.GetEnumKeys(enumType, value))
        {
            var result = LocalizeDynamicKeyForLocale(key, localeCode, key);
            if (!string.Equals(result, key, StringComparison.Ordinal))
                return result;
        }

        return fallback;
    }

    static string CombineNameSegment(string left, string right, string separator)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right;
        if (string.IsNullOrWhiteSpace(right))
            return left;
        return $"{left}{separator}{right}";
    }

    static GlobalString CombineNameParts(GlobalString left, GlobalString right)
    {
        return new GlobalString()
        {
            english = CombineNameSegment(left?.english, right?.english, " "),
            japanese = CombineNameSegment(left?.japanese, right?.japanese, string.Empty),
            chineseSimplified = CombineNameSegment(left?.chineseSimplified, right?.chineseSimplified, string.Empty),
            chineseTraditional = CombineNameSegment(left?.chineseTraditional, right?.chineseTraditional, string.Empty),
        };
    }

    static GlobalString AppendGeneratedNameIndex(GlobalString baseName, int index)
    {
        var suffix = index.ToString();
        return new GlobalString()
        {
            english = CombineNameSegment(baseName?.english, suffix, " "),
            japanese = CombineNameSegment(baseName?.japanese, suffix, string.Empty),
            chineseSimplified = CombineNameSegment(baseName?.chineseSimplified, suffix, string.Empty),
            chineseTraditional = CombineNameSegment(baseName?.chineseTraditional, suffix, string.Empty),
        };
    }

    static string GetEnglishName(GlobalString name) => name?.GetNameFromType(LanguageType.English)?.Trim();

    static GlobalString GetGeneratedSubGroupRoleName(StrategicGroup sourceGroup)
    {
        if (sourceGroup.IsNavy())
            return LocalizeEnumGlobalString(StrategicGroup.Type.Fleet);

        if (sourceGroup.size != StrategicUnitSize.Unspecified)
            return LocalizeEnumGlobalString(sourceGroup.size);

        return LocalizeEnumGlobalString(sourceGroup.type);
    }

    public static GlobalString BuildGeneratedSubGroupName(StrategicGroup sourceGroup)
    {
        var baseName = CombineNameParts(
            LocalizeEnumGlobalString(sourceGroup.country),
            GetGeneratedSubGroupRoleName(sourceGroup)
        );
        var existingEnglishNames = StrategicGameState.Instance.strategicGroups
            .Where(group => group != null)
            .Select(group => GetEnglishName(group.name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextIndex = 1;
        while (existingEnglishNames.Contains(GetEnglishName(AppendGeneratedNameIndex(baseName, nextIndex))))
        {
            nextIndex++;
        }

        return AppendGeneratedNameIndex(baseName, nextIndex);
    }

    public static StrategicGroup CreateNewSubGroup(StrategicGroup sourceGroup, bool createIndependent, Action<StrategicGroup> configureNewGroup = null)
    {
        if (sourceGroup == null)
            return null;

        var newGroup = new StrategicGroup()
        {
            name = BuildGeneratedSubGroupName(sourceGroup),
            type = sourceGroup.type,
            size = sourceGroup.size,
            country = sourceGroup.country,
            deployState = createIndependent ? StrategicGroup.DeployState.Independent : StrategicGroup.DeployState.Combined,
        };

        configureNewGroup?.Invoke(newGroup);

        var gameState = StrategicGameState.Instance;
        var sourceIndex = gameState.strategicGroups.IndexOf(sourceGroup);
        if (sourceIndex >= 0)
        {
            gameState.strategicGroups.Insert(sourceIndex + 1, newGroup);
        }
        else
        {
            gameState.strategicGroups.Add(newGroup);
        }

        EntityManager.Instance.Register(newGroup, null);
        newGroup.AttachTo(sourceGroup);
        if (createIndependent)
        {
            newGroup.MoveToCell(sourceGroup.cell, false);
        }
        else
        {
            newGroup.deployState = StrategicGroup.DeployState.Combined;
        }
        return newGroup;
    }

    public static bool NeedsDetachForRepair(ShipLog shipLog)
    {
        if (shipLog == null || shipLog.mapState != MapState.Deployed)
            return false;

        var maxDamagePoint = Math.Max(1f, shipLog.shipClass?.damagePoint ?? 0f);
        return shipLog.damagePoint / maxDamagePoint > 0.1f || shipLog.GetMaxSpeedKnots() <= 4f;
    }

    public static List<ShipLog> CollectDirectSubordinateShipsNeedingDetach(StrategicGroup group)
    {
        if (group == null)
            return new();

        return group.directMemberReferences
            .Select(reference => reference.Get())
            .OfType<ShipLog>()
            .Where(shipLog => shipLog.mapState == MapState.Deployed)
            .Where(NeedsDetachForRepair)
            .ToList();
    }

    public static List<ShipLog> CollectCombinedHierarchyShipsNeedingDetach(StrategicGroup group)
    {
        if (group == null)
            return new();

        return group.WalkGroupMembersDeployedShips()
            .Where(NeedsDetachForRepair)
            .ToList();
    }

    public class RepairDetachResult
    {
        public StrategicGroup sourceGroup;
        public StrategicGroup detachedGroup;
        public List<ShipLog> detachedShips = new();
        public bool applied;
        public bool convertedSourceGroupInPlace;
        public bool createdDetachedGroup => detachedGroup != null;
    }

    static List<ShipLog> NormalizeDetachedShipList(IEnumerable<ShipLog> shipLogs)
    {
        return shipLogs?
            .Where(shipLog => shipLog != null)
            .Distinct()
            .ToList() ?? new();
    }

    static bool WouldDetachAllDeployedShips(StrategicGroup sourceGroup, IReadOnlyCollection<ShipLog> detachedShips)
    {
        if (sourceGroup == null || detachedShips == null || detachedShips.Count == 0)
            return false;

        var detachedShipIds = detachedShips
            .Where(shipLog => !string.IsNullOrWhiteSpace(shipLog.objectId))
            .Select(shipLog => shipLog.objectId)
            .ToHashSet();

        if (detachedShipIds.Count == 0)
            return false;

        return sourceGroup.WalkGroupMembersDeployedShips()
            .Select(shipLog => shipLog.objectId)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .All(detachedShipIds.Contains);
    }

    static void PrepareShipsForDetachedAutoReattach(IEnumerable<ShipLog> detachedShips)
    {
        foreach (var shipLog in detachedShips)
        {
            shipLog.enableAutoReattach = false;
        }
    }

    static void InterruptAssignedMissionForForcedReturn(StrategicGroup group)
    {
        var mission = group?.GetAssignedMission();
        if (mission != null)
        {
            mission.InterruptNow();
        }
    }

    public static RepairDetachResult DetachDamagedShipsForRepair(
        StrategicGroup initialGroup,
        List<ShipLog> detachedShips = null)
    {
        var result = new RepairDetachResult()
        {
            sourceGroup = initialGroup,
        };

        if (initialGroup == null || initialGroup.cell == null)
            return result;

        detachedShips = NormalizeDetachedShipList(detachedShips ?? CollectCombinedHierarchyShipsNeedingDetach(initialGroup));
        result.detachedShips = detachedShips;
        if (detachedShips.Count == 0)
            return result;

        var wouldEmptySourceGroup = WouldDetachAllDeployedShips(initialGroup, detachedShips);
        var shouldKeepSourceGroup = wouldEmptySourceGroup;
        if (shouldKeepSourceGroup)
        {
            PrepareShipsForDetachedAutoReattach(detachedShips);
            initialGroup.forcedReturningToBase = true;
            InterruptAssignedMissionForForcedReturn(initialGroup);
            initialGroup.StartReturnToBase(24);

            result.applied = true;
            result.convertedSourceGroupInPlace = true;
            return result;
        }

        var newGroup = CreateNewSubGroup(initialGroup, true, group =>
        {
            group.homeBaseObjectId = initialGroup.homeBaseObjectId;
        });

        PrepareShipsForDetachedAutoReattach(detachedShips);

        foreach (var shipLog in detachedShips)
        {
            IStrategicGroupMemberReferenceable.TemporaryAttachTo(shipLog, newGroup);
        }

        newGroup.StartReturnToBase(24);

        result.applied = true;
        result.detachedGroup = newGroup;
        return result;
    }

    public static string BuildDetachDamagedShipList(IEnumerable<ShipLog> shipLogs)
    {
        return string.Join("\n", shipLogs.Select(shipLog =>
        {
            var maxDamagePoint = shipLog.shipClass?.damagePoint ?? 0f;
            return $"- {shipLog.namedShip?.name?.GetMergedName() ?? Localize("[Undefined or Invalid ShipLog]")} ({shipLog.damagePoint:0.##} / {maxDamagePoint:0.##} DP, {shipLog.GetMaxSpeedKnots():0.##} kts)";
        }));
    }
}

public class StrategicGroupTransferDialog
{
    class TransferAtom
    {
        public string objectId;
        public string rootObjectId;
        public float power;
    }

    class MemberSelectionSummary
    {
        public int totalAtoms;
        public int selectedAtoms;
        public float totalPower;
        public float selectedPower;

        public bool anySelected => selectedAtoms > 0;
        public bool allSelected => totalAtoms > 0 && selectedAtoms == totalAtoms;
        public bool isPartial => anySelected && !allSelected;
    }

    enum AttachMode
    {
        Permanent,
        TemporaryAttach,
    }

    enum DeployStateHandlingMode
    {
        Exclude,
        Atom,
    }

    class TargetOption
    {
        public string label;
        public StrategicGroup group;
        public bool isCreateNew;
    }

    const string CreateNewTargetValue = "__CREATE_NEW_SUB_GROUP__";
    const float PowerEpsilon = 0.0001f;

    static readonly Dictionary<string, LocalizedString> localizedStringMap = new();

    static string Localize(string key, params object[] args)
    {
        if (!localizedStringMap.TryGetValue(key, out var localizedString))
        {
            localizedString = new LocalizedString("Standard Table", key);
            localizedStringMap[key] = localizedString;
        }

        var result = localizedString.GetLocalizedString(args);
        if (result == null || result.StartsWith("No translation found"))
        {
            result = args.Length == 0 ? key : string.Format(key, args);
        }

        return result;
    }

    public string initialGroupObjectId;

    bool includeCombined;
    bool suppressCallbacks;

    DropdownField sourceDropdownField;
    DropdownField targetDropdownField;
    DropdownField attachModeDropdownField;
    DropdownField independentHandlingDropdownField;
    DropdownField notDeployedHandlingDropdownField;
    Toggle includeCombinedToggle;
    Toggle createIndependentSubGroupToggle;
    VisualElement createIndependentSubGroupRow;
    Slider transferPowerRatioSlider;
    Label transferPowerRatioValueLabel;
    ListView sourceListView;
    ListView targetListView;

    List<StrategicGroup> sourceCandidates = new();
    List<TargetOption> targetOptions = new();
    List<StrategicGroupTransferDialogItem> leftItems = new();
    List<StrategicGroupTransferDialogItem> rightItems = new();
    HashSet<string> originalSourceIds = new();
    HashSet<string> originalTargetIds = new();
    List<string> originalSourceRootIds = new();
    List<string> originalTargetRootIds = new();
    HashSet<string> stagedTargetToSourceIds = new();
    HashSet<string> stagedSourceAtomIds = new();
    HashSet<string> stagedZeroPowerSourceRootIds = new();
    List<TransferAtom> orderedSourceAtoms = new();
    Dictionary<string, List<TransferAtom>> sourceAtomsByRootId = new();
    Dictionary<string, float> sourceAtomPowerById = new();
    float totalSourcePower;

    string selectedSourceGroupId;
    string selectedTargetGroupId = CreateNewTargetValue;
    bool createIndependentSubGroup = true;
    AttachMode attachMode;
    DeployStateHandlingMode independentHandlingMode = DeployStateHandlingMode.Exclude;
    DeployStateHandlingMode notDeployedHandlingMode = DeployStateHandlingMode.Exclude;

    StrategicGroup InitialGroup => EntityManager.Instance.Get<StrategicGroup>(initialGroupObjectId);

    public bool CanOpen(out string message)
    {
        var initialGroup = InitialGroup;
        if (initialGroup?.cell == null)
        {
            message = Localize("Transfer requires the current group to resolve to a cell.");
            return false;
        }

        var candidates = CollectCandidateGroups(initialGroup, initialGroup.deployState == StrategicGroup.DeployState.Combined);
        if (candidates.Count == 0)
        {
            message = Localize("No valid transfer groups found for the current cell.");
            return false;
        }

        message = null;
        return true;
    }

    public void OnCreated(object sender, VisualElement el)
    {
        sourceDropdownField = el.Q<DropdownField>("SourceGroupDropdownField");
        targetDropdownField = el.Q<DropdownField>("TargetGroupDropdownField");
        attachModeDropdownField = el.Q<DropdownField>("AttachModeDropdownField");
        independentHandlingDropdownField = el.Q<DropdownField>("IndependentHandlingDropdownField");
        notDeployedHandlingDropdownField = el.Q<DropdownField>("NotDeployedHandlingDropdownField");
        includeCombinedToggle = el.Q<Toggle>("IncludeCombinedToggle");
        createIndependentSubGroupToggle = el.Q<Toggle>("CreateIndependentSubGroupToggle");
        createIndependentSubGroupRow = el.Q<VisualElement>("CreateIndependentSubGroupRow");
        transferPowerRatioSlider = el.Q<Slider>("TransferPowerRatioSlider");
        transferPowerRatioValueLabel = el.Q<Label>("TransferPowerRatioValueLabel");
        sourceListView = el.Q<ListView>("SourceSubordinatesListView");
        targetListView = el.Q<ListView>("TargetSubordinatesListView");

        ConfigureListView(sourceListView, moveToRight: true);
        ConfigureListView(targetListView, moveToRight: false);

        includeCombined = InitialGroup?.deployState == StrategicGroup.DeployState.Combined;
        includeCombinedToggle?.SetValueWithoutNotify(includeCombined);
        createIndependentSubGroupToggle?.SetValueWithoutNotify(true);
        createIndependentSubGroup = true;
        attachMode = AttachMode.Permanent;
        if (attachModeDropdownField != null)
        {
            attachModeDropdownField.choices = new()
            {
                Localize("Permanent"),
                Localize("Temporarily Attach"),
            };
            attachModeDropdownField.index = 0;
        }
        if (independentHandlingDropdownField != null)
        {
            independentHandlingDropdownField.choices = new()
            {
                Localize("Exclude"),
                Localize("Atom"),
            };
            independentHandlingDropdownField.index = 0;
        }
        if (notDeployedHandlingDropdownField != null)
        {
            notDeployedHandlingDropdownField.choices = new()
            {
                Localize("Exclude"),
                Localize("Atom"),
            };
            notDeployedHandlingDropdownField.index = 0;
        }
        UpdateCreateIndependentSubGroupRow(resetToggle: true);

        sourceDropdownField?.RegisterValueChangedCallback(_ =>
        {
            if (suppressCallbacks)
                return;

            SyncSelectedSourceGroupFromDropdown();
            RefreshTargetOptions();
            ResetStagedLists();
        });

        targetDropdownField?.RegisterValueChangedCallback(_ =>
        {
            if (suppressCallbacks)
                return;

            var previousTargetGroupId = selectedTargetGroupId;
            SyncSelectedTargetGroupFromDropdown();
            UpdateCreateIndependentSubGroupRow(resetToggle: previousTargetGroupId != CreateNewTargetValue && selectedTargetGroupId == CreateNewTargetValue);
            ResetStagedLists();
        });

        includeCombinedToggle?.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            includeCombined = evt.newValue;
            RefreshSourceOptions();
            RefreshTargetOptions();
            ResetStagedLists();
        });

        createIndependentSubGroupToggle?.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            createIndependentSubGroup = evt.newValue;
        });

        attachModeDropdownField?.RegisterValueChangedCallback(_ =>
        {
            if (suppressCallbacks)
                return;

            attachMode = attachModeDropdownField.index == 1
                ? AttachMode.TemporaryAttach
                : AttachMode.Permanent;
        });

        independentHandlingDropdownField?.RegisterValueChangedCallback(_ =>
        {
            if (suppressCallbacks)
                return;

            independentHandlingMode = GetHandlingModeFromDropdown(independentHandlingDropdownField);
            ResetStagedLists();
        });

        notDeployedHandlingDropdownField?.RegisterValueChangedCallback(_ =>
        {
            if (suppressCallbacks)
                return;

            notDeployedHandlingMode = GetHandlingModeFromDropdown(notDeployedHandlingDropdownField);
            ResetStagedLists();
        });

        transferPowerRatioSlider?.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            ApplyRequestedTransferRatio(evt.newValue);
        });

        RefreshSourceOptions();
        RefreshTargetOptions();
        ResetStagedLists();
    }

    public void OnConfirmed(object sender, VisualElement el)
    {
        var sourceGroup = GetSelectedSourceGroup();
        if (sourceGroup == null)
            return;

        var targetGroup = GetSelectedTargetGroup();
        var sourceHasTransfer = HasSourceSelection();
        var targetToSourceIds = stagedTargetToSourceIds.ToList();

        if (targetGroup == null)
        {
            if (!sourceHasTransfer)
                return;

            targetGroup = StrategicGroupSubGroupUtility.CreateNewSubGroup(sourceGroup, createIndependentSubGroup);
        }

        if (!sourceHasTransfer && targetToSourceIds.Count == 0)
            return;

        if (sourceHasTransfer)
        {
            ApplySourceSelectionToTarget(sourceGroup, targetGroup);
        }

        foreach (var objectId in targetToSourceIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);
            if (member != null)
            {
                ApplyMemberTransfer(member, sourceGroup);
            }
        }
    }

    void ApplyMemberTransfer(IStrategicGroupMemberReferenceable member, StrategicGroup targetGroup)
    {
        if (attachMode == AttachMode.TemporaryAttach)
        {
            IStrategicGroupMemberReferenceable.TemporaryAttachTo(member, targetGroup);
        }
        else
        {
            IStrategicGroupMemberReferenceable.PermanentTransferTo(member, targetGroup);
        }
    }

    void ConfigureListView(ListView listView, bool moveToRight)
    {
        if (listView == null)
            return;

        listView.makeItem = () =>
        {
            var item = listView.itemTemplate.CloneTree();
            item.RegisterCallback<ClickEvent>(_ =>
            {
                if (item.dataSource is StrategicGroupTransferDialogItem dialogItem)
                {
                    MoveItemBetweenLists(dialogItem, moveToRight);
                }
            });
            return item;
        };
        listView.bindItem = (item, index) =>
        {
            if (listView.itemsSource is not List<StrategicGroupTransferDialogItem> items ||
                index < 0 ||
                index >= items.Count)
                return;

            item.dataSource = items[index];
        };
    }

    void RefreshSourceOptions()
    {
        var initialGroup = InitialGroup;
        var preferredSourceId = selectedSourceGroupId ?? initialGroup?.objectId;

        sourceCandidates = initialGroup == null ? new() : CollectCandidateGroups(initialGroup, includeCombined);

        suppressCallbacks = true;

        if (sourceDropdownField != null)
        {
            sourceDropdownField.choices = sourceCandidates.Select(GetGroupDropdownLabel).ToList();
            sourceDropdownField.userData = sourceCandidates;
        }

        if (sourceCandidates.Count == 0)
        {
            selectedSourceGroupId = null;
            if (sourceDropdownField != null)
            {
                sourceDropdownField.index = -1;
            }
            suppressCallbacks = false;
            return;
        }

        var selectedIndex = sourceCandidates.FindIndex(group => group.objectId == preferredSourceId);
        if (selectedIndex < 0)
            selectedIndex = 0;

        selectedSourceGroupId = sourceCandidates[selectedIndex].objectId;
        if (sourceDropdownField != null)
        {
            sourceDropdownField.index = selectedIndex;
        }

        suppressCallbacks = false;
    }

    void RefreshTargetOptions()
    {
        var preferredTargetId = selectedTargetGroupId;
        targetOptions = new()
        {
            new TargetOption()
            {
                label = Localize("Create New Sub Group"),
                isCreateNew = true,
            }
        };

        targetOptions.AddRange(
            sourceCandidates
                .Where(group => group.objectId != selectedSourceGroupId)
                .Select(group => new TargetOption()
                {
                    label = GetGroupDropdownLabel(group),
                    group = group,
                })
        );

        suppressCallbacks = true;

        if (targetDropdownField != null)
        {
            targetDropdownField.choices = targetOptions.Select(option => option.label).ToList();
            targetDropdownField.userData = targetOptions;
        }

        var selectedIndex = 0;
        if (!string.IsNullOrEmpty(preferredTargetId) && preferredTargetId != CreateNewTargetValue)
        {
            var foundIndex = targetOptions.FindIndex(option => option.group?.objectId == preferredTargetId);
            if (foundIndex >= 0)
                selectedIndex = foundIndex;
        }

        selectedTargetGroupId = selectedIndex == 0 ? CreateNewTargetValue : targetOptions[selectedIndex].group?.objectId;
        if (targetDropdownField != null)
        {
            targetDropdownField.index = selectedIndex;
        }

        suppressCallbacks = false;
        UpdateCreateIndependentSubGroupRow(resetToggle: selectedTargetGroupId == CreateNewTargetValue && preferredTargetId != CreateNewTargetValue);
    }

    void SyncSelectedSourceGroupFromDropdown()
    {
        if (sourceDropdownField?.userData is not List<StrategicGroup> groups ||
            sourceDropdownField.index < 0 ||
            sourceDropdownField.index >= groups.Count)
        {
            selectedSourceGroupId = null;
            return;
        }

        selectedSourceGroupId = groups[sourceDropdownField.index].objectId;
    }

    void SyncSelectedTargetGroupFromDropdown()
    {
        if (targetDropdownField?.userData is not List<TargetOption> options ||
            targetDropdownField.index < 0 ||
            targetDropdownField.index >= options.Count)
        {
            selectedTargetGroupId = CreateNewTargetValue;
            return;
        }

        var option = options[targetDropdownField.index];
        selectedTargetGroupId = option.isCreateNew ? CreateNewTargetValue : option.group?.objectId;
    }

    void ResetStagedLists()
    {
        var sourceGroup = GetSelectedSourceGroup();
        var targetGroup = GetSelectedTargetGroup();

        originalSourceRootIds = CollectDirectMemberIds(sourceGroup);
        originalTargetRootIds = CollectDirectMemberIds(targetGroup);
        originalSourceIds = originalSourceRootIds.ToHashSet();
        originalTargetIds = originalTargetRootIds.ToHashSet();

        stagedTargetToSourceIds.Clear();
        stagedSourceAtomIds.Clear();
        stagedZeroPowerSourceRootIds.Clear();

        RebuildSourceTransferAtoms(sourceGroup);
        RefreshDisplayedItems();
    }

    void ApplyItems(ListView listView, List<StrategicGroupTransferDialogItem> items)
    {
        if (listView == null)
            return;

        listView.ClearSelection();
        listView.itemsSource = items;
        listView.Rebuild();
    }

    void UpdateCreateIndependentSubGroupRow(bool resetToggle)
    {
        var showRow = selectedTargetGroupId == CreateNewTargetValue;
        if (createIndependentSubGroupRow != null)
        {
            createIndependentSubGroupRow.style.display = showRow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (showRow && resetToggle)
        {
            createIndependentSubGroup = true;
            createIndependentSubGroupToggle?.SetValueWithoutNotify(true);
        }
    }

    void MoveItemBetweenLists(StrategicGroupTransferDialogItem item, bool moveToRight)
    {
        if (item == null)
            return;

        var interactionObjectId = item.interactionObjectId;
        if (string.IsNullOrEmpty(interactionObjectId))
            return;

        if (item.IsSourceOrigin)
        {
            var destinationGroup = moveToRight ? GetSelectedTargetGroup() : GetSelectedSourceGroup();
            if (!CanMoveItemToGroup(interactionObjectId, destinationGroup))
                return;

            SetWholeSourceRootTransfer(interactionObjectId, moveToRight);
        }
        else
        {
            var destinationGroup = moveToRight ? GetSelectedTargetGroup() : GetSelectedSourceGroup();
            if (!CanMoveItemToGroup(interactionObjectId, destinationGroup))
                return;

            if (moveToRight)
            {
                stagedTargetToSourceIds.Remove(interactionObjectId);
            }
            else
            {
                stagedTargetToSourceIds.Add(interactionObjectId);
            }
        }

        RefreshDisplayedItems();
    }

    List<string> CollectDirectMemberIds(StrategicGroup group)
    {
        return group?.directMemberReferences
            .Select(reference => reference.referenceId)
            .Where(objectId => !string.IsNullOrEmpty(objectId))
            .Select(objectId => EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId))
            .Where(member => member != null && ShouldIncludeMemberInDialog(member))
            .Select(member => member.objectId)
            .ToList() ?? new();
    }

    void RebuildSourceTransferAtoms(StrategicGroup sourceGroup)
    {
        orderedSourceAtoms = new();
        sourceAtomsByRootId = new();
        sourceAtomPowerById = new();

        foreach (var rootId in originalSourceRootIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(rootId);
            if (member == null)
                continue;

            var atoms = new List<TransferAtom>();
            CollectTransferAtoms(member, rootId, atoms);
            sourceAtomsByRootId[rootId] = atoms;

            foreach (var atom in atoms)
            {
                orderedSourceAtoms.Add(atom);
                sourceAtomPowerById[atom.objectId] = atom.power;
            }
        }

        totalSourcePower = orderedSourceAtoms.Sum(atom => atom.power);
    }

    void CollectTransferAtoms(IStrategicGroupMemberReferenceable member, string rootObjectId, List<TransferAtom> atoms)
    {
        if (member == null)
            return;

        if (!ShouldIncludeMemberInDialog(member))
            return;

        if (member is StrategicGroup group && group.deployState == StrategicGroup.DeployState.Combined)
        {
            foreach (var reference in group.directMemberReferences.ToList())
            {
                var child = reference.Get();
                if (child != null)
                {
                    CollectTransferAtoms(child, rootObjectId, atoms);
                }
            }
            return;
        }

        atoms.Add(new TransferAtom()
        {
            objectId = member.objectId,
            rootObjectId = rootObjectId,
            power = Mathf.Max(0f, member.GetCombinedPowerPoint(true)),
        });
    }

    void RefreshDisplayedItems()
    {
        leftItems = new();
        rightItems = new();

        foreach (var rootId in originalSourceRootIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(rootId);
            if (member == null)
                continue;

            BuildSourceRootItems(member, rootId);
        }

        foreach (var rootId in originalTargetRootIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(rootId);
            if (member == null)
                continue;

            var item = StrategicGroupTransferDialogItem.CreateLive(rootId, StrategicGroupTransferDialogItem.ItemOrigin.Target);
            if (stagedTargetToSourceIds.Contains(rootId))
            {
                leftItems.Add(item);
            }
            else
            {
                rightItems.Add(item);
            }
        }

        ApplyItems(sourceListView, leftItems);
        ApplyItems(targetListView, rightItems);
        UpdateTransferPowerRatioControl();
    }

    void BuildSourceRootItems(IStrategicGroupMemberReferenceable member, string rootId)
    {
        sourceAtomsByRootId.TryGetValue(rootId, out var rootAtoms);
        rootAtoms ??= new();

        if (rootAtoms.Count == 0)
        {
            var liveItem = StrategicGroupTransferDialogItem.CreateLive(rootId, StrategicGroupTransferDialogItem.ItemOrigin.Source);
            if (stagedZeroPowerSourceRootIds.Contains(rootId))
            {
                rightItems.Add(liveItem);
            }
            else
            {
                leftItems.Add(liveItem);
            }
            return;
        }

        var summary = BuildMemberSelectionSummary(member);
        if (!summary.anySelected)
        {
            leftItems.Add(StrategicGroupTransferDialogItem.CreateLive(rootId, StrategicGroupTransferDialogItem.ItemOrigin.Source));
            return;
        }

        if (summary.allSelected)
        {
            rightItems.Add(StrategicGroupTransferDialogItem.CreateLive(rootId, StrategicGroupTransferDialogItem.ItemOrigin.Source));
            return;
        }

        var remainingPower = Mathf.Max(0f, summary.totalPower - summary.selectedPower);
        leftItems.Add(StrategicGroupTransferDialogItem.CreatePreview(
            member,
            rootId,
            StrategicGroupTransferDialogItem.ItemOrigin.Source,
            BuildPartialPreviewDescription(remainingPower, summary.totalPower)
        ));
        rightItems.Add(StrategicGroupTransferDialogItem.CreatePreview(
            member,
            rootId,
            StrategicGroupTransferDialogItem.ItemOrigin.Source,
            BuildPartialPreviewDescription(summary.selectedPower, summary.totalPower)
        ));
    }

    string BuildPartialPreviewDescription(float partialPower, float totalPower)
    {
        return $"{partialPower:0.##} / {totalPower:0.##} power";
    }

    MemberSelectionSummary BuildMemberSelectionSummary(IStrategicGroupMemberReferenceable member)
    {
        if (member == null)
            return new();

        if (!ShouldIncludeMemberInDialog(member))
            return new();

        if (member is StrategicGroup group && group.deployState == StrategicGroup.DeployState.Combined)
        {
            var summary = new MemberSelectionSummary();
            foreach (var reference in group.directMemberReferences.ToList())
            {
                var child = reference.Get();
                if (child == null)
                    continue;

                var childSummary = BuildMemberSelectionSummary(child);
                summary.totalAtoms += childSummary.totalAtoms;
                summary.selectedAtoms += childSummary.selectedAtoms;
                summary.totalPower += childSummary.totalPower;
                summary.selectedPower += childSummary.selectedPower;
            }
            return summary;
        }

        var isSelected = stagedSourceAtomIds.Contains(member.objectId);
        var power = Mathf.Max(0f, member.GetCombinedPowerPoint(true));
        return new MemberSelectionSummary()
        {
            totalAtoms = 1,
            selectedAtoms = isSelected ? 1 : 0,
            totalPower = power,
            selectedPower = isSelected ? power : 0f,
        };
    }

    void UpdateTransferPowerRatioControl()
    {
        var ratio = GetCurrentTransferredSourceRatio();

        suppressCallbacks = true;
        if (transferPowerRatioSlider != null)
        {
            transferPowerRatioSlider.lowValue = 0f;
            transferPowerRatioSlider.highValue = 1f;
            transferPowerRatioSlider.SetValueWithoutNotify(ratio);
            transferPowerRatioSlider.SetEnabled(totalSourcePower > PowerEpsilon);
        }
        suppressCallbacks = false;

        if (transferPowerRatioValueLabel != null)
        {
            transferPowerRatioValueLabel.text = $"{ratio * 100f:0.##}%";
        }
    }

    float GetCurrentTransferredSourceRatio()
    {
        if (totalSourcePower <= PowerEpsilon)
            return 0f;

        return Mathf.Clamp01(GetCurrentTransferredSourcePower() / totalSourcePower);
    }

    float GetCurrentTransferredSourcePower()
    {
        return stagedSourceAtomIds.Sum(atomId => sourceAtomPowerById.GetValueOrDefault(atomId, 0f));
    }

    void ApplyRequestedTransferRatio(float requestedRatio)
    {
        stagedSourceAtomIds.Clear();

        if (orderedSourceAtoms.Count == 0 || totalSourcePower <= PowerEpsilon)
        {
            RefreshDisplayedItems();
            return;
        }

        var clampedRatio = Mathf.Clamp01(requestedRatio);
        var requestedPower = clampedRatio * totalSourcePower;

        var bestPrefixLength = 0;
        var bestDiff = float.MaxValue;
        var cumulativePower = 0f;
        for (var prefixLength = 0; prefixLength <= orderedSourceAtoms.Count; prefixLength++)
        {
            var diff = Mathf.Abs(cumulativePower - requestedPower);
            if (diff < bestDiff - PowerEpsilon)
            {
                bestDiff = diff;
                bestPrefixLength = prefixLength;
            }

            if (prefixLength < orderedSourceAtoms.Count)
            {
                cumulativePower += orderedSourceAtoms[prefixLength].power;
            }
        }

        for (var i = 0; i < bestPrefixLength; i++)
        {
            stagedSourceAtomIds.Add(orderedSourceAtoms[i].objectId);
        }

        RefreshDisplayedItems();
    }

    void SetWholeSourceRootTransfer(string rootId, bool transferToTarget)
    {
        sourceAtomsByRootId.TryGetValue(rootId, out var atoms);
        atoms ??= new();

        if (atoms.Count == 0)
        {
            if (transferToTarget)
            {
                stagedZeroPowerSourceRootIds.Add(rootId);
            }
            else
            {
                stagedZeroPowerSourceRootIds.Remove(rootId);
            }
            return;
        }

        foreach (var atom in atoms)
        {
            if (transferToTarget)
            {
                stagedSourceAtomIds.Add(atom.objectId);
            }
            else
            {
                stagedSourceAtomIds.Remove(atom.objectId);
            }
        }
    }

    bool HasSourceSelection()
    {
        return stagedSourceAtomIds.Count > 0 || stagedZeroPowerSourceRootIds.Count > 0;
    }

    void ApplySourceSelectionToTarget(StrategicGroup sourceGroup, StrategicGroup targetGroup)
    {
        foreach (var rootId in originalSourceRootIds.ToList())
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(rootId);
            if (member == null)
                continue;

            if (stagedZeroPowerSourceRootIds.Contains(rootId))
            {
                ApplyMemberTransfer(member, targetGroup);
                continue;
            }

            var summary = BuildMemberSelectionSummary(member);
            if (!summary.anySelected)
                continue;

            if (summary.allSelected)
            {
                ApplyMemberTransfer(member, targetGroup);
                continue;
            }

            if (member is not StrategicGroup sourceSubGroup || sourceSubGroup.deployState != StrategicGroup.DeployState.Combined)
                continue;

            var splitGroup = CreateSplitGroupLike(sourceSubGroup, sourceGroup);
            MaterializePartialGroupSelection(sourceSubGroup, splitGroup);
            if (splitGroup.directMemberReferences.Count == 0)
            {
                DestroyEmptySplitGroup(splitGroup);
                continue;
            }

            ApplyMemberTransfer(splitGroup, targetGroup);
        }
    }

    StrategicGroup CreateSplitGroupLike(StrategicGroup templateGroup, StrategicGroup parentGroup)
    {
        var splitGroup = new StrategicGroup()
        {
            name = StrategicGroupSubGroupUtility.BuildGeneratedSubGroupName(templateGroup),
            type = templateGroup.type,
            size = templateGroup.size,
            country = templateGroup.country,
            deployState = StrategicGroup.DeployState.Combined,
            homeBaseObjectId = templateGroup.homeBaseObjectId,
        };

        var gameState = StrategicGameState.Instance;
        var templateIndex = gameState.strategicGroups.IndexOf(templateGroup);
        if (templateIndex >= 0)
        {
            gameState.strategicGroups.Insert(templateIndex + 1, splitGroup);
        }
        else
        {
            gameState.strategicGroups.Add(splitGroup);
        }

        EntityManager.Instance.Register(splitGroup, null);
        splitGroup.AttachTo(parentGroup);
        splitGroup.deployState = StrategicGroup.DeployState.Combined;
        return splitGroup;
    }

    void MaterializePartialGroupSelection(StrategicGroup sourceGroup, StrategicGroup splitGroup)
    {
        foreach (var reference in sourceGroup.directMemberReferences.ToList())
        {
            var member = reference.Get();
            if (member == null)
                continue;

            if (!ShouldIncludeMemberInDialog(member))
                continue;

            var summary = BuildMemberSelectionSummary(member);
            if (!summary.anySelected)
                continue;

            if (summary.allSelected)
            {
                IStrategicGroupMemberReferenceable.PermanentTransferTo(member, splitGroup);
                continue;
            }

            if (member is StrategicGroup childGroup && childGroup.deployState == StrategicGroup.DeployState.Combined)
            {
                var childSplitGroup = CreateSplitGroupLike(childGroup, splitGroup);
                MaterializePartialGroupSelection(childGroup, childSplitGroup);
                if (childSplitGroup.directMemberReferences.Count == 0)
                {
                    DestroyEmptySplitGroup(childSplitGroup);
                }
            }
        }
    }

    void DestroyEmptySplitGroup(StrategicGroup group)
    {
        if (group == null || group.directMemberReferences.Count > 0)
            return;

        group.AttachTo(null);
        EntityManager.Instance.Unregister(group);
        StrategicGameState.Instance?.strategicGroups.Remove(group);
    }

    DeployStateHandlingMode GetHandlingModeFromDropdown(DropdownField dropdownField)
    {
        return dropdownField?.index == 1
            ? DeployStateHandlingMode.Atom
            : DeployStateHandlingMode.Exclude;
    }

    bool ShouldIncludeMemberInDialog(IStrategicGroupMemberReferenceable member)
    {
        if (member is not StrategicGroup group)
            return true;

        return GetDeployStateHandlingMode(group.deployState) != DeployStateHandlingMode.Exclude;
    }

    DeployStateHandlingMode GetDeployStateHandlingMode(StrategicGroup.DeployState deployState)
    {
        return deployState switch
        {
            StrategicGroup.DeployState.Independent => independentHandlingMode,
            StrategicGroup.DeployState.NotDeployed => notDeployedHandlingMode,
            _ => DeployStateHandlingMode.Atom,
        };
    }

    List<StrategicGroup> CollectCandidateGroups(StrategicGroup initialGroup, bool includeCombinedChildren)
    {
        var candidates = new List<StrategicGroup>();
        var seenIds = new HashSet<string>();

        foreach (var cellGroup in initialGroup.cell.StrategicGroupReferences.Select(reference => reference.Get()))
        {
            if (cellGroup == null ||
                cellGroup.side != initialGroup.side ||
                cellGroup.deployState != StrategicGroup.DeployState.Independent)
                continue;

            if (seenIds.Add(cellGroup.objectId))
            {
                candidates.Add(cellGroup);
            }

            if (!includeCombinedChildren)
                continue;

            foreach (var combinedChild in WalkCombinedChildren(cellGroup))
            {
                if (combinedChild != null && seenIds.Add(combinedChild.objectId))
                {
                    candidates.Add(combinedChild);
                }
            }
        }

        return candidates;
    }

    IEnumerable<StrategicGroup> WalkCombinedChildren(StrategicGroup group)
    {
        foreach (var subordinateGroup in group.WalkDescendantStrategicGroups())
        {
            if (subordinateGroup.deployState == StrategicGroup.DeployState.Combined)
            {
                yield return subordinateGroup;
            }
        }
    }

    bool CanMoveItemToGroup(string objectId, StrategicGroup destinationGroup)
    {
        if (destinationGroup == null)
            return true;

        if (EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId) is not StrategicGroup movingGroup)
            return true;

        if (movingGroup.objectId == destinationGroup.objectId)
            return false;

        return !IsDescendantOf(destinationGroup, movingGroup);
    }

    bool IsDescendantOf(StrategicGroup candidateGroup, StrategicGroup potentialAncestor)
    {
        var current = candidateGroup;
        while (current != null)
        {
            if (current.objectId == potentialAncestor.objectId)
                return true;

            current = current.parentGroupReference.Get();
        }

        return false;
    }

    string GetGroupDropdownLabel(StrategicGroup group) => group?.name?.GetMergedName() ?? "[Undefined or Invalid]";

    StrategicGroup GetSelectedSourceGroup() => EntityManager.Instance.Get<StrategicGroup>(selectedSourceGroupId);

    StrategicGroup GetSelectedTargetGroup()
    {
        if (string.IsNullOrEmpty(selectedTargetGroupId) || selectedTargetGroupId == CreateNewTargetValue)
            return null;

        return EntityManager.Instance.Get<StrategicGroup>(selectedTargetGroupId);
    }

}
