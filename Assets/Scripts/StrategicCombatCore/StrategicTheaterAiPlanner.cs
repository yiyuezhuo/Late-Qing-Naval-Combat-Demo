using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using YYZ.PathFinding;

namespace StrategicCombatCore
{
    public sealed class StrategicTheaterAiPlanner
    {
        readonly StrategicGameState state;

        IEnumerable<Theater> theaters => state.theaters;

        public StrategicTheaterAiPlanner(StrategicGameState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        IEnumerable<StrategicGroup> IterIndependentStrategicGroups() => state.IterIndependentStrategicGroups();

        static HashSet<(int, int)> BuildTheaterCellKeySet(IEnumerable<XY> cells)
        {
            var result = new HashSet<(int, int)>();
            foreach (var cell in cells ?? Enumerable.Empty<XY>())
            {
                if (cell?.areaCellObjectId != null)
                    continue;

                result.Add((cell.x, cell.y));
            }

            return result;
        }

        static bool IsHostile(SideState left, SideState right)
        {
            if (left == null || right == null || left == right)
                return false;

            return GetDiplomacyState(left, right) == DiplomacyState.War ||
                   GetDiplomacyState(right, left) == DiplomacyState.War;
        }

        static DiplomacyState? GetDiplomacyState(SideState from, SideState to)
        {
            return from?.diplomacyRelations?
                .FirstOrDefault(relation => relation?.sideObjectId == to?.objectId)?
                .state;
        }

        const float AiFrontlineAllocationEpsilon = 0.0001f;
        public static float reservedPercentForCounterAttack = 0.5f;

        sealed class AiSourcePlanState
        {
            public StrategicGroup group;
            public List<StrategicGroupTransferAtom> orderedAtoms = new();
            public int nextAtomIndex;
            public float remainingPower;

            public Cell cell => group?.cell;
        }

        sealed class AiFrontlineDemandState
        {
            public Cell cell;
            public float remainingDemand;
            public float requestedWeight;
        }

        sealed class AiFrontlineCandidate
        {
            public AiSourcePlanState source;
            public AiFrontlineDemandState demand;
            public float distance;
        }

        sealed class AiPlannedAssignment
        {
            public StrategicGroup rootGroup;
            public Cell targetCell;
            public List<string> atomObjectIds = new();
        }

        sealed class AiAttackTargetState
        {
            public Cell cell;
            public float friendlyPowerInCell;
            public float enemyPower;
        }

        sealed class AiReservedCommitState
        {
            public Cell cell;
            public float remainingPower;
            public int targetOptionCount;
        }

        enum TheaterMergeAssignmentReason
        {
            None,
            CurrentParent,
            DetachedFrom,
            MergeContainer,
        }

        sealed class TheaterMergeMemberPlan
        {
            public IStrategicGroupMemberReferenceable member;
            public StrategicGroup sourceRoot;
            public StrategicGroup originalParent;
            public StrategicGroup plannedParent;
            public TheaterMergeAssignmentReason reason;
        }

        sealed class AiAttackEvaluationContext
        {
            public Dictionary<(int x, int y), AiAttackTargetState> targets = new();
            public Dictionary<(int x, int y), List<(int x, int y)>> targetToReservedKeys = new();
            public Dictionary<(int x, int y), AiReservedCommitState> reservedCommits = new();
            public HashSet<((int x, int y) reservedKey, (int x, int y) targetKey)> coveredThreatPairs = new();
        }

        public void Advance1Day()
        {
            var viewerSideObjectId = StrategicGameManager.Instance?.GetViewerSide()?.objectId;
            foreach (var theater in theaters ?? Enumerable.Empty<Theater>())
            {
                if (theater?.side == null)
                    continue;

                if (!string.IsNullOrEmpty(viewerSideObjectId) && theater.side.objectId == viewerSideObjectId)
                    continue;

                Advance1DayForAiTheaterFrontlineAllocation(theater);
            }
        }

        void Advance1DayForAiTheaterFrontlineAllocation(Theater theater)
        {
            if (theater?.side == null)
                return;

            var theaterCellSet = BuildTheaterCellKeySet(theater.cells);
            var sourceStates = BuildAiTheaterSourceStates(theater.side, theaterCellSet);
            if (sourceStates.Count == 0)
            {
                TryAutoMergeIndependentLandGroupsInTheater(theater, theaterCellSet);
                state.ValidateStrategicGroupMembership("after Theater AI merge without source assignments");
                return;
            }

            var frontlineMovementGraph = new DynamicCellGraphArmyTheaterFrontline() { movingSide = theater.side };
            var frontlinePlan = BuildAiFrontlinePlan(theater, CloneAiSourcePlanStates(sourceStates), frontlineMovementGraph);
            var consumedAtomIds = new HashSet<string>();
            if (theater.posture == TheaterPosture.Attack)
            {
                var attackPlan = BuildAiAttackPlan(theater, CloneAiSourcePlanStates(sourceStates));
                ApplyAiPlannedAssignments(attackPlan, consumedAtomIds);
            }

            ApplyAiPlannedAssignments(frontlinePlan, consumedAtomIds, frontlineMovementGraph);
            TryAutoMergeIndependentLandGroupsInTheater(theater, theaterCellSet);
            state.ValidateStrategicGroupMembership("after Theater AI split/merge assignments");
        }

        static bool IsBetterAiFrontlineCandidate(
            float distance,
            AiFrontlineDemandState demand,
            AiSourcePlanState source,
            AiFrontlineCandidate currentBest)
        {
            if (currentBest == null)
                return true;

            var distanceCompare = distance.CompareTo(currentBest.distance);
            if (distanceCompare != 0)
                return distanceCompare < 0;

            var demandCompare = demand.remainingDemand.CompareTo(currentBest.demand.remainingDemand);
            if (demandCompare != 0)
                return demandCompare > 0;

            var sourceCompare = source.remainingPower.CompareTo(currentBest.source.remainingPower);
            if (sourceCompare != 0)
                return sourceCompare > 0;

            var sourceIdCompare = string.CompareOrdinal(source.group?.objectId, currentBest.source.group?.objectId);
            if (sourceIdCompare != 0)
                return sourceIdCompare < 0;

            return string.CompareOrdinal(
                GetAiFrontlineDemandStableId(demand),
                GetAiFrontlineDemandStableId(currentBest.demand)) < 0;
        }

        static string GetAiFrontlineDemandStableId(AiFrontlineDemandState demand)
        {
            if (demand?.cell == null)
                return string.Empty;

            return $"{demand.cell.x:D4},{demand.cell.y:D4}";
        }

        static string GetAiCellStableId(Cell cell)
        {
            if (cell == null)
                return string.Empty;

            return $"{cell.x:D4},{cell.y:D4}";
        }

        static bool ShouldIncludeMemberInAiFrontlineSplit(IStrategicGroupMemberReferenceable member)
        {
            return member is not StrategicGroup group ||
                (!group.IsFixed && group.deployState == StrategicGroup.DeployState.Combined);
        }

        static bool IsAiCombatableArmyGroup(StrategicGroup group)
        {
            return group != null &&
                   group.LandCombatable() &&
                   !group.IsBase() &&
                   group.type != StrategicGroup.Type.HeadQuarter;
        }

        bool IsExcludedFromTheaterAi(StrategicGroup group)
        {
            return group == null ||
                group.type == StrategicGroup.Type.HeadQuarter ||
                state.landOperationDailyResult?.IsDriven(group) == true;
        }

        bool IsAiTheaterAvailableArmyGroup(StrategicGroup group)
        {
            return IsAiCombatableArmyGroup(group) && !IsExcludedFromTheaterAi(group);
        }

        bool IsAiTheaterSourceGroup(StrategicGroup group, SideState side, HashSet<(int, int)> theaterCellSet)
        {
            return IsAiTheaterAvailableArmyGroup(group) &&
                   group.side == side &&
                   group.cell != null &&
                   theaterCellSet.Contains((group.cell.x, group.cell.y));
        }

        List<StrategicGroupTransferAtom> CollectAiSourceAtoms(StrategicGroup sourceGroup)
        {
            var orderedAtoms = new List<StrategicGroupTransferAtom>();
            if (sourceGroup == null)
                return orderedAtoms;

            foreach (var rootReference in sourceGroup.directMemberReferences.ToList())
            {
                var member = rootReference.Get();
                if (member == null || !ShouldIncludeMemberInAiFrontlineSplit(member))
                    continue;

                StrategicGroupTransferSplitUtility.CollectTransferAtoms(
                    member,
                    member.objectId,
                    orderedAtoms,
                    ShouldIncludeMemberInAiFrontlineSplit);
            }

            return orderedAtoms;
        }

        List<AiSourcePlanState> BuildAiTheaterSourceStates(SideState side, HashSet<(int, int)> theaterCellSet)
        {
            var sourceStates = new List<AiSourcePlanState>();
            foreach (var group in IterIndependentStrategicGroups()
                .Where(group => IsAiTheaterSourceGroup(group, side, theaterCellSet)))
            {
                var orderedAtoms = CollectAiSourceAtoms(group);
                var remainingPower = orderedAtoms.Sum(atom => atom?.power ?? 0f);
                if (remainingPower <= AiFrontlineAllocationEpsilon)
                    continue;

                sourceStates.Add(new AiSourcePlanState()
                {
                    group = group,
                    orderedAtoms = orderedAtoms,
                    remainingPower = remainingPower,
                });
            }

            return sourceStates;
        }

        static List<AiSourcePlanState> CloneAiSourcePlanStates(IEnumerable<AiSourcePlanState> sourceStates)
        {
            return (sourceStates ?? Enumerable.Empty<AiSourcePlanState>())
                .Where(state => state?.group != null && state.remainingPower > AiFrontlineAllocationEpsilon)
                .Select(state => new AiSourcePlanState()
                {
                    group = state.group,
                    orderedAtoms = state.orderedAtoms?.ToList() ?? new List<StrategicGroupTransferAtom>(),
                    nextAtomIndex = state.nextAtomIndex,
                    remainingPower = state.remainingPower,
                })
                .ToList();
        }

        static bool CanSplitAiSourceGroup(StrategicGroup group)
        {
            // Theater AI split is allowed even for already-detached groups. The split semantics
            // follow the transfer dialog's simplify behavior rather than treating detached groups
            // as terminal nodes.
            return group != null;
        }

        bool TryPromoteSingleSelectedGroupForAiSplit(
            StrategicGroup sourceGroup,
            HashSet<string> selectedAtomIdSet,
            out StrategicGroup assignedGroup)
        {
            assignedGroup = null;
            if (sourceGroup == null || selectedAtomIdSet == null || selectedAtomIdSet.Count == 0)
                return false;

            var selectedMembers = StrategicGroupTransferSplitUtility.CollectTransferMembers(
                sourceGroup,
                selectedAtomIdSet,
                ShouldIncludeMemberInAiFrontlineSplit);
            if (selectedMembers.Count != 1 ||
                selectedMembers[0] is not StrategicGroup singleSelectedGroup)
            {
                return false;
            }

            // Preserve the existing subgroup when the requested slice collapses cleanly to one
            // StrategicGroup. This matches the transfer dialog's simplify mode and avoids
            // creating redundant wrapper groups such as "new group -> A1".
            singleSelectedGroup.SetDeployState(StrategicGroup.DeployState.Independent);
            CopyAiFrontlineMovementState(sourceGroup, singleSelectedGroup);
            assignedGroup = singleSelectedGroup;
            return true;
        }

        static bool TryConsumeAiSourceAtoms(
            AiSourcePlanState sourceState,
            float requestedPower,
            bool forceAtLeastOneAtom,
            out List<string> atomObjectIds,
            out float selectedPower,
            out bool usedAllRemaining)
        {
            atomObjectIds = null;
            selectedPower = 0f;
            usedAllRemaining = false;
            if (sourceState == null || sourceState.remainingPower <= AiFrontlineAllocationEpsilon)
                return false;

            var remainingAtoms = (sourceState.orderedAtoms ?? new List<StrategicGroupTransferAtom>())
                .Skip(sourceState.nextAtomIndex)
                .Where(atom => atom != null && atom.power > AiFrontlineAllocationEpsilon)
                .ToList();
            if (remainingAtoms.Count == 0)
                return false;

            var canSplit = CanSplitAiSourceGroup(sourceState.group) && remainingAtoms.Count > 1;
            var selectCount = remainingAtoms.Count;
            if (canSplit && requestedPower < sourceState.remainingPower - AiFrontlineAllocationEpsilon)
            {
                if (forceAtLeastOneAtom && requestedPower <= AiFrontlineAllocationEpsilon)
                {
                    selectCount = 1;
                }
                else
                {
                    var bestPrefixLength = -1;
                    var bestDiff = float.PositiveInfinity;
                    var cumulativePower = 0f;
                    for (var prefixLength = 1; prefixLength < remainingAtoms.Count; prefixLength++)
                    {
                        cumulativePower += remainingAtoms[prefixLength - 1].power;
                        var diff = Math.Abs(cumulativePower - requestedPower);
                        if (diff < bestDiff - AiFrontlineAllocationEpsilon)
                        {
                            bestDiff = diff;
                            bestPrefixLength = prefixLength;
                        }
                    }

                    if (bestPrefixLength > 0)
                        selectCount = bestPrefixLength;
                }
            }

            atomObjectIds = remainingAtoms.Take(selectCount).Select(atom => atom.objectId).ToList();
            selectedPower = remainingAtoms.Take(selectCount).Sum(atom => atom.power);
            sourceState.nextAtomIndex += selectCount;
            sourceState.remainingPower = Math.Max(0f, sourceState.remainingPower - selectedPower);
            usedAllRemaining = sourceState.remainingPower <= AiFrontlineAllocationEpsilon ||
                               sourceState.nextAtomIndex >= sourceState.orderedAtoms.Count;
            return atomObjectIds.Count > 0 && selectedPower > AiFrontlineAllocationEpsilon;
        }

        List<AiPlannedAssignment> BuildAiFrontlinePlan(
            Theater theater,
            List<AiSourcePlanState> sourceStates,
            IGraphEnumerable<Cell> movementGraph)
        {
            var assignments = new List<AiPlannedAssignment>();
            if (theater == null || sourceStates == null || sourceStates.Count == 0)
                return assignments;

            var weightedFrontlineCells = (theater.frontlineCellInfos ?? Enumerable.Empty<FrontlineCellInfo>())
                .Where(info => info != null && info.weightRequested > AiFrontlineAllocationEpsilon)
                .Select(info => info.xy?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => new
                {
                    cell = group.First(),
                    weight = theater.frontlineCellInfos
                        .Where(info => info != null && info.x == group.Key.x && info.y == group.Key.y)
                        .Sum(info => info.weightRequested)
                })
                .Where(item => item.weight > AiFrontlineAllocationEpsilon)
                .ToList();
            if (weightedFrontlineCells.Count == 0)
                return assignments;

            var totalPower = sourceStates.Sum(state => state.remainingPower);
            var totalWeight = weightedFrontlineCells.Sum(item => item.weight);
            if (totalPower <= AiFrontlineAllocationEpsilon || totalWeight <= AiFrontlineAllocationEpsilon)
                return assignments;

            var demandStates = weightedFrontlineCells
                .Select(item => new AiFrontlineDemandState()
                {
                    cell = item.cell,
                    requestedWeight = item.weight,
                    remainingDemand = totalPower * item.weight / totalWeight,
                })
                .Where(state => state.remainingDemand > AiFrontlineAllocationEpsilon)
                .ToList();
            if (demandStates.Count == 0)
                return assignments;

            var pathCostCache = new Dictionary<(Cell src, Cell dst), AStarResult<Cell>>();
            while (sourceStates.Count > 0 && demandStates.Count > 0)
            {
                AiFrontlineCandidate bestCandidate = null;
                foreach (var sourceState in sourceStates)
                {
                    foreach (var demandState in demandStates)
                    {
                        if (!TryGetAiFrontlineEffectiveDistance(
                            sourceState.group,
                            demandState.cell,
                            movementGraph,
                            pathCostCache,
                            out var distance))
                        {
                            continue;
                        }

                        if (IsBetterAiFrontlineCandidate(distance, demandState, sourceState, bestCandidate))
                        {
                            bestCandidate = new AiFrontlineCandidate()
                            {
                                source = sourceState,
                                demand = demandState,
                                distance = distance,
                            };
                        }
                    }
                }

                if (bestCandidate == null)
                    break;

                var source = bestCandidate.source;
                var demand = bestCandidate.demand;
                var requestedPower = source.remainingPower <= demand.remainingDemand + AiFrontlineAllocationEpsilon
                    ? source.remainingPower
                    : demand.remainingDemand;
                if (!TryConsumeAiSourceAtoms(
                    source,
                    requestedPower,
                    false,
                    out var atomObjectIds,
                    out var selectedPower,
                    out var usedAllRemaining))
                {
                    sourceStates.Remove(source);
                    continue;
                }

                AppendAiPlannedAssignment(assignments, source.group, demand.cell, atomObjectIds);
                demand.remainingDemand = usedAllRemaining
                    ? Math.Max(0f, demand.remainingDemand - selectedPower)
                    : 0f;

                if (source.remainingPower <= AiFrontlineAllocationEpsilon)
                    sourceStates.Remove(source);
                if (demand.remainingDemand <= AiFrontlineAllocationEpsilon)
                    demandStates.Remove(demand);
            }

            return assignments;
        }

        float GetAiFriendlyPowerInCell(Cell cell, SideState side)
        {
            return GetAiPowerInCell(cell, group => group.side == side);
        }

        float GetAiHostilePowerInCell(Cell cell, SideState side)
        {
            return GetAiPowerInCell(cell, group => IsHostile(side, group.side));
        }

        float GetAiPowerInCell(Cell cell, Func<StrategicGroup, bool> predicate)
        {
            if (cell == null || predicate == null)
                return 0f;

            return cell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .Where(group => IsAiTheaterAvailableArmyGroup(group) && predicate(group))
                .Sum(group => group.WalkGroupMembers<LandUnit>().Sum(landUnit => landUnit?.GetCombinedPowerPoint(false) ?? 0f));
        }

        bool HasAiFriendlyPresenceAroundCell(Cell cell, SideState side)
        {
            if (cell == null || side == null)
                return false;

            foreach (var candidate in cell.GetNeighbors().Prepend(cell))
            {
                if (GetAiFriendlyPowerInCell(candidate, side) > AiFrontlineAllocationEpsilon)
                    return true;
            }

            return false;
        }

        IEnumerable<Cell> BuildAiAttackCandidateTargets(Theater theater, IEnumerable<AiSourcePlanState> sourceStates)
        {
            if (theater?.side == null || sourceStates == null)
                yield break;

            var yielded = new HashSet<(int, int)>();
            foreach (var sourceCell in sourceStates
                .Select(state => state?.cell)
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable()))
            {
                foreach (var cell in sourceCell.GetNeighbors().Prepend(sourceCell))
                {
                    if (cell == null ||
                        !cell.IsGridCell() ||
                        !cell.IsArmyPassable() ||
                        !yielded.Add((cell.x, cell.y)))
                    {
                        continue;
                    }

                    var enemyPower = GetAiHostilePowerInCell(cell, theater.side);
                    var isControlled = cell.GetHexSide()?.objectId == theater.side.objectId;
                    if ((enemyPower > AiFrontlineAllocationEpsilon || !isControlled) &&
                        HasAiFriendlyPresenceAroundCell(cell, theater.side))
                    {
                        yield return cell;
                    }
                }
            }
        }

