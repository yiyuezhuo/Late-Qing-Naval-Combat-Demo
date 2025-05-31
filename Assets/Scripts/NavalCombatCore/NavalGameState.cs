using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;


namespace NavalCombatCore
{

    public enum PostureType
    {
        Friendly,
        Hostile,
        Neutral,
        Unknown
    }

    [Serializable]
    public class NavalGameState
    {
        public List<Leader> leaders = new();
        public List<ShipClass> shipClasses = new();
        // public List<ShipLog> shipLogs = new() { new() };
        public List<NamedShip> namedShips = new();
        public List<ShipLog> shipLogs = new();
        // public List<ShipGroup> rootShipGroups = new();
        public List<ShipGroup> shipGroups = new();
        public ScenarioState scenarioState = new();

        public event EventHandler<List<ShipGroup>> shipGroupsChanged;

        static NavalGameState _instance;
        public static NavalGameState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }

        public void ResetAndRegisterAll()
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
            foreach (var shipGroup in shipGroups)
            {
                EntityManager.Instance.Register(shipGroup, null);
                // ResetAndRegisterAllShipGroup(shipGroup);
            }
        }

        public string LeadersToXML()
        {
            return XmlUtils.ToXML(leaders);
        }

        public void LeadersFromXML(string xml)
        {
            leaders = XmlUtils.FromXML<List<Leader>>(xml);

            ResetAndRegisterAll();
        }

        public string ShipClassesToXML()
        {
            var serializedXml = XmlUtils.ToXML(shipClasses);
            return serializedXml;
        }

        public void ShipClassesFromXML(string xml)
        {
            shipClasses = XmlUtils.FromXML<List<ShipClass>>(xml);

            ResetAndRegisterAll();
        }

        public string NamedShipsToXML()
        {
            return XmlUtils.ToXML(namedShips);
        }

        public void NamedShipsFromXML(string xml)
        {
            namedShips = XmlUtils.FromXML<List<NamedShip>>(xml);

            ResetAndRegisterAll();
        }

        public string ShipLogsToXML()
        {
            return XmlUtils.ToXML(shipLogs);
        }

        public void ShipLogsFromXML(string xml)
        {
            shipLogs = XmlUtils.FromXML<List<ShipLog>>(xml);

            ResetAndRegisterAll();
        }

        public string ShipGroupsToXML()
        {
            return XmlUtils.ToXML(shipGroups);
        }

        public void ShipGroupsFromXML(string xml)
        {
            shipGroups = XmlUtils.FromXML<List<ShipGroup>>(xml);

            ResetAndRegisterAll();
            SyncShipLogParentWithGroupHierarchy();

            shipGroupsChanged?.Invoke(this, shipGroups);
        }

        public void SyncShipLogParentWithGroupHierarchy()
        {
            var shipTracked = new HashSet<string>();

            foreach (var shipGroup in shipGroups)
            {
                foreach (var child in shipGroup.GetChildren()) // if it's not resolved, child may be null and raise exception
                {
                    child.parentObjectId = shipGroup.objectId;
                    if (child is ShipLog subShipLog)
                    {
                        shipTracked.Add(subShipLog.objectId);
                    }
                }
            }

            foreach (var shipLog in shipLogs)
            {
                if (!shipTracked.Contains(shipLog.objectId))
                {
                    shipLog.parentObjectId = null;
                }
            }
        }

        public void UpdateTo(NavalGameState newState)
        {
            shipClasses = newState.shipClasses;
            shipLogs = newState.shipLogs;
            // rootShipGroups = newState.rootShipGroups;
            shipGroups = newState.shipGroups;

            ResetAndRegisterAll();
            SyncShipLogParentWithGroupHierarchy();

            // rootShipGroupsChanged?.Invoke(this, rootShipGroups);
            shipGroupsChanged?.Invoke(this, shipGroups);
        }

        public IEnumerable<IShipGroupMember> GetShipGroupMembersRecursive()
        {
            foreach (var shipGroup in shipGroups)
            {
                if (shipGroup.parentObjectId == null) // "root" groups
                {
                    foreach (var ret in GetShipGroupMembersRecursive(shipGroup))
                    {
                        yield return ret;
                    }
                }
            }
        }

        public IEnumerable<IShipGroupMember> GetShipGroupMembersRecursive(ShipGroup shipGroup)
        {
            yield return shipGroup;

            foreach (var child in shipGroup.GetChildren())
            {
                if (child is ShipLog subShipLog)
                {
                    yield return subShipLog;
                }
                else if (child is ShipGroup subShipGroup)
                {
                    foreach (var ret in GetShipGroupMembersRecursive(subShipGroup))
                    {
                        yield return ret;
                    }
                }
            }
        }

        public Dictionary<IShipGroupMember, PostureType> CalcualtePostureMap(IShipGroupMember refGroup)
        {
            var refRoot = FindRoot(refGroup);
            return GetShipGroupMembersRecursive().ToDictionary(
                g => g,
                g => FindRoot(g) == refRoot ? PostureType.Friendly : PostureType.Hostile
            );
        }

        public static IShipGroupMember FindRoot(IShipGroupMember member)
        {
            if (member == null)
                return null;

            var p = member;
            while (p.GetParentGroup() != null)
            {
                p = p.GetParentGroup();
            }
            return p;
        }

        public void Step(float deltaSeconds)
        {
            scenarioState.Step(deltaSeconds);

            foreach (var shipLog in shipLogs)
            {
                shipLog.Step(deltaSeconds);
            }
        }
    }
}