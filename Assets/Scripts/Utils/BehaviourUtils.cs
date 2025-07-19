using UnityEngine;
using UnityEngine.UIElements;

public class BehaviourUtils : SingletonMonoBehaviour<BehaviourUtils>
{
    public void ScheduleToSetSelectionForListView(ListView listView, int idx)
    {
        StartCoroutine(Utils.SetSelectionForListView(listView, idx));
    }
}