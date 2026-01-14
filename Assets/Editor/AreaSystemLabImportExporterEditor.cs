using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System;
using System.IO;
using CoreUtils;
using StrategicCombatCore;
using YYZ;


[CustomEditor(typeof(AreaSystemImportExporter), true)]
public class AreaSystemImportExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var me = (AreaSystemImportExporter)target;

        EditorGUILayout.Space();



        if(GUILayout.Button("Set Background"))
        {
            var path = EditorUtility.OpenFilePanel("Select background in streaming asset", Application.streamingAssetsPath, "");
            path = path.Replace("\\", "/");
            me.path = Path.GetRelativePath(Application.streamingAssetsPath, path);
            
            me.isBuiltin = true;
            // me.path = path.Replace(Application.streamingAssetsPath, "").Substring(1); // Path
            EditorUtility.SetDirty(me);
        }

        if (GUILayout.Button("Export"))
        {
            var xml = me.ToXml();

            string defaultName = "AreaSystem.xml";
            string defaultFolder = "Assets";

            string path = EditorUtility.SaveFilePanel(
                "Save AreaSystem",
                defaultFolder,
                defaultName,
                "xml");
            File.WriteAllText(path, xml);
        }

        if(GUILayout.Button("Import"))
        {
            var path = EditorUtility.OpenFilePanel("Import", Application.streamingAssetsPath, "xml");
            var xml = File.ReadAllText(path);
            FromXml(me, xml);
        }

    }

    public void FromXml(AreaSystemImportExporter me, string xml)
    {
        var areaSystem = XmlUtils.FromXML<AreaSystem>(xml);
        
        me.isBuiltin = areaSystem.backgroundReference.isBuiltin;
        me.path = areaSystem.backgroundReference.path;

        EditorUtility.SetDirty(me);

        // var children = areasTransform.GetComponentInChildren<Transform>();
        // foreach(Transform t in children)
        // {
        //     Object.DestroyImmediate(t.gameObject);
        // }
        for (int i = me.areasTransform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(me.areasTransform.GetChild(i).gameObject);
        }

        foreach(var areaState in areaSystem.areaStates)
        {
            var newObj = PrefabUtility.InstantiatePrefab(me.hitAreaPrefab, me.areasTransform) as GameObject;
            newObj.transform.localPosition = new Vector3(areaState.posX, areaState.posY, 0);
            newObj.transform.localScale = new Vector3(areaState.scaleX, areaState.scaleY, 1);
            newObj.name = areaState.name;
        }
    }

}
