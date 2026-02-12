using UnityEngine;
using System;
using NavalCombatCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;
using UnityEngine.UIElements.Experimental;
using Unity.Properties;
using UnityEngine.Networking;
using System.IO;
using StrategicCombatCore;
using UnityEngine.SceneManagement;

using CoreUtils;

public static class Utils
{
    // public static float r = 2000000;
    // public static float r = 1000000;
    // public static float r = 200000;
    // public static float r = 75000;
    // public static float r = 50000;
    public static float r = 10000; // world unit
    // public static float r = 50000;
    // public static float r = 5000;
    // public static float r = 500; // 50 world unit (wu) = 6371km (earth radius)
    // public static float r = 50;
    public static float earthRadiusKm = 6371;
    public static float wuToKm = earthRadiusKm / r;
    public static float wuToNmi = wuToKm / 1.852f;
    public static float wuToKyd = wuToNmi * 2.025f;
    public static float wuToYards = wuToKyd * 1000;
    public static float wuToFoot = wuToYards * 3;
    public static float footToWu = 1 / wuToFoot;
    public static float yardsToWu = 1 / wuToYards;

    public static Vector3 LatitudeLongitudeDegHeightFootToVector3(float latDeg, float lonDeg, float heightFoot)
    {
        var latRad = latDeg * Mathf.Deg2Rad;
        var lonRad = lonDeg * Mathf.Deg2Rad;

        var _r = r + (heightFoot * footToWu);

        var y = _r * Mathf.Sin(latRad);
        var hr = Mathf.Abs(_r * Mathf.Cos(latRad));
        var x = hr * Mathf.Sin(lonRad);
        var z = hr * -Mathf.Cos(lonRad);

        return new Vector3(x, y, z);
    }

    public static Vector3 LatLonHeightFootToVector3(LatLon latLon, float heightFoot)
    {
        return LatitudeLongitudeDegHeightFootToVector3(latLon.LatDeg, latLon.LonDeg, heightFoot);
    }

    public static Vector3 LatitudeLongitudeDegToVector3(float latDeg, float lonDeg)
    {
        var latRad = latDeg * Mathf.Deg2Rad;
        var lonRad = lonDeg * Mathf.Deg2Rad;

        var y = r * Mathf.Sin(latRad);
        var hr = Mathf.Abs(r * Mathf.Cos(latRad));
        var x = hr * Mathf.Sin(lonRad);
        var z = hr * -Mathf.Cos(lonRad);

        return new Vector3(x, y, z);
    }

    public static Vector3 LatLonToVector3(LatLon latLon)
    {
        return LatitudeLongitudeDegToVector3(latLon.LatDeg, latLon.LonDeg);
    }

    public static float TrueNorthClockwiseDegToUnityDeg(float trueNorthClockwisedeg)
    {
        return 90 - trueNorthClockwisedeg;
    }

    public static float TrueNorthCWDegToRightCCWDeg(float trueNorthCWDeg)
    {
        return 90 - trueNorthCWDeg;
    }

    public static (float latDeg, float lonDeg) Vector3ToLatitudeLongitudeDeg(Vector3 point)
    {
        var x = point.x;
        var y = point.y;
        var z = point.z;

        var hr = Mathf.Sqrt(z * z + x * x);
        var latRad = Mathf.Atan2(y, hr);
        // var lonRad = Mathf.Acos(-z / hr);
        var lonRad = Mathf.Atan2(x, -z);

        var latDeg = latRad * Mathf.Rad2Deg;
        var lonDeg = lonRad * Mathf.Rad2Deg;

        return (latDeg, lonDeg);
    }

    public static LatLon Vector3ToLatLon(Vector3 point)
    {
        var (latDeg, lonDeg) = Vector3ToLatitudeLongitudeDeg(point);
        return new LatLon(latDeg, lonDeg);
    }

    public static Action<IEnumerable<int>> MakeCallbackForItemsAdded<T>(BaseListView listView, Func<object> parentProvider) where T : new()
    {
        return (IEnumerable<int> index) =>
        {
            foreach (var i in index)
            {
                var v = listView.itemsSource[i];
                if (v == null)
                {
                    var obj = new T();
                    listView.itemsSource[i] = obj;

                    if (obj is IObjectIdLabeled labeledObj)
                    {
                        EntityManager.Instance.Register(labeledObj, parentProvider());
                    }
                }
            }
        };
    }

    public static Action<IEnumerable<int>> MakeCallbackForItemsRemoved(BaseListView listView)
    {
        return (IEnumerable<int> index) =>
        {
            foreach (var i in index)
            {
                var v = listView.itemsSource[i];
                if (v is IObjectIdLabeled labeledObj)
                {
                    EntityManager.Instance.Unregister(labeledObj);
                }
            }
        };
    }

    public static void BindItemsAddedRemoved<T>(BaseListView listView, Func<object> parentProvider) where T : new()
    {
        listView.itemsAdded += MakeCallbackForItemsAdded<T>(listView, parentProvider);
        listView.itemsRemoved += MakeCallbackForItemsRemoved(listView);
    }

    public static void BindParentGroupChildrenAddedRemoved<T>(BaseListView listView, Func<object> parentProvider) where T : new()
    {
        // listView.itemsAdded += MakeCallbackForItemsAdded<T>(listView, parentProvider);
        // listView.itemsRemoved += MakeCallbackForItemsRemoved(listView);
        BindItemsAddedRemoved<T>(listView, parentProvider);

        listView.itemsRemoved += (IEnumerable<int> index) =>
        {
            foreach (var i in index)
            {
                var v = listView.itemsSource[i];
                if (v is StrategicGroupMemberReference strategicGroupMemberReference)
                {
                    var obj = strategicGroupMemberReference.Get();
                    if (obj != null)
                        obj.strategicGroupReference.referenceId = null;
                }
            }
        };
    }

    public static void BindMissionMembershipAddedRemoved<T>(BaseListView listView, Func<object> parentProvider) where T : new()
    {
        BindItemsAddedRemoved<T>(listView, parentProvider);
        
        listView.itemsRemoved += (IEnumerable<int> index) =>
        {
            foreach (var i in index)
            {
                var v = listView.itemsSource[i];
                if (v is StrategicGroupMemberReference strategicGroupMemberReference)
                {
                    var obj = strategicGroupMemberReference.Get() as StrategicGroup;
                    if (obj != null)
                        obj.assignedMissionObjectId = null;
                }
            }
        };
    }

    public static void BindItemsSourceRecursive(VisualElement root)
    {
        foreach (var listView in root.Query<BaseListView>().ToList())
        {
            listView.SetBinding("itemsSource", new DataBinding());
        }
    }

    public static void SyncListPairLength<T, T2>(List<T> list1, List<T2> list2, object parent) where T2 : IObjectIdLabeled, new()
    {
        SyncListToLength(list1.Count, list2, parent);
    }

    public static void SyncListToLength<T2>(int expectedLength, List<T2> list2, object parent) where T2 : IObjectIdLabeled, new()
    {
        var addElements = expectedLength - list2.Count;
        var removeElements = list2.Count - expectedLength;
        if (removeElements > 0)
        {
            for (int i = 0; i < removeElements; i++)
            {
                var el = list2[list2.Count - 1];
                EntityManager.Instance.Unregister(el);
                list2.RemoveAt(list2.Count - 1);
            }
        }
        if (addElements > 0)
        {
            for (int i = 0; i < addElements; i++)
            {
                var el = new T2();
                list2.Add(el);
                EntityManager.Instance.Register(el, parent);
            }
        }
    }

    readonly static string linkCursorClassName = "link-cursor"; // a hand icon

    public static void RegisterLinkTag(Label label, Dictionary<string, Action> handlerMap)
    {
        label.RegisterCallback<PointerOverLinkTagEvent>(
            _ => label.AddToClassList(linkCursorClassName)
        );

        label.RegisterCallback<PointerOutLinkTagEvent>(
            _ => label.RemoveFromClassList(linkCursorClassName)
        );

        label.RegisterCallback<PointerUpLinkTagEvent>(evt =>
        {
            var handler = handlerMap.GetValueOrDefault(evt.linkID);
            if (handler != null)
            {
                handler();
            }
            else
            {
                Debug.LogWarning($"No handler found for linkID {evt.linkID}");
            }
        });
    }

    // public static bool TryResolveCurrentValueForBinding<T>(VisualElement el, out T ret)
    // {
    //     var ctx = el.GetHierarchicalDataSourceContext();
    //     return PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ret);
    // }


    public static bool TryResolveCurrentValueForBinding<T>(VisualElement el, out T ret) where T: class
    {
        var ctx = el.GetHierarchicalDataSourceContext();
        
        if(ctx.dataSourcePath.Length == 0)
        {
            ret = ctx.dataSource as T;
            return ret != null;
        }

        return PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ret);
    }

    // public static bool TryResolveCurrentValueForBinding2<T>(VisualElement el, out T ret) where T: class
    // {
    //     var ctx = el.GetHierarchicalDataSourceContext();
    //     if(ctx.dataSourcePath.Length == 0)
    //     {
    //         ret = ctx.dataSource as T;
    //         return ret != null;
    //     }
    //     return PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ret);
    // }

    public static Func<T> MakeDynamicResolveProvider<T>(VisualElement el) where T: class
    {
        return () =>
        {
            var isSucc = TryResolveCurrentValueForBinding(el, out T ret);
            return ret;
        };
    }


    // public static void Test()
    // {
    //     Debug.unityLogger.
    // }

    public static IEnumerator FetchFile(string subPath, Action<string> callback)
    {
        var root = Application.streamingAssetsPath;
        var path = root + "/" + subPath;
        var request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Success: {path}");
            callback(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"failed to fetch and setup: {path}");
        }
    }

    public static void SyncTransformViewerLength(Transform containerTransform, int length, GameObject prefab)
    {
        List<GameObject> childList = new List<GameObject>();

        for (int i = 0; i < containerTransform.childCount; i++)
        {
            childList.Add(containerTransform.GetChild(i).gameObject);
        }

        var diff = length - childList.Count;
        if (diff > 0)
        {
            for (int i = 0; i < diff; i++)
            {
                GameObject.Instantiate(prefab, containerTransform);
            }
        }
        else if (diff < 0)
        {
            for (int i = 0; i < -diff; i++)
            {
                GameObject.Destroy(childList[i]);
            }
        }
    }

    public static string GetCountryPath(Country country)
    {
        return Application.streamingAssetsPath + "/Pictures/Flags/" + country.ToString() + ".png";
    }

    public static bool SceneInBuildSettings(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
                return true;
        }
        return false;
    }

    public struct ArcSegmentDeg
    {
        public float startDeg;
        public float sweepDeg;
    }

    struct ArcIntervalDeg
    {
        public float startDeg;
        public float endDeg;
    }

    static int circlePoints = 72;

    public static void DrawCircleForLineRenderer(LineRenderer lineRenderer, float latDeg, float lonDeg, float rangeM)
    {
        var points = new Vector3[circlePoints + 1];
        for (int i = 0; i <= circlePoints; i++)
        {
            var bearingDeg = 360f * ((float)i / circlePoints);
            var (lat2Deg, lon2Deg) = MeasureStats.Approximation.CalculateNewPosition(latDeg, lonDeg, bearingDeg, rangeM);
            points[i] = LatitudeLongitudeDegHeightFootToVector3((float)lat2Deg, (float)lon2Deg, 100);
        }
        lineRenderer.positionCount = circlePoints + 1;
        lineRenderer.SetPositions(points);
    }

    public static void DrawArcForLineRenderer(LineRenderer lineRenderer, float latDeg, float lonDeg, float rangeM, float startDeg, float sweepDeg)
    {
        var absSweepDeg = Mathf.Abs(sweepDeg);
        if (absSweepDeg >= 359.999f)
        {
            DrawCircleForLineRenderer(lineRenderer, latDeg, lonDeg, rangeM);
            return;
        }

        var pointsCount = Mathf.Max(2, Mathf.CeilToInt(circlePoints * absSweepDeg / 360f) + 1);
        var center = LatitudeLongitudeDegHeightFootToVector3(latDeg, lonDeg, 100);
        var points = new Vector3[pointsCount + 2];
        points[0] = center;
        for (int i = 0; i < pointsCount; i++)
        {
            var t = pointsCount <= 1 ? 0f : (float)i / (pointsCount - 1);
            var bearingDeg = MeasureUtils.NormalizeAngle(startDeg + sweepDeg * t);
            var (lat2Deg, lon2Deg) = MeasureStats.Approximation.CalculateNewPosition(latDeg, lonDeg, bearingDeg, rangeM);
            points[i + 1] = LatitudeLongitudeDegHeightFootToVector3((float)lat2Deg, (float)lon2Deg, 100);
        }
        points[points.Length - 1] = center;

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }

    public static List<ArcSegmentDeg> MergeArcSegments(IEnumerable<ArcSegmentDeg> arcSegments, float minSweepDeg = 0.1f)
    {
        const float fullCircleDeg = 360f;
        const float eps = 0.001f;

        var intervals = new List<ArcIntervalDeg>();
        foreach (var arcSegment in arcSegments)
        {
            var startDeg = MeasureUtils.NormalizeAngle(arcSegment.startDeg);
            var sweepDeg = arcSegment.sweepDeg;
            if (Mathf.Abs(sweepDeg) < minSweepDeg)
                continue;

            if (Mathf.Abs(sweepDeg) >= fullCircleDeg - eps)
            {
                return new List<ArcSegmentDeg> { new() { startDeg = 0f, sweepDeg = fullCircleDeg } };
            }

            if (sweepDeg < 0)
            {
                startDeg = MeasureUtils.NormalizeAngle(startDeg + sweepDeg);
                sweepDeg = -sweepDeg;
            }

            var endDeg = startDeg + sweepDeg;
            if (endDeg <= fullCircleDeg + eps)
            {
                intervals.Add(new ArcIntervalDeg { startDeg = startDeg, endDeg = Mathf.Min(endDeg, fullCircleDeg) });
            }
            else
            {
                intervals.Add(new ArcIntervalDeg { startDeg = startDeg, endDeg = fullCircleDeg });
                intervals.Add(new ArcIntervalDeg { startDeg = 0f, endDeg = endDeg - fullCircleDeg });
            }
        }

        if (intervals.Count == 0)
            return new List<ArcSegmentDeg>();

        intervals.Sort((a, b) => a.startDeg.CompareTo(b.startDeg));
        var mergedIntervals = new List<ArcIntervalDeg> { intervals[0] };
        for (var i = 1; i < intervals.Count; i++)
        {
            var current = intervals[i];
            var last = mergedIntervals[mergedIntervals.Count - 1];
            if (current.startDeg <= last.endDeg + eps)
            {
                last.endDeg = Mathf.Max(last.endDeg, current.endDeg);
                mergedIntervals[mergedIntervals.Count - 1] = last;
            }
            else
            {
                mergedIntervals.Add(current);
            }
        }

        if (mergedIntervals.Count > 1 && mergedIntervals[0].startDeg <= eps && mergedIntervals[mergedIntervals.Count - 1].endDeg >= fullCircleDeg - eps)
        {
            var first = mergedIntervals[0];
            var last = mergedIntervals[mergedIntervals.Count - 1];
            mergedIntervals[0] = new ArcIntervalDeg { startDeg = last.startDeg, endDeg = first.endDeg + fullCircleDeg };
            mergedIntervals.RemoveAt(mergedIntervals.Count - 1);
        }

        return mergedIntervals
            .Select(interval => new ArcSegmentDeg
            {
                startDeg = interval.startDeg,
                sweepDeg = interval.endDeg - interval.startDeg
            })
            .Where(arcSegment => arcSegment.sweepDeg >= minSweepDeg)
            .ToList();
    }

    public static List<ArcSegmentDeg> DistinctArcSegments(IEnumerable<ArcSegmentDeg> arcSegments, float minSweepDeg = 0.1f, float quantizeDeg = 0.01f)
    {
        const float fullCircleDeg = 360f;
        const float eps = 0.001f;

        var normalizedArcSegments = new List<ArcSegmentDeg>();
        foreach (var arcSegment in arcSegments)
        {
            var startDeg = MeasureUtils.NormalizeAngle(arcSegment.startDeg);
            var sweepDeg = arcSegment.sweepDeg;
            if (Mathf.Abs(sweepDeg) < minSweepDeg)
                continue;

            if (Mathf.Abs(sweepDeg) >= fullCircleDeg - eps)
            {
                return new List<ArcSegmentDeg> { new() { startDeg = 0f, sweepDeg = fullCircleDeg } };
            }

            if (sweepDeg < 0)
            {
                startDeg = MeasureUtils.NormalizeAngle(startDeg + sweepDeg);
                sweepDeg = -sweepDeg;
            }

            normalizedArcSegments.Add(new ArcSegmentDeg
            {
                startDeg = startDeg,
                sweepDeg = sweepDeg
            });
        }

        var dedupedArcSegments = new List<ArcSegmentDeg>();
        var seenKeys = new HashSet<string>();
        foreach (var arcSegment in normalizedArcSegments)
        {
            var keyStart = Mathf.RoundToInt(arcSegment.startDeg / quantizeDeg);
            var keySweep = Mathf.RoundToInt(arcSegment.sweepDeg / quantizeDeg);
            var key = $"{keyStart}:{keySweep}";
            if (seenKeys.Add(key))
            {
                dedupedArcSegments.Add(arcSegment);
            }
        }
        return dedupedArcSegments;
    }

    // public static void BindIStrategicGroupMemberReferenceable<T>(VisualElement root, SingletonDocument<T> meDoc) where T : MonoBehaviour
    public static void BindIStrategicGroupMemberReferenceable(VisualElement root)
    {
        var gotoParentButton = root.Q<Button>("GotoParentButton");
        gotoParentButton.clicked += () =>
        {
            if (TryResolveCurrentValueForBinding(gotoParentButton, out IStrategicGroupMemberReferenceable group))
            {
                var parentGroup = group.strategicGroupReference.Get();
                // var idx = StrategicGameState.Instance.strategicGroups.IndexOf(parentGroup);
                // if (parentGroup != null && idx != -1)
                // {
                //     if (!StrategicGroupEditor.Instance.gameObject.activeSelf)
                //     {
                //         meDoc.Hide();
                //         StrategicGroupEditor.Instance.Show();
                //     }
                //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
                // }
                SwitchCenter.Instance.SwitchToStrategicGroupView(parentGroup);
            }
        };

        var currentSourceDepotButton = root.Q<Button>("CurrentSourceDepotButton");
        currentSourceDepotButton.clicked += () =>
        {
            if (TryResolveCurrentValueForBinding(currentSourceDepotButton, out IStrategicGroupMemberReferenceable group))
            {
                var currentSourceDepot = group.GetCurrentSourceDepot();
                // if (currentSourceDepot != null)
                // {
                //     var idx = StrategicGameState.Instance.landUnits.IndexOf(currentSourceDepot);
                //     if (idx != -1)
                //     {
                //         // Hide();
                //         if (!LandUnitEditor.Instance.gameObject.activeSelf)
                //         {
                //             meDoc.Hide();
                //             LandUnitEditor.Instance.Show();
                //         }
                //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                //     }
                // }
                SwitchCenter.Instance.SwitchToLandUnitView(currentSourceDepot);
            }
        };
    }

    // static void GotoReferenceable(IStrategicGroupMemberReferenceable gotoObj, IHidable meDoc)
    // static void GotoReferenceable(IStrategicGroupMemberReferenceable gotoObj)
    // {
    //     if (gotoObj is StrategicGroup group)
    //     {
    //         // var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
    //         // if (group != null && idx != -1)
    //         // {
    //         //     if ((object)meDoc != StrategicGroupEditor.Instance)
    //         //     {
    //         //         meDoc?.Hide();
    //         //         StrategicGroupEditor.Instance.Show();
    //         //     }
    //         //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
    //         // }

    //         SwitchCenter.Instance.SwitchToStrategicGroupView(group);
    //     }
    //     else if (gotoObj is ShipLog shipLog)
    //     {
    //         // var idx = StrategicGameState.Instance.shipLogs.IndexOf(shipLog);
    //         // if (shipLog != null && idx != -1)
    //         // {
    //         //     if ((object)meDoc != ShipLogEditor.Instance)
    //         //     {
    //         //         meDoc?.Hide();
    //         //         ShipLogEditor.Instance.Show();
    //         //     }
    //         //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
    //         // }

    //         SwitchCenter.Instance.SwitchToShipLogView(shipLog);

    //     }
    //     else if (gotoObj is LandUnit landUnit)
    //     {
    //         // var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnit);
    //         // if (landUnit != null && idx != -1)
    //         // {
    //         //     if ((object)meDoc != LandUnitEditor.Instance)
    //         //     {
    //         //         meDoc?.Hide();
    //         //         LandUnitEditor.Instance.Show();
    //         //     }
    //         //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
    //         // }

    //         SwitchCenter.Instance.SwitchToLandUnitView(landUnit);
    //     }
    // }

    // public static void BindGotoButton(VisualElement item, IHidable meDoc)
    public static void BindGotoButton(VisualElement item)
    {
        var gotoButton = item.Q<Button>("GotoButton");
        gotoButton.clicked += () =>
        {
            if (TryResolveCurrentValueForBinding(gotoButton, out StrategicGroupMemberReference fieldReference))
            {
                Debug.Log("reference GotoButton clicked");

                var gotoObj = fieldReference.Get();
                
                // GotoReferenceable(gotoObj);
                SwitchCenter.Instance.SwitchByIStrategicGroupMemberReferenceable(gotoObj);
            }
        };
    }

    // public static void BindStrategicGroupMemberReferenceListView(ListView subordinatesCombinedListView, VisualElement contentContainer, IHidable meDoc)
    public static void BindStrategicGroupMemberReferenceListView(ListView subordinatesCombinedListView, VisualElement contentContainer)
    {
        // BindItemsAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);
        BindParentGroupChildrenAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);

        subordinatesCombinedListView.makeItem = () =>
        {
            var item = subordinatesCombinedListView.itemTemplate.CloneTree();
            // BindStrategicGroupMemberReference(item);

            var setButton = item.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (TryResolveCurrentValueForBinding(contentContainer, out StrategicGroup selectedStrategicGroup) &&
                    TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
                {
                    Debug.Log("reference SetButton clicked");

                    DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
                    {
                        var oldObj = fieldReference.Get();
                        var selectedReferenceable = selectedReferenceables.FirstOrDefault();

                        if(selectedStrategicGroup.objectId == selectedReferenceable.objectId) // Prevent Looping, currently this will compromise UITK update?
                        {
                            return;
                        }

                        if (oldObj != null)
                        {
                            // oldObj.SetStrategicGroupReference(null);
                            oldObj.strategicGroupReference.referenceId = null;
                        }

                        if (selectedReferenceable != null && selectedStrategicGroup != null)
                        {
                            selectedReferenceable.SetStrategicGroupReference(null);
                            fieldReference.referenceId = selectedReferenceable.objectId;
                            selectedReferenceable.strategicGroupReference.referenceId = selectedStrategicGroup.objectId;
                        }
                    }, SubordinatePickerDialog.Mode.ParentUnassignedMember);
                }
            };

            // BindGotoButton(item, meDoc);
            BindGotoButton(item);

            return item;
        };
    }

    public static void BindMissionMembership(ListView subordinatesCombinedListView, VisualElement contentContainer, IHidable meDoc)
    {
        // BindItemsAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);
        BindMissionMembershipAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);

        subordinatesCombinedListView.makeItem = () =>
        {
            var item = subordinatesCombinedListView.itemTemplate.CloneTree();
            // BindStrategicGroupMemberReference(item);

            var setButton = item.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (TryResolveCurrentValueForBinding(contentContainer, out StrategicMission selectedMission) &&
                    TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
                {
                    Debug.Log("reference SetButton clicked");

                    // var pickerMode = selectedMission.type switch
                    // {
                    //     StrategicMission.MissionType.NavalTransfer => SubordinatePickerDialog.Mode.MissionUnassignedGroup,
                    //     _ => SubordinatePickerDialog.Mode.MissionUnassignedFleetGroup
                    // };

                    var pickerMode = selectedMission.IsNavyOnly() ?  SubordinatePickerDialog.Mode.MissionUnassignedFleetGroup : SubordinatePickerDialog.Mode.MissionUnassignedGroup;

                    DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
                    {
                        var oldObj = fieldReference.Get() as StrategicGroup;
                        if (oldObj != null)
                        {
                            // oldObj.SetStrategicGroupReference(null);
                            oldObj.assignedMissionObjectId = null;
                        }

                        var dialogSelectedStrategicGroup = selectedReferenceables.FirstOrDefault() as StrategicGroup;

                        if (dialogSelectedStrategicGroup != null && selectedMission != null)
                        {
                            dialogSelectedStrategicGroup.SetAssignedMission(null);
                            fieldReference.referenceId = dialogSelectedStrategicGroup.objectId;
                            dialogSelectedStrategicGroup.assignedMissionObjectId = selectedMission.objectId;
                        }
                    }, pickerMode);
                    // }, SubordinatePickerDialog.Mode.MissionUnassignedFleetGroup);
                }
            };

            // BindGotoButton(item, meDoc);
            BindGotoButton(item);

            return item;
        };
    }

    public static void LayoutStackTransform(List<Transform> transforms, Vector3 basePos, float stackSpace)
    {
        var count = transforms.Count;
        if (count == 1)
        {
            transforms[0].position = basePos;
            return;
        }
        var step = stackSpace / (count - 1);
        for (int i = 0; i < count; i++)
        {
            var delta = -stackSpace / 2 + i * step;
            // transforms[i].position = basePos + new Vector3(delta, delta, 0);
            var z = -(i * step); // negative z is closer to camera
            transforms[i].position = basePos + new Vector3(delta, delta, z);
        }
    }

    public static void DestroyChildrensFor(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    public static Vector3[] XYListToVector3Array(List<XY> pathCells)
    {
        return pathCells.Select(xy =>
        {
            var posZ = -0.1f;
            var cell = xy.GetCell();
            if(cell.IsGridCell())
            {
                var (xf, yf) = HexMapShower.CellXYToLocalXY(xy.x, xy.y);
                var pos = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
                return new Vector3(pos.x, pos.y, posZ);
            }
            else
            {
                var hitArea = StrategicGameManager.Instance.areaCellObjectIdToHitArea[cell.objectId];
                return new Vector3(hitArea.transform.position.x, hitArea.transform.position.y, posZ);
            }
        }).ToArray();
    }

    // public static class UIInputGuard
    // {
    //     public static bool IsTyping()
    //     {
    // #if UNITY_UI_TOOLKIT
    //         var panel = UnityEngine.UIElements.PanelSettings.current;
    //         var focused = panel?.focusController?.focusedElement;
    //         return focused is UnityEngine.UIElements.ITextInputField;
    // #else
    //         var go = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
    //         return go != null &&
    //             (go.GetComponent<TMPro.TMP_InputField>() != null ||
    //                 go.GetComponent<UnityEngine.UI.InputField>() != null);
    // #endif
    //     }

    //     public static bool UITKCheck()
    //     {
    //         var panel = UnityEngine.UIElements.PanelSettings.current;
    //         var focused = panel?.focusController?.focusedElement;
    //         return focused is UnityEngine.UIElements.ITextInputField;
    //     }

    //     public static bool UGUICheck()
    //     {
    //         var go = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
    //         return go != null &&
    //             (go.GetComponent<TMPro.TMP_InputField>() != null ||
    //                 go.GetComponent<UnityEngine.UI.InputField>() != null);
    //     }
    // }
}
