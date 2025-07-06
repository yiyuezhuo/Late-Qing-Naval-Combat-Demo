using UnityEngine;
using System;

public class FixedSizeLine : MonoBehaviour
{
    LineRenderer lineRenderer;
    public float scaleFactor = 1;
    public bool updateTextrureScaleX;
    public float lengthWidthRatio;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();   
    }

    public void LateUpdate()
    {
        var cam = CameraController2.Instance.cam;

        var width = cam.orthographicSize * scaleFactor;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        if (updateTextrureScaleX)
        {
            lineRenderer.textureScale = new Vector2(1 / width / lengthWidthRatio, 1);
        }
    }
}