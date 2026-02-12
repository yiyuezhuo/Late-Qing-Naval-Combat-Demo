using UnityEngine;
using TMPro;
using UnityEngine.UIElements;
using System;
using System.Linq;

using CoreUtils;
using NavalCombatCore;
using System.Collections.Generic;


public interface IPortraitViewerObservable : IObjectIdLabeled, ICollider // Abstraction from ShipLog to support view of torpedo, land battery / target and possbily projectile.
{
    // public float GetLengthFoot();
    // public float GetBeamFoot();
    // public LatLon GetPosition();
    // public float GetHeadingDeg();

    // public string GetPortraitTopCode(); // main View
    public PictureReference GetPortraitTopReference();
    public PictureReference GetPortraitIconReference();
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

    // public enum Type
    // {
    //     ShipTopPortrait,
    //     CaptainPortrait,
    //     ShipShape,
    //     Point, // If the unit is too small to be spotted
    // }
    // public Type type;
    public enum Mode
    {
        Icon, // basically transparent top image
        Counter // default top image
    }
    public static Mode mode = Mode.Icon;

    public static float modelScale = 1.5f;
    // public static float textScaleFactor = 0.015f;
    public static float textScaleFactor = 0.012f;
    public static float iconBeamScale = 1.25f; // Increase icon's beam size to increase recognition

    public MeshRenderer iconRenderer;
    public TMP_Text text;
    public Transform iconTransform;
    public Transform leafTransform;
    public Transform textBaseTransform;
    public Transform headingTransform;
    public Transform flagRotationBase;
    public Transform arrowBaseTransform;
    public Transform cubeColliderTransform;
    public Transform torpedoThreatCubeColliderTransform;
    
    public MeshRenderer flagRenderer;
    public GameObject selectedIndicator;
    public MeshRenderer healthBarRenderer;
    // public MeshRenderer sunkCrossRenderer;

    public List<GameObject> deployedGameObjects;
    public List<GameObject> destroyedGameObjects;
    public List<GameObject> wakeGameObjects;
    public bool autoCreateWakeTrails = true;
    public float wakeSpeedThresholdKnots = 1f;

    public AudioClip gunfireSound;
    public AudioClip torpedoFireSound;
    public AudioClip explosionSound;
    AudioSource audioSource;
    readonly List<LineRenderer> autoWakeLines = new();
    readonly List<WakeSample> wakeSamples = new();
    Transform wakePortAnchor;
    Transform wakeStarboardAnchor;
    Material wakeTrailMaterial;
    public float wakeSimulationHistorySeconds = 180f;
    public float wakeSampleMinDistanceWu = 0.003f;

    struct WakeSample
    {
        public long ticks;
        public Vector3 portPos;
        public Vector3 starboardPos;
    }

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
            countryTex,
            GetTransparent()
        );
    }

    void Awake()
    {
        leafTransform.localPosition = new Vector3(0, 0, -Utils.r);
        flagRenderer.material = flagRenderer.material; // copy material

        audioSource = GetComponent<AudioSource>();
        TryBindWakeGameObjectsByName();
    }

    void TryBindWakeGameObjectsByName()
    {
        if (wakeGameObjects != null && wakeGameObjects.Count > 0)
            return;

        wakeGameObjects = GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t != transform)
            .Where(t =>
            {
                var n = t.name.ToLowerInvariant();
                return n.Contains("wake") || n.Contains("foam");
            })
            .Select(t => t.gameObject)
            .ToList();
    }

    void EnsureAutoWakeLines()
    {
        if (!autoCreateWakeTrails || autoWakeLines.Count > 0)
            return;

        wakePortAnchor = new GameObject("WakePortAnchor").transform;
        wakePortAnchor.SetParent(headingTransform, false);
        wakeStarboardAnchor = new GameObject("WakeStarboardAnchor").transform;
        wakeStarboardAnchor.SetParent(headingTransform, false);

        autoWakeLines.Add(CreateWakeLine("WakePortLine"));
        autoWakeLines.Add(CreateWakeLine("WakeStarboardLine"));
    }

    LineRenderer CreateWakeLine(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0.02f, 0), new GradientAlphaKey(0.3f, 1) }
        );
        line.colorGradient = gradient;

        line.widthCurve = new AnimationCurve(
            new Keyframe(0, 0.1f),
            new Keyframe(1, 1f)
        );

        if (wakeTrailMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            wakeTrailMaterial = shader != null ? new Material(shader) : null;
        }
        if (wakeTrailMaterial != null)
            line.material = wakeTrailMaterial;

        return line;
    }

    static long GetSimulationTicks()
    {
        var scenarioState = NavalGameState.Instance?.scenarioState;
        if (scenarioState != null)
            return scenarioState.dateTime.Ticks;
        return DateTime.UtcNow.Ticks;
    }

    void ClearAutoWakeLines()
    {
        wakeSamples.Clear();
        foreach (var line in autoWakeLines)
        {
            if (line == null)
                continue;
            line.enabled = false;
            line.positionCount = 0;
        }
    }

    void PushWakeSample(long nowTicks, Vector3 portWorldPos, Vector3 starboardWorldPos)
    {
        if (wakeSamples.Count > 0)
        {
            var last = wakeSamples[wakeSamples.Count - 1];
            if (nowTicks < last.ticks)
            {
                ClearAutoWakeLines();
            }
            else
            {
                var distPort = Vector3.Distance(last.portPos, portWorldPos);
                var distStarboard = Vector3.Distance(last.starboardPos, starboardWorldPos);
                var movedEnough = distPort >= wakeSampleMinDistanceWu || distStarboard >= wakeSampleMinDistanceWu;
                if (!movedEnough)
                    return;
            }
        }

        wakeSamples.Add(new WakeSample
        {
            ticks = nowTicks,
            portPos = portWorldPos,
            starboardPos = starboardWorldPos
        });
    }

    void TrimWakeSamples(long nowTicks)
    {
        if (wakeSimulationHistorySeconds <= 0)
        {
            ClearAutoWakeLines();
            return;
        }

        var keepTicks = (long)(wakeSimulationHistorySeconds * TimeSpan.TicksPerSecond);
        var cutoffTicks = nowTicks - keepTicks;

        while (wakeSamples.Count > 0 && wakeSamples[0].ticks < cutoffTicks)
        {
            wakeSamples.RemoveAt(0);
        }
    }

    void SyncWakeLineRenderers()
    {
        if (autoWakeLines.Count < 2)
            return;

        var pointCount = wakeSamples.Count;
        var hasEnoughPoints = pointCount >= 2;

        for (int i = 0; i < autoWakeLines.Count; i++)
        {
            var line = autoWakeLines[i];
            if (line == null)
                continue;
            line.enabled = hasEnoughPoints;
            line.positionCount = pointCount;
        }

        if (!hasEnoughPoints)
            return;

        var portPoints = new Vector3[pointCount];
        var starboardPoints = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            portPoints[i] = wakeSamples[i].portPos;
            starboardPoints[i] = wakeSamples[i].starboardPos;
        }
        autoWakeLines[0].SetPositions(portPoints);
        autoWakeLines[1].SetPositions(starboardPoints);
    }

    void UpdateWakeEffects(ShipLog shipLog, float shipLengthWu, float shipBeamWu)
    {
        var isMoving = shipLog != null && shipLog.mapState == MapState.Deployed && Math.Abs(shipLog.speedKnots) >= wakeSpeedThresholdKnots;

        if (wakeGameObjects != null)
        {
            foreach (var go in wakeGameObjects)
            {
                if (go != null)
                    go.SetActive(isMoving);
            }
        }

        if (shipLog == null)
        {
            ClearAutoWakeLines();
            return;
        }

        EnsureAutoWakeLines();
        if (autoWakeLines.Count == 0 || wakePortAnchor == null || wakeStarboardAnchor == null)
            return;

        wakePortAnchor.localPosition = new Vector3(-shipLengthWu * 0.45f, shipBeamWu * 0.22f, 0.0005f);
        wakeStarboardAnchor.localPosition = new Vector3(-shipLengthWu * 0.45f, -shipBeamWu * 0.22f, 0.0005f);

        var wakeWidth = Mathf.Clamp(shipBeamWu * 0.7f, 0.003f, 0.05f);
        foreach (var line in autoWakeLines)
        {
            if (line == null)
                continue;
            line.widthMultiplier = wakeWidth;
        }

        var nowTicks = GetSimulationTicks();
        if (isMoving)
        {
            PushWakeSample(nowTicks, wakePortAnchor.position, wakeStarboardAnchor.position);
        }
        TrimWakeSamples(nowTicks);
        SyncWakeLineRenderers();
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
        t.localScale = Vector3.one * cam.orthographicSize * textScaleFactor;
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

    public bool GetTransparent()
    {
        if(model == null)
            return false;
        if(model is ShipLog shipLog && shipLog.mapState == MapState.Destroyed)
            return true;
        return false;
    }

    public void Update()
    {
        // selectedIndicator
        if (model == null)
            return;

        // TODO: Temp Hack
        var shipLog = model as ShipLog;
        if (shipLog != null && shipLog.mapState == MapState.Deployed && GamePreference.Instance.showDamagePointBar)
        {
            healthBarRenderer.gameObject.SetActive(true);

            var p = Math.Min(1, shipLog.damagePoint / Math.Max(1, shipLog?.shipClass.damagePoint ?? 0));
            healthBarRenderer.material.SetFloat("_FillAmount", 1 - p);
        }
        else
        {
            healthBarRenderer.gameObject.SetActive(false);
        }

        if(shipLog != null)
        {
            // Maintain Sounds (TODO: Move to some Manager Singleton as we don't use 3DSFX here?)
            if(shipLog.firingRounds > 0)
            {
                shipLog.firingRounds = 0; // TODO: Code Smell?

                audioSource.PlayOneShot(gunfireSound);
                // Debug.Log("gunfireSound");
            }
            if(shipLog.firingTorpedos > 0)
            {
                shipLog.firingTorpedos = 0;

                audioSource.PlayOneShot(torpedoFireSound);
                // Debug.Log("torpedoFireSound");
            }
            if(shipLog.startingExplosions > 0)
            {
                shipLog.startingExplosions = 0;

                audioSource.PlayOneShot(explosionSound);
            }

            // Maintain deployed & destroyed state
            foreach(var obj in deployedGameObjects)
            {
                obj.SetActive(shipLog.mapState == MapState.Deployed);
            }
            foreach(var obj in destroyedGameObjects)
            {
                obj.SetActive(shipLog.mapState == MapState.Destroyed);
            }
        }

        selectedIndicator.SetActive(model.objectId == GameManager.Instance.selectedShipLogObjectId);

        var latLon = model.GetPosition();
        transform.localEulerAngles = new Vector3(latLon.LatDeg, -latLon.LonDeg, 0);

        var shipLengthFoot = model?.GetLengthFoot() ?? 300;
        var shipBeamFoot = model?.GetBeamFoot() ?? 60;

        var beamWu = shipBeamFoot * Utils.footToWu * modelScale;
        if(mode == Mode.Icon)
        {
            beamWu *= iconBeamScale;
        }
        var lengthWu = shipLengthFoot * Utils.footToWu * modelScale;

        iconTransform.localScale = new Vector3(
            lengthWu,
            beamWu,
            1
        );
        cubeColliderTransform.localScale = new Vector3(
            shipLengthFoot * Utils.footToWu * 1,
            shipBeamFoot * Utils.footToWu * 1,
            200 * Utils.footToWu
        ); // 100 foots above-waterline height for LOS calculation
        torpedoThreatCubeColliderTransform.localScale = new Vector3(
            400 * Utils.yardsToWu * 1,
            shipBeamFoot * Utils.yardsToWu * 1,
            200 * Utils.footToWu
        );

        var zEuler = Utils.TrueNorthCWDegToRightCCWDeg(model.GetHeadingDeg());
        headingTransform.localEulerAngles = new Vector3(0, 0, zEuler);

        MaintainTextDirectionSize();
        MaintainArrowRotation();

        // text.text = $"{model.GetAcronym()} {model.GetName().GetNameFromType(GameManager.Instance.iconLanuageType)}";
        text.enabled = GamePreference.Instance.showUnitLabel;
        text.text = $"{model.GetAcronym()} {model.GetName().GetNameFromType(GamePreference.Instance.shortLabelLanguageType)}";

        MaintainFlagRotationSize();
        UpdateWakeEffects(shipLog, lengthWu, beamWu);

        var portraitRef = mode switch
        {
            Mode.Icon => model.GetPortraitIconReference(),
            Mode.Counter => model.GetPortraitTopReference(),
            _ => model.GetPortraitTopReference()
        };
        portraitTex = UnityWebRequestImageReader.Instance.FetchTexture2D(portraitRef.ResolvePath());
        countryTex = UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(model.GetCountry()));

        var newViewHashCode = GetViewHashCode();
        if (oldViewHashCode == newViewHashCode)
            return;

        oldViewHashCode = newViewHashCode;

        var isTransparent = GetTransparent();

        flagRenderer.material.SetTexture("_MainTex", countryTex);
        flagRenderer.material.color = isTransparent ? transparentColor : Color.white;

        iconRenderer.material.SetTexture("_MainTex", portraitTex);
        var mainColor = isTransparent ? transparentColor : Color.white;
        iconRenderer.material.SetColor("_MainColor", mainColor);

        text.color = isTransparent ? transparentColor : Color.white;
    }

    // static Color transparentColor = new Color(1, 1, 1, 0.5f);
    static Color transparentColor = new Color(1, 1, 1, 0.25f);
}
