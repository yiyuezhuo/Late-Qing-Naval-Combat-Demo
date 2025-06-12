using System.Collections.Generic;
using UnityEngine;

public class ManifestModel
{
    public string remark = "This file is only used in platform that don't support File System (E.X. WebGL)";
    public List<string> builtinScripts = new();
}


public class StreamingAssetsHelper
{
    // public List<string> GetFilePathList()
    // {
    //     // TODO: Fetch info from File System in platforems which support File System

    //     string streamingAssetsPath = Application.streamingAssetsPath;
    //     var manifestPath = streamingAssetsPath + "/Manifest.xml";

    // }
}