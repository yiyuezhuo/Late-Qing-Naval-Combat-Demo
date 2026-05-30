using System;
using CoreUtils;
using NavalCombatCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YYZ;

public class ExactPathfindingLine : SingletonMonoBehaviour<ExactPathfindingLine>
{
    public enum State
    {
        Idle,
        ChooseStart,
        ChooseEnd,
        Fixed
    }

    const string IconLayerName = "Icon";

    public State state = State.Idle;
    public LineRenderer lineRenderer;
    public TMP_Text text;

    LatLon startLatLon;
    Vector3 startPosition;
    ExactROIShoreFieldPathfinder pathfinder;
    ElevationProvider elevationProvider;
    PathfindingResult currentResult;
    int lastPreviewIndex = -2;
    float lastPreviewThreshold = float.NaN;

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    void Awake()
    {
        gameObject.layer = GetIconLayer();
        EnsureVisuals();
        Hide();
    }

    public void BeginChooseStart()
    {
        EnsureVisuals();
        elevationProvider = ElevationService.Instance.elevationProvider as ElevationProvider;
        pathfinder = null;
        state = State.ChooseStart;
        currentResult = null;
        lastPreviewIndex = -2;
        lastPreviewThreshold = float.NaN;
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        ShowText(Localize("Exact Pathfinding: choose source"), TryGetCurrentHitPoint(out var hitPoint) ? hitPoint : transform.position);
    }

    public void Cancel()
    {
        state = State.Idle;
        ClearAndHide();
    }

    void EnsureVisuals()
    {
        var iconLayer = GetIconLayer();
        gameObject.layer = iconLayer;

        if (lineRenderer == null)
        {
            var lineObject = new GameObject("ExactPathfindingRoute");
            lineObject.transform.SetParent(transform, false);
            lineObject.layer = iconLayer;

            lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.useWorldSpace = true;
            lineRenderer.widthMultiplier = 0.1f;
            lineRenderer.positionCount = 0;
            lineRenderer.startColor = new Color(1f, 0.7f, 0.1f, 1f);
            lineRenderer.endColor = new Color(1f, 0.7f, 0.1f, 1f);

            var fixedSizeLine = lineObject.AddComponent<FixedSizeLine>();
            fixedSizeLine.scaleFactor = 0.01f;
        }
        else
        {
            lineRenderer.gameObject.layer = iconLayer;
        }

        if (text == null)
        {
            var textObject = new GameObject("ExactPathfindingText");
            textObject.transform.SetParent(transform, false);
            textObject.layer = iconLayer;

            var worldText = textObject.AddComponent<TextMeshPro>();
            worldText.font = TMP_Settings.defaultFontAsset;
            worldText.fontSize = 36f;
            worldText.alignment = TextAlignmentOptions.Center;
            worldText.textWrappingMode = TextWrappingModes.NoWrap;
            worldText.color = Color.white;
            worldText.text = string.Empty;

            var fixedDirectionalSizeIcon = textObject.AddComponent<FixedDirectionalSizeIcon>();
            fixedDirectionalSizeIcon.scaleFactor = 0.025f;

            text = worldText;
        }
        else
        {
            text.gameObject.layer = iconLayer;
        }
    }

    static int GetIconLayer()
    {
        var iconLayer = LayerMask.NameToLayer(IconLayerName);
        return iconLayer >= 0 ? iconLayer : 6;
    }

    void ShowText(string message, Vector3 position)
    {
        text.enabled = true;
        text.text = message;
        text.transform.position = position;
    }

    void Hide()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
        }

        if (text != null)
        {
            text.enabled = false;
        }
    }

    void ClearAndHide()
    {
        currentResult = null;
        lastPreviewIndex = -2;
        lastPreviewThreshold = float.NaN;
        Hide();
    }

    bool TryGetCurrentHitPoint(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        var controller = CameraController2.Instance;
        if (controller == null)
        {
            return false;
        }

        hitPoint = controller.GetHitPoint();
        return hitPoint != Vector3.zero;
    }

    bool TryGetCurrentLatLon(out Vector3 hitPoint, out LatLon latLon)
    {
        hitPoint = Vector3.zero;
        latLon = default;
        if (!TryGetCurrentHitPoint(out hitPoint))
        {
            return false;
        }

        latLon = Utils.Vector3ToLatLon(hitPoint);
        return true;
    }

    string GetFailureMessage(PathfindingFailureReason failureReason)
    {
        return failureReason switch
        {
            PathfindingFailureReason.ShoreFieldUnavailable => Localize("Pathfinding: shore field unavailable"),
            PathfindingFailureReason.OutsideROI => Localize("Pathfinding: outside ROI"),
            PathfindingFailureReason.SearchWindowExceeded => Localize("Pathfinding: outside exact search window"),
            PathfindingFailureReason.SourceBlocked => Localize("Pathfinding: source blocked"),
            PathfindingFailureReason.DestinationBlocked => Localize("Pathfinding: destination blocked"),
            PathfindingFailureReason.NoPath => Localize("Pathfinding: no path"),
            _ => string.Empty
        };
    }

    void RenderResult(PathfindingResult result, Vector3 anchorPosition)
    {
        EnsureVisuals();
        if (result == null || !result.success || result.points == null || result.points.Count == 0)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
            ShowText(GetFailureMessage(result?.failureReason ?? PathfindingFailureReason.NoPath), anchorPosition);
            return;
        }

        var positions = new Vector3[result.points.Count];
        for (var i = 0; i < result.points.Count; i++)
        {
            positions[i] = Utils.LatLonToVector3(result.points[i]);
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);

        var distanceNm = result.routedDistanceMeters / MeasureUtils.navalMileToMeter;
        ShowText(Localize("Exact Pathfinding: routed distance {0:0.00} nm", distanceNm), positions[positions.Length - 1]);
    }

    void UpdateChooseStart()
    {
        if (!TryGetCurrentHitPoint(out var hitPoint))
        {
            ShowText(Localize("Exact Pathfinding: choose source"), transform.position);
            return;
        }

        ShowText(Localize("Exact Pathfinding: choose source"), hitPoint);
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (!TryGetCurrentLatLon(out hitPoint, out var latLon))
        {
            return;
        }

        elevationProvider = ElevationService.Instance.elevationProvider as ElevationProvider;
        pathfinder = elevationProvider != null ? new ExactROIShoreFieldPathfinder(elevationProvider, latLon) : null;
        var result = pathfinder?.FindPath(latLon, latLon, GamePreference.Instance.pathfindingShorePassableDistancePixels);
        if (result == null)
        {
            ShowText(GetFailureMessage(PathfindingFailureReason.ShoreFieldUnavailable), hitPoint);
            return;
        }

        if (!result.success)
        {
            RenderResult(result, hitPoint);
            return;
        }

        startLatLon = latLon;
        startPosition = hitPoint;
        state = State.ChooseEnd;
        currentResult = null;
        lastPreviewIndex = -2;
        lastPreviewThreshold = float.NaN;
        lineRenderer.enabled = false;
        lineRenderer.positionCount = 0;
        ShowText(Localize("Exact Pathfinding: choose destination"), hitPoint);
    }

    void UpdateChooseEnd()
    {
        if (!TryGetCurrentLatLon(out var hitPoint, out var currentLatLon))
        {
            ShowText(Localize("Exact Pathfinding: choose destination"), startPosition);
            return;
        }

        var threshold = GamePreference.Instance.pathfindingShorePassableDistancePixels;
        var previewIndex = -1;
        pathfinder?.TryGetWindowNodeIndex(currentLatLon, out previewIndex);

        if (pathfinder == null || previewIndex != lastPreviewIndex || Math.Abs(lastPreviewThreshold - threshold) > 1e-5f)
        {
            currentResult = pathfinder?.FindPath(startLatLon, currentLatLon, threshold);
            lastPreviewIndex = previewIndex;
            lastPreviewThreshold = threshold;
        }

        RenderResult(currentResult, hitPoint);
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        currentResult = pathfinder?.FindPath(startLatLon, currentLatLon, threshold);
        if (currentResult == null || !currentResult.success)
        {
            RenderResult(currentResult, hitPoint);
            return;
        }

        RenderResult(currentResult, hitPoint);
        state = State.Fixed;
    }

    void Update()
    {
        switch (state)
        {
            case State.ChooseStart:
                UpdateChooseStart();
                break;
            case State.ChooseEnd:
                UpdateChooseEnd();
                break;
            case State.Fixed:
                break;
        }

        var isPressingAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (!EventSystem.current.IsPointerOverGameObject() && Input.GetKeyDown(KeyCode.P) && isPressingAlt)
        {
            BeginChooseStart();
        }

    }
}
