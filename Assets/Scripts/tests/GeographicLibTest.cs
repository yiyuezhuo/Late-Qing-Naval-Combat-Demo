using UnityEngine;

using GeographicLib;

public class GeographicLibTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Ex1();
        Ex2();
    }

    void Ex1()
    {
        double
        lat1 = 40.6, lon1 = -73.8, // JFK Airport
        lat2 = 51.6, lon2 = -0.5;  // LHR Airport

        double arcLength = Geodesic.WGS84.Inverse(lat1, lon1, lat2, lon2, out double distance);

        Debug.Log($"({lat1}, {lon1}) -> ({lat2}, {lon2}): arcLength={arcLength}, distance={distance}"); // meter
    }

    void Ex2()
    {
        double
        lat1 = 40.6, lon1 = -73.8,
        s12 = 5500e3, azi1 = 51;

        double arcLength = Geodesic.WGS84.Direct(lat1, lon1, azi1, s12, out double lat2, out double lon2);

        Debug.Log($"({lat1}, {lon1}), s12={s12}, azi1={s12}): arcLength={arcLength}, lat2={lat2}, lon2={lon2}");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
