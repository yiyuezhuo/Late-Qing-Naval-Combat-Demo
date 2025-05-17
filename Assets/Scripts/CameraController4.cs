using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController4 : MonoBehaviour
{
    public Camera cam;
    public Camera camIcon;
    // Vector2 prevMousePos;
    // Vector2 prevCamPos;
    bool dragging = false;

    // public float MovingSpeed = 0.1f;
    public float zoomSpeed = 1f;

    Vector3 lastTrackedPos;


    // Vector3 initialPosition;

    // // Start is called before the first frame update
    // void Start()
    // {
    //     // cam = GetComponent<Camera>();
    //     initialPosition = transform.position;
    // }

    // public void ResetToInitialPosition()
    // {
    //     if(initialPosition != Vector3.zero)
    //         transform.position = initialPosition;
    // }

    Vector3 GetHitPoint()
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
        lastTrackedPos = GetHitPoint();
    }

    void DragHitPoint()
    {
        var newTrackedPos = GetHitPoint();

        var lastTrackedScreenPos = cam.WorldToViewportPoint(lastTrackedPos);
        var newTrackedScreenPos = cam.WorldToViewportPoint(newTrackedPos);
        var screenDelta = newTrackedScreenPos - lastTrackedScreenPos;
        var orthoHeight = cam.orthographicSize;
        var orthoWidth = cam.aspect * orthoHeight;
        var worldDelta = new Vector2(screenDelta.x * orthoWidth * 2, screenDelta.y * orthoHeight * 2);

        var newTrackedPosExt = lastTrackedPos + worldDelta.x * cam.transform.right + worldDelta.y * cam.transform.up;
        var q = Quaternion.FromToRotation(lastTrackedPos.normalized, newTrackedPosExt.normalized);
        transform.Rotate(-q.eulerAngles);

        // var camNewProjWorldPos = cam.transform.position - worldDelta.x * cam.transform.right - worldDelta.y * cam.transform.up;
        // var q = Quaternion.FromToRotation(cam.transform.position, camNewProjWorldPos.normalized);
        // transform.Rotate(q.eulerAngles);

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
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Zoom
        // if(Input.mouseScrollDelta.y != 0 && EventSystem.current && !EventSystem.current.IsPointerOverGameObject())
        // if(Input.mouseScrollDelta.y != 0 && EventSystem.current && !UnityUtils.IsPointerOverNonIconUI())
        if (Input.mouseScrollDelta.y != 0)
        {
            UpdateZoom(cam);
            UpdateZoom(camIcon);
        }

        // Dragging Navigation
        if (Input.GetMouseButton(1))
        {
            // var mousePosition = (Vector2)Input.mousePosition * mouseAdjustedCoef;
            if (!dragging)
            {
                dragging = true;
                UpdateHitPoint();
            }
        }
        else
        {
            if (dragging)
            {
                dragging = false;
                DragHitPoint();
            }
        }
    }

    static CameraController4 _instance;
    public static CameraController4 Instance
    {
        get
        {
            if(_instance == null)
                _instance = FindFirstObjectByType<CameraController4>();
            return _instance;
        }
    }

    public void OnDestroy()
    {        
        if(_instance == this)
            _instance = null;
    }
}