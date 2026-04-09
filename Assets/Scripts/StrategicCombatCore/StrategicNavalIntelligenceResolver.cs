using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;
using YYZ;

namespace StrategicCombatCore
{
    public sealed class StrategicNavalIntelligenceResolver
    {
        readonly StrategicGameState state;

        List<NavalContactReport> navalContactReports => state.navalContactReports;
        StrategicScenarioState scenarioState => state.scenarioState;

        public StrategicNavalIntelligenceResolver(StrategicGameState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        IEnumerable<StrategicGroup> IterIndependentStrategicGroups() => state.IterIndependentStrategicGroups();

        void AddLog(LazyLocalizedString log, SideState side) => state.AddLog(log, side);

        static TimeSpan oneWeekTimeSpan = TimeSpan.FromDays(7);

        public void Advance1HourForContactReport()
        {
            var observerObservedCellToContactReport = navalContactReports.ToDictionary(
                c => (c.GetObserverSide(), c.GetObservedSide(), c.GetCell()),
                c => c
            );

            // Create or update Contact Report
            foreach(var cellFleetGroupsGrouping in IterIndependentStrategicGroups()
                    .Where(g => g.type == StrategicGroup.Type.Fleet)
                    .GroupBy(g => g.cell))
            {
                var cell = cellFleetGroupsGrouping.Key;
                var sideFleetGroupsGroupings = cellFleetGroupsGrouping.GroupBy(g => g.side).ToList();

                foreach(var observedSideFleetGroupsGrouping in sideFleetGroupsGroupings)
                {
                    var observedSide = observedSideFleetGroupsGrouping.Key;
                    var observedSideFleetGroups = observedSideFleetGroupsGrouping.ToList();
                    // ObservedSide may be observed by static observe point (coast watcher, friendly merchant, or not explicily modeled aux ships) or other side's ship
                    var observedSideDeployedShipLogs = observedSideFleetGroups.SelectMany(g => g.WalkGroupMembersDeployedShips()).ToList();
                    var footprint = observedSideDeployedShipLogs.Count * 1f;

                    // Collect internal hide value for observed side
                    var observedSideInternalHideValue = cell.CellSideInfos.FirstOrDefault(info => info.GetSide() == observedSide)?.interalHideValue ?? 0;
                    var totalFootprint = footprint - observedSideInternalHideValue;

                    // Collect Interval search value for observer side
                    // var observerSideToSearchValue = cell.CellSideInfos.ToDictionary(info => info.GetSide(), info => info.internalSearchValue + info.merchantShipTraffic);
                    var observerSideToSearchValue = cell.CellSideInfos.ToDictionary(info => info.GetSide(), info => info.internalSearchValue);
                    foreach(var observerSideFleetGroupsGrouping in sideFleetGroupsGroupings)
                    {
                        var observerSide = observerSideFleetGroupsGrouping.Key;
                        var observerDeployShipCount = observerSideFleetGroupsGrouping.Sum(g => g.WalkGroupMembersDeployedShips().Count());
                        if(!observerSideToSearchValue.ContainsKey(observerSide))
                        {
                            observerSideToSearchValue[observerSide] = 0;
                        }
                        observerSideToSearchValue[observerSide] += observerDeployShipCount; // Use a coef?
                    }

                    foreach(var (observerSide, observerSideSearchValue) in observerSideToSearchValue)
                    {
                        if(observerSide != observedSide)
                        {
                            var combinedSearchValue = observerSideSearchValue + totalFootprint;
                            combinedSearchValue *= GetSearchValueDayNightCoef(cell);

                            // Cancel cell.SearchAreaSqKm to favor of hard-coded parameter to find a good "feel" now, poor "analytic" model should be abandoned at this point.
                            // var searchAreaCoef = Math.Max(cell.SearchAreaSqKm, 1) / 2500; // TODO: Redesign is expected, now handle it as a const 1
                            var searchAreaCoef = cell.IsAreaCell() ? 3 : 0.5f;
                            if(RandomUtils.NextFloat() <= combinedSearchValue / (100 * searchAreaCoef))
                            {
                                var key = (observerSide, observedSide, cell);
                                var matchedContactReport = observerObservedCellToContactReport.GetValueOrDefault(key);
                                UpdateContactReport(observerSide, observedSide, cell, observedSideDeployedShipLogs, matchedContactReport);

                                // var key = (observerSide, observedSide, cell);
                                // if(!observerObservedCellToContactReport.TryGetValue(key, out var matchedContactReport))
                                // {
                                //     matchedContactReport = new()
                                //     {
                                //         observerSideId = observerSide.objectId,
                                //         observedSideId = observedSide.objectId,
                                //         position = cell.ToXY(),
                                //     };
                                //     EntityManager.Instance.Register(matchedContactReport, state);
                                //     navalContactReports.Add(matchedContactReport);
                                // }

                                // // matchedContactReport.UpdateTo(scenarioState.dateTime, shipLogs);
                                // matchedContactReport.UpdateTo(scenarioState.dateTime, observedSideDeployedShipLogs);
                                
                                // var msg = $"Contact Report: {matchedContactReport}";
                                // ServiceLocator.Get<ILoggerService>().Log(msg);
                                // AddLog(msg, observerSide);
                            }
                        }
                    }
                }
            }

            // Delete outdated Contact Report
            // var outdatedContactReports = navalContactReports.Where(c => scenarioState.dateTime - c.dateTime > oneWeekTimeSpan).ToList();
            var outdatedContactReports = navalContactReports.Where(c => c.GetHoursToCurrent() > NavalContactReport.threatMaintainedHours).ToList();
            foreach(var outdatedContactReport in outdatedContactReports)
            {
                navalContactReports.Remove(outdatedContactReport);

                // erviceLocator.Get<ILoggerService>().Log($"Lost Contact: {outdatedContactReport}");
            }
        }

        public void UpdateContactReport(SideState observerSide, SideState observedSide, Cell cell, List<ShipLog> observedSideDeployedShipLogs, NavalContactReport matchedContactReport)
        {
            if(matchedContactReport == null)
            {
                matchedContactReport = new()
                {
                    observerSideId = observerSide.objectId,
                    observedSideId = observedSide.objectId,
                    position = cell.ToXY(),
                };
                EntityManager.Instance.Register(matchedContactReport, state);
                navalContactReports.Add(matchedContactReport);
            }

            // matchedContactReport.UpdateTo(scenarioState.dateTime, shipLogs);
            matchedContactReport.UpdateTo(scenarioState.dateTime, observedSideDeployedShipLogs);
            
            var msg = $"Contact Report: {matchedContactReport}";
            ServiceLocator.Get<ILoggerService>().Log(msg);
            
            // AddLog(msg, observerSide);
            AddLog(matchedContactReport.ToLazyLocalizedString(), observerSide);
        }

        public void Advance1HourForRaiding()
        {
            foreach(var cellFleetGroupsGrouping in IterIndependentStrategicGroups()
                    .Where(g => g.type == StrategicGroup.Type.Fleet)
                    .GroupBy(g => g.cell))
            {
                var cell = cellFleetGroupsGrouping.Key;
                var sideFleetGroupsGroupings = cellFleetGroupsGrouping.GroupBy(g => g.side).ToList();
                var searchAreaCoef = cell.IsAreaCell() ? 3 : 0.5f;

                foreach(var sideFleetGroupsGrouping in sideFleetGroupsGroupings)
                {
                    var raidingSide = sideFleetGroupsGrouping.Key;
                    var raidingFleetGroups = sideFleetGroupsGrouping.ToList();
                    var raidingSideSearchShips = raidingFleetGroups.Sum(g => g.WalkGroupMembersDeployedShips().Count());
                    // var raidingSideSearchValue = raidingSideSearchShips * 1f;
                    // var raidingSideSearchValue = raidingSideSearchShips * 2f; // Increase the base raiding probability
                    var raidingSideSearchValue = raidingSideSearchShips * 8f; // Increase the base raiding probability
                    raidingSideSearchValue *= GetSearchValueDayNightCoef(cell);

                    foreach(var raidedSideInfo in cell.CellSideInfos.Where(info => info.sideObjectId != raidingSide.objectId && info.merchantShipTraffic > 0))
                    {
                        // var merchantShipProb = raidedSideInfo.merchantShipTraffic / 100;
                        var merchantShipProb = 2 * raidedSideInfo.merchantShipTraffic / 100;
                        if(RandomUtils.NextFloat() <= merchantShipProb) // A potential target appear
                        {
                            if(RandomUtils.NextFloat() <= raidingSideSearchValue / (100 * searchAreaCoef))
                            {
                                // Locate and destroy a cargo (assume no ammu is used at the current setting)
                                raidingSide.victoryPoints += 1;
                                // AddLog($"Raiders of {raidingSide.name.GetShortName()} sink a merchant ship in the {cell.GetLocationSummary()}", null);
                                
                                var log = LazyLocalizedString.MakeTemplate(
                                    "Raiders of {0} sank a merchant ship in the {1}",
                                    LazyLocalizedString.MakeGlobalStringShort(raidingSide.name),
                                    LazyLocalizedString.MakeGlobalStringShort(cell.GetLocationSummaryGlobalString())
                                );
                                AddLog(log, null);

                                var raidedSide = raidedSideInfo.GetSide();
                                UpdateContactReport(
                                    raidedSide,
                                    raidingSide,
                                    cell,
                                    raidingFleetGroups.SelectMany(g => g.WalkGroupMembersDeployedShips()).ToList(),
                                    navalContactReports.FirstOrDefault(c => c.observerSideId == raidedSide.objectId && c.observedSideId == raidingSide.objectId && c.GetCell() == cell)
                                );
                            }
                        }
                    }
                }
            }
        }


        public float GetSearchValueDayNightCoef(Cell cell)
        {
            var sunPos = NavalUtils.GetSunPosition(scenarioState.dateTime, new LatLon(cell.latitude, cell.longitude));
            return sunPos.GetDayNightLevel() switch
            {
                DayNightLevel.Day => 1,
                DayNightLevel.Twilight => 0.25f,
                DayNightLevel.Night => 0.01f,
                _ => 1
            };
        }
    }
}
