using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;


public class HighCommandEditor : HideableDocument<HighCommandEditor>
{
    public DepartmentPosition selectedDepartment;

    void OnEnable()
    {
        root.dataSource = StrategicGameManager.Instance;

        var leaderBackgrounds = root.Query<VisualElement>("LeaderBackground").ToList();
        foreach (var leaderBackground in leaderBackgrounds)
        {
            leaderBackground.RegisterCallback<ClickEvent>(evt =>
            {
                Debug.Log($"leaderBackground clicked: {leaderBackground}");
                if (Utils.TryResolveCurrentValueForBinding(leaderBackground, out DepartmentPosition departmentPosition))
                {
                    Debug.Log($"departmentPosition clicked: {departmentPosition}");
                    selectedDepartment = departmentPosition;

                    DialogRoot.Instance.PopupLeaderSelectorDialogForCallback(LeaderSelectorDialogCallback);
                }
            });
        }

        root.Q<Button>("ConfirmButton").clicked += Hide;
    }

    void LeaderSelectorDialogCallback(Leader leader)
    {
        Debug.Log($"selected leader: {leader}");
        if (leader != null && selectedDepartment != null)
        {
            selectedDepartment.objectId = leader.objectId;
        }
    }
}