using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlaneCameraController : SingletonMonoBehaviour<PlaneCameraController>
{
    public Camera cam;
    // Vector2 prevMousePos;
    // Vector2 prevCamPos;
    bool dragging = false;

    // public float MovingSpeed = 0.1f;
    public float zoomSpeed = 1f;
    public float zSpeed = 0.25f;

    Vector2 lastTrackedPos;

    public enum ScrollMode
    {
        Orthographic,
        Perspective
    }

    public ScrollMode mode;

    // static Vector2 mouseAdjustedCoef = new Vector2(1, -1);
    static Vector2 mouseAdjustedCoef = new Vector2(1, 1);

    Vector3 initialPosition;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        initialPosition = transform.position;
    }

    public void ResetToInitialPosition()
    {
        if(initialPosition != Vector3.zero)
            transform.position = initialPosition;
    }

    Vector2 GetHitPoint()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out var distance))
        {
            var hitPoint = ray.GetPoint(distance);
            return (Vector2)hitPoint;
        }
        return Vector2.zero;
    }

    void UpdateHitPoint()
    {
        lastTrackedPos = GetHitPoint();
    }

    void DragHitPoint()
    {
        var newTrackedPos = GetHitPoint();
        // Debug.Log(newTrackedPos);
        var diff = newTrackedPos - lastTrackedPos;
        // Debug.Log($"Before: {transform.position}");
        // transform.Translate(-diff * mouseAdjustedCoef);
        var diff2 = diff * mouseAdjustedCoef;
        transform.position = transform.position - new Vector3(diff2.x, diff2.y, 0);
        // Debug.Log($"After: {transform.position}");
        UpdateHitPoint();
    }

    // Update is called once per frame
    void Update()
    {
        if(EventSystem.current.IsPointerOverGameObject())
            return;
        
        // Zoom
        if(Input.mouseScrollDelta.y != 0)
        {
            switch(mode)
            {
                case ScrollMode.Orthographic:
                    var newSize = cam.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed;
                    if (newSize > 0)
                    {
                        cam.orthographicSize = newSize;
                        GetHitPoint();
                    }
                    break;
                case ScrollMode.Perspective:
                    var newZ = cam.transform.position.z + Input.mouseScrollDelta.y * zSpeed;
                    if (cam.transform.position.z * newZ < 0)
                        break;
                    cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, newZ);
                    break;
            }
        }

        // Dragging Navigation
        if(Input.GetMouseButton(1))
        {
            var mousePosition = (Vector2)Input.mousePosition * mouseAdjustedCoef;
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
}