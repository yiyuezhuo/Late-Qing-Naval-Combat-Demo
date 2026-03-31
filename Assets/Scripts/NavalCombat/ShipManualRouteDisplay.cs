using System.Collections.Generic;
using TMPro;
using UnityEngine;
using NavalCombatCore;

public class ShipManualRouteDisplay : MonoBehaviour
{
    const string IconLayerName = "Icon";

    LineRenderer committedLineRenderer;
    LineRenderer previewLineRenderer;
    TMP_Text text;

    void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    public void Render(ShipLog shipLog, PathfindingResult previewResult, string message, Vector3? messageAnchorWorld)
    {
        EnsureVisuals();

        RenderCommittedRoute(shipLog);
        RenderPreviewRoute(previewResult);

        var hasAnyRoute = committedLineRenderer.enabled || previewLineRenderer.enabled;
        if (string.IsNullOrEmpty(message))
        {
            text.enabled = false;
            if (!hasAnyRoute)
                Hide();
            return;
        }

        text.enabled = true;
        text.text = message;
        text.transform.position = ResolveMessageAnchor(shipLog, previewResult, messageAnchorWorld);
    }

    public void Hide()
    {
        if (committedLineRenderer != null)
        {
            committedLineRenderer.enabled = false;
            committedLineRenderer.positionCount = 0;
        }

        if (previewLineRenderer != null)
        {
            previewLineRenderer.enabled = false;
            previewLineRenderer.positionCount = 0;
        }

        if (text != null)
        {
            text.enabled = false;
            text.text = string.Empty;
        }
    }

    void EnsureVisuals()
    {
        var iconLayer = GetIconLayer();
        gameObject.layer = iconLayer;

        if (committedLineRenderer == null)
        {
            committedLineRenderer = CreateLineRenderer("ShipManualRouteCommitted", iconLayer, new Color(0.15f, 0.95f, 0.95f, 1f));
        }

        if (previewLineRenderer == null)
        {
            previewLineRenderer = CreateLineRenderer("ShipManualRoutePreview", iconLayer, new Color(1f, 0.78f, 0.18f, 1f));
        }

        if (text == null)
        {
            var textObject = new GameObject("ShipManualRouteText");
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

    LineRenderer CreateLineRenderer(string objectName, int iconLayer, Color color)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        lineObject.layer = iconLayer;

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = 0.12f;
        lineRenderer.positionCount = 0;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        var fixedSizeLine = lineObject.AddComponent<FixedSizeLine>();
        fixedSizeLine.scaleFactor = 0.01f;

        return lineRenderer;
    }

    static int GetIconLayer()
    {
        var iconLayer = LayerMask.NameToLayer(IconLayerName);
        return iconLayer >= 0 ? iconLayer : 6;
    }

    void RenderCommittedRoute(ShipLog shipLog)
    {
        if (shipLog == null || !shipLog.HasManualRoute())
        {
            committedLineRenderer.enabled = false;
            committedLineRenderer.positionCount = 0;
            return;
        }

        var positions = new List<Vector3>(shipLog.manualRoute.Count + 1)
        {
            Utils.LatLonToVector3(shipLog.position)
        };
        foreach (var waypoint in shipLog.manualRoute)
        {
            if (waypoint != null)
                positions.Add(Utils.LatLonToVector3(waypoint));
        }

        committedLineRenderer.enabled = positions.Count >= 2;
        committedLineRenderer.positionCount = positions.Count;
        committedLineRenderer.SetPositions(positions.ToArray());
    }

    void RenderPreviewRoute(PathfindingResult previewResult)
    {
        if (previewResult == null || !previewResult.success || previewResult.points == null || previewResult.points.Count < 2)
        {
            previewLineRenderer.enabled = false;
            previewLineRenderer.positionCount = 0;
            return;
        }

        var positions = new Vector3[previewResult.points.Count];
        for (var i = 0; i < previewResult.points.Count; i++)
        {
            positions[i] = Utils.LatLonToVector3(previewResult.points[i]);
        }

        previewLineRenderer.enabled = true;
        previewLineRenderer.positionCount = positions.Length;
        previewLineRenderer.SetPositions(positions);
    }

    Vector3 ResolveMessageAnchor(ShipLog shipLog, PathfindingResult previewResult, Vector3? explicitAnchorWorld)
    {
        if (explicitAnchorWorld.HasValue)
            return explicitAnchorWorld.Value;

        if (previewResult != null && previewResult.success && previewResult.points != null && previewResult.points.Count > 0)
            return Utils.LatLonToVector3(previewResult.points[^1]);

        if (shipLog != null && shipLog.HasManualRoute())
            return Utils.LatLonToVector3(shipLog.manualRoute[^1]);

        if (shipLog != null)
            return Utils.LatLonToVector3(shipLog.position);

        return transform.position;
    }
}
