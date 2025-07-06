using UnityEngine;
using System;

public class FixedDirectionalSizeIcon : MonoBehaviour
{
    public float scaleFactor = 1;

    // public void Update()
    public void LateUpdate()
    {

        // var cam = CameraController.Instance.camIcon;
        var cam = CameraController2.Instance.cam;

        transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);

        transform.localScale = Vector3.one * cam.orthographicSize * scaleFactor;
    }
}