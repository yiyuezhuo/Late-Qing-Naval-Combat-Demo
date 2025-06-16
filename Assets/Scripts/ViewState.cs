using NavalCombatCore;
using UnityEngine;

public class ViewState // only viewpoint related view state is captured though
{
    public float xRotation;
    public float yRotation;
    // public float sphereSize; // 100, r=50
    // public float z; // -100
    public float orthographicSize;

    public float GetCenterLatitude() => xRotation;
    public float GetCenterLongitude()
    {
        var lonDeg = MeasureUtils.NormalizeAngle(-yRotation);
        if(lonDeg > 180)
            lonDeg -= 360;

        return lonDeg;
    }
}