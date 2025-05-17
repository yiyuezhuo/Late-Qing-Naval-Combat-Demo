using UnityEngine;
using System;

public class FixedSizeLine : MonoBehaviour
{
    LineRenderer lineRenderer;
    public float scaleFactor = 1;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();   
    }

    public void LateUpdate()
    {
        var cam = CameraController2.Instance.cam;

        lineRenderer.startWidth = cam.orthographicSize * scaleFactor;
        lineRenderer.endWidth = cam.orthographicSize * scaleFactor;
    }
}