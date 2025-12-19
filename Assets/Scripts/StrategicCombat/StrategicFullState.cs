using System.Collections.Generic;
using System.Xml.Serialization;
using StrategicCombatCore;

public class StrategicViewState
{
    // camera's position
    public float xPosition;
    public float yPosition;

    public float orthographicSize;

    public class HitAreaMapRecord
    {
        [XmlAttribute]
        public string hitAreaObjectId;

        [XmlAttribute]
        public string areaCellObjectId;
    }

    public List<HitAreaMapRecord> hitAreaMapRecords = new();
}

public class StrategicFullState
{
    // TODO: Add Streaming Asset Reference
    public StrategicGameState gameState;
    public StrategicViewState viewState;
    // TODO: Add eventState?
}