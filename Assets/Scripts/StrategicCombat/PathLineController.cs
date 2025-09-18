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
            firstSegmentLineRenderer.SetPositions(new[]{positions[0], progressBreak});

            positions[0] = progressBreak;

            otherSegmentLineRenderer.positionCount = pathCells.Count;
            otherSegmentLineRenderer.SetPositions(positions);
        }
        
        // var positionsEnum = pathCells.Select(xy =>
        // {
        //     var (xf, yf) = HexMapShower.CellXYToLocalXY(xy.x, xy.y);
        //     var pos = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
        //     return new Vector3(pos.x, pos.y, 0);
        // }); //.ToArray();

        // var showFirstSegment = pathCells.Count >= 2;
        // var showOtherSegment = pathCells.Count > 2;

        // firstSegmentLineRenderer.gameObject.SetActive(showFirstSegment);
        // otherSegmentLineRenderer.gameObject.SetActive(showOtherSegment);

        // if (showFirstSegment)
        // {
        //     firstSegmentLineRenderer.positionCount = 2;
        //     firstSegmentLineRenderer.SetPositions(positionsEnum.Take(2).ToArray());

        //     var gradient = new Gradient();
        //     gradient.SetKeys(
        //         new GradientColorKey[] {
        //             new GradientColorKey(Color.blue, 0f),
        //             new GradientColorKey(Color.blue, firstSegmentProgress - 0.0001f),
        //             new GradientColorKey(Color.white, firstSegmentProgress),
        //             new GradientColorKey(Color.white, 1f)
        //         },
        //         new GradientAlphaKey[] {
        //             new GradientAlphaKey(1f, 0f),
        //             new GradientAlphaKey(1f, 1f)
        //         }
        //     );
        //     gradient.mode = GradientMode.Fixed;
        //     firstSegmentLineRenderer.colorGradient = gradient;
        // }

        // if (otherSegmentLineRenderer)
        // {
        //     otherSegmentLineRenderer.positionCount = pathCells.Count - 1;
        //     otherSegmentLineRenderer.SetPositions(positionsEnum.Skip(1).ToArray());
        // }
    }
}
