using UnityEngine;
using NavalCombatCore;
// using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using GeographicLib;
using System.Runtime.InteropServices;
using System;


public class LOSLine : SingletonMonoBehaviour<LOSLine>, IMaskCheckService
{
    public enum State
    {
        Idle,
        ChooseStart,
        ChooseEnd,
        Fixed
    }

    public State state = State.Idle;

    public LatLon startLatLon;
    // public Vector3 startPos;
    public LineRenderer lineRenderer;
    public TMP_Text text;
    LayerMask losLayerMask;

    void Awake()
    {
        // lineRenderer = GetComponent<LineRenderer>();
        Hide();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        losLayerMask = LayerMask.GetMask("LOSAndCollide");

        ServiceLocator.Register<IMaskCheckService>(this);
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

    public (MaskCheckResult, Vector3, Vector3) Check2(LatLon src, LatLon dst)
    {
        var raycastStart = Utils.LatLonHeightFootToVector3(src, 100);
        var raycastEnd = Utils.LatLonHeightFootToVector3(dst, 100);
        var raycastDistance = Vector3.Distance(raycastStart, raycastEnd);
        var direction = (raycastEnd - raycastStart).normalized;

        var endPos = raycastEnd;

        var result = new MaskCheckResult();
        if (Physics.Raycast(raycastStart, direction, out RaycastHit hit, raycastDistance, losLayerMask))
        {
            endPos = hit.point;
            var colliderRootProvider = hit.collider.GetComponent<IColliderRootProvider>();
            if (colliderRootProvider != null)
            {
                var root = colliderRootProvider.GetRoot();
                var shipLog = root?.GetComponent<PortraitViewer>()?.model;
                if (shipLog != null)
                {
                    result.message = $"Masked by {shipLog.GetName().GetMergedName()}";
                }
                else
                {
                    result.message = $"Masked by {root.name} (DEBUG)";
                }

                result.isMasked = true;
                result.maskedObject = root;
            }
        }

        return (result, raycastStart, endPos);
    }

    public MaskCheckResult Check(LatLon src, LatLon dst)
    {
        var (maskedResult, startPos, endPos) = Check2(src, dst);
        return maskedResult;
    }

    public MaskCheckResult Check(ShipLog observer, ShipLog target)
    {
        var src = observer.position;
        var dst = target.position;

        var raycastStart = Utils.LatLonHeightFootToVector3(src, 100);
        var raycastEnd = Utils.LatLonHeightFootToVector3(dst, 100);
        var raycastDistance = Vector3.Distance(raycastStart, raycastEnd);
        var direction = (raycastEnd - raycastStart).normalized;

        var result = new MaskCheckResult();
        var raycastHits = Physics.RaycastAll(raycastStart, direction, raycastDistance, losLayerMask);
        foreach (var raycastHit in raycastHits)
        {
            var shipLog = raycastHit.collider.GetComponent<IColliderRootProvider>()?.GetRoot()?.GetComponent<PortraitViewer>()?.model as ShipLog; // TODO: if something other than ShipLog can block LOS, add an interface for here
            if (shipLog != null && shipLog != observer && shipLog != target)
            {
                var obsSize = observer.shipClass.targetSizeModifier;
                var blkSize = shipLog.shipClass.targetSizeModifier;
                var blkTgtDistYards = MeasureStats.Approximation.HaversineDistanceYards(shipLog, target);

                var targetCondition = blkSize >= obsSize;
                var distanceCond = blkTgtDistYards < 2000;

                if (targetCondition || distanceCond)
                {
                    result = new MaskCheckResult
                    {
                        isMasked = true,
                        maskedObject = shipLog,
                        message = $"{observer.objectId} (size={obsSize}) is masked by {shipLog.objectId} (size={blkSize}, to tgt dist={blkTgtDistYards})"
                    };
                }
            }
        }
        return result;
    }

    public CollideCheckResult CollideCheck(ShipLog observer, float testDistanceYards) // In General, detectionDistanceYards = moved distance and length / 2
    {
        if (GameManager.Instance.objectId2Viewer.TryGetValue(observer.objectId, out var portraitViewer))
        {
            var origin = portraitViewer.headingTransform.position;
            var direction = portraitViewer.headingTransform.right; // right = x-axis, which is the true "forward" in the current implementation detail
            var maxDistance = testDistanceYards * Utils.yardsToWu;

            var raycastHits = Physics.RaycastAll(origin, direction, maxDistance, losLayerMask);
            foreach (var raycastHit in raycastHits)
            {
                var collidedPortraitViewer = raycastHit.collider.GetComponent<IColliderRootProvider>()?.GetRoot()?.GetComponent<PortraitViewer>();
                var collidedShipLog = collidedPortraitViewer?.model as ShipLog; // TODO: if something other than ShipLog can block LOS, add an interface for here
                if (collidedShipLog != null && collidedShipLog != observer)
                {
                    var localPoint = collidedPortraitViewer.headingTransform.InverseTransformPoint(raycastHit.point);
                    var collidedVerticalFoot = localPoint.x * Utils.wuToFoot;
                    var hitBeltEnd = Math.Abs(collidedVerticalFoot) > collidedShipLog.GetLengthFoot() / 2 * 0.7;
                    var hitArmorLocation = hitBeltEnd ? ArmorLocation.BeltEnd : ArmorLocation.MainBelt;
                    var impactAngleDeg = Mathf.Abs(Mathf.Atan2(localPoint.x, localPoint.y)) * Mathf.Rad2Deg;
                    return new()
                    {
                        collided = collidedShipLog,
                        collideLocation = hitArmorLocation,
                        impactAngleDeg = impactAngleDeg,
                    };
                }
            }
        }
        return null;
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

                var (maskedResult, startPos, endPos) = Check2(startLatLon, currentLatLon);

                // Check Earth's curvature, use Geodesic distance and visibility table of SK 5 for size 3
                // Earth bacll itself can work as a collider but the mesh is roughtly approximated for the small area we usually consider so we abandon this approach.
                var endLatLon = Utils.Vector3ToLatLon(endPos);
                var inverseLine = Geodesic.WGS84.InverseLine(startLatLon.LatDeg, startLatLon.LonDeg, endLatLon.LatDeg, endLatLon.LonDeg);

                // if (inverseLine.Distance > 0)
                // {
                //     Debug.Log($"{currentLatLon} -> {endLatLon}: {inverseLine.Distance}");
                // }

                if (inverseLine.Distance * MeasureUtils.meterToYard > 32900) // blocked by Earth's curvature (SK5 Table D1, Size 4 and up in best visibility)
                {
                    maskedResult.message = $"Blocked by Earth's curvature";
                    var _endLatLon = inverseLine.Position(32900 * MeasureUtils.yardToMeter);
                    endPos = Utils.LatitudeLongitudeDegToVector3((float)_endLatLon.Latitude, (float)_endLatLon.Longitude);
                }

                var positions = new Vector3[2]{startPos, endPos};
                lineRenderer.positionCount = 2;
                lineRenderer.SetPositions(positions);

                text.text = maskedResult.message ?? "Passed";
                text.transform.position = endPos;

                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Fixed;
                }
                break;

            case State.Fixed:
                break;
        }

        if (!EventSystem.current.IsPointerOverGameObject() && Input.GetKeyDown(KeyCode.S)) // S denotes line of Sight or Sight line
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