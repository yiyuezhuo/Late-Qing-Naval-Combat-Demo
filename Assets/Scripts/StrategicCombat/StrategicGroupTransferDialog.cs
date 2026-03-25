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

    IStrategicGroupMemberReferenceable GetMember() => EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);

    [CreateProperty]
    public string name
    {
        get
        {
            var member = GetMember();
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
    }

    [CreateProperty]
    public StyleBackground icon
    {
        get
        {
            var member = GetMember();
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
    }

    [CreateProperty]
    public string desc
    {
        get
        {
            var member = GetMember();
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
        if (shipLog == null)
            return false;

        var maxDamagePoint = Math.Max(1f, shipLog.shipClass?.damagePoint ?? 0f);
        return shipLog.damagePoint / maxDamagePoint > 0.1f || shipLog.GetMaxSpeedKnots() <= 4f;
    }

    public static List<ShipLog> CollectDirectSubordinateShipsNeedingDetach(StrategicGroup group)
    {
        if (group == null)
            return new();

        return group.subordinatesCombined
            .Select(reference => reference.Get())
            .OfType<ShipLog>()
            .Where(shipLog => shipLog.mapState == MapState.Deployed)
            .Where(NeedsDetachForRepair)
            .ToList();
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
    class TargetOption
    {
        public string label;
        public StrategicGroup group;
        public bool isCreateNew;
    }

    const string CreateNewTargetValue = "__CREATE_NEW_SUB_GROUP__";

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
    Toggle includeCombinedToggle;
    Toggle createIndependentSubGroupToggle;
    VisualElement createIndependentSubGroupRow;
    ListView sourceListView;
    ListView targetListView;

    List<StrategicGroup> sourceCandidates = new();
    List<TargetOption> targetOptions = new();
    List<StrategicGroupTransferDialogItem> leftItems = new();
    List<StrategicGroupTransferDialogItem> rightItems = new();
    HashSet<string> originalSourceIds = new();
    HashSet<string> originalTargetIds = new();

    string selectedSourceGroupId;
    string selectedTargetGroupId = CreateNewTargetValue;
    bool createIndependentSubGroup = true;

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
        includeCombinedToggle = el.Q<Toggle>("IncludeCombinedToggle");
        createIndependentSubGroupToggle = el.Q<Toggle>("CreateIndependentSubGroupToggle");
        createIndependentSubGroupRow = el.Q<VisualElement>("CreateIndependentSubGroupRow");
        sourceListView = el.Q<ListView>("SourceSubordinatesListView");
        targetListView = el.Q<ListView>("TargetSubordinatesListView");

        ConfigureListView(sourceListView, moveToRight: true);
        ConfigureListView(targetListView, moveToRight: false);

        includeCombined = InitialGroup?.deployState == StrategicGroup.DeployState.Combined;
        includeCombinedToggle?.SetValueWithoutNotify(includeCombined);
        createIndependentSubGroupToggle?.SetValueWithoutNotify(true);
        createIndependentSubGroup = true;
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
        var sourceToTargetIds = rightItems
            .Select(item => item.objectId)
            .Where(id => originalSourceIds.Contains(id))
            .ToList();

        if (targetGroup == null)
        {
            if (sourceToTargetIds.Count == 0)
                return;

            var newGroup = StrategicGroupSubGroupUtility.CreateNewSubGroup(sourceGroup, createIndependentSubGroup);
            foreach (var objectId in sourceToTargetIds)
            {
                var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);
                if (member != null)
                {
                    sourceGroup.MoveElementTo(member, newGroup);
                }
            }
            return;
        }

        var targetToSourceIds = leftItems
            .Select(item => item.objectId)
            .Where(id => originalTargetIds.Contains(id))
            .ToList();

        if (sourceToTargetIds.Count == 0 && targetToSourceIds.Count == 0)
            return;

        foreach (var objectId in sourceToTargetIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);
            if (member != null)
            {
                sourceGroup.MoveElementTo(member, targetGroup);
            }
        }

        foreach (var objectId in targetToSourceIds)
        {
            var member = EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId);
            if (member != null)
            {
                targetGroup.MoveElementTo(member, sourceGroup);
            }
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
                    MoveItemBetweenLists(dialogItem.objectId, moveToRight);
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

        leftItems = sourceGroup == null
            ? new()
            : MakeItems(sourceGroup.subordinatesCombined.Select(reference => reference.referenceId));
        rightItems = targetGroup == null
            ? new()
            : MakeItems(targetGroup.subordinatesCombined.Select(reference => reference.referenceId));

        originalSourceIds = leftItems.Select(item => item.objectId).ToHashSet();
        originalTargetIds = rightItems.Select(item => item.objectId).ToHashSet();

        ApplyItems(sourceListView, leftItems);
        ApplyItems(targetListView, rightItems);
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

    void MoveItemBetweenLists(string objectId, bool moveToRight)
    {
        if (moveToRight)
        {
            var targetGroup = GetSelectedTargetGroup();
            if (!CanMoveItemToGroup(objectId, targetGroup))
                return;

            var item = leftItems.FirstOrDefault(candidate => candidate.objectId == objectId);
            if (item == null)
                return;

            leftItems.RemoveAll(candidate => candidate.objectId == objectId);
            rightItems.Add(item);
        }
        else
        {
            var sourceGroup = GetSelectedSourceGroup();
            if (!CanMoveItemToGroup(objectId, sourceGroup))
                return;

            var item = rightItems.FirstOrDefault(candidate => candidate.objectId == objectId);
            if (item == null)
                return;

            rightItems.RemoveAll(candidate => candidate.objectId == objectId);
            leftItems.Add(item);
        }

        ApplyItems(sourceListView, leftItems);
        ApplyItems(targetListView, rightItems);
    }

    List<StrategicGroupTransferDialogItem> MakeItems(IEnumerable<string> objectIds)
    {
        return objectIds
            .Where(objectId => !string.IsNullOrEmpty(objectId) && EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(objectId) != null)
            .Select(objectId => new StrategicGroupTransferDialogItem() { objectId = objectId })
            .ToList();
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
        foreach (var subordinateReference in group.subordinatesCombined)
        {
            if (subordinateReference.Get() is not StrategicGroup subordinateGroup ||
                subordinateGroup.deployState != StrategicGroup.DeployState.Combined)
                continue;

            yield return subordinateGroup;

            foreach (var nestedGroup in WalkCombinedChildren(subordinateGroup))
            {
                yield return nestedGroup;
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

            current = current.strategicGroupReference.Get();
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
