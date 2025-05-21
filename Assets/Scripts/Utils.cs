using UnityEngine;
using System;
using NavalCombatCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;

public static class Utils
{
    public static float r = 50;

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
}