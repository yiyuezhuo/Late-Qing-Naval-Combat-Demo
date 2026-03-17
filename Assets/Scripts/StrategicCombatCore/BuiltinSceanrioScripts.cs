using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{
    public static class BuiltinScenarioScripts
    {
        public static DateTime karlJessenAppointment = new DateTime(1904, 3, 17);

        public class AppointmentReplacementRecord
        {
            public DateTime time;
            public string oldLeaderNameString;
            public string newLeaderNameString;

            public AppointmentReplacementRecord(DateTime time, string oldLeaderNameString, string newLeaderNameString)
            {
                this.time = time;
                this.oldLeaderNameString = oldLeaderNameString;
                this.newLeaderNameString = newLeaderNameString;
            }

            public string GetKey() => $"{time}_{oldLeaderNameString}_{newLeaderNameString}";
        }

        static List<AppointmentReplacementRecord> appointmentReplacementRecords = new()
        {
            new(new(1904, 3, 17), "Nikolai Reitzenstein", "Karl Jessen"),
            new(new(1904, 6, 12), "Karl Jessen", "Pyotr Bezobrazov"),
            new(new(1904, 7, 15), "Pyotr Bezobrazov", "Karl Jessen"),
        };

        public static void RunVladivostokSquadronScript(StrategicGameState state)
        {
            // Enforce leader replacement
            foreach(var record in appointmentReplacementRecords)
            {
                if(state.scenarioState.dateTime > record.time)
                {
                    var key = record.GetKey();
                    var executed = state.customBoolMap.GetValueOrDefault(key);
                    if(!executed)
                    {
                        state.customBoolMap[key] = true;

                        var fromLeader = state.leaders.FirstOrDefault(leader => leader.name.MatchAny(record.oldLeaderNameString));
                        var toLeader = state.leaders.FirstOrDefault(leader => leader.name.MatchAny(record.newLeaderNameString));

                        if(fromLeader != null && toLeader != null)
                        {
                            foreach(var group in state.strategicGroups)
                            {
                                var currentLeader = group.leaderReference.Get();
                                if(currentLeader == null)
                                {
                                    continue;
                                }

                                if(currentLeader.name.MatchAny(record.oldLeaderNameString))
                                {
                                    group.leaderReference.referenceObjectId = toLeader.objectId;

                                    // Make notification
                                    var msg = LazyLocalizedString.MakeTemplate(
                                        "Replace commander {0} => {1} ({2})", 
                                        LazyLocalizedString.MakeGlobalStringLong(currentLeader.name),
                                        LazyLocalizedString.MakeGlobalStringLong(toLeader.name),
                                        LazyLocalizedString.MakeGlobalStringLong(group.name)
                                    );
                                    state.AddLog(msg, group.side);
                                }
                            }

                            // TODO: Handle ship's captain
                        }
                    }
                }
            }
        }
    }
}
