using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using NavalCombatCore;
using CoreUtils;

public class ManifestModel
{
    public string remark = "This file is only used in platform that don't support File System (E.X. WebGL)";
    public List<string> builtinScripts = new();
    public List<string> scenarioFiles = new();
}

public class ManifestModelCache : SingletonMonoBehaviour<ManifestModelCache> // It can be attached to GameManager's gameObject
{
    public ManifestModel manifestModel;
    public bool isDone;
    public List<Action<ManifestModel>> callbacks = new();
    public void CommitTask(Action<ManifestModel> callback)
    {
        if (isDone)
        {
            callback(manifestModel);
        }
        else
        {
            callbacks.Add(callback);
        }
    }

    IEnumerator FetchMenifestModel()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        var manifestPath = streamingAssetsPath + "/Manifest.xml";

        var request = UnityWebRequest.Get(manifestPath);
        Debug.Log($"manifestPath={manifestPath}");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            manifestModel = XmlUtils.FromXML<ManifestModel>(request.downloadHandler.text);
            isDone = true;
            foreach (var callback in callbacks)
            {
                callback(manifestModel);
            }
        }
    }

    void Awake()
    {
        StartCoroutine(FetchMenifestModel());
    }
}
