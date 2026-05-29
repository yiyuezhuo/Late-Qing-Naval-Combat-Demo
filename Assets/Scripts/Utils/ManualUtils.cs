using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class ManualUtils
{
    const string ManualReadmeRelativePath = "Manuals/readme.pdf";
    const string ManualReadmeUrl = "manual://readme";
    const string ManualMimeType = "application/pdf";

    public static string GetReadmePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return ManualReadmeUrl;
#else
        return GetStreamingReadmePath();
#endif
    }

    public static void PopupReadme()
    {
        DialogRoot.Instance.PopupConfirmOpenURLDialog(ManualReadmeUrl);
    }

    public static bool IsManualUrl(string url)
    {
        return string.Equals(url, ManualReadmeUrl, StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenReadme()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var runner = DialogRoot.Instance as MonoBehaviour ?? BehaviourUtils.Instance;
        if (runner == null)
        {
            Debug.LogError("Cannot open manual: no active MonoBehaviour is available to run the Android copy coroutine.");
            return;
        }

        runner.StartCoroutine(OpenReadmeAndroidCoroutine());
#else
        Application.OpenURL(GetStreamingReadmePath());
#endif
    }

    static string GetStreamingReadmePath()
    {
        return $"{Application.streamingAssetsPath}/{ManualReadmeRelativePath}".Replace('\\', '/');
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static IEnumerator OpenReadmeAndroidCoroutine()
    {
        var targetPath = Path.Combine(Application.persistentDataPath, ManualReadmeRelativePath);

        using (var request = UnityWebRequest.Get(GetStreamingReadmePath()))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowOpenFailed($"Failed to load manual from StreamingAssets: {request.error}");
                yield break;
            }

            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(targetPath, request.downloadHandler.data);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowOpenFailed($"Failed to copy manual: {exception.Message}");
                yield break;
            }
        }

        try
        {
            OpenAndroidPdf(targetPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowOpenFailed($"Failed to open manual: {exception.Message}");
        }
    }

    static void OpenAndroidPdf(string filePath)
    {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var file = new AndroidJavaObject("java.io.File", filePath);
        using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");

        var authority = $"{activity.Call<string>("getPackageName")}.manualfileprovider";
        using var uri = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
        using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");

        intent.Call<AndroidJavaObject>("setDataAndType", uri, ManualMimeType);
        intent.Call<AndroidJavaObject>("addFlags", 0x00000001); // FLAG_GRANT_READ_URI_PERMISSION
        activity.Call("startActivity", intent);
    }

    static void ShowOpenFailed(string message)
    {
        var dialogRoot = DialogRoot.Instance;
        if (dialogRoot != null)
            dialogRoot.PopupMessageDialog(message);
        else
            Debug.LogError(message);
    }
#endif
}
