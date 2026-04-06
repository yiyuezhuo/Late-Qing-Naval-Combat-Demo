using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace StrategicCombatCore
{
    public enum TheaterPosture
    {
        Attack,
        Defense,
    }

    public class FrontlineCellInfo
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public float weightRequested;

        [XmlIgnore]
        public XY xy => new() { x = x, y = y };
    }

    public partial class Theater : IObjectIdLabeled, INamed
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public string sideObjectId;
        [XmlAttribute]
        public TheaterPosture posture = TheaterPosture.Attack;
        public List<XY> cells = new();
        public List<FrontlineCellInfo> frontlineCellInfos = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);
        public GlobalString GetName() => name;

        [XmlIgnore]
        public SideState side => GetSide();

        public override string ToString()
        {
            return $"Theater({name?.GetMergedName()}, {posture}, {cells?.Count ?? 0}, frontline {frontlineCellInfos?.Count ?? 0})";
        }
    }

    public sealed class StrategicGroupTransferAtom
    {
        public string objectId;
        public string rootObjectId;
        public float power;
    }

    public sealed class StrategicGroupTransferSelectionSummary
    {
        public int totalAtoms;
        public int selectedAtoms;
        public float totalPower;
        public float selectedPower;

        public bool anySelected => selectedAtoms > 0;
        public bool allSelected => totalAtoms > 0 && selectedAtoms == totalAtoms;
        public bool isPartial => anySelected && !allSelected;
    }

    public static class StrategicGroupTransferSplitUtility
    {
        public static List<IStrategicGroupMemberReferenceable> CollectTransferMembers(
            IStrategicGroupMemberReferenceable member,
            HashSet<string> selectedAtomIds,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            var members = new List<IStrategicGroupMemberReferenceable>();
            CollectTransferMembers(member, selectedAtomIds, members, shouldIncludeMember);
            return members;
        }

        public static List<IStrategicGroupMemberReferenceable> CollectTransferMembers(
            StrategicGroup sourceGroup,
            HashSet<string> selectedAtomIds,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            var members = new List<IStrategicGroupMemberReferenceable>();
            if (sourceGroup == null)
                return members;

            shouldIncludeMember ??= static _ => true;
            foreach (var reference in sourceGroup.directMemberReferences.ToList())
            {
                var member = reference.Get();
                if (member == null)
                    continue;

                CollectTransferMembers(member, selectedAtomIds, members, shouldIncludeMember);
            }

            return members;
        }

        static void CollectTransferMembers(
            IStrategicGroupMemberReferenceable member,
            HashSet<string> selectedAtomIds,
            List<IStrategicGroupMemberReferenceable> members,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            if (member == null || members == null)
                return;

            shouldIncludeMember ??= static _ => true;
            if (!shouldIncludeMember(member))
                return;

            if (member is StrategicGroup group && group.deployState == StrategicGroup.DeployState.Combined)
            {
                var summary = BuildSelectionSummary(member, selectedAtomIds, shouldIncludeMember);
                if (!summary.anySelected)
                    return;

                if (summary.allSelected)
                {
                    members.Add(member);
                    return;
                }

                foreach (var reference in group.directMemberReferences.ToList())
                {
                    var child = reference.Get();
                    if (child != null)
                    {
                        CollectTransferMembers(child, selectedAtomIds, members, shouldIncludeMember);
                    }
                }
                return;
            }

            if (selectedAtomIds != null && selectedAtomIds.Contains(member.objectId))
            {
                members.Add(member);
            }
        }

        public static void CollectTransferAtoms(
            IStrategicGroupMemberReferenceable member,
            string rootObjectId,
            List<StrategicGroupTransferAtom> atoms,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            if (member == null || atoms == null)
                return;

            shouldIncludeMember ??= static _ => true;
            if (!shouldIncludeMember(member))
                return;

            if (member is StrategicGroup group && group.deployState == StrategicGroup.DeployState.Combined)
            {
                foreach (var reference in group.directMemberReferences.ToList())
                {
                    var child = reference.Get();
                    if (child != null)
                    {
                        CollectTransferAtoms(child, rootObjectId, atoms, shouldIncludeMember);
                    }
                }
                return;
            }

            atoms.Add(new StrategicGroupTransferAtom()
            {
                objectId = member.objectId,
                rootObjectId = rootObjectId,
                power = Math.Max(0f, member.GetCombinedPowerPoint(true)),
            });
        }

        public static StrategicGroupTransferSelectionSummary BuildSelectionSummary(
            IStrategicGroupMemberReferenceable member,
            HashSet<string> selectedAtomIds,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            var summary = new StrategicGroupTransferSelectionSummary();
            if (member == null)
                return summary;

            shouldIncludeMember ??= static _ => true;
            if (!shouldIncludeMember(member))
                return summary;

            if (member is StrategicGroup group && group.deployState == StrategicGroup.DeployState.Combined)
            {
                foreach (var reference in group.directMemberReferences.ToList())
                {
                    var child = reference.Get();
                    if (child == null)
                        continue;

                    var childSummary = BuildSelectionSummary(child, selectedAtomIds, shouldIncludeMember);
                    summary.totalAtoms += childSummary.totalAtoms;
                    summary.selectedAtoms += childSummary.selectedAtoms;
                    summary.totalPower += childSummary.totalPower;
                    summary.selectedPower += childSummary.selectedPower;
                }
                return summary;
            }

            var power = Math.Max(0f, member.GetCombinedPowerPoint(true));
            summary.totalAtoms = 1;
            summary.selectedAtoms = selectedAtomIds != null && selectedAtomIds.Contains(member.objectId) ? 1 : 0;
            summary.totalPower = power;
            summary.selectedPower = summary.selectedAtoms > 0 ? power : 0f;
            return summary;
        }

        public static StrategicGroup CreateSplitGroupLike(
            StrategicGroup templateGroup,
            StrategicGroup parentGroup,
            StrategicGroup.DeployState deployState)
        {
            if (templateGroup == null)
                return null;

            var splitGroup = new StrategicGroup()
            {
                name = StrategicGroupNamingUtility.BuildGeneratedSubGroupName(templateGroup),
                type = templateGroup.type,
                size = templateGroup.size,
                country = templateGroup.country,
                deployState = deployState,
                homeBaseObjectId = templateGroup.homeBaseObjectId,
            };

            var gameState = StrategicGameState.Instance;
            var templateIndex = gameState?.strategicGroups.IndexOf(templateGroup) ?? -1;
            if (gameState != null)
            {
                if (templateIndex >= 0)
                {
                    gameState.strategicGroups.Insert(templateIndex + 1, splitGroup);
                }
                else
                {
                    gameState.strategicGroups.Add(splitGroup);
                }
            }

            EntityManager.Instance.Register(splitGroup, null);
            splitGroup.AttachTo(parentGroup);

            if (deployState == StrategicGroup.DeployState.Independent && templateGroup.cell != null)
            {
                splitGroup.MoveToCell(templateGroup.cell, false);
            }
            else
            {
                splitGroup.deployState = deployState;
            }

            return splitGroup;
        }

        public static void MaterializePartialGroupSelection(
            StrategicGroup sourceGroup,
            StrategicGroup splitGroup,
            HashSet<string> selectedAtomIds,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            if (sourceGroup == null || splitGroup == null)
                return;

            foreach (var member in CollectTransferMembers(sourceGroup, selectedAtomIds, shouldIncludeMember))
            {
                IStrategicGroupMemberReferenceable.PermanentTransferTo(member, splitGroup);
            }
        }

        public static void MaterializePartialGroupSelectionTemporaryAttach(
            StrategicGroup sourceGroup,
            StrategicGroup splitGroup,
            HashSet<string> selectedAtomIds,
            Func<IStrategicGroupMemberReferenceable, bool> shouldIncludeMember)
        {
            if (sourceGroup == null || splitGroup == null)
                return;

            foreach (var member in CollectTransferMembers(sourceGroup, selectedAtomIds, shouldIncludeMember))
            {
                IStrategicGroupMemberReferenceable.TemporaryAttachTo(member, splitGroup);
            }
        }

        public static void DestroyEmptySplitGroup(StrategicGroup group)
        {
            if (group == null || group.directMemberReferences.Count > 0)
                return;

            StrategicGameState.Instance?.TryDestroyGroupIfEmptyRecursive(group);
        }
    }

    public static class StrategicGroupNamingUtility
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

            var deployState = createIndependent
                ? StrategicGroup.DeployState.Independent
                : StrategicGroup.DeployState.Combined;
            var newGroup = StrategicGroupTransferSplitUtility.CreateSplitGroupLike(sourceGroup, sourceGroup, deployState);
            configureNewGroup?.Invoke(newGroup);
            return newGroup;
        }
    }
}
