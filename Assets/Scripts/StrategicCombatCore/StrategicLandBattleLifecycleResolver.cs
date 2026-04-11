using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{
    public sealed class StrategicLandBattleLifecycleResolver
    {
        readonly StrategicGameState state;

        List<StrategicGroup> strategicGroups => state.strategicGroups;
        List<LandBattle> landBattles => state.landBattles;
        StrategicScenarioState scenarioState => state.scenarioState;

        public StrategicLandBattleLifecycleResolver(StrategicGameState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        void AddLog(LazyLocalizedString log, SideState side) => state.AddLog(log, side);

        LazyLocalizedString GetCellNameLazyStr(XY cellXY) => state.GetCellNameLazyStr(cellXY);

        HashSet<(Cell, SideState, SideState)> CollectHappeningBattleKeys()
        {
            var happeningBattleKeys = new HashSet<(Cell, SideState, SideState)>(); // Cell, Attacker, Defender

            foreach (var g in strategicGroups
                .Where(g => g.LandCombatable() && g.HasCombatEffectiveLandUnit())
                .GroupBy(g => g.cell))
            {
                var cell = g.Key;
                cell.RefreshControlState(); // TODO: Code smell? Extract it to the top level?

                var side2GroupsGp = g.GroupBy(g => g.side).ToList();
                var hexSide = cell.GetHexSide();
                if (hexSide != null && side2GroupsGp.Count >= 2)
                {
                    var g0 = side2GroupsGp[0];
                    var g1 = side2GroupsGp[1];

                    var g0hasActive = g0.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);
                    var g1hasActive = g1.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);
                    if (g0hasActive || g1hasActive)
                    {
                        SideState attacker = null;
                        SideState defender = null;
                        if (g0hasActive && g1hasActive)
                        {
                            var isG0HexController = g0.Key == hexSide;
                            if (isG0HexController)
                            {
                                attacker = g1.Key;
                                defender = g0.Key;
                            }
                            else
                            {
                                attacker = g0.Key;
                                defender = g1.Key;
                            }
                        }
                        else if (g0hasActive)
                        {
                            attacker = g0.Key;
                            defender = g1.Key;
                        }
                        else // if(g1hasActive)
                        {
                            attacker = g1.Key;
                            defender = g0.Key;
                        }
                        happeningBattleKeys.Add((cell, attacker, defender));
                    }
                }
            }

            return happeningBattleKeys;
        }
        
        public void Advance1Hour()
        {
            ResolveOverruns();
            CreateNewLandBattles();
            ConcludeLandBattles();

            // Resolve undetermined battle
            foreach(var landBattle in landBattles.Where(b => !b.end))
            {
                landBattle.Step();
            }

            ConcludeLandBattles();
        }

        void ResolveOverruns()
        {
            var updatedCells = new HashSet<Cell>();
            var groupsByCell = strategicGroups
                .Where(g => g != null && g.LandCombatable())
                .GroupBy(g => g.cell)
                .Where(g => g.Key != null)
                .ToList();

            foreach (var cellGroup in groupsByCell)
            {
                var positiveStrengthSides = cellGroup
                    .Where(IsOverrunAttacker)
                    .Select(g => g.side)
                    .Where(s => s != null)
                    .ToHashSet();

                if (positiveStrengthSides.Count == 0)
                    continue;

                foreach (var overrunTarget in cellGroup
                    .Where(IsOverrunTarget)
                    .Where(g => g.side != null && positiveStrengthSides.Any(side => side != g.side))
                    .ToList())
                {
                    overrunTarget.MarkAsDestroyed();
                    updatedCells.Add(cellGroup.Key);
                }
            }

            foreach (var cell in updatedCells)
            {
                cell.RefreshControlState();
                state.InvokeMapCellUpdated(cell);
            }
        }

        static bool IsOverrunAttacker(StrategicGroup group)
        {
            return group != null &&
                group.LandCombatable() &&
                group.HasCombatEffectiveLandUnit();
        }

        static bool IsOverrunTarget(StrategicGroup group)
        {
            return group != null &&
                group.LandCombatable() &&
                group.GetStrengthMen() == 0 &&
                group.type != StrategicGroup.Type.Fleet &&
                group.type != StrategicGroup.Type.CoastArtillery &&
                group.type != StrategicGroup.Type.Base;
        }

        void CreateNewLandBattles()
        {
            var happeningBattleKeys = CollectHappeningBattleKeys();
            var prevHappendBattlesMap = landBattles.Where(b => !b.end).ToDictionary(b => b.GetKey(), b => b);
            var prevHappendBattleKeys = prevHappendBattlesMap.Keys.ToHashSet();

            // Create new battle

            foreach (var happenningBattleKey in happeningBattleKeys)
            {
                if (!prevHappendBattleKeys.Contains(happenningBattleKey))
                {
                    var (cell, attacker, defender) = happenningBattleKey;
                    var battle = new LandBattle()
                    {
                        cellXY = new() { x = cell.x, y = cell.y },
                        attacker = new() { sideId = attacker.objectId },
                        defender = new() { sideId = defender.objectId },
                        beginDateTime = scenarioState.dateTime
                    };
                    EntityManager.Instance.Register(battle, null); // ID assigned here

                    landBattles.Add(battle);

                    cell.landBattleId = battle.objectId;

                    // AddLog($"New land battle begin: {battle.cellXY} {attacker.name.GetShortName()} vs {defender.name.GetShortName()}");
                    AddLog(LazyLocalizedString.MakeTemplate(
                        "New land battle begin: {0} {1} vs {2}",
                        GetCellNameLazyStr(battle.cellXY),
                        LazyLocalizedString.MakeGlobalStringShort(attacker.name),
                        LazyLocalizedString.MakeGlobalStringShort(defender.name)
                    ), null);
                }
            }
        }

        void ConcludeLandBattles()
        {
            var happeningBattleKeys = CollectHappeningBattleKeys();
            var prevHappendBattlesMap = landBattles.Where(b => !b.end).ToDictionary(b => b.GetKey(), b => b);
            var prevHappendBattleKeys = prevHappendBattlesMap.Keys.ToHashSet();

            // Set concluded/invalid battle to ended. ("Natural Disengagement")
            foreach(var prevHappendBattleKey in prevHappendBattleKeys)
            {
                if(!happeningBattleKeys.Contains(prevHappendBattleKey))
                {
                    var battle = prevHappendBattlesMap[prevHappendBattleKey];
                    // battle.end = true;
                    // battle.endDateTime = scenarioState.dateTime;
                    battle.GoToEnd();

                    var (cell, attacker, defender) = prevHappendBattleKey;
                    var cellGroups = cell.StrategicGroupReferences.Select(gr => gr.Get());
                    // battle.attackerVictory = cellGroups.Any(
                    //     g => g.IsOnMap() &&
                    //     g.posture != StrategicGroup.GroupPostureType.Disengaged &&
                    //     g.side == attacker &&
                    //     g.type != StrategicGroup.Type.Fleet
                    // );
                    battle.attackerVictory = cellGroups.Any(
                        g => g.IsOnMap() &&
                        g.posture == StrategicGroup.GroupPostureType.Active &&
                        g.side == attacker &&
                        g.type != StrategicGroup.Type.Fleet
                    );

                    MarkDestroyedLandBattleGroups(battle.attacker.participantGroupIds);
                    MarkDestroyedLandBattleGroups(battle.defender.participantGroupIds);

                    cell.landBattleId = null;

                    var vicDesc = battle.attackerVictory ? "Attacker Victory" : "Defender Victory";
                    // AddLog($"Land battle end: {battle.cellXY} {attacker.name.GetShortName()} vs {defender.name.GetShortName()}, {vicDesc}");
                    AddLog(LazyLocalizedString.MakeTemplate(
                        "Land battle end: {0} {1}, {2} ({3}) vs {4} ({5})",
                        LazyLocalizedString.MakeRaw(battle.cellXY),
                        LazyLocalizedString.MakeLocalizedRequired(vicDesc),
                        LazyLocalizedString.MakeGlobalStringShort(attacker.name),
                        battle.attacker.GetSummary(),
                        LazyLocalizedString.MakeGlobalStringShort(defender.name),
                        battle.defender.GetSummary()
                    ), null);
                }
            }
        }

        void MarkDestroyedLandBattleGroups(IEnumerable<string> participantGroupIds)
        {
            if (participantGroupIds == null)
                return;

            foreach (var groupId in participantGroupIds)
            {
                var group = EntityManager.Instance.Get<StrategicGroup>(groupId);
                if (group == null)
                    continue;

                foreach (var candidate in EnumerateLandBattleParticipantGroups(group))
                {
                    if (candidate.type == StrategicGroup.Type.Fleet ||
                        candidate.type == StrategicGroup.Type.CoastArtillery ||
                        candidate.type == StrategicGroup.Type.Base)
                    {
                        continue;
                    }

                    if (candidate.GetStrengthMen() != 0)
                        continue;

                    candidate.MarkAsDestroyed();
                }
            }
        }

        IEnumerable<StrategicGroup> EnumerateLandBattleParticipantGroups(StrategicGroup rootGroup)
        {
            if (rootGroup == null)
                yield break;

            yield return rootGroup;
            foreach (var candidate in rootGroup.WalkDescendantStrategicGroups())
            {
                yield return candidate;
            }
        }

    }
}
