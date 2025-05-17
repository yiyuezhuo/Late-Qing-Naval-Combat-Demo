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
        public List<ShipClass> shipClasses = new() {
            new() { name=new() { english="114514"} },
            new() { name=new() { english="abs"} }
        };
        public List<ShipLog> shipLogs = new() { new() };

        static XmlSerializer serializer = new XmlSerializer(typeof(List<ShipClass>));
        
        public string ShipClassesToXML()
        {
            using(var textWriter = new StringWriter())
            {
                using(var xmlWriter = XmlWriter.Create(textWriter))
                {
                    serializer.Serialize(xmlWriter, shipClasses);
                    string serializedXml = textWriter.ToString();

                    return serializedXml;
                }
            }
        }
    }
}