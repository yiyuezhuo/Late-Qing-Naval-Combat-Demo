using System.Collections.Generic;
using NavalCombatCore;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class NavalLocationLabelOverlay : MonoBehaviour
{
    const float MarkerDiameterYards = 100f;
    const float MarkerRadiusFoot = 150f;
    const float TextHeightFoot = 550f;
    const string IconLayerName = "Icon";

    class LocationLabelView
    {
        public Transform markerTransform;
        public TMP_Text text;
    }

    readonly List<LocationLabelView> views = new();

    Transform rootTransform;
    Transform markerRoot;
    Transform textRoot;
    TMP_FontAsset portraitLabelFont;
    Material portraitLabelSharedMaterial;
    float portraitLabelFontSize = 36f;
    TextAlignmentOptions portraitLabelAlignment = TextAlignmentOptions.Center;

    void LateUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.earthTransform == null)
            return;

        EnsureRoots();

        var labels = NavalGameState.Instance?.scenarioState?.locationLabels;
        if (labels == null)
        {
            SyncViewCount(0);
            return;
        }

        SyncViewCount(labels.Count);

        var markerScale = Vector3.one * (MarkerDiameterYards * Utils.yardsToWu);
        var cam = CameraController2.Instance?.cam;
        for (int i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            var view = views[i];
            if (label == null)
            {
                view.markerTransform.gameObject.SetActive(false);
                view.text.gameObject.SetActive(false);
                continue;
            }

            view.markerTransform.gameObject.SetActive(true);
            view.text.gameObject.SetActive(true);

            var markerPosition = Utils.LatitudeLongitudeDegHeightFootToVector3(label.latitude, label.longitude, MarkerRadiusFoot);
            var textPosition = Utils.LatitudeLongitudeDegHeightFootToVector3(label.latitude, label.longitude, TextHeightFoot);

            view.markerTransform.localPosition = markerPosition;
            view.markerTransform.localScale = markerScale;
            view.text.transform.localPosition = textPosition;
            view.text.text = label?.name?.GetShortName() ?? string.Empty;

            if (cam != null)
            {
                var t = view.text.transform;
                t.LookAt(t.position + cam.transform.rotation * Vector3.forward,
                    cam.transform.rotation * Vector3.up);
                t.localScale = Vector3.one * cam.orthographicSize * PortraitViewer.textScaleFactor;
            }
        }
    }

    void EnsureRoots()
    {
        if (rootTransform != null)
            return;

        rootTransform = new GameObject("NavalLocationLabels").transform;
        rootTransform.SetParent(GameManager.Instance.earthTransform, false);
        rootTransform.gameObject.layer = LayerMask.NameToLayer(IconLayerName);

        markerRoot = new GameObject("Markers").transform;
        markerRoot.SetParent(rootTransform, false);
        markerRoot.gameObject.layer = LayerMask.NameToLayer(IconLayerName);

        textRoot = new GameObject("Texts").transform;
        textRoot.SetParent(rootTransform, false);
        textRoot.gameObject.layer = LayerMask.NameToLayer(IconLayerName);
    }

    void SyncViewCount(int expectedCount)
    {
        while (views.Count < expectedCount)
        {
            views.Add(CreateView());
        }

        while (views.Count > expectedCount)
        {
            var view = views[views.Count - 1];
            if (view.markerTransform != null)
                Destroy(view.markerTransform.gameObject);
            if (view.text != null)
                Destroy(view.text.gameObject);
            views.RemoveAt(views.Count - 1);
        }
    }

    LocationLabelView CreateView()
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "LocationLabelMarker";
        marker.transform.SetParent(markerRoot, false);
        marker.layer = LayerMask.NameToLayer(IconLayerName);

        var collider = marker.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var markerRenderer = marker.GetComponent<MeshRenderer>();
        markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
        markerRenderer.material.color = Color.black;

        var textObject = new GameObject("LocationLabelText");
        textObject.transform.SetParent(textRoot, false);
        textObject.layer = LayerMask.NameToLayer(IconLayerName);
        var text = textObject.AddComponent<TextMeshPro>();
        ApplyPortraitLabelStyle(text);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(18f, 3f);

        return new LocationLabelView
        {
            markerTransform = marker.transform,
            text = text
        };
    }

    void ApplyPortraitLabelStyle(TextMeshPro text)
    {
        CachePortraitLabelStyle();

        if (portraitLabelFont != null)
            text.font = portraitLabelFont;
        if (portraitLabelSharedMaterial != null)
            text.fontSharedMaterial = portraitLabelSharedMaterial;

        text.fontSize = portraitLabelFontSize;
        text.alignment = portraitLabelAlignment;
    }

    void CachePortraitLabelStyle()
    {
        if (portraitLabelFont != null)
            return;

        var portraitViewer = GameManager.Instance?.shipUnitPrefab?.GetComponent<PortraitViewer>();
        var portraitText = portraitViewer?.text;
        if (portraitText == null)
            return;

        portraitLabelFont = portraitText.font;
        portraitLabelSharedMaterial = portraitText.fontSharedMaterial;
        portraitLabelFontSize = portraitText.fontSize;
        portraitLabelAlignment = portraitText.alignment;
    }
}
