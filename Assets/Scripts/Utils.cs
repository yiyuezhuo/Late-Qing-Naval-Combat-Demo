using UnityEngine;
using System;
using NavalCombatCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;
using UnityEngine.UIElements.Experimental;
using Unity.Properties;

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

    public static NavalCombatCore.LatLon Vector3ToLatLon(Vector3 point)
    {
        var (latDeg, lonDeg) = Vector3ToLatitudeLongitudeDeg(point);
        return new NavalCombatCore.LatLon(latDeg, lonDeg);
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

    public static bool TryResolveCurrentValueForBinding<T>(VisualElement el, out T ret)
    {
        var ctx = el.GetHierarchicalDataSourceContext();
        return PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ret);
    }

    public static Func<T> MakeDynamicResolveProvider<T>(VisualElement el)
    {
        return () =>
        {
            var isSucc = TryResolveCurrentValueForBinding(el, out T ret);
            return ret;
        };
    }

    public static IEnumerator SetSelectionForListView(ListView listView, int idx)
    {
        // yield return new WaitForNextFrameUnit();
        yield return null;
        listView.SetSelection(idx);
        listView.ScrollToItem(idx);
    }

    // public static void Test()
    // {
    //     Debug.unityLogger.
    // }

}