using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;


namespace NavalCombatCore
{
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
        }

        static XmlSerializer shipClassListSerializer = new XmlSerializer(typeof(List<ShipClass>));
        static XmlSerializer shipLogListSerializer = new XmlSerializer(typeof(List<ShipLog>));

        public string ShipClassesToXML()
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

        public string ShipLogsToXML()
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

    }
}