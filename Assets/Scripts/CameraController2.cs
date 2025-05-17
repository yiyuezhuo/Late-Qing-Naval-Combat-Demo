using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController2 : MonoBehaviour
{
    public Camera cam;
    // public Camera camIcon;
    public List<Camera> cameras;
    // Vector2 prevMousePos;
    // Vector2 prevCamPos;
    bool dragging = false;

    // public float MovingSpeed = 0.1f;
    public float zoomSpeed = 1f;

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
        if(Physics.Raycast(ray, out var hit, 100))
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

        var euler = new Vector3(-(newTrackedLat - lastTrackedLat), newTrackedLon - lastTrackedLon, 0);
        // Debug.Log(euler);
        // Debug.Log($"x={euler.x}, y={euler.y}, z={euler.z}");
        transform.localEulerAngles = transform.localEulerAngles + new Vector3(-(newTrackedLat - lastTrackedLat), newTrackedLon - lastTrackedLon, 0);
        // transform.Rotate(euler);

        // var diff = newTrackedPos - lastTrackedPos;
        // transform.position = transform.position - new Vector3(diff.x * mouseAdjustedCoef.x, 0, diff.z * mouseAdjustedCoef.z);
        UpdateHitPoint();
    }

    void UpdateZoom(Camera cam)
    {
        var newSize = cam.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed;
        if (newSize > 0.001f)
        {
            cam.orthographicSize = newSize;
            GetHitPoint();
        }
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