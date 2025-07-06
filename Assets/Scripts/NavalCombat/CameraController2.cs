using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController2 : MonoBehaviour
{
    public Camera cam;
    // public Camera camIcon;
    public List<Camera> cameras;
    public Transform leafTransform;
    // Vector2 prevMousePos;
    // Vector2 prevCamPos;
    bool dragging = false;

    // public float MovingSpeed = 0.1f;
    // public float zoomSpeed = 1f;

    // [Serializable]
    // public class ZoomSpeedReocrd
    // {
    //     public float zoomSizeThreashold;
    //     public float zoomSpeed;
    // }
    // public List<ZoomSpeedReocrd> zoomSpeedRecords = new();
    // public float minSize = 0.001f;

    List<float> zoomLevel = new List<float>
    {
        0.004f,
        0.01f,
        0.02f,
        0.04f,
        0.1f,
        0.2f,
        0.4f,
        1.0f,
        2.0f,
        4.0f,
        10.0f,
        20.0f,
        40.0f,
        100.0f,
        200.0f,
        400.0f,
        1000.0f,
        2000.0f,
        4000.0f,
        10000.0f,
        20000.0f,
        40000.0f,
        100000.0f,
        200000.0f,
        400000.0f,
        1000000.0f,
    };

    // Vector3 lastTrackedPos;
    float lastTrackedLat;
    float lastTrackedLon;
    // public Transform leafTransform;

    public enum ScrollMode
    {
        Orthographic,
        Perspective
    }

    public ScrollMode mode;

    // static Vector2 mouseAdjustedCoef = new Vector2(1, -1);
    // static Vector3 mouseAdjustedCoef = new Vector3(1, 1, 1);

    Vector3 initialPosition;

    // Start is called before the first frame update
    void Start()
    {
        // cam = GetComponent<Camera>();
        initialPosition = transform.position;

        cameras = GetComponentsInChildren<Camera>().ToList();
        cam = cameras[0];

        var delta = Math.Min(Utils.r, 1000);
        leafTransform.localPosition = new Vector3(0, 0, -(Utils.r + delta));
    }

    public void ResetToInitialPosition()
    {
        if(initialPosition != Vector3.zero)
            transform.position = initialPosition;
    }

    public Vector3 GetHitPoint()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        // var plane = new Plane(Vector3.forward, Vector3.zero);
        if(Physics.Raycast(ray, out var hit))
        {
            return hit.point;
        }
        return Vector3.zero;
    }

    void UpdateHitPoint()
    {
        var lastTrackedPos = GetHitPoint();
        (lastTrackedLat, lastTrackedLon) = Utils.Vector3ToLatitudeLongitudeDeg(lastTrackedPos);
    }

    void DragHitPoint()
    {
        var newTrackedPos = GetHitPoint();
        (var newTrackedLat, var newTrackedLon) = Utils.Vector3ToLatitudeLongitudeDeg(newTrackedPos);

        // var euler = new Vector3(-(newTrackedLat - lastTrackedLat), newTrackedLon - lastTrackedLon, 0);
        // Debug.Log(euler);
        // Debug.Log($"x={euler.x}, y={euler.y}, z={euler.z}");
        var delta = new Vector3(-(newTrackedLat - lastTrackedLat), newTrackedLon - lastTrackedLon, 0);
        if (Math.Max(Math.Abs(delta.x), Math.Abs(delta.y)) > 0.0001)
        {
            transform.localEulerAngles = transform.localEulerAngles + delta;
            // transform.Rotate(euler);

            // var diff = newTrackedPos - lastTrackedPos;
            // transform.position = transform.position - new Vector3(diff.x * mouseAdjustedCoef.x, 0, diff.z * mouseAdjustedCoef.z);
            UpdateHitPoint();
        }
    }

    // float GetZoomSpeed()
    // {
    //     var record = zoomSpeedRecords.Where(r => cam.orthographicSize <= r.zoomSizeThreashold).FirstOrDefault();
    //     return record?.zoomSpeed ?? 1;
    // }

    void UpdateZoom(Camera cam)
    {
        var dists = zoomLevel.Select(z => Math.Abs(cam.orthographicSize - z)).ToList();
        var zoomIdx = dists.IndexOf(dists.Min());
        var delta = -Math.Sign(Input.mouseScrollDelta.y);
        var newZoomIdx = zoomIdx + delta;
        if (newZoomIdx >= 0 && newZoomIdx < zoomLevel.Count)
        {
            cam.orthographicSize = zoomLevel[newZoomIdx];
        }

        // var newSize = cam.orthographicSize - Input.mouseScrollDelta.y * GetZoomSpeed() * zoomSpeed;
        // var newSize = cam.orthographicSize - GetZoomSpeed() * zoomSpeed;
        // if (newSize > minSize)
        // {
        //     cam.orthographicSize = newSize;
        //     // GetHitPoint();
        // }
    }

    // Update is called once per frame
    void Update()
    {
        if(EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Zoom
        // if(Input.mouseScrollDelta.y != 0 && EventSystem.current && !EventSystem.current.IsPointerOverGameObject())
        // if(Input.mouseScrollDelta.y != 0 && EventSystem.current && !UnityUtils.IsPointerOverNonIconUI())
        if (Input.mouseScrollDelta.y != 0)
        {
            // UpdateZoom(cam);
            // UpdateZoom(camIcon);
            foreach (var camera in cameras)
            {
                UpdateZoom(camera);
            }
        }

        // Dragging Navigation
        if(Input.GetMouseButton(1))
        {
            // var mousePosition = (Vector2)Input.mousePosition * mouseAdjustedCoef;
            if (!dragging)
            {
                dragging = true;
                UpdateHitPoint();
            }
            else
            {
                DragHitPoint();
            }
        }
        else
        {
            dragging = false;
        }
    }

    static CameraController2 _instance;
    public static CameraController2 Instance
    {
        get
        {
            if(_instance == null)
                _instance = FindFirstObjectByType<CameraController2>();
            return _instance;
        }
    }

    public void OnDestroy()
    {        
        if(_instance == this)
            _instance = null;
    }
}