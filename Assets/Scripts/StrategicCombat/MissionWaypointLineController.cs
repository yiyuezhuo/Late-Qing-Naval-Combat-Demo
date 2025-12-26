using System.Collections.Generic;
using UnityEngine;
using StrategicCombatCore;
using System.Linq;

public class WaypointController : MonoBehaviour
{
    public LineRenderer lineRenderer;

    public void Sync(List<XY> waypoints)
    {
        // var positions = waypoints.Select(xy =>
        // {
        //     var (xf, yf) = HexMapShower.CellXYToLocalXY(xy.x, xy.y);
        //     var pos = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
        //     var posZ = -0.1f;
        //     return new Vector3(pos.x, pos.y, posZ);
        // }).ToArray();
        var positions = Utils.XYListToVector3Array(waypoints);

        lineRenderer.positionCount = waypoints.Count;
        lineRenderer.SetPositions(positions);
    }
}