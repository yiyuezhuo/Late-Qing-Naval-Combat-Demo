using UnityEngine;
using NavalCombatCore;
// using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using GeographicLib;


public class LOSLine : MonoBehaviour
{
    public enum State
    {
        Idle,
        ChooseStart,
        ChooseEnd,
        Fixed
    }

    public State state = State.Idle;

    public LatLon startLatLon;
    // public Vector3 startPos;
    public LineRenderer lineRenderer;
    public TMP_Text text;
    LayerMask losLayerMask;

    void Awake()
    {
        // lineRenderer = GetComponent<LineRenderer>();
        Hide();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        losLayerMask = LayerMask.GetMask("LOSAndCollide");
    }

    void Show()
    {
        lineRenderer.enabled = true;
        text.enabled = true;
    }

    void Hide()
    {
        lineRenderer.enabled = false;
        text.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        var controlPressing = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        switch (state)
        {
            case State.ChooseStart:
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.ChooseEnd;
                    Show();

                    var lastTrackedPos = CameraController2.Instance.GetHitPoint();
                    startLatLon = Utils.Vector3ToLatLon(lastTrackedPos);
                }
                break;

            case State.ChooseEnd:
                var currentPos = CameraController2.Instance.GetHitPoint();
                var currentLatLon = Utils.Vector3ToLatLon(currentPos);

                var raycastStart = Utils.LatLonHeightFootToVector3(startLatLon, 100);
                var raycastEnd = Utils.LatLonHeightFootToVector3(currentLatLon, 100);
                var direction = (raycastEnd - raycastStart).normalized;

                var endPos = raycastEnd;

                string maskMessage = null;
                // var maskDistanceM = 0;
                if (Physics.Raycast(raycastStart, direction, out RaycastHit hit, Mathf.Infinity, losLayerMask))
                {
                    endPos = hit.point;
                    var colliderRootProvider = hit.collider.GetComponent<IColliderRootProvider>();
                    if (colliderRootProvider != null)
                    {
                        var root = colliderRootProvider.GetRoot();
                        var shipLog = root?.GetComponent<PortraitViewer>()?.shipLog;
                        if (shipLog != null)
                        {
                            maskMessage = $"Masked by {shipLog.namedShip.name.GetMergedName()}";
                        }
                        else
                        {
                            maskMessage = $"Masked by {root.name} (DEBUG)";
                        }
                    }
                    // var hitLatLon = Utils.Vector3ToLatLon(hit.point);
                    // Geodesic.WGS84.Inverse(currentLatLon.LatDeg, currentLatLon.LonDeg, hitLatLon.LatDeg, hitLatLon.LonDeg, out var s12);
                    // maskDistanceM = s12
                }

                // Check Earth's curvature, use Geodesic distance and visibility table of SK 5 for size 3
                // Earth bacll itself can work as a collider but the mesh is roughtly approximated for the small area we usually consider so we abandon this approach.
                var endLatLon = Utils.Vector3ToLatLon(endPos);
                var inverseLine = Geodesic.WGS84.InverseLine(startLatLon.LatDeg, startLatLon.LonDeg, endLatLon.LatDeg, endLatLon.LonDeg);

                if (inverseLine.Distance > 0)
                {
                    Debug.Log($"{currentLatLon} -> {endLatLon}: {inverseLine.Distance}");
                }

                if (inverseLine.Distance * MeasureUtils.meterToYard > 32900) // blocked by Earth's curvature (SK5 Table D1, Size 4 and up in best visibility)
                {
                    maskMessage = $"Blocked by Earth's curvature";
                    var _endLatLon = inverseLine.Position(32900 * MeasureUtils.yardToMeter);
                    endPos = Utils.LatitudeLongitudeDegToVector3((float)_endLatLon.Latitude, (float)_endLatLon.Longitude);
                }

                var positions = new Vector3[2]{raycastStart, endPos};
                lineRenderer.positionCount = 2;
                lineRenderer.SetPositions(positions);

                text.text = maskMessage != null ? maskMessage : "Passed";
                text.transform.position = endPos;

                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Fixed;
                }
                break;

            case State.Fixed:
                break;
        }

        // if (controlPressing && Input.GetKeyDown(KeyCode.D))
        if (!EventSystem.current.IsPointerOverGameObject() && Input.GetKeyDown(KeyCode.S)) // S denotes line of Sight or Sight line
        {
            state = State.ChooseStart;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            state = State.Idle;
            Hide();
        }
    }
}