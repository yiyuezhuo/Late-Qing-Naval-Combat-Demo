using System.Collections.Generic;
using System.Xml.Serialization;
using CoreUtils;

namespace NavalCombatCore
{
    public class LocationLabel
    {
        [XmlAttribute]
        public float latitude;

        [XmlAttribute]
        public float longitude;

        public GlobalString name = new();

        public LocationLabel Clone()
        {
            return new()
            {
                latitude = latitude,
                longitude = longitude,
                name = name?.Clone() ?? new GlobalString()
            };
        }

        public void CopyFrom(LocationLabel other)
        {
            if (other == null)
                return;

            latitude = other.latitude;
            longitude = other.longitude;
            name = other.name?.Clone() ?? new GlobalString();
        }

        public string GetShortSummary()
        {
            return $"{name?.GetShortName()} ({latitude:0.####}, {longitude:0.####})";
        }
    }

    public partial class ScenarioState
    {
        public List<LocationLabel> locationLabels = new();
        public bool ShouldSerializeLocationLabels() => locationLabels != null && locationLabels.Count > 0;
    }
}
