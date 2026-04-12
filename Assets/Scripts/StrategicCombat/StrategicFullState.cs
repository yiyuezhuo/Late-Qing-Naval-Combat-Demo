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

    public string viewerSideId;

    public void CopyCameraStateFrom(StrategicViewState other)
    {
        if (other == null)
            return;

        xPosition = other.xPosition;
        yPosition = other.yPosition;
        orthographicSize = other.orthographicSize;
    }
}

public class StrategicFullState
{
    // TODO: Add Streaming Asset Reference
    public StrategicGameState gameState;
    public StrategicViewState viewState;
    // TODO: Add eventState?
}
