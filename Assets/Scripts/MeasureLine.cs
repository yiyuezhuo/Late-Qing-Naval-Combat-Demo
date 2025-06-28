using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

public class MeasureLine : SingletonMonoBehaviour<MeasureLine>
{
    public enum State
    {
        Idle,
        ChooseStart,
        ChooseEnd,
        Fixed
    }

    public State state = State.Idle;
    public int segments = 10;

    public LatLon startLatLon;
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
                // if(currentLatLon != lastTrackedLatLon)

                var inverseLine = Geodesic.WGS84.InverseLine(
                    startLatLon.LatDeg, startLatLon.LonDeg,
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
                var distYards = distM * 1.09361;
                var bearing = inverseLine.Azimuth;
                text.text = $"{distNm.ToString("0.00")}nm\n{distYards.ToString("0.00")}yards\n{bearing.ToString("0.00")}deg";
                text.transform.position = currentPos;

                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Fixed;
                }
                break;

            case State.Fixed:
                break;
        }

        if (!EventSystem.current.IsPointerOverGameObject() && Input.GetKeyDown(KeyCode.D))
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
