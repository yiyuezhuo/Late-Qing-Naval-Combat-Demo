using UnityEngine;

using NavalCombatCore;


public class DynamicLine : MonoBehaviour
{
    LineRenderer lineRenderer;

    public void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    public void SetBeginEndByLatLon(LatLon src, LatLon dst)
    {
        var srcVec3 = Utils.LatLonToVector3(src);
        var dstVec3 = Utils.LatLonToVector3(dst);

        lineRenderer.positionCount = 2;
        lineRenderer.SetPositions(new Vector3[] { srcVec3, dstVec3 });
    }

    public void SetColor(Color color)
    {
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }
}