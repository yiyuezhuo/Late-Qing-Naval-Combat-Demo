using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml;

using CoreUtils;
using NavalCombatCore;

public class FullState
{
    public StreamingAssetReference streamingAssetReference;
    public NavalGameState navalGameState;
    public ViewState viewState;

    public string ToXML()
    {

        return XmlUtils.ToXML(this);
    }

    public static FullState FromXML(string xml)
    {
        return XmlUtils.FromXML<FullState>(xml);
    }
}