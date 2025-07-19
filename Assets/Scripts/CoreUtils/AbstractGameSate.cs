using NavalCombatCore;
using System.Collections.Generic;

namespace CoreUtils
{
    // public interface IAbstractGameState
    // {
    //     public List<Leader> leaders { get; set; }

    //     // [XmlArray(IsNullable = true)]
    //     public List<ShipClass> shipClasses { get; set; }
    //     // public List<ShipLog> shipLogs = new() { new() };

    //     // [XmlArray(IsNullable = true)]
    //     public List<NamedShip> namedShips { get; set; }

    //     public List<ShipLog> shipLogs { get; set; }
    // }

    public class AbstractGameState// : IAbstractGameState
    {
        // public List<Leader> leaders { get; set; } = new();
        // public List<ShipClass> shipClasses { get; set; } = new();
        // public List<NamedShip> namedShips { get; set; } = new();
        // public List<ShipLog> shipLogs { get; set; } = new();

        public List<Leader> leaders = new();
        public List<ShipClass> shipClasses = new();
        public List<NamedShip> namedShips = new();
        public List<ShipLog> shipLogs = new();



        public string LeadersToXML()
        {
            return XmlUtils.ToXML(leaders);
        }

        public void LeadersFromXML(string xml)
        {
            leaders = XmlUtils.FromXML<List<Leader>>(xml);

            // ResetAndRegisterAll();
        }

        public string ShipClassesToXML()
        {
            var serializedXml = XmlUtils.ToXML(shipClasses);
            return serializedXml;
        }

        public void ShipClassesFromXML(string xml)
        {
            shipClasses = XmlUtils.FromXML<List<ShipClass>>(xml);

            // ResetAndRegisterAll();
        }

        public string NamedShipsToXML()
        {
            return XmlUtils.ToXML(namedShips);
        }

        public void NamedShipsFromXML(string xml)
        {
            namedShips = XmlUtils.FromXML<List<NamedShip>>(xml);

            // ResetAndRegisterAll();
        }

        public string ShipLogsToXML()
        {
            return XmlUtils.ToXML(shipLogs);
        }

        public void ShipLogsFromXML(string xml)
        {
            shipLogs = XmlUtils.FromXML<List<ShipLog>>(xml);

            // ResetAndRegisterAll();
        }

        public virtual void ResetAndRegisterAll()
        {
            EntityManager.Instance.Reset();

            foreach (var leader in leaders)
            {
                EntityManager.Instance.Register(leader, null);
            }
            foreach (var shipClasses in shipClasses)
            {
                EntityManager.Instance.Register(shipClasses, null);
            }
            foreach (var namedShip in namedShips)
            {
                EntityManager.Instance.Register(namedShip, null);
            }
            foreach (var shipLog in shipLogs)
            {
                EntityManager.Instance.Register(shipLog, null);
            }
        }
    }
}