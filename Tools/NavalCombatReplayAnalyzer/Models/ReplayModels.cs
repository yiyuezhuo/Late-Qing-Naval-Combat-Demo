using System.Xml.Serialization;
using NavalCombatCore;
using YYZ;

namespace NavalCombatReplayAnalyzer.Models;

[XmlRoot("FullState")]
public class ReplayFullState
{
    [XmlIgnore]
    public string SourcePath;

    public ReplayStreamingAssetReference streamingAssetReference = new();
    public NavalGameState navalGameState;

    public string ToXML() => XmlUtils.ToXML(this);
}

public class ReplayStreamingAssetReference
{
    public string leadersPath = "Leaders.xml";
    public string shipClassesPath = "ShipClasses.xml";
    public string namedShipsPath = "NamedShips.xml";
}

public class ReplayViewModel
{
    public string sourceName { get; set; }
    public DateTime startTime { get; set; }
    public DateTime endTime { get; set; }
    public List<ReplayShip> ships { get; set; } = new();
    public List<ReplayShot> shots { get; set; } = new();
    public List<ReplayEvent> events { get; set; } = new();
    public List<DateTime> sampleTimes { get; set; } = new();
}

public class ReplayShip
{
    public string id { get; set; }
    public string name { get; set; }
    public string groupName { get; set; }
    public string type { get; set; }
    public string country { get; set; }
    public string color { get; set; }
    public bool isDestroyed { get; set; }
    public float finalDamagePoint { get; set; }
    public float maxDamagePoint { get; set; }
    public List<ReplayPoint> track { get; set; } = new();
}

public class ReplayPoint
{
    public DateTime time { get; set; }
    public double lat { get; set; }
    public double lon { get; set; }
    public float speedKnots { get; set; }
    public float headingDeg { get; set; }
    public float damagePoint { get; set; }
    public string operationalState { get; set; }
    public string mapState { get; set; }
}

public class ReplayShot
{
    public DateTime time { get; set; }
    public string shooterId { get; set; }
    public string shooterName { get; set; }
    public string targetId { get; set; }
    public string targetName { get; set; }
    public string weapon { get; set; }
    public float damagePoint { get; set; }
    public ReplayPoint shooterPoint { get; set; }
    public ReplayPoint targetPoint { get; set; }
}

public class ReplayEvent
{
    public DateTime time { get; set; }
    public string shipId { get; set; }
    public string shipName { get; set; }
    public string kind { get; set; }
    public string description { get; set; }
}
