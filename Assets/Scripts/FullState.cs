using NavalCombatCore;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml;

public class FullState
{
    public NavalGameState navalGameState;
    public ViewState viewState;

    // static XmlSerializer fullStateSerializer = new XmlSerializer(typeof(FullState));

    public string ToXML()
    {
        // using (var textWriter = new StringWriter())
        // {
        //     using (XmlWriter xmlWriter = XmlWriter.Create(textWriter))
        //     {
        //         fullStateSerializer.Serialize(xmlWriter, this);
        //         string serializedXml = textWriter.ToString();

        //         return serializedXml;
        //     }
        // }

        return XmlUtils.ToXML(this);
    }

    public static FullState FromXML(string xml)
    {
        // using (var reader = new StringReader(xml))
        // {
        //     return (FullState)fullStateSerializer.Deserialize(reader);
        // }
        return XmlUtils.FromXML<FullState>(xml);
    }
}