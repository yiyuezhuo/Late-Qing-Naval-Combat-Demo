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

    long oldViewHashCode;

    public long GetViewHashCode()
    {
        return HashCode.Combine(
            type,
            shipLog?.shipClass?.portraitTopCode,
            shipLog?.captainPortraitCode,
            shipLog.shipClass.GetAcronym()
        );
    }

    void Awake()
    {
        // iconRenderer = GetComponent<MeshRenderer>();
    }

    public void Update()
    {
        // transform.localPosition = Utils.LatitudeLongitudeDegToVector3(shipLog.GetLatitudeDeg(), shipLog.GetLongitudeDeg());
        var latLon = shipLog.position;
        transform.localEulerAngles = new Vector3(latLon.LatDeg, -latLon.LonDeg, 0);

        // TODO: rescale with length, beam, draft parameter
        var shipLengthFoot = shipLog?.shipClass.lengthFoot ?? 300;
        var shipBeamFoot = shipLog?.shipClass.beamFoot ?? 60;
        iconTransform.localScale = new Vector3(shipLengthFoot * Utils.footToWu * modelScale, shipBeamFoot * Utils.footToWu * modelScale, 1);

        var zEuler = Utils.TrueNorthCWDegToRightCCWDeg(shipLog.GetHeadingDeg());
        iconTransform.localEulerAngles = new Vector3(0, 0, zEuler);

        var newViewHashCode = GetViewHashCode();
        if (oldViewHashCode == newViewHashCode)
            return;

        oldViewHashCode = newViewHashCode;

        // text.text = shipLog.shipClass.GetAcronym();

        var shipClass = shipLog.shipClass;
        var portraitTopCode = shipClass.portraitTopCode;
        var portraitTop = ResourceManager.GetShipPortraitSprite(portraitTopCode);
        iconRenderer.material.SetTexture("_MainTex", portraitTop.texture);
    }
}