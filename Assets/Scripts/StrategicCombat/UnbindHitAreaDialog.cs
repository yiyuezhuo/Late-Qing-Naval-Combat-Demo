using CoreUtils;
using StrategicCombatCore;
using UnityEngine.UIElements;
using UnityEngine;

class UnbindHitAreaDialog
{
    public HitArea currentHitArea;
    public string testGuid;

    public void OnCreated(object sender, VisualElement el)
    {
        testGuid = EntityManager.Instance.GetDistinctGuid();
    }

    public void OnConfirm(object sender, VisualElement el)
    {
        // Create a Area Cell, assign a distinct id and create a binding relationship in the View state (StrategicGameManager)
        var areaCell = new Cell()
        {
            x = -1,
            y = -1,
            objectId = testGuid
        };
        EntityManager.Instance.Register(areaCell, null);
        StrategicGameState.Instance.areaCells.Add(areaCell);

        currentHitArea.areaCellObjectId = areaCell.objectId;
        Debug.Log($"Hit Area Map: {currentHitArea.hitAreaObjectId} -> {currentHitArea.areaCellObjectId}");
    }
}