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
        public List<ShipClass> shipClasses = new()
        {
            // new() { name=new() { english="114514"} },
            // new() { name=new() { english="abs"} }
        };
        // public List<ShipLog> shipLogs = new() { new() };
        public List<ShipLog> shipLogs = new();
        public List<ShipGroup> rootShipGroups = new();

        public event EventHandler<List<ShipGroup>> rootShipGroupsChanged;

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

            foreach (var shipClasses in shipClasses)
            {
                EntityManager.Instance.Register(shipClasses, null);
            }
            foreach (var shipLog in shipLogs)
            {
                EntityManager.Instance.Register(shipLog, null);
            }
            foreach (var shipGroup in rootShipGroups)
            {
                ResetAndRegisterAllShipGroup(shipGroup);
            }
        }

        public void ResetAndRegisterAllShipGroup(ShipGroup shipGroup)
        {
            EntityManager.Instance.Register(shipGroup, null);
            foreach (var child in shipGroup.GetChildren())
            {
                if (child is ShipGroup subShipGroup)
                {
                    ResetAndRegisterAllShipGroup(subShipGroup);
                }
            }
        }

        static XmlSerializer shipClassListSerializer = new XmlSerializer(typeof(List<ShipClass>));
        static XmlSerializer shipLogListSerializer = new XmlSerializer(typeof(List<ShipLog>));
        static XmlSerializer shipGroupListSerializer = new XmlSerializer(typeof(List<ShipGroup>));

        public string ShipClassesToXml()
        {
            using (var textWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(textWriter))
                {
                    shipClassListSerializer.Serialize(xmlWriter, shipClasses);
                    string serializedXml = textWriter.ToString();

                    return serializedXml;
                }
            }
        }

        public void ShipClassesFromXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                shipClasses = (List<ShipClass>)shipClassListSerializer.Deserialize(reader);
            }

            ResetAndRegisterAll();
        }

        public string ShipLogsToXml()
        {
            using (var textWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(textWriter))
                {
                    shipLogListSerializer.Serialize(xmlWriter, shipLogs);
                    string serializedXml = textWriter.ToString();

                    return serializedXml;
                }
            }
        }

        public void ShipLogsFromXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                shipLogs = (List<ShipLog>)shipLogListSerializer.Deserialize(reader);
            }

            ResetAndRegisterAll();
        }

        public string RootShipGroupsToXml()
        {
            using (var writer = new StringWriter())
            {
                shipGroupListSerializer.Serialize(writer, rootShipGroups);
                return writer.ToString();
            }
        }

        public void RootShipGroupsFromXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                rootShipGroups = (List<ShipGroup>)shipGroupListSerializer.Deserialize(reader);
            }

            ResetAndRegisterAll();
            SyncShipLogParentWithGroupHierarchy();

            rootShipGroupsChanged?.Invoke(this, rootShipGroups);
        }

        public void SyncShipLogParentWithGroupHierarchy()
        {
            var shipTracked = new HashSet<string>();
            foreach (var shipGroup in rootShipGroups)
            {
                SyncShipLogParentWithGroupHierarchy(shipGroup, ref shipTracked);
            }
            foreach (var shipLog in shipLogs)
            {
                if (!shipTracked.Contains(shipLog.objectId))
                {
                    shipLog.parentObjectId = null;
                }
            }
        }

        public void SyncShipLogParentWithGroupHierarchy(ShipGroup shipGroup, ref HashSet<string> shipTracked)
        {
            foreach (var child in shipGroup.GetChildren())
            {
                child.parentObjectId = shipGroup.objectId;
                if (child is ShipGroup subShipGroup)
                {
                    SyncShipLogParentWithGroupHierarchy(subShipGroup, ref shipTracked);
                }
                if (child is ShipLog subShipLog)
                {
                    shipTracked.Add(subShipLog.objectId);
                }
            }
        }

        public void UpdateTo(NavalGameState newState)
        {
            shipClasses = newState.shipClasses;
            shipLogs = newState.shipLogs;
            rootShipGroups = newState.rootShipGroups;

            ResetAndRegisterAll();
            SyncShipLogParentWithGroupHierarchy();

            rootShipGroupsChanged?.Invoke(this, rootShipGroups);
        }

        public IEnumerable<IShipGroupMember> GetShipGroupMembersRecursive()
        {
            // foreach(var)
            foreach (var shipGroup in rootShipGroups)
            {
                foreach (var ret in GetShipGroupMembersRecursive(shipGroup))
                {
                    yield return ret;
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
            // var members = GetShipGroupMembersRecursive();
            // var ret = new Dictionary<IShipGroupMember, PostureType>();

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
    }
}