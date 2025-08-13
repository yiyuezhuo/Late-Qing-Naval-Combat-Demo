using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

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

    InputAction scrollWheelAction;
    InputAction rightClickAction;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        initialPosition = transform.position;

        scrollWheelAction = InputSystem.actions.FindAction("ScrollWheel");
        rightClickAction = InputSystem.actions.FindAction("RightClick");

        // var touchSupported = Touchscreen.current != null;
        // var touchSupported = Application.isMobilePlatform; // HACK: temp hack since the above check doesn't work for Unity Remote
        // var touchSupported = Touchscreen.current != null || SystemInfo.deviceModel.Contains("Unity Remote");
        var touchSupported = true;
        Debug.Log($"touchSupported={touchSupported}, SystemInfo.deviceModel={SystemInfo.deviceModel}");
        if (touchSupported)
        {
            Debug.LogWarning("EnhancedTouchSupport is Enabled");
            EnhancedTouchSupport.Enable(); // TODO: The API design suggest that we use an independent class to manage Enable & Disable of it?
        }
    }

    public void ResetToInitialPosition()
    {
        if(initialPosition != Vector3.zero)
            transform.position = initialPosition;
    }

    Vector2 GetHitPoint()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        // var ray = cam.ScreenPointToRay(Utils.mousePosition);
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
        if(!dragging && EventSystem.current.IsPointerOverGameObject())
            return;

        // Zoom
        var mouseScrollDelta = scrollWheelAction.ReadValue<Vector2>();

        // if (Input.mouseScrollDelta.y != 0)
        if (mouseScrollDelta.y != 0)
        {
            switch (mode)
            {
                case ScrollMode.Orthographic:
                    // var newSize = cam.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed;
                    var newSize = cam.orthographicSize - mouseScrollDelta.y * zoomSpeed;
                    if (newSize > 0)
                    {
                        cam.orthographicSize = newSize;
                        GetHitPoint();
                    }
                    break;
                case ScrollMode.Perspective:
                    // var newZ = cam.transform.position.z + Input.mouseScrollDelta.y * zSpeed;
                    var newZ = cam.transform.position.z + mouseScrollDelta.y * zSpeed;
                    if (cam.transform.position.z * newZ < 0)
                        break;
                    cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, newZ);
                    break;
            }
        }

        // Touch Pinch Zooming
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
        {
            var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            var touch2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

            float currentDistance = Vector2.Distance(touch1.screenPosition, touch2.screenPosition);
            
            float prevDistance = Vector2.Distance(
                touch1.screenPosition - touch1.delta,
                touch2.screenPosition - touch2.delta);

            if (prevDistance > 0)
            {
                cam.orthographicSize = Mathf.Clamp(
                    cam.orthographicSize * prevDistance / currentDistance,
                    0.01f,
                    1000
                );
            }
        }
        
        if (rightClickAction.IsPressed() ||
            (EnhancedTouchSupport.enabled && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 1))
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
}