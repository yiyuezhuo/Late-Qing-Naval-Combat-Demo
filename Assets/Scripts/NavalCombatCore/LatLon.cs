namespace NavalCombatCore
{

    public class LatLon
    {
        public float LatDeg;
        public float LonDeg;

        public LatLon()
        {

        }

        public LatLon(float latDeg, float lonDeg)
        {
            LatDeg = latDeg;
            LonDeg = lonDeg;
        }

        public override string ToString()
        {
            return $"LatLon({LatDeg}, {LonDeg})";
        }

        public LatLon Clone() => new LatLon(LatDeg, LonDeg);
    }

}