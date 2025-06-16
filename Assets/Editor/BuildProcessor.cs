using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NavalCombatCore;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;


public class BuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 100; } }


    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("Preprocess build started for: " + report.summary.platform);
        BuildManifest();
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
}