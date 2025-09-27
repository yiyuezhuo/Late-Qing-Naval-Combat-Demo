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
        var positions = pathCells.Select(xy =>
        {
            var (xf, yf) = HexMapShower.CellXYToLocalXY(xy.x, xy.y);
            var pos = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
            var posZ = -0.1f;
            return new Vector3(pos.x, pos.y, posZ);
        }).ToArray();

        var show = positions.Length >= 2;
        firstSegmentLineRenderer.gameObject.SetActive(show);
        otherSegmentLineRenderer.gameObject.SetActive(show);

        if (show)
        {
            var p = firstSegmentProgress;
            var progressBreak = (1 - p) * positions[0] + p * positions[1];

            firstSegmentLineRenderer.positionCount = 2;
            firstSegmentLineRenderer.SetPositions(new[] { positions[0], progressBreak });

            positions[0] = progressBreak;

            otherSegmentLineRenderer.positionCount = pathCells.Count;
            otherSegmentLineRenderer.SetPositions(positions);
        }
    }
}
