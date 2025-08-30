using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml;

using CoreUtils;
using NavalCombatCore;
using NavalCombat;

public class FullState
{
    public StreamingAssetReference streamingAssetReference;
    public NavalGameState navalGameState;
    public ViewState viewState;
    public EventState eventState;

    public string ToXML()
    {
        return XmlUtils.ToXML(this);
    }

    public static FullState FromXML(string xml)
    {
        return XmlUtils.FromXML<FullState>(xml);
    }
}