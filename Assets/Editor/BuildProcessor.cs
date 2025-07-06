using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

using CoreUtils;
using NavalCombatCore;


public class BuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 100; } }


    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("Preprocess build started for: " + report.summary.platform);
        BuildManifest();

        Debug.Log("Checking MultiColumnListView integrity...");
        CheckMultiColumnListViewBlockOnly();
    }

    public void CheckMultiColumnListViewBlockOnly()
    {
        string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
        foreach (var uxmlGuid in uxmlGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(uxmlGuid);

            if (!assetPath.StartsWith("Assets/"))
                continue;

            VisualTreeAsset vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);

            var el = vta.CloneTree();
            var multiColumnListViews = el.Query<MultiColumnListView>().ToList();
            if (multiColumnListViews.Count > 0)
            {
                foreach (var mclv in multiColumnListViews)
                {
                    foreach (var col in mclv.columns)
                    {
                        if (col.cellTemplate == null)
                        {
                            throw new BuildFailedException($"cellTemplate missing: col.title={col.title}, mclv.name={mclv.name}, vta.name={vta.name} (assetPath={assetPath}, uxmlGuid={uxmlGuid})");
                        }
                    }
                }
            }
        }
    }

    [MenuItem("Custom/Build Manifest for platform without File System")]
    public static void BuildManifest()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;

        Debug.Log($"streamingAssetsPath={streamingAssetsPath}");

        var builtinScripts = Directory.GetFiles(streamingAssetsPath + "/BuiltinScripts", "*.js")
            .Select(GetRelativeToAndNormalizePath).ToList();
        var scenarioFiles = Directory.GetFiles(streamingAssetsPath + "/Scenarios", "*.scen.xml")
                .Select(GetRelativeToAndNormalizePath).ToList();

        var manifestModel = new ManifestModel()
        {
            builtinScripts = builtinScripts,
            scenarioFiles = scenarioFiles
        };

        var manifestXml = XmlUtils.ToXML(manifestModel);
        File.WriteAllText(streamingAssetsPath + "/Manifest.xml", manifestXml);
    }

    static string GetRelativeToAndNormalizePath(string path)
    {
        return path.Replace("\\", "/").Replace(Application.streamingAssetsPath, "");
    }

    [MenuItem("Custom/Check MultiColumnListView cellTemplate missing")]
    public static void CheckMultiColumnListView()
    {
        string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
        foreach (var uxmlGuid in uxmlGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(uxmlGuid);

            if (!assetPath.StartsWith("Assets/"))
                continue;

            VisualTreeAsset vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);

            // Debug.Log($"vta.name={vta.name}");

            var el = vta.CloneTree();
            var multiColumnListViews = el.Query<MultiColumnListView>().ToList();
            if (multiColumnListViews.Count > 0)
            {
                Debug.Log($"vta.name={vta.name} (assetPath={assetPath}, uxmlGuid={uxmlGuid})");
                foreach (var mclv in multiColumnListViews)
                {
                    Debug.Log($"mclv.name={mclv.name}");
                    var hasMissing = false;
                    foreach (var col in mclv.columns)
                    {
                        if (col.cellTemplate != null)
                        {
                            Debug.Log($"col.title={col.title}, col.cellTemplate.name={col.cellTemplate.name}");
                        }
                        else
                        {
                            Debug.LogWarning($"cellTemplate missing: col.title={col.title}, col.cellTemplate={col.cellTemplate}");
                            hasMissing = true;
                        }
                    }
                    if (hasMissing)
                    {
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                        Debug.LogWarning("Reimporting...");
                    }
                }
            }

            // var field = typeof(VisualTreeAsset).GetField("m_VisualElementAssets", BindingFlags.NonPublic | BindingFlags.Instance);
            // if (field != null)
            // {
            //     var m_VisualElementAssets = field.GetValue(vta); // List<VisualElementAsset>
            //     Debug.Log(m_VisualElementAssets);

            //     var count = m_VisualElementAssets.GetType().GetProperty("Count").GetValue(m_VisualElementAssets);
            //     Debug.Log(count);

            //     foreach (var vea in (IList)m_VisualElementAssets)
            //     {
            //         // Debug.Log(vea);
            //         // m_SerializedData
            //         var _m_SerializedData = vea.GetType().GetField("m_SerializedData");
            //         if (_m_SerializedData != null)
            //         {
            //             var serializedData = _m_SerializedData.GetValue(vea);
            //             Debug.Log($"serializedData={serializedData}");
            //         }
            //     }
            // }
        }
    }

    [MenuItem("Custom/Reserialize scenarios")]
    public static void ReserializeScenarios()
    {
        var scenarioFiles = Directory.GetFiles(Application.streamingAssetsPath + "/Scenarios", "*.scen.xml");
        foreach (var path in scenarioFiles)
        {
            var xml = File.ReadAllText(path);
            var fullState = XmlUtils.FromXML<FullState>(xml);
            var reserializedXml = XmlUtils.ToXML(fullState);
            File.WriteAllText(path, reserializedXml);
        }
    }
    
}