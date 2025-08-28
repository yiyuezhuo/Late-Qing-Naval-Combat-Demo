using UnityEngine;
using TMPro;
using UnityEngine.UIElements;
using System;

using CoreUtils;
using NavalCombatCore;


public interface IPortraitViewerObservable : IObjectIdLabeled, ICollider // Abstraction from ShipLog to support view of torpedo, land battery / target and possbily projectile.
{
    // public float GetLengthFoot();
    // public float GetBeamFoot();
    // public LatLon GetPosition();
    // public float GetHeadingDeg();

    // public string GetPortraitTopCode(); // main View
    public PictureReference GetPortraitTopReference();
    public Country GetCountry(); // flag
    public bool IsShowArrow();
    public GlobalString GetName();
    public float GetDesiredHeadingDeg();
    public string GetAcronym();
}

public class PortraitViewer : MonoBehaviour, IDataSourceViewHashProvider
{
    public string modelObjectId;
    public IPortraitViewerObservable model { get => EntityManager.Instance.Get<IPortraitViewerObservable>(modelObjectId); }

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
    public Transform flagRotationBase;
    public Transform arrowBaseTransform;
    public Transform cubeColliderTransform;
    float scaleFactor = 0.015f;
    public MeshRenderer flagRenderer;
    public GameObject selectedIndicator;
    public MeshRenderer healthBarRenderer;

    //
    Texture2D portraitTex;
    Texture2D countryTex;
    long oldViewHashCode;

    public long GetViewHashCode()
    {
        return HashCode.Combine(
            // type,
            portraitTex,
            // shipLog?.leader?.portraitCode,
            countryTex
        );
    }

    void Awake()
    {
        leafTransform.localPosition = new Vector3(0, 0, -Utils.r);
        flagRenderer.material = flagRenderer.material; // copy material
    }

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

    void MaintainFlagRotationSize()
    {
        var cam = CameraController2.Instance.cam;

        var t = flagRotationBase;

        t.LookAt(t.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);

        var shipLengthFoot = model?.GetLengthFoot() ?? 300;
        var x = shipLengthFoot * Utils.footToWu * modelScale * 10;
        t.localScale = new Vector3(x, x, x);
    }

    void MaintainArrowRotation()
    {
        // var isIndependentControlled = model.GetEffectiveControlMode() == ControlMode.Independent;
        var isShowArrow = model.IsShowArrow();
        arrowBaseTransform.gameObject.SetActive(isShowArrow);

        if (isShowArrow)
        {
            arrowBaseTransform.gameObject.SetActive(true);
            arrowBaseTransform.localEulerAngles = new Vector3(0, 0, -model.GetDesiredHeadingDeg());
            var s = modelScale;
            arrowBaseTransform.localScale = new Vector3(s, s, s);
        }
    }

    public void Update()
    {
        // selectedIndicator
        if (model == null)
            return;

        // TODO: Temp Hack
        if (model is ShipLog shipLog)
        {
            var p = Math.Min(1, shipLog.damagePoint / Math.Max(1, shipLog?.shipClass.damagePoint ?? 0));
            healthBarRenderer.material.SetFloat("_FillAmount", 1 - p);
        }
        else
        {
            healthBarRenderer.gameObject.SetActive(false);
        }

        selectedIndicator.SetActive(model.objectId == GameManager.Instance.selectedShipLogObjectId);

        var latLon = model.GetPosition();
        transform.localEulerAngles = new Vector3(latLon.LatDeg, -latLon.LonDeg, 0);

        var shipLengthFoot = model?.GetLengthFoot() ?? 300;
        var shipBeamFoot = model?.GetBeamFoot() ?? 60;
        iconTransform.localScale = new Vector3(shipLengthFoot * Utils.footToWu * modelScale, shipBeamFoot * Utils.footToWu * modelScale, 1);
        cubeColliderTransform.localScale = new Vector3(shipLengthFoot * Utils.footToWu * 1, shipBeamFoot * Utils.footToWu * 1, 200 * Utils.footToWu); // 100 foots above-waterline height for LOS calculation  

        var zEuler = Utils.TrueNorthCWDegToRightCCWDeg(model.GetHeadingDeg());
        headingTransform.localEulerAngles = new Vector3(0, 0, zEuler);

        MaintainTextDirectionSize();
        MaintainArrowRotation();

        // text.text = $"{model.GetAcronym()} {model.GetName().GetNameFromType(GameManager.Instance.iconLanuageType)}";
        text.text = $"{model.GetAcronym()} {model.GetName().GetNameFromType(GamePreference.Instance.shortLabelLanguageType)}";

        MaintainFlagRotationSize();

        portraitTex = UnityWebRequestImageReader.Instance.FetchTexture2D(model.GetPortraitTopReference().ResolvePath());
        countryTex = UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(model.GetCountry()));

        var newViewHashCode = GetViewHashCode();
        if (oldViewHashCode == newViewHashCode)
            return;

        oldViewHashCode = newViewHashCode;

        iconRenderer.material.SetTexture("_MainTex", portraitTex);        
        flagRenderer.material.SetTexture("_MainTex", countryTex);
    }
}