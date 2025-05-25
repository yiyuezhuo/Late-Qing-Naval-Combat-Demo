using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SFB;
using System;
using System.Collections;

public class IOManager: SingletonMonoBehaviour<IOManager>
{
    public event EventHandler<string> textLoaded;

    private IEnumerator OutputRoutine(string url) {
        Debug.Log($"OutputRoutine({url})");
        var loader = new WWW(url); // TODO: Use UnityWebRequest
        yield return loader;
        textLoaded?.Invoke(null, loader.text);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Open a download dialog and download given text data to disk using file system.
    [DllImport("__Internal")]
    private static extern void DownloadFile(string gameObjectName, string methodName, string filename, byte[] byteArray, int byteArraySize);

    // Open a upload dialog and call the given callback with loaded data once load is completed
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);

    // Called from browser
    public void OnFileDownload() {
        Debug.Log("OnFileDownload");
    }

    public void OnFileUpload(string url) {
        Debug.Log($"OnFileUpload({url})");
        StartCoroutine(OutputRoutine(url));
    }
#endif

    public void SaveTextFile(string _data, string name="sample", string ext="txt")
    {
        Debug.Log("SaveTextFile");
#if UNITY_WEBGL && !UNITY_EDITOR
        var bytes = System.Text.Encoding.UTF8.GetBytes(_data);
        DownloadFile(gameObject.name, "OnFileDownload", $"{name}.{ext}", bytes, bytes.Length);

#else

        var path = SFB.StandaloneFileBrowser.SaveFilePanel("Title", "", name, ext);
        if (!string.IsNullOrEmpty(path)) {
            System.IO.File.WriteAllText(path, _data);
        }

#endif
    }

    public void LoadTextFile(string ext="txt")
    {
        Debug.Log("LoadTextFile");
#if UNITY_WEBGL && !UNITY_EDITOR
        UploadFile(gameObject.name, "OnFileUpload", $".{ext}", false);
#else
        var paths = StandaloneFileBrowser.OpenFilePanel("Title", "", ext, false);
        if (paths.Length > 0) {
            StartCoroutine(OutputRoutine(new System.Uri(paths[0]).AbsoluteUri));
        }
#endif
    }
}