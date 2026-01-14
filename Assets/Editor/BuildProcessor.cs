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
using System.Runtime.InteropServices;
using YYZ;


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

    static List<string> tagOrderList = new()
    {
        "TT", // Tutorial
        "SJH", // Historical scenarios of Sino-Japanese War
        "RJH", // Historical scenario of Russo-Japanese War
        "SJS", // Sino-Japanese small/skirmish scenario (for test, quick battle or local scenario)
        "RJS", // Russo-Japanese War small/skirmish scenario
    };

    static int GetTagIndex(string path)
    {
        var tagName = Path.GetFileName(path).Split(" - ")[0];
        return tagOrderList.IndexOf(tagName);
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

        var subPathToFullState = scenarioFiles.Select(p => 
        {
            var path = Application.streamingAssetsPath + "/" + p;
            var xml = File.ReadAllText(path);
            var fullState = XmlUtils.FromXML<FullState>(xml);
            return (p, fullState);
        }).ToDictionary(p => p.p, p => p.fullState);

        scenarioFiles.Sort((left, right) =>
        {
            var leftTagIdx = GetTagIndex(left);
            var rightTagIdx = GetTagIndex(right);
            if(leftTagIdx != rightTagIdx)
                return leftTagIdx.CompareTo(rightTagIdx);
            
            var leftDateTime = subPathToFullState[left].navalGameState.scenarioState.dateTime;
            var rightDateTime = subPathToFullState[right].navalGameState.scenarioState.dateTime;
            var dateTimeCmp =  leftDateTime.CompareTo(rightDateTime);
            if(dateTimeCmp != 0)
                return dateTimeCmp;

            return left.CompareTo(right);
        });
        // scenarioFiles.Reverse();

        // scenarioFiles.Sort((left, right) =>
        // {
        //     var leftTutorial = left.Contains("Tutorial"); // TODO: Introduce extra info?
        //     var rightTutorial = right.Contains("Tutorial");
        //     if (leftTutorial && !rightTutorial)
        //         return -1;
        //     if (!leftTutorial && rightTutorial)
        //         return 1;
        //     return left.CompareTo(right);
        // });

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