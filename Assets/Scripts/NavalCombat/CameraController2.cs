using System;
using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class CameraController2 : MonoBehaviour
{
    public Camera cam;
    public List<Camera> cameras;
    public Transform leafTransform;
    bool dragging = false;

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

    LayerMask sphereLayerMask;

    public enum ScrollMode
    {
        Orthographic,
        Perspective
    }

    public ScrollMode mode;

    // static Vector2 mouseAdjustedCoef = new Vector2(1, -1);
    // static Vector3 mouseAdjustedCoef = new Vector3(1, 1, 1);

    Vector3 initialPosition;

    InputAction scrollWheelAction;
    InputAction rightClickAction;

    [Header("Camera Rotation (Middle Mouse)")]
    public float middleMouseRotateSpeed = 0.2f;
    public float middleMousePitchMin = -75f;
    public float middleMousePitchMax = 75f;
    public float quick3DViewPitchDeg = 45f;
    bool middleMouseRotating;
    Vector2 lastMiddleMousePosition;
    LatLon middleMouseRotationAnchorLatLon;
    bool hasMiddleMouseRotationAnchor;

    public EventHandler cameraMoved;
    public EventHandler cameraZoomed;

    void Awake()
    {
        cameras = GetComponentsInChildren<Camera>().ToList();
        cam = cameras[0];

        sphereLayerMask = LayerMask.GetMask("Sphere");
    }

    // Start is called before the first frame update
    void Start()
    {
        scrollWheelAction = InputSystem.actions.FindAction("ScrollWheel");
        rightClickAction = InputSystem.actions.FindAction("RightClick");

        EnhancedTouchSupport.Enable();

        initialPosition = transform.position;

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
        if(Physics.Raycast(ray, out var hit, Mathf.Infinity, sphereLayerMask))
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

    void UpdateZoom(Camera cam)
    {
        var delta = -GetZoomDeltaSign();
        if (delta != 0)
        {
            var dists = zoomLevel.Select(z => Math.Abs(cam.orthographicSize - z)).ToList();
            var zoomIdx = dists.IndexOf(dists.Min());

            // var delta = -Math.Sign(Input.mouseScrollDelta.y);

            var newZoomIdx = zoomIdx + delta;
            if (newZoomIdx >= 0 && newZoomIdx < zoomLevel.Count)
            {
                cam.orthographicSize = zoomLevel[newZoomIdx];
            }

            cameraZoomed?.Invoke(this, EventArgs.Empty);
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
                    zoomLevel[0],
                    zoomLevel[^1]
                );
            }
        }
    }

    public int GetZoomDeltaSign()
    {
        var scrollZoomSign = GetScrollZoomDeltaSign();

        if(scrollZoomSign != 0)
            return scrollZoomSign;

        return 0;
    }

    public int GetScrollZoomDeltaSign()
    {
        return Math.Sign(Input.mouseScrollDelta.y);
    }

    public void SetCameraState(LatLon latLon, float zoom)
    {
        transform.rotation = Quaternion.Euler(latLon.LatDeg, -latLon.LonDeg, 0);

        foreach (var camera in cameras)
        {
            Debug.LogWarning($"camera.orthographicSize = zoom => {zoom}");
            camera.orthographicSize = zoom;
        }
    }

    static float NormalizeAngle180(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    bool TryGetScreenCenterLatLon(out LatLon latLon)
    {
        latLon = null;
        if (cam == null)
            return false;

        var centerScreen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        var ray = cam.ScreenPointToRay(centerScreen);
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, sphereLayerMask))
            return false;

        latLon = Utils.Vector3ToLatLon(hit.point);
        return true;
    }

    void KeepMiddleMouseRotationAnchorAtScreenCenter()
    {
        if (!hasMiddleMouseRotationAnchor)
            return;

        if (!TryGetScreenCenterLatLon(out var currentLatLon))
            return;

        var delta = new Vector3(
            -(currentLatLon.LatDeg - middleMouseRotationAnchorLatLon.LatDeg),
            currentLatLon.LonDeg - middleMouseRotationAnchorLatLon.LonDeg,
            0f
        );

        if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) <= 0.0001f)
            return;

        transform.localEulerAngles += delta;
    }

    void KeepLatLonAtScreenCenter(LatLon targetLatLon)
    {
        if (targetLatLon == null)
            return;

        // Two passes reduce residual drift after a large tilt reset.
        for (var i = 0; i < 2; i++)
        {
            if (!TryGetScreenCenterLatLon(out var currentLatLon))
                return;

            var delta = new Vector3(
                -(currentLatLon.LatDeg - targetLatLon.LatDeg),
                currentLatLon.LonDeg - targetLatLon.LonDeg,
                0f
            );

            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) <= 0.0001f)
                return;

            transform.localEulerAngles += delta;
        }
    }

    void ResetMiddleMouseCameraView()
    {
        if (leafTransform == null)
            return;

        TryGetScreenCenterLatLon(out var centerBeforeReset);
        leafTransform.localRotation = Quaternion.identity;
        KeepLatLonAtScreenCenter(centerBeforeReset);

        middleMouseRotating = false;
        hasMiddleMouseRotationAnchor = false;
    }

    public void ReturnTo2DView()
    {
        ResetMiddleMouseCameraView();
    }

    public void GoTo3DView()
    {
        if (leafTransform == null)
            return;

        TryGetScreenCenterLatLon(out var centerBeforeAdjust);

        var euler = leafTransform.localEulerAngles;
        var yaw = NormalizeAngle180(euler.y);
        var safePitchMin = Mathf.Min(middleMousePitchMin, middleMousePitchMax);
        var safePitchMax = Mathf.Max(middleMousePitchMin, middleMousePitchMax);
        var pitch = Mathf.Clamp(quick3DViewPitchDeg, safePitchMin, safePitchMax);

        leafTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        KeepLatLonAtScreenCenter(centerBeforeAdjust);

        middleMouseRotating = false;
        hasMiddleMouseRotationAnchor = false;
    }

    public bool HandleMiddleMouseCameraRotation()
    {
        if (leafTransform == null)
            return false;

        if (Input.GetMouseButtonDown(2))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            middleMouseRotating = true;
            lastMiddleMousePosition = Input.mousePosition;
            hasMiddleMouseRotationAnchor = TryGetScreenCenterLatLon(out middleMouseRotationAnchorLatLon);
            return true;
        }

        if (!middleMouseRotating)
            return false;

        if (Input.GetMouseButton(2))
        {
            var currentMousePosition = (Vector2)Input.mousePosition;
            var mouseDelta = currentMousePosition - lastMiddleMousePosition;
            lastMiddleMousePosition = currentMousePosition;

            var pitchDelta = -mouseDelta.y * middleMouseRotateSpeed;
            if (Mathf.Abs(pitchDelta) > 0.0001f)
            {
                var euler = leafTransform.localEulerAngles;
                var safePitchMin = Mathf.Min(middleMousePitchMin, middleMousePitchMax);
                var safePitchMax = Mathf.Max(middleMousePitchMin, middleMousePitchMax);

                var pitch = NormalizeAngle180(euler.x) + pitchDelta;
                var yaw = NormalizeAngle180(euler.y); // keep yaw fixed: disable middle-mouse left/right dragging behavior
                pitch = Mathf.Clamp(pitch, safePitchMin, safePitchMax);

                leafTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                KeepMiddleMouseRotationAnchorAtScreenCenter();
            }

            return true;
        }

        middleMouseRotating = false;
        hasMiddleMouseRotationAnchor = false;
        return false;
    }

    // Update is called once per frame
    void Update()
    {
        if (HandleMiddleMouseCameraRotation())
        {
            return;
        }

        if(EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Zoom        
        foreach (var camera in cameras)
        {
            UpdateZoom(camera);
        }

        // Dragging Navigation
        // if (Input.GetMouseButton(1))
        if (rightClickAction.IsPressed() ||
            (EnhancedTouchSupport.enabled && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 1))
        {
            // var mousePosition = (Vector2)Input.mousePosition * mouseAdjustedCoef;
            if (!dragging)
            {
                dragging = true;
                UpdateHitPoint();
                Debug.Log($"Start dragging at lat={lastTrackedLat}, lon={lastTrackedLon}, cam.orthographicSize={cam.orthographicSize}");
            }
            else
            {
                DragHitPoint();
            }
        }
        else
        {
            if (dragging)
            {
                cameraMoved?.Invoke(this, EventArgs.Empty);
            }
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