        AiAttackEvaluationContext BuildAiAttackEvaluationContext(
            SideState side,
            IEnumerable<Cell> targetCells,
            IEnumerable<AiSourcePlanState> sourceStates,
            IEnumerable<Cell> selectableTargets = null,
            IEnumerable<AiPlannedAssignment> assignments = null)
        {
            var context = new AiAttackEvaluationContext();
            if (side == null)
                return context;

            var sourceStatesByCell = (sourceStates ?? Enumerable.Empty<AiSourcePlanState>())
                .Where(state => state?.cell != null && state.remainingPower > AiFrontlineAllocationEpsilon)
                .GroupBy(state => (state.cell.x, state.cell.y))
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var cellGroup in sourceStatesByCell)
            {
                var cell = cellGroup.Value[0].cell;
                if (cell == null || GetAiHostilePowerInCell(cell, side) > AiFrontlineAllocationEpsilon)
                    continue;

                context.reservedCommits[cellGroup.Key] = new AiReservedCommitState()
                {
                    cell = cell,
                    remainingPower = cellGroup.Value.Sum(state => state.remainingPower),
                };
            }

            foreach (var targetCell in (targetCells ?? Enumerable.Empty<Cell>())
                .Where(cell => cell != null)
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First()))
            {
                var key = (targetCell.x, targetCell.y);
                context.targets[key] = new AiAttackTargetState()
                {
                    cell = targetCell,
                    friendlyPowerInCell = GetAiFriendlyPowerInCell(targetCell, side),
                    enemyPower = GetAiHostilePowerInCell(targetCell, side),
                };
                context.targetToReservedKeys[key] = targetCell.GetNeighbors()
                    .Where(cell => cell != null)
                    .Select(cell => (cell.x, cell.y))
                    .Where(context.reservedCommits.ContainsKey)
                    .Distinct()
                    .ToList();
            }

            var selectableTargetSet = (selectableTargets ?? targetCells ?? Enumerable.Empty<Cell>())
                .Where(cell => cell != null)
                .Select(cell => (cell.x, cell.y))
                .ToHashSet();

            foreach (var reservedKey in context.reservedCommits.Keys.ToList())
            {
                context.reservedCommits[reservedKey].targetOptionCount = context.targetToReservedKeys
                    .Count(entry => selectableTargetSet.Contains(entry.Key) && entry.Value.Contains(reservedKey));
            }

            foreach (var assignment in assignments ?? Enumerable.Empty<AiPlannedAssignment>())
            {
                if (assignment?.rootGroup?.cell == null ||
                    assignment.targetCell == null ||
                    assignment.atomObjectIds == null ||
                    assignment.atomObjectIds.Count == 0)
                {
                    continue;
                }

                context.coveredThreatPairs.Add((
                    (assignment.rootGroup.cell.x, assignment.rootGroup.cell.y),
                    (assignment.targetCell.x, assignment.targetCell.y)));
            }

            foreach (var group in IterIndependentStrategicGroups())
            {
                if (!IsAiTheaterAvailableArmyGroup(group) ||
                    group.side != side ||
                    group.cell == null ||
                    group.plannedPath == null ||
                    group.plannedPath.Count < 2)
                {
                    continue;
                }

                var nextCell = group.plannedPath[1].GetCell();
                if (nextCell == null)
                    continue;

                context.coveredThreatPairs.Add((
                    (group.cell.x, group.cell.y),
                    (nextCell.x, nextCell.y)));
            }

