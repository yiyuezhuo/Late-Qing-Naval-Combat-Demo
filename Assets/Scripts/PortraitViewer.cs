using UnityEngine;
using NavalCombatCore;
using TMPro;
using UnityEngine.UIElements;
using System;

public class PortraitViewer : MonoBehaviour, IDataSourceViewHashProvider
{    
    public string modelObjectId;
    public ShipLog shipLog{get => EntityManager.Instance.Get<ShipLog>(modelObjectId);}

    public enum Type
    {
        ShipTopPortrait,
        CaptainPortrait,
        ShipShape,
        Point, // If the unit is too small to be spotted
    }
    public Type type;

    public float modelScale = 1f;
    public MeshRenderer iconRenderer;
    public TMP_Text text;
    public Transform iconTransform;
    public Transform leafTransform;
    public Transform textBaseTransform;
    public Transform headingTransform;
    float scaleFactor = 0.02f;
    // public RectTransform canvasRectTransform;

    long oldViewHashCode;

    public long GetViewHashCode()
    {
        return HashCode.Combine(
            type,
            shipLog?.shipClass?.portraitTopCode,
            // shipLog?.captainPortraitCode,
            shipLog?.leader?.portraitCode,
            shipLog.shipClass.GetAcronym()
        );
    }

    void Awake()
    {
        // iconRenderer = GetComponent<MeshRenderer>();
        leafTransform.localPosition = new Vector3(0, 0, -Utils.r);
    }

    // void UpdateTextLocation()
    // {
    //     var targetTransform = transform;
    //     var mainCamera = CameraController2.Instance.cam;

    //     Vector3 screenPosition = mainCamera.WorldToScreenPoint(targetTransform.position);

    //     RectTransformUtility.ScreenPointToLocalPointInRectangle(
    //         canvasRectTransform,
    //         screenPosition,
    //         mainCamera,
    //         out Vector2 localPoint
    //     );

    //     text.rectTransform.localPosition = localPoint;
    // }

    void MaintainTextDirectionSize()
    {
        var cam = CameraController2.Instance.cam;

        // text.transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
        //                  cam.transform.rotation * Vector3.up);

        var t = text.transform;

        t.LookAt(t.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);

        // text.transform.localScale = Vector3.one * cam.orthographicSize * scaleFactor;
        t.localScale = Vector3.one * cam.orthographicSize * scaleFactor;
        // text.transform.localScale = Vector3.one * cam.orthographicSize * scaleFactor;
    }

    public void Update()
    {
        // UpdateTextLocation();

        // transform.localPosition = Utils.LatitudeLongitudeDegToVector3(shipLog.GetLatitudeDeg(), shipLog.GetLongitudeDeg());
        var latLon = shipLog.position;
        transform.localEulerAngles = new Vector3(latLon.LatDeg, -latLon.LonDeg, 0);

        // TODO: rescale with length, beam, draft parameter
        var shipLengthFoot = shipLog?.shipClass.lengthFoot ?? 300;
        var shipBeamFoot = shipLog?.shipClass.beamFoot ?? 60;
        iconTransform.localScale = new Vector3(shipLengthFoot * Utils.footToWu * modelScale, shipBeamFoot * Utils.footToWu * modelScale, 1);

        var zEuler = Utils.TrueNorthCWDegToRightCCWDeg(shipLog.GetHeadingDeg());
        // iconTransform.localEulerAngles = new Vector3(0, 0, zEuler);
        headingTransform.localEulerAngles = new Vector3(0, 0, zEuler);

        MaintainTextDirectionSize();

        var newViewHashCode = GetViewHashCode();
        if (oldViewHashCode == newViewHashCode)
            return;

        oldViewHashCode = newViewHashCode;

        text.text = shipLog.namedShip.name.english;

        var shipClass = shipLog.shipClass;
        var portraitTopCode = shipClass.portraitTopCode;
        var portraitTop = ResourceManager.GetShipPortraitSprite(portraitTopCode);
        iconRenderer.material.SetTexture("_MainTex", portraitTop.texture);
    }
}