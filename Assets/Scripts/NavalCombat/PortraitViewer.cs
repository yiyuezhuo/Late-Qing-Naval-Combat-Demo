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
    enum SfxType
    {
        Gun,
        Torpedo,
        Explosion
    }

    struct SfxLimiterConfig
    {
        public int maxPerFrame;
        public int maxConcurrent;
        public float cooldownSec;
        public float minVolume;
        public float maxVolume;
    }

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
    public ParticleSystem funnelSmokeParticleSystem;
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
    static int sfxLimiterFrame = -1;
    static readonly Dictionary<SfxType, int> sfxCountPerFrame = new();
    static readonly Dictionary<SfxType, float> sfxLastPlayTime = new();
    static readonly Dictionary<SfxType, List<float>> sfxActiveEndTimes = new()
    {
        [SfxType.Gun] = new(),
        [SfxType.Torpedo] = new(),
        [SfxType.Explosion] = new()
    };
    static readonly Dictionary<SfxType, SfxLimiterConfig> sfxLimiterConfigs = new()
    {
        [SfxType.Gun] = new SfxLimiterConfig
        {
            maxPerFrame = 6,
            maxConcurrent = 10,
            cooldownSec = 0.03f,
            minVolume = 0.85f,
            maxVolume = 1f
        },
        [SfxType.Torpedo] = new SfxLimiterConfig
        {
            maxPerFrame = 2,
            maxConcurrent = 3,
            cooldownSec = 0.10f,
            minVolume = 0.90f,
            maxVolume = 1f
        },
        [SfxType.Explosion] = new SfxLimiterConfig
        {
            maxPerFrame = 4,
            maxConcurrent = 6,
            cooldownSec = 0.06f,
            minVolume = 0.90f,
            maxVolume = 1f
        }
    };
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
    GameObject runtimeHullObject;
    MeshFilter runtimeHullMeshFilter;
    MeshRenderer runtimeHullMeshRenderer;
    Material runtimeHullMaterial;
    Texture2D runtimeHullSourceTexture;
    Color runtimeHullBaseTint = Color.white;
    bool runtimeHullRefreshRequested;
    static readonly int mainTexPropertyId = Shader.PropertyToID("_MainTex");
    static readonly int mainColorPropertyId = Shader.PropertyToID("_MainColor");
    static readonly int colorPropertyId = Shader.PropertyToID("_Color");
    const float runtimeHullAlphaThreshold = 0.1f;
    const float runtimeHullTopOffsetWu = 0.0005f;
    const float runtimeHullMinDepthWu = 0.0001f;

    // float initialEmissionRateOverTimeConstant;

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

        // initialEmissionRateOverTimeConstant = funnelSmokeParticleSystem.emission.rateOverTime.constant; // x120 reference
        if (funnelSmokeParticleSystem != null)
            funnelSmokeParticleSystem.Pause();

        GamePreference.Instance.enable3DBaseChanged -= OnEnable3DBaseChanged;
        GamePreference.Instance.enable3DBaseChanged += OnEnable3DBaseChanged;
    }

    void OnDestroy()
    {
        GamePreference.Instance.enable3DBaseChanged -= OnEnable3DBaseChanged;
        DestroyRuntimeHullObject();
        if (runtimeHullMaterial != null)
            Destroy(runtimeHullMaterial);
    }

    void OnEnable3DBaseChanged(object sender, bool enabled)
    {
        if (!enabled)
        {
            runtimeHullRefreshRequested = false;
            DestroyRuntimeHullObject();
            return;
        }

        runtimeHullSourceTexture = null;
        runtimeHullRefreshRequested = true;
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

    void DestroyRuntimeHullObject()
    {
        if (runtimeHullObject != null)
            Destroy(runtimeHullObject);

        runtimeHullObject = null;
        runtimeHullMeshFilter = null;
        runtimeHullMeshRenderer = null;
        runtimeHullSourceTexture = null;
        runtimeHullBaseTint = Color.white;
    }

    void EnsureRuntimeHullObject()
    {
        if (runtimeHullObject != null)
            return;

        runtimeHullObject = new GameObject("RuntimeHullPreview");
        runtimeHullObject.transform.SetParent(headingTransform, false);
        runtimeHullObject.transform.localPosition = new Vector3(0f, 0f, runtimeHullTopOffsetWu);
        runtimeHullObject.transform.localRotation = Quaternion.identity;
        runtimeHullObject.layer = iconRenderer != null ? iconRenderer.gameObject.layer : gameObject.layer;

        runtimeHullMeshFilter = runtimeHullObject.AddComponent<MeshFilter>();
        runtimeHullMeshRenderer = runtimeHullObject.AddComponent<MeshRenderer>();
        runtimeHullMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        runtimeHullMeshRenderer.receiveShadows = false;
        runtimeHullMeshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;

        var baseMaterial = iconRenderer != null
            ? (iconRenderer.sharedMaterial != null ? iconRenderer.sharedMaterial : iconRenderer.material)
            : null;

        runtimeHullMaterial = baseMaterial != null ? new Material(baseMaterial) : null;

        if (runtimeHullMaterial == null)
        {
            var fallbackShader = Shader.Find("Unlit/Color");
            if (fallbackShader != null)
                runtimeHullMaterial = new Material(fallbackShader);
        }

        if (runtimeHullMaterial != null && runtimeHullMaterial.HasProperty(mainTexPropertyId))
            runtimeHullMaterial.SetTexture(mainTexPropertyId, Texture2D.whiteTexture);
        if (runtimeHullMaterial != null)
            runtimeHullMeshRenderer.sharedMaterial = runtimeHullMaterial;

        runtimeHullObject.SetActive(false);
    }

    static void SetMaterialTint(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty(mainColorPropertyId))
            material.SetColor(mainColorPropertyId, color);
        if (material.HasProperty(colorPropertyId))
            material.SetColor(colorPropertyId, color);
    }

    static void SetMaterialMainTexture(Material material, Texture texture)
    {
        if (material == null || !material.HasProperty(mainTexPropertyId))
            return;

        material.SetTexture(mainTexPropertyId, texture);
    }

    void UpdateRuntimeHullPreview(ShipLog shipLog, float lengthWu, float beamWu, Color mainColor)
    {
        if (!GamePreference.Instance.enable3DBase)
        {
            DestroyRuntimeHullObject();
            return;
        }

        if ((shipLog == null || shipLog.shipClass == null || shipLog.IsLandBattery()) || portraitTex == null)
        {
            DestroyRuntimeHullObject();
            return;
        }

        EnsureRuntimeHullObject();
        if (runtimeHullObject == null || runtimeHullMeshFilter == null || runtimeHullMeshRenderer == null)
            return;

        if (runtimeHullSourceTexture != portraitTex)
        {
            if (!PortraitHullRuntimeBuilder.TryGetOrBuildNormalizedHull(portraitTex, out var hullResult, runtimeHullAlphaThreshold))
            {
                DestroyRuntimeHullObject();
                return;
            }

            runtimeHullSourceTexture = portraitTex;
            runtimeHullMeshFilter.sharedMesh = hullResult.mesh;
            runtimeHullBaseTint = hullResult.baseTint;
        }

        var hullMesh = runtimeHullMeshFilter.sharedMesh;
        if (hullMesh == null)
        {
            DestroyRuntimeHullObject();
            return;
        }

        var draftWu = Mathf.Max(shipLog.shipClass.draftFoot * Utils.footToWu * modelScale, runtimeHullMinDepthWu);
        runtimeHullObject.transform.localPosition = new Vector3(0f, 0f, runtimeHullTopOffsetWu);
        runtimeHullObject.transform.localRotation = Quaternion.identity;
        runtimeHullObject.transform.localScale = new Vector3(lengthWu, beamWu, draftWu);

        var hullColor = new Color(
            runtimeHullBaseTint.r * mainColor.r,
            runtimeHullBaseTint.g * mainColor.g,
            runtimeHullBaseTint.b * mainColor.b,
            mainColor.a
        );
        SetMaterialMainTexture(runtimeHullMaterial, Texture2D.whiteTexture);
        SetMaterialTint(runtimeHullMaterial, hullColor);

        runtimeHullObject.SetActive(true);
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

    static void CleanupExpiredActiveVoices(SfxType type)
    {
        var now = Time.unscaledTime;
        var activeEndTimes = sfxActiveEndTimes[type];
        for (int i = activeEndTimes.Count - 1; i >= 0; i--)
        {
            if (activeEndTimes[i] <= now)
                activeEndTimes.RemoveAt(i);
        }
    }

    static bool TryAcquireSfxToken(SfxType type)
    {
        if (Time.frameCount != sfxLimiterFrame)
        {
            sfxLimiterFrame = Time.frameCount;
            sfxCountPerFrame.Clear();
        }

        var config = sfxLimiterConfigs[type];
        CleanupExpiredActiveVoices(type);
        if (sfxActiveEndTimes[type].Count >= config.maxConcurrent)
            return false;

        sfxCountPerFrame.TryGetValue(type, out var count);
        if (count >= config.maxPerFrame)
            return false;

        if (sfxLastPlayTime.TryGetValue(type, out var lastTime))
        {
            if (Time.unscaledTime - lastTime < config.cooldownSec)
                return false;
        }

        sfxCountPerFrame[type] = count + 1;
        sfxLastPlayTime[type] = Time.unscaledTime;
        return true;
    }

    void TryPlaySfx(SfxType type, AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        if (!TryAcquireSfxToken(type))
            return;

        var config = sfxLimiterConfigs[type];
        var volumeScale = UnityEngine.Random.Range(config.minVolume, config.maxVolume);
        audioSource.PlayOneShot(clip, volumeScale);
        sfxActiveEndTimes[type].Add(Time.unscaledTime + clip.length);
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

    static string ResolveLocalizedName(GlobalString name)
    {
        return name?.GetNameFromType(GamePreference.Instance.shortLabelLanguageType) ?? string.Empty;
    }

    bool TryResolveLabelText(out string labelText)
    {
        labelText = string.Empty;

        var displayMode = GamePreference.Instance.unitLabelDisplayMode;
        if (displayMode == GamePreference.UnitLabelDisplayMode.None)
            return false;

        if (displayMode == GamePreference.UnitLabelDisplayMode.Unit)
        {
            var unitName = ResolveLocalizedName(model.GetName());
            labelText = $"{model.GetAcronym()} {unitName}".Trim();
            return true;
        }

        var shipLog = model as ShipLog;
        if (shipLog == null)
            return false;
        if (shipLog.GetEffectiveControlMode() != ControlMode.Independent)
            return false;

        if (shipLog.IsFormationLeadShipInParentGroup())
        {
            var parentGroup = ((IShipGroupMember)shipLog).GetParentGroup();
            var groupName = ResolveLocalizedName(parentGroup?.name);
            if (!string.IsNullOrEmpty(groupName))
            {
                labelText = groupName;
                return true;
            }
        }

        var shipName = ResolveLocalizedName(shipLog.namedShip?.name);
        labelText = string.IsNullOrEmpty(shipName) ? "*" : $"* {shipName}";
        return true;
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

        var isLandBattery = shipLog != null && shipLog.IsLandBattery();

        if(shipLog != null)
        {
            // Maintain Sounds (TODO: Move to some Manager Singleton as we don't use 3DSFX here?)
            if(shipLog.firingRounds > 0)
            {
                shipLog.firingRounds = 0; // TODO: Code Smell?

                TryPlaySfx(SfxType.Gun, gunfireSound);
                // Debug.Log("gunfireSound");
            }
            if(shipLog.firingTorpedos > 0)
            {
                shipLog.firingTorpedos = 0;

                TryPlaySfx(SfxType.Torpedo, torpedoFireSound);
                // Debug.Log("torpedoFireSound");
            }
            if(shipLog.startingExplosions > 0)
            {
                shipLog.startingExplosions = 0;

                TryPlaySfx(SfxType.Explosion, explosionSound);
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

        if (TryResolveLabelText(out var resolvedLabelText))
        {
            text.enabled = true;
            text.text = resolvedLabelText;
        }
        else
        {
            text.enabled = false;
        }

        MaintainFlagRotationSize();
        UpdateWakeEffects(shipLog, lengthWu, beamWu);

        if (isLandBattery)
        {
            if (funnelSmokeParticleSystem != null)
            {
                funnelSmokeParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                funnelSmokeParticleSystem.Pause(withChildren: true);
            }
            shipLog.dirtySeconds = 0;
        }
        else if ((shipLog?.dirtySeconds ?? 0) > 0 && funnelSmokeParticleSystem != null)
        {
            var pendingSeconds = shipLog.dirtySeconds;
            shipLog.dirtySeconds = 0;
            
            funnelSmokeParticleSystem.Simulate(pendingSeconds / 120, withChildren:true, restart: false, fixedTimeStep: true);
        }

        var portraitRef = mode switch
        {
            Mode.Icon => model.GetPortraitIconReference(),
            Mode.Counter => model.GetPortraitTopReference(),
            _ => model.GetPortraitTopReference()
        };
        portraitTex = UnityWebRequestImageReader.Instance.FetchTexture2D(portraitRef.ResolvePath());
        countryTex = UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(model.GetCountry()));

        var newViewHashCode = GetViewHashCode();
        if (oldViewHashCode == newViewHashCode && !runtimeHullRefreshRequested)
            return;

        oldViewHashCode = newViewHashCode;

        var isTransparent = GetTransparent();

        flagRenderer.material.SetTexture("_MainTex", countryTex);
        flagRenderer.material.color = isTransparent ? transparentColor : Color.white;

        iconRenderer.material.SetTexture("_MainTex", portraitTex);
        var mainColor = isTransparent ? transparentColor : Color.white;
        iconRenderer.material.SetColor("_MainColor", mainColor);
        UpdateRuntimeHullPreview(shipLog, lengthWu, beamWu, mainColor);
        runtimeHullRefreshRequested = false;

        text.color = isTransparent ? transparentColor : Color.white;
    }

    // static Color transparentColor = new Color(1, 1, 1, 0.5f);
    static Color transparentColor = new Color(1, 1, 1, 0.25f);
}