            return context;
        }

        bool IsAiThreatCovered(
            AiAttackEvaluationContext context,
            (int x, int y) reservedKey,
            (int x, int y) targetKey,
            SideState side)
        {
            if (context == null || side == null)
                return false;

            if (context.coveredThreatPairs.Contains((reservedKey, targetKey)))
                return true;

            var targetCell = context.targets.GetValueOrDefault(targetKey)?.cell;
            var reservedCell = context.reservedCommits.GetValueOrDefault(reservedKey)?.cell;
            if (targetCell == null || reservedCell == null)
                return false;

            return targetCell.TryGetDirection(reservedCell, out var edge) &&
                   targetCell.GetEdgeSide(edge) == side;
        }

        bool IsAiHostileThreatCell(Cell cell, SideState side)
        {
            return cell != null &&
                   side != null &&
                   GetAiHostilePowerInCell(cell, side) > AiFrontlineAllocationEpsilon;
        }

        bool HasUncoveredAiCounterAttackThreat(
            AiAttackEvaluationContext context,
            (int x, int y) reservedKey,
            (int x, int y) activeTargetKey,
            SideState side)
        {
            if (context == null ||
                !context.reservedCommits.TryGetValue(reservedKey, out var reservedState) ||
                reservedState?.cell == null)
            {
                return false;
            }

            foreach (var neighbor in reservedState.cell.GetNeighbors())
            {
                if (neighbor == null)
                    continue;

                var threatKey = (neighbor.x, neighbor.y);
                if (threatKey == activeTargetKey)
                    continue;

                if (!IsAiHostileThreatCell(neighbor, side))
                    continue;

                if (!IsAiThreatCovered(context, reservedKey, threatKey, side))
                    return true;
            }

            return false;
        }

        List<Cell> PruneAiAttackTargets(SideState side, IEnumerable<Cell> candidateTargets, IEnumerable<AiSourcePlanState> sourceStates)
        {
            var targets = (candidateTargets ?? Enumerable.Empty<Cell>())
                .Where(cell => cell != null)
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First())
                .ToList();
            if (side == null || targets.Count == 0)
                return new List<Cell>();

            while (true)
            {
                var context = BuildAiAttackEvaluationContext(side, targets, sourceStates);
                var nextTargets = targets
                    .Where(target =>
                    {
                        var key = (target.x, target.y);
                        if (!context.targetToReservedKeys.TryGetValue(key, out var reservedKeys) || reservedKeys.Count == 0)
                            return false;

                        var targetState = context.targets[key];
                        if (targetState.enemyPower <= AiFrontlineAllocationEpsilon)
                            return true;

                        var reservablePower = reservedKeys
                            .Select(reservedKey => context.reservedCommits.GetValueOrDefault(reservedKey)?.remainingPower ?? 0f)
                            .Sum();
                        return targetState.friendlyPowerInCell + reservablePower + AiFrontlineAllocationEpsilon >=
                               targetState.enemyPower * 1.5f;
                    })
                    .ToList();
                if (nextTargets.Count == targets.Count)
                    return nextTargets;

                targets = nextTargets;
                if (targets.Count == 0)
                    return targets;
            }
        }

        static Cell SelectBestAiAttackTarget(AiAttackEvaluationContext context, IEnumerable<Cell> candidateTargets)
        {
            Cell bestTarget = null;
            var bestReservedCount = int.MaxValue;
            var bestEnemyPower = float.PositiveInfinity;
            var bestStableId = string.Empty;
            foreach (var target in candidateTargets ?? Enumerable.Empty<Cell>())
            {
                if (target == null)
                    continue;

                var key = (target.x, target.y);
                var reservedCount = context.targetToReservedKeys.GetValueOrDefault(key)?.Count ?? 0;
                if (reservedCount <= 0)
                    continue;

                var enemyPower = context.targets.GetValueOrDefault(key)?.enemyPower ?? 0f;
                var stableId = GetAiCellStableId(target);
                if (bestTarget == null ||
                    reservedCount < bestReservedCount ||
                    (reservedCount == bestReservedCount && enemyPower < bestEnemyPower - AiFrontlineAllocationEpsilon) ||
                    (reservedCount == bestReservedCount &&
                     Math.Abs(enemyPower - bestEnemyPower) <= AiFrontlineAllocationEpsilon &&
                     string.CompareOrdinal(stableId, bestStableId) < 0))
                {
                    bestTarget = target;
                    bestReservedCount = reservedCount;
                    bestEnemyPower = enemyPower;
                    bestStableId = stableId;
                }
            }

            return bestTarget;
        }

        bool ShouldContinueAiAttackPass1Allocation(float enemyPower, float friendlyPowerInCell, float reservedCommittedPower)
        {
            if (enemyPower <= AiFrontlineAllocationEpsilon)
                return friendlyPowerInCell + reservedCommittedPower <= AiFrontlineAllocationEpsilon;

            return friendlyPowerInCell + reservedCommittedPower + AiFrontlineAllocationEpsilon < enemyPower * 3f;
        }

        static void AppendAiPlannedAssignment(
            List<AiPlannedAssignment> assignments,
            StrategicGroup rootGroup,
            Cell targetCell,
            IEnumerable<string> atomObjectIds)
        {
            if (assignments == null || rootGroup == null || targetCell == null)
                return;

            var atomIds = (atomObjectIds ?? Enumerable.Empty<string>())
                .Where(atomObjectId => !string.IsNullOrWhiteSpace(atomObjectId))
                .ToList();
            if (atomIds.Count == 0)
                return;

            var existing = assignments.FirstOrDefault(assignment =>
                assignment?.rootGroup == rootGroup &&
                assignment.targetCell != null &&
                assignment.targetCell.x == targetCell.x &&
                assignment.targetCell.y == targetCell.y);
            if (existing == null)
            {
                existing = new AiPlannedAssignment()
                {
                    rootGroup = rootGroup,
                    targetCell = targetCell,
                };
                assignments.Add(existing);
            }

            existing.atomObjectIds.AddRange(atomIds);
        }

        void AllocateAiAttackTarget(
            AiAttackEvaluationContext context,
            SideState side,
            Cell targetCell,
            List<AiSourcePlanState> sourceStates,
            List<AiPlannedAssignment> assignments,
            bool limitToThreeToOne)
        {
            if (context == null || side == null || targetCell == null || sourceStates == null || assignments == null)
                return;

            var targetKey = (targetCell.x, targetCell.y);
            if (!context.targetToReservedKeys.TryGetValue(targetKey, out var reservedKeys) || reservedKeys.Count == 0)
                return;

            var targetState = context.targets.GetValueOrDefault(targetKey);
            if (targetState == null)
                return;

            var sourceStateSnapshots = sourceStates.ToDictionary(
                state => state,
                state => (nextAtomIndex: state.nextAtomIndex, remainingPower: state.remainingPower));
            var assignmentSnapshots = assignments
                .Select(assignment => new AiPlannedAssignment()
                {
                    rootGroup = assignment.rootGroup,
                    targetCell = assignment.targetCell,
                    atomObjectIds = assignment.atomObjectIds?.ToList() ?? new List<string>(),
                })
                .ToList();

            var orderedReservedKeys = reservedKeys
                .OrderBy(key => context.reservedCommits.GetValueOrDefault(key)?.targetOptionCount ?? int.MaxValue)
                .ThenBy(key => key.x)
                .ThenBy(key => key.y)
                .ToList();
            var reservedCommittedPower = 0f;
            foreach (var reservedKey in orderedReservedKeys)
            {
                var cellSources = sourceStates
                    .Where(state =>
                        state?.cell != null &&
                        state.remainingPower > AiFrontlineAllocationEpsilon &&
                        state.cell.x == reservedKey.x &&
                        state.cell.y == reservedKey.y)
                    .OrderBy(state => state.group?.objectId)
                    .ToList();
                if (cellSources.Count == 0)
                    continue;

                while (cellSources.Any(state => state.remainingPower > AiFrontlineAllocationEpsilon))
                {
                    if (limitToThreeToOne &&
                        !ShouldContinueAiAttackPass1Allocation(
                            targetState.enemyPower,
                            targetState.friendlyPowerInCell,
                            reservedCommittedPower))
                    {
                        return;
                    }

                    var sourceState = cellSources.FirstOrDefault(state => state.remainingPower > AiFrontlineAllocationEpsilon);
                    if (sourceState == null)
                        break;

                    var remainingCellPower = cellSources
                        .Where(state => state.remainingPower > AiFrontlineAllocationEpsilon)
                        .Sum(state => state.remainingPower);
                    var hasUncoveredThreat = HasUncoveredAiCounterAttackThreat(
                        context,
                        reservedKey,
                        targetKey,
                        side);
                    var reservePower = hasUncoveredThreat
                        ? remainingCellPower * Math.Clamp(reservedPercentForCounterAttack, 0f, 1f)
                        : 0f;
                    var maxAllocatablePower = Math.Max(0f, remainingCellPower - reservePower);
                    if (maxAllocatablePower <= AiFrontlineAllocationEpsilon)
                        break;

                    var requestedPower = limitToThreeToOne
                        ? Math.Max(0f, targetState.enemyPower * 3f - targetState.friendlyPowerInCell - reservedCommittedPower)
                        : float.PositiveInfinity;
                    requestedPower = Math.Min(requestedPower, maxAllocatablePower);
                    var forceAtLeastOneAtom = limitToThreeToOne &&
                                              targetState.enemyPower <= AiFrontlineAllocationEpsilon &&
                                              targetState.friendlyPowerInCell + reservedCommittedPower <= AiFrontlineAllocationEpsilon;

                    List<string> atomObjectIds = null;
                    float selectedPower = 0f;
                    var consumed = false;
                    foreach (var candidateSource in cellSources
                        .Where(state => state.remainingPower > AiFrontlineAllocationEpsilon)
                        .OrderBy(state => state.group?.objectId))
                    {
                        var snapshot = (candidateSource.nextAtomIndex, candidateSource.remainingPower);
                        if (!TryConsumeAiSourceAtoms(
                            candidateSource,
                            requestedPower,
                            forceAtLeastOneAtom,
                            out atomObjectIds,
                            out selectedPower,
                            out _))
                        {
                            continue;
                        }

                        if (selectedPower <= maxAllocatablePower + AiFrontlineAllocationEpsilon)
                        {
                            sourceState = candidateSource;
                            consumed = true;
                            break;
                        }

                        candidateSource.nextAtomIndex = snapshot.nextAtomIndex;
                        candidateSource.remainingPower = snapshot.remainingPower;
                    }

                    if (!consumed)
                    {
                        break;
                    }

                    AppendAiPlannedAssignment(assignments, sourceState.group, targetCell, atomObjectIds);
                    reservedCommittedPower += selectedPower;
                    context.coveredThreatPairs.Add((reservedKey, targetKey));
                }
            }

            if (targetState.enemyPower > AiFrontlineAllocationEpsilon &&
                targetState.friendlyPowerInCell + reservedCommittedPower + AiFrontlineAllocationEpsilon < targetState.enemyPower * 1.5f)
            {
                foreach (var snapshot in sourceStateSnapshots)
                {
                    snapshot.Key.nextAtomIndex = snapshot.Value.nextAtomIndex;
                    snapshot.Key.remainingPower = snapshot.Value.remainingPower;
                }

                assignments.Clear();
                assignments.AddRange(assignmentSnapshots);
            }
        }

        void RunAiAttackPlanningPass(
            SideState side,
            List<AiSourcePlanState> sourceStates,
            IEnumerable<Cell> baseTargets,
            List<AiPlannedAssignment> assignments,
            bool limitToThreeToOne)
        {
            var remainingTargets = (baseTargets ?? Enumerable.Empty<Cell>())
                .Where(cell => cell != null)
                .GroupBy(cell => (cell.x, cell.y))
                .ToDictionary(group => group.Key, group => group.First());
            while (remainingTargets.Count > 0)
            {
                var context = BuildAiAttackEvaluationContext(
                    side,
                    baseTargets,
                    sourceStates,
                    remainingTargets.Values,
                    assignments);
                var targetCell = SelectBestAiAttackTarget(
                    context,
                    remainingTargets.Values.Where(target =>
                        context.targetToReservedKeys.GetValueOrDefault((target.x, target.y))?.Count > 0));
                if (targetCell == null)
                    break;

                AllocateAiAttackTarget(context, side, targetCell, sourceStates, assignments, limitToThreeToOne);
                remainingTargets.Remove((targetCell.x, targetCell.y));
            }
        }

        List<AiPlannedAssignment> BuildAiAttackPlan(Theater theater, List<AiSourcePlanState> sourceStates)
        {
            var assignments = new List<AiPlannedAssignment>();
            if (theater?.side == null || sourceStates == null || sourceStates.Count == 0)
                return assignments;

            var validTargets = PruneAiAttackTargets(
                theater.side,
                BuildAiAttackCandidateTargets(theater, sourceStates),
                sourceStates);
            if (validTargets.Count == 0)
                return assignments;

            RunAiAttackPlanningPass(theater.side, sourceStates, validTargets, assignments, true);
            RunAiAttackPlanningPass(theater.side, sourceStates, validTargets, assignments, false);
            return assignments;
        }

        static void CopyAiFrontlineMovementState(StrategicGroup sourceGroup, StrategicGroup targetGroup)
        {
            if (sourceGroup == null || targetGroup == null)
                return;

            targetGroup.plannedPath = sourceGroup.plannedPath
                .Select(xy => new XY()
                {
                    x = xy.x,
                    y = xy.y,
                    areaCellObjectId = xy.areaCellObjectId,
                })
                .ToList();
            targetGroup.moveProgressionKm = sourceGroup.moveProgressionKm;
        }

        bool TryMaterializeAiAssignmentGroup(
            StrategicGroup sourceGroup,
            IReadOnlyCollection<string> requestedAtomIds,
            out StrategicGroup assignedGroup,
            out List<string> materializedAtomIds)
        {
            assignedGroup = null;
            materializedAtomIds = new List<string>();
            if (sourceGroup == null || requestedAtomIds == null || requestedAtomIds.Count == 0)
                return false;

            var availableAtoms = CollectAiSourceAtoms(sourceGroup);
            materializedAtomIds = availableAtoms
                .Where(atom => atom != null && requestedAtomIds.Contains(atom.objectId))
                .Select(atom => atom.objectId)
                .ToList();
            if (materializedAtomIds.Count == 0)
                return false;

            if (materializedAtomIds.Count == availableAtoms.Count)
            {
                assignedGroup = sourceGroup;
                return true;
            }

            if (!CanSplitAiSourceGroup(sourceGroup))
                return false;

            var selectedAtomIdSet = materializedAtomIds.ToHashSet();
            if (TryPromoteSingleSelectedGroupForAiSplit(sourceGroup, selectedAtomIdSet, out assignedGroup))
                return true;

            var parentGroup = sourceGroup.parentGroupReference.Get();
            assignedGroup = StrategicGroupTransferSplitUtility.CreateSplitGroupLike(
                sourceGroup,
                parentGroup,
                StrategicGroup.DeployState.Independent,
                nonHistorical: true);
            if (assignedGroup == null)
                return false;

            CopyAiFrontlineMovementState(sourceGroup, assignedGroup);
            // Newly created AI split groups are just containers for the detached members chosen in
            // this assignment. The container itself should not remember a detached-from source;
            // only members whose parent changes should keep detached-from references, using the
            // same semantics as TemporaryAttachTo.
            IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(assignedGroup);
            StrategicGroupTransferSplitUtility.MaterializePartialGroupSelectionTemporaryAttach(
                sourceGroup,
                assignedGroup,
                selectedAtomIdSet,
                ShouldIncludeMemberInAiFrontlineSplit);

            if (assignedGroup.directMemberReferences.Count == 0)
            {
                StrategicGroupTransferSplitUtility.DestroyEmptySplitGroup(assignedGroup);
                assignedGroup = null;
                return false;
            }

            return true;
        }

        void ApplyAiPlannedAssignments(
            IEnumerable<AiPlannedAssignment> assignments,
            HashSet<string> consumedAtomIds,
            IGraphEnumerable<Cell> movementGraph = null)
        {
            if (assignments == null)
                return;

            consumedAtomIds ??= new HashSet<string>();
            movementGraph ??= new DynamicCellGraphArmy();
            foreach (var assignment in assignments)
            {
                var sourceGroup = assignment?.rootGroup;
                var targetCell = assignment?.targetCell;
                if (sourceGroup?.cell == null ||
                    targetCell == null ||
                    sourceGroup.deployState != StrategicGroup.DeployState.Independent)
                {
                    continue;
                }

                var requestedAtomIds = assignment.atomObjectIds
                    .Where(atomObjectId => !string.IsNullOrWhiteSpace(atomObjectId) && !consumedAtomIds.Contains(atomObjectId))
                    .ToList();
                if (requestedAtomIds.Count == 0 ||
                    !TryMaterializeAiAssignmentGroup(sourceGroup, requestedAtomIds, out var group, out var materializedAtomIds))
                {
                    continue;
                }

                foreach (var atomObjectId in materializedAtomIds)
                    consumedAtomIds.Add(atomObjectId);

                if (group.cell == targetCell)
                {
                    group.ClearPlannedPath();
                    continue;
                }

                var pathResult = PathFinding<Cell>.AStar3(movementGraph, group.cell, targetCell);
                if (pathResult?.Path == null || pathResult.Path.Count < 2)
                    continue;

                group.SetPlannedPath(pathResult.Path.Select(cell => cell.ToXY()).ToList());
            }
        }

        bool IsEligibleTheaterLandMergeGroup(StrategicGroup group, Theater theater, HashSet<(int, int)> theaterCellSet)
        {
            return group != null &&
                   group.deployState == StrategicGroup.DeployState.Independent &&
                   group.CanActStrategically &&
                   group.IsArmy() &&
                   group.type != StrategicGroup.Type.Base &&
                   group.type != StrategicGroup.Type.HeadQuarter &&
                   !IsExcludedFromTheaterAi(group) &&
                   group.side == theater?.side &&
                   group.cell != null &&
                   theaterCellSet.Contains((group.cell.x, group.cell.y));
        }

        static bool HasEquivalentTheaterMergeMovementState(StrategicGroup left, StrategicGroup right)
        {
            if (left == null || right == null)
                return false;

            var leftMoving = left.IsMovingStrategically;
            var rightMoving = right.IsMovingStrategically;
            if (!leftMoving || !rightMoving)
                return !leftMoving && !rightMoving;

            return left.HasSamePlannedPathAndProgressAs(right);
        }

        static void RecordTheaterMergeDirectMemberOrder(
            StrategicGroup group,
            Dictionary<(string parentId, string memberId), int> originalDirectMemberOrders)
        {
            if (group?.directMemberReferences == null || originalDirectMemberOrders == null)
                return;

            for (var idx = 0; idx < group.directMemberReferences.Count; idx++)
            {
                var memberId = group.directMemberReferences[idx]?.referenceId;
                if (!string.IsNullOrWhiteSpace(memberId))
                    originalDirectMemberOrders.TryAdd((group.objectId, memberId), idx);
            }
        }

        static void CollectTheaterMergePoolMember(
            IStrategicGroupMemberReferenceable member,
            StrategicGroup sourceRoot,
            List<IStrategicGroupMemberReferenceable> poolMembers,
            List<StrategicGroup> nonHistoricalContainers,
            HashSet<string> seenMemberIds,
            Dictionary<string, StrategicGroup> sourceRootByMemberId,
            Dictionary<(string parentId, string memberId), int> originalDirectMemberOrders)
        {
            if (member == null ||
                string.IsNullOrWhiteSpace(member.objectId) ||
                !seenMemberIds.Add(member.objectId))
            {
                return;
            }

            sourceRootByMemberId?.TryAdd(member.objectId, sourceRoot);
            if (member is StrategicGroup group)
            {
                RecordTheaterMergeDirectMemberOrder(group, originalDirectMemberOrders);
                if (group.nonHistorical)
                    nonHistoricalContainers.Add(group);
                else
                    poolMembers.Add(group);

                if (group.nonHistorical || group.deployState == StrategicGroup.DeployState.Combined)
                {
                    foreach (var memberRef in group.directMemberReferences.ToList())
                    {
                        CollectTheaterMergePoolMember(
                            memberRef.Get(),
                            sourceRoot,
                            poolMembers,
                            nonHistoricalContainers,
                            seenMemberIds,
                            sourceRootByMemberId,
                            originalDirectMemberOrders);
                    }
                }

                return;
            }

            poolMembers.Add(member);
        }

        static bool IsValidTheaterMergePoolParent(
            IStrategicGroupMemberReferenceable member,
            StrategicGroup parent,
            HashSet<string> poolGroupIds)
        {
            if (member == null ||
                parent == null ||
                string.IsNullOrWhiteSpace(parent.objectId) ||
                poolGroupIds == null ||
                !poolGroupIds.Contains(parent.objectId))
            {
                return false;
            }

            if (member is StrategicGroup memberGroup)
            {
                if (parent == memberGroup || parent.IsDescendantOf(memberGroup))
                    return false;
            }

            return true;
        }

        static bool TryGetTheaterMergePlannedCycle(
            Dictionary<string, TheaterMergeMemberPlan> plansByMemberId,
            HashSet<string> poolGroupIds,
            out List<string> cycleGroupIds)
        {
            cycleGroupIds = null;
            foreach (var rootPlan in plansByMemberId.Values)
            {
                if (rootPlan?.member is not StrategicGroup rootGroup)
                    continue;

                var path = new List<string>();
                var pathIndices = new Dictionary<string, int>();
                var currentGroupId = rootGroup.objectId;
                while (!string.IsNullOrWhiteSpace(currentGroupId) &&
                       poolGroupIds.Contains(currentGroupId) &&
                       plansByMemberId.TryGetValue(currentGroupId, out var currentPlan))
                {
                    if (pathIndices.TryGetValue(currentGroupId, out var cycleStart))
                    {
                        cycleGroupIds = path.Skip(cycleStart).ToList();
                        return true;
                    }

                    pathIndices.Add(currentGroupId, path.Count);
                    path.Add(currentGroupId);
                    currentGroupId = currentPlan.plannedParent?.objectId;
                }
            }

            return false;
        }

        static void BreakTheaterMergePlannedCycles(Dictionary<string, TheaterMergeMemberPlan> plansByMemberId, HashSet<string> poolGroupIds)
        {
            while (TryGetTheaterMergePlannedCycle(plansByMemberId, poolGroupIds, out var cycleGroupIds))
            {
                var detachedCyclePlan = cycleGroupIds
                    .Select(groupId => plansByMemberId.GetValueOrDefault(groupId))
                    .FirstOrDefault(plan => plan?.reason == TheaterMergeAssignmentReason.DetachedFrom);
                var planToBreak = detachedCyclePlan ?? plansByMemberId.GetValueOrDefault(cycleGroupIds.FirstOrDefault());
                if (planToBreak == null)
                    return;

                planToBreak.plannedParent = null;
                planToBreak.reason = TheaterMergeAssignmentReason.None;
            }
        }

        static List<TheaterMergeMemberPlan> BuildTheaterMergeMemberPlans(
            List<IStrategicGroupMemberReferenceable> poolMembers,
            HashSet<string> poolGroupIds)
        {
            var plans = new List<TheaterMergeMemberPlan>();
            foreach (var member in poolMembers ?? new List<IStrategicGroupMemberReferenceable>())
            {
                var plan = new TheaterMergeMemberPlan()
                {
                    member = member,
                    originalParent = member.parentGroupReference.Get(),
                };
                var detachedFromGroup = member.GetDetachedFromGroup();
                if (IsValidTheaterMergePoolParent(member, detachedFromGroup, poolGroupIds))
                {
                    plan.plannedParent = detachedFromGroup;
                    plan.reason = TheaterMergeAssignmentReason.DetachedFrom;
                }
                else if (IsValidTheaterMergePoolParent(member, plan.originalParent, poolGroupIds))
                {
                    plan.plannedParent = plan.originalParent;
                    plan.reason = TheaterMergeAssignmentReason.CurrentParent;
                }

                plans.Add(plan);
            }

            BreakTheaterMergePlannedCycles(
                plans
                    .Where(plan => plan?.member != null && !string.IsNullOrWhiteSpace(plan.member.objectId))
                    .ToDictionary(plan => plan.member.objectId, plan => plan),
                poolGroupIds);
            return plans;
        }

        static StrategicGroup SelectTheaterMergeContainer(IEnumerable<StrategicGroup> activeGroups)
        {
            return (activeGroups ?? Enumerable.Empty<StrategicGroup>())
                .Where(group => group != null && group.nonHistorical)
                .OrderByDescending(group => group.GetStrengthMen())
                .ThenByDescending(group => group.GetCombinedPowerPoint(true))
                .ThenBy(group => group.objectId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        static int GetOriginalTheaterMergeDirectMemberOrder(
            StrategicGroup parent,
            string memberId,
            Dictionary<(string parentId, string memberId), int> originalDirectMemberOrders)
        {
            if (parent == null || string.IsNullOrWhiteSpace(memberId) || originalDirectMemberOrders == null)
                return int.MaxValue;

            return originalDirectMemberOrders.GetValueOrDefault((parent.objectId, memberId), int.MaxValue);
        }

        static void SortTheaterMergeDirectMembers(
            StrategicGroup parent,
            Dictionary<(string parentId, string memberId), int> originalDirectMemberOrders)
        {
            if (parent?.directMemberReferences == null)
                return;

            parent.directMemberReferences.Sort((left, right) =>
            {
                var leftOrder = GetOriginalTheaterMergeDirectMemberOrder(parent, left?.referenceId, originalDirectMemberOrders);
                var rightOrder = GetOriginalTheaterMergeDirectMemberOrder(parent, right?.referenceId, originalDirectMemberOrders);
                var orderResult = leftOrder.CompareTo(rightOrder);
                if (orderResult != 0)
                    return orderResult;

                var leftMember = left?.Get();
                var rightMember = right?.Get();
                if (leftMember == null || rightMember == null)
                {
                    if (leftMember == null && rightMember == null)
                        return string.Compare(left?.referenceId, right?.referenceId, StringComparison.Ordinal);
                    return leftMember == null ? 1 : -1;
                }

                var powerResult = rightMember.GetCombinedPowerPoint(true).CompareTo(leftMember.GetCombinedPowerPoint(true));
                if (powerResult != 0)
                    return powerResult;

                return string.Compare(leftMember.objectId, rightMember.objectId, StringComparison.Ordinal);
            });
        }

        void DestroyEmptyTheaterMergeNonHistoricalContainer(StrategicGroup group, IEnumerable<IStrategicGroupMemberReferenceable> poolMembers)
        {
            if (group == null || !group.nonHistorical || group.directMemberReferences.Count > 0)
                return;

            foreach (var member in poolMembers ?? Enumerable.Empty<IStrategicGroupMemberReferenceable>())
            {
                if (member?.detachedFromGroupReference?.referenceId == group.objectId)
                    IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(member);
            }

            group.ClearPlannedPath();
            group.RemoveFromMap();
            IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(group);
            group.AttachTo(null);
            EntityManager.Instance.Unregister(group);
            state.strategicGroups.Remove(group);
        }

        void ApplyTheaterMergePlan(
            List<TheaterMergeMemberPlan> plans,
            List<StrategicGroup> activeGroups,
            List<StrategicGroup> nonHistoricalContainers,
            Dictionary<(string parentId, string memberId), int> originalDirectMemberOrders)
        {
            if (plans == null || plans.Count == 0 || activeGroups == null || activeGroups.Count == 0)
                return;

            var rootHistoricalPlans = plans
                .Where(plan => plan.member is StrategicGroup && plan.plannedParent == null)
                .ToList();
            var unassignedMemberPlans = plans
                .Where(plan => plan.member is not StrategicGroup && plan.plannedParent == null)
                .ToList();
            var needsMergeContainer = rootHistoricalPlans.Count != 1 || unassignedMemberPlans.Count > 0;
            var mergeContainer = needsMergeContainer
                ? SelectTheaterMergeContainer(activeGroups)
                : null;
            if (needsMergeContainer && mergeContainer == null)
            {
                var templateGroup = activeGroups
                    .OrderByDescending(group => group.GetStrengthMen())
                    .ThenByDescending(group => group.GetCombinedPowerPoint(true))
                    .ThenBy(group => group.objectId, StringComparer.Ordinal)
                    .FirstOrDefault();
                mergeContainer = StrategicGroupTransferSplitUtility.CreateSplitGroupLike(
                    templateGroup,
                    null,
                    StrategicGroup.DeployState.Independent,
                    nonHistorical: true);
                CopyAiFrontlineMovementState(templateGroup, mergeContainer);
                IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(mergeContainer);
                RecordTheaterMergeDirectMemberOrder(mergeContainer, originalDirectMemberOrders);
                nonHistoricalContainers.Add(mergeContainer);
            }

            if (needsMergeContainer && mergeContainer == null)
                return;

            foreach (var plan in plans.Where(plan => plan.plannedParent == null))
            {
                plan.plannedParent = needsMergeContainer ? mergeContainer : null;
                if (needsMergeContainer)
                    plan.reason = TheaterMergeAssignmentReason.MergeContainer;
            }

            var affectedGroups = plans
                .Select(plan => plan.member as StrategicGroup)
                .Where(group => group != null)
                .Concat(nonHistoricalContainers ?? new List<StrategicGroup>())
                .Append(mergeContainer)
                .Where(group => group != null)
                .Distinct()
                .ToList();
            var affectedGroupIds = affectedGroups
                .Where(group => !string.IsNullOrWhiteSpace(group.objectId))
                .Select(group => group.objectId)
                .ToHashSet();
            var rewrittenMemberIds = plans
                .Select(plan => plan.member?.objectId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Concat((nonHistoricalContainers ?? new List<StrategicGroup>())
                    .Select(group => group?.objectId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)))
                .ToHashSet();
            var preservedDirectMemberReferences = affectedGroups
                .ToDictionary(
                    group => group,
                    group => group.directMemberReferences
                        .Where(reference => reference != null &&
                            !string.IsNullOrWhiteSpace(reference.referenceId) &&
                            !rewrittenMemberIds.Contains(reference.referenceId))
                        .Select(reference => new StrategicGroupMemberReference() { referenceId = reference.referenceId })
                        .ToList());

            if (mergeContainer != null)
            {
                var mergeContainerParent = mergeContainer.parentGroupReference.Get();
                if (mergeContainerParent != null && affectedGroupIds.Contains(mergeContainerParent.objectId))
                    StrategicGroup.ReassignMember(mergeContainer, null);
            }

            var mergeCell = activeGroups.FirstOrDefault(group => group?.cell != null)?.cell;
            var resultHistoricalGroup = !needsMergeContainer
                ? rootHistoricalPlans.FirstOrDefault()?.member as StrategicGroup
                : null;
            if (resultHistoricalGroup != null && mergeCell != null)
            {
                var sourceRoot = rootHistoricalPlans.FirstOrDefault()?.sourceRoot;
                CopyAiFrontlineMovementState(sourceRoot, resultHistoricalGroup);
                resultHistoricalGroup.MoveToCell(mergeCell, false);
            }

            foreach (var group in affectedGroups)
            {
                group.directMemberReferences.Clear();
                group.directMemberReferences.AddRange(preservedDirectMemberReferences.GetValueOrDefault(group) ?? new List<StrategicGroupMemberReference>());
            }

            foreach (var plan in plans)
            {
                var member = plan.member;
                if (member == null)
                    continue;

                var originalParent = plan.originalParent;
                var newParent = plan.plannedParent;
                if (newParent == null &&
                    originalParent != null &&
                    !affectedGroupIds.Contains(originalParent.objectId) &&
                    member is StrategicGroup groupMember &&
                    groupMember == resultHistoricalGroup)
                {
                    newParent = originalParent;
                }

                if (originalParent != null &&
                    originalParent != newParent &&
                    !affectedGroupIds.Contains(originalParent.objectId))
                {
                    originalParent.RemoveDirectMemberReference(member.objectId);
                }

                if (member is StrategicGroup memberGroup)
                {
                    if (memberGroup == resultHistoricalGroup)
                    {
                        if (mergeCell != null)
                            memberGroup.MoveToCell(mergeCell, false);
                    }
                    else if (newParent != null)
                    {
                        memberGroup.ClearPlannedPath();
                        memberGroup.SetDeployState(StrategicGroup.DeployState.Combined);
                    }
                }

                member.parentGroupReference.referenceId = newParent?.objectId;
                newParent?.EnsureDirectMemberReference(member.objectId);

                if (plan.reason == TheaterMergeAssignmentReason.DetachedFrom && originalParent != newParent)
                    IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(member);
            }

            foreach (var group in affectedGroups)
                SortTheaterMergeDirectMembers(group, originalDirectMemberOrders);

            foreach (var group in (nonHistoricalContainers ?? new List<StrategicGroup>())
                .Where(group => group != mergeContainer)
                .ToList())
            {
                DestroyEmptyTheaterMergeNonHistoricalContainer(group, plans.Select(plan => plan.member));
            }

            if (mergeContainer != null)
            {
                mergeContainer.MoveToCell(mergeCell, false);
                StrategicGroupNamingUtility.RefreshGeneratedGroupIdentity(mergeContainer);
            }
        }

        void TryAutoMergeTheaterLandBucket(List<StrategicGroup> bucket)
        {
            if (bucket == null || bucket.Count < 2)
                return;

            var activeGroups = bucket
                .Select(group => group == null ? null : EntityManager.Instance.Get<StrategicGroup>(group.objectId))
                .Where(group => group != null && group.deployState == StrategicGroup.DeployState.Independent)
                .Distinct()
                .ToList();
            if (activeGroups.Count < 2)
                return;

            var poolMembers = new List<IStrategicGroupMemberReferenceable>();
            var nonHistoricalContainers = new List<StrategicGroup>();
            var seenMemberIds = new HashSet<string>();
            var sourceRootByMemberId = new Dictionary<string, StrategicGroup>();
            var originalDirectMemberOrders = new Dictionary<(string parentId, string memberId), int>();
            foreach (var group in activeGroups)
            {
                CollectTheaterMergePoolMember(
                    group,
                    group,
                    poolMembers,
                    nonHistoricalContainers,
                    seenMemberIds,
                    sourceRootByMemberId,
                    originalDirectMemberOrders);
            }

            if (poolMembers.Count == 0)
                return;

            var poolGroupIds = poolMembers
                .OfType<StrategicGroup>()
                .Select(group => group.objectId)
                .ToHashSet();
            var plans = BuildTheaterMergeMemberPlans(poolMembers, poolGroupIds);
            foreach (var plan in plans)
            {
                plan.sourceRoot = plan.member != null && sourceRootByMemberId.TryGetValue(plan.member.objectId, out var sourceRoot)
                    ? sourceRoot
                    : plan.member as StrategicGroup;
            }

            ApplyTheaterMergePlan(plans, activeGroups, nonHistoricalContainers, originalDirectMemberOrders);
        }

        void TryAutoMergeIndependentLandGroupsInTheater(Theater theater, HashSet<(int, int)> theaterCellSet)
        {
            if (theater?.side == null || theaterCellSet == null || theaterCellSet.Count == 0)
                return;

            var candidatesByCell = IterIndependentStrategicGroups()
                .Where(group => IsEligibleTheaterLandMergeGroup(group, theater, theaterCellSet))
                .GroupBy(group => group.cell)
                .ToList();

            foreach (var cellGrouping in candidatesByCell)
            {
                var pendingIds = cellGrouping
                    .Where(group => group != null)
                    .Select(group => group.objectId)
                    .ToList();
                while (pendingIds.Count > 0)
                {
                    var seedId = pendingIds[0];
                    pendingIds.RemoveAt(0);

                    var seedGroup = EntityManager.Instance.Get<StrategicGroup>(seedId);
                    if (!IsEligibleTheaterLandMergeGroup(seedGroup, theater, theaterCellSet))
                        continue;

                    var bucket = new List<StrategicGroup>() { seedGroup };
                    for (var idx = pendingIds.Count - 1; idx >= 0; idx--)
                    {
                        var otherGroup = EntityManager.Instance.Get<StrategicGroup>(pendingIds[idx]);
                        if (!IsEligibleTheaterLandMergeGroup(otherGroup, theater, theaterCellSet) ||
                            otherGroup.cell != seedGroup.cell ||
                            !HasEquivalentTheaterMergeMovementState(seedGroup, otherGroup))
                        {
                            continue;
                        }

                        bucket.Add(otherGroup);
                        pendingIds.RemoveAt(idx);
                    }

                    TryAutoMergeTheaterLandBucket(bucket);
                }
            }
        }

        static bool TryGetAiFrontlineEffectiveDistance(
            StrategicGroup group,
            Cell targetCell,
            IGraphEnumerable<Cell> movementGraph,
            Dictionary<(Cell src, Cell dst), AStarResult<Cell>> pathCostCache,
            out float effectiveDistance)
        {
            effectiveDistance = float.PositiveInfinity;
            if (group?.cell == null || targetCell == null)
                return false;

            if (group.TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out var segmentDistanceKm))
            {
                var nextCell = group.GetPathNextCell();
                if (nextCell != null &&
                    TryGetAiFrontlinePathCost(nextCell, targetCell, movementGraph, pathCostCache, out var tailCost))
                {
                    effectiveDistance = GetAiFrontlineRemainingSegmentCost(group, nextCell, movementGraph, segmentDistanceKm) + tailCost;
                    return !float.IsPositiveInfinity(effectiveDistance);
                }
            }

            return TryGetAiFrontlinePathCost(group.cell, targetCell, movementGraph, pathCostCache, out effectiveDistance);
        }

        static float GetAiFrontlineRemainingSegmentCost(
            StrategicGroup group,
            Cell nextCell,
            IGraphEnumerable<Cell> movementGraph,
            float segmentDistanceKm)
        {
            if (group?.cell == null || nextCell == null)
                return 0f;

            if (segmentDistanceKm <= AiFrontlineAllocationEpsilon)
                return 0f;

            var progressedDistanceKm = Math.Max(0f, Math.Min(group.moveProgressionKm, segmentDistanceKm));
            var remainingRatio = Math.Max(0f, segmentDistanceKm - progressedDistanceKm) / segmentDistanceKm;
            return movementGraph.MoveCost(group.cell, nextCell) * remainingRatio;
        }

        static bool TryGetAiFrontlinePathCost(
            Cell srcCell,
            Cell dstCell,
            IGraphEnumerable<Cell> movementGraph,
            Dictionary<(Cell src, Cell dst), AStarResult<Cell>> pathCostCache,
            out float pathCost)
        {
            pathCost = float.PositiveInfinity;
            if (srcCell == null || dstCell == null)
                return false;

            if (srcCell == dstCell)
            {
                pathCost = 0f;
                return true;
            }

            var key = (srcCell, dstCell);
            if (!pathCostCache.TryGetValue(key, out var pathResult))
            {
                pathResult = PathFinding<Cell>.AStar3(movementGraph, srcCell, dstCell);
                pathCostCache[key] = pathResult;
            }

            if (pathResult?.Path == null || pathResult.Path.Count == 0 || float.IsPositiveInfinity(pathResult.Cost))
                return false;

            pathCost = pathResult.Cost;
            return true;
        }
    }
}
