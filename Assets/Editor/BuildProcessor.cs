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

        var files = Directory.GetFiles(streamingAssetsPath + "/BuiltinScripts", "*.js");

        var manifestModel = new ManifestModel() { builtinScripts = files.Select(
            s => s.Replace('\\', '/').Replace(streamingAssetsPath, "")
        ).ToList() };

        var manifestXml = XmlUtils.ToXML(manifestModel);
        File.WriteAllText(streamingAssetsPath + "/Manifest.xml", manifestXml);
    }
}