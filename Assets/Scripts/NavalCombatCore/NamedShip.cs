using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using System.Xml.Serialization;


namespace NavalCombatCore
{
    /// <summary>
    /// In historical gaming, NamedShip represents a ship's state in a spcific timestamp. So we may have Yoshino 1894, Yoshino 1900 etc.
    /// In dynamic gaming, NamedShip always represents lastest state of a ship, and some attribute are explained differently (fate) or droped (applicable years)
    /// </summary>
    public partial class NamedShip : IObjectIdLabeled
    {
        public string objectId { get; set; }

        public string shipClassObjectId;

        [XmlIgnore]
        ShipClass shipClassCache;

        public ShipClass shipClass
        {
            // get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
            // get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
            get
            {
                if (NavalGameState.Instance.scenarioState.doingStep)
                {
                    if (shipClassCache == null)
                    {
                        shipClassCache = EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
                    }
                    return shipClassCache;
                }
                return EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
            }
        }


        public GlobalString name = new();
        public GlobalString builderDesc = new();
        public string launchedDate;
        public string completedDate;
        public GlobalString fateDesc = new();
        public int applicableYearBegin = 1900;
        public int applicableYearEnd = 1900;
        public string defaultLeaderObjectId; // If ShipLog (Scenario level state) does not override the leader (new leader succeed the default one placeholder leader if the old leader is killed), default leader is used as leader.
        public Leader defaultLeader
        {
            get => EntityManager.Instance.Get<Leader>(defaultLeaderObjectId);
        }
        public int crewRating;
        public float speedModifier; // boiler ageing factor etc, -0.1 => -10%

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }

}