using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class BehaviourUtils : SingletonMonoBehaviour<BehaviourUtils>
{
    public void ScheduleToSetSelectionForListView(ListView listView, int idx)
    {
        StartCoroutine(Utils.SetSelectionForListViewNextFrame(listView, idx));
    }

    public IEnumerator StartAndWaitAll(IEnumerable<IEnumerator> enumerators)
    {
        var coroutines = enumerators.Select(it => StartCoroutine(it));
        foreach (var cor in coroutines)
            yield return cor;
    }
}