using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class BehaviourUtils : SingletonMonoBehaviour<BehaviourUtils>
{
    public void ScheduleToSetSelectionForListView(ListView listView, int idx)
    {
        StartCoroutine(SetSelectionForListViewNextFrame(listView, idx));
    }
    

    public void ScheduleToSetSelectionForListView(ListView listView, Func<int> idxProvider)
    {
        StartCoroutine(SetSelectionForListViewNextFrame(listView, idxProvider));
    }

    public static IEnumerator SetSelectionForListViewNextFrame(ListView listView, int idx)
    {
        // yield return new WaitForNextFrameUnit();
        yield return null;
        SetSelectionForListView(listView, idx);
    }

    public static IEnumerator SetSelectionForListViewNextFrame(ListView listView, Func<int> idxProvider)
    {
        // yield return new WaitForNextFrameUnit();
        yield return null;
        var idx = idxProvider(); // TODO: Handle -1?
        SetSelectionForListView(listView, idx);
    }

    public static void SetSelectionForListView(ListView listView, int idx)
    {
        listView.SetSelection(idx);
        listView.ScrollToItem(idx);
    }

    public IEnumerator StartAndWaitAll(IEnumerable<IEnumerator> enumerators)
    {
        var coroutines = enumerators.Select(it => StartCoroutine(it));
        foreach (var cor in coroutines)
            yield return cor;
    }
}