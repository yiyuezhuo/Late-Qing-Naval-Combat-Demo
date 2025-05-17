using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class MeasureLine : MonoBehaviour
{
    public enum State
    {
        Idle,
        Hovering,
        Fixed
    }

    public State state = State.Idle;
    public int segments = 10;

    public LatLon lastTrackedLatLon;
    public LineRenderer lineRenderer;
    public TMP_Text text;

    void Awake()
    {
        // lineRenderer = GetComponent<LineRenderer>();
        Hide();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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

        if (controlPressing && Input.GetKeyDown(KeyCode.D))
        {
            state = State.Hovering;
            Show();

            var lastTrackedPos = CameraController2.Instance.GetHitPoint();
            lastTrackedLatLon = Utils.Vector3ToLatLon(lastTrackedPos);
        }
        if (state == State.Hovering && Input.GetMouseButton(0))
        {
            state = State.Fixed;    
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            state = State.Idle;
            Hide();
        }

        if (state == State.Hovering)
        {
            var currentPos = CameraController2.Instance.GetHitPoint();
            var currentLatLon = Utils.Vector3ToLatLon(currentPos);
            // if(currentLatLon != lastTrackedLatLon)

            var inverseLine = Geodesic.WGS84.InverseLine(
                lastTrackedLatLon.LatDeg, lastTrackedLatLon.LonDeg,
                currentLatLon.LatDeg, currentLatLon.LonDeg
            );
            var distM = inverseLine.Distance;

            // Update measure line
            var positions = new Vector3[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var p = (float)i / segments;
                var pos = inverseLine.Position(distM * p);
                var vec3 = Utils.LatitudeLongitudeDegToVector3((float)pos.Latitude, (float)pos.Longitude);
                positions[i] = vec3;
            }
            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);

            // Update measure text
            var distNm = distM / 1852;
            var bearing = inverseLine.Azimuth;
            text.text = $"{distNm.ToString("0.00")}nm {bearing.ToString("0.00")}deg";
            text.transform.position = currentPos;
        }
    }
}
