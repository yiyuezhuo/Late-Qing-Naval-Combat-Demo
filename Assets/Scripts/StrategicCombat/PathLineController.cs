using UnityEngine;
using System.Collections.Generic;
using StrategicCombatCore;
using System.Linq;

public class PathLineController : MonoBehaviour
{
    public LineRenderer firstSegmentLineRenderer;
    public LineRenderer otherSegmentLineRenderer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Sync(List<XY> pathCells, float firstSegmentProgress)
    {
        // var positions = pathCells.Select(xy =>
        // {
        //     var posZ = -0.1f;
        //     var cell = xy.GetCell();
        //     if(cell.IsGridCell())
        //     {
        //         var (xf, yf) = HexMapShower.CellXYToLocalXY(xy.x, xy.y);
        //         var pos = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
        //         return new Vector3(pos.x, pos.y, posZ);
        //     }
        //     else
        //     {
        //         var hitArea = StrategicGameManager.Instance.areaCellObjectIdToHitArea[cell.objectId];
        //         return new Vector3(hitArea.transform.position.x, hitArea.transform.position.y, posZ);
        //     }
        // }).ToArray();
        var positions = Utils.XYListToVector3Array(pathCells);

        var show = positions.Length >= 2;
        firstSegmentLineRenderer.gameObject.SetActive(show);
        otherSegmentLineRenderer.gameObject.SetActive(show);

        if (show)
        {
            var p = Mathf.Clamp01(firstSegmentProgress);
            var progressBreak = (1 - p) * positions[0] + p * positions[1];
            StrategicLineRenderUtils.ConfigureLineRenderer(firstSegmentLineRenderer);
            StrategicLineRenderUtils.ConfigureLineRenderer(otherSegmentLineRenderer);

            var firstSegmentPositions = new[] { positions[0], progressBreak };
            firstSegmentLineRenderer.positionCount = firstSegmentPositions.Length;
            firstSegmentLineRenderer.SetPositions(firstSegmentPositions);

            Vector3[] otherSegmentPositions;
            if (positions.Length == 2)
            {
                otherSegmentPositions = new[] { progressBreak, positions[1] };
            }
            else
            {
                var remainingAnchors = positions.Skip(1).ToArray();
                var smoothedRemaining = StrategicLineRenderUtils.BuildSmoothPolyline(remainingAnchors);
                otherSegmentPositions = new Vector3[smoothedRemaining.Length + 1];
                otherSegmentPositions[0] = progressBreak;
                for (int i = 0; i < smoothedRemaining.Length; i++)
                {
                    otherSegmentPositions[i + 1] = smoothedRemaining[i];
                }
            }

            otherSegmentLineRenderer.positionCount = otherSegmentPositions.Length;
            otherSegmentLineRenderer.SetPositions(otherSegmentPositions);
        }
    }
}
