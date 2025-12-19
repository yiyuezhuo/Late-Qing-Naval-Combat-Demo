using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System;
using System.IO;
using CoreUtils;
using StrategicCombatCore;


[CustomEditor(typeof(HitArea))]
public class HitAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var me = (HitArea)target;

        EditorGUILayout.Space();

        if(GUILayout.Button("Assign Distinct GUID to every HitArea in the scene"))
        {
            var hitAreas = FindObjectsByType<HitArea>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ).ToList();
            Debug.Log($"hitAreas.Count={hitAreas.Count}");

            foreach(var hitArea in hitAreas)
            {
                if(hitArea.hitAreaObjectId == null || hitArea.hitAreaObjectId == "")
                {
                    var guid = System.Guid.NewGuid().ToString();
                    hitArea.hitAreaObjectId = guid;
                    Debug.Log($"Assign guid {guid} to {hitArea.gameObject.name}");
                    EditorUtility.SetDirty(hitArea);
                }
            }
        }
    }
}