using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Collections;

public class ImageFetchTask
{
    public enum State
    {
        Downloading,
        Fail,
        Downloaded
    }

    public string path;
    // public UnityWebRequest request;
    public Texture2D texture;
    public State state;

    StyleBackground _styleBackground;
    public StyleBackground styleBackground
    {
        get
        {
            if (texture == null)
            {
                return null;
            }

            if (_styleBackground == null)
            {
                _styleBackground = new StyleBackground(texture);
            }
            return _styleBackground;
        }
    }

    Sprite _sprite;
    public Sprite sprite
    {
        get
        {
            if (texture == null)
            {
                return null;
            }

            if (_sprite == null)
            {
                _sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100.0f
                );
            }
            return _sprite;
        }
    }
}

public class UnityWebRequestImageReader
{
    Dictionary<string, ImageFetchTask> taskMap = new();

    public StyleBackground FetchStyleBackground(string path)
    {
        if(path == null)
            return null;

        var task = EnsureDownloadCompletedOrStartedAndGetTask(path);
        return task.styleBackground;
    }

    public Texture2D FetchTexture2D(string path)
    {
        if(path == null)
            return null;

        var task = EnsureDownloadCompletedOrStartedAndGetTask(path);
        return task.texture;
    }

    public Sprite FetchSprite(string path)
    {
        if(path == null)
            return null;

        var task = EnsureDownloadCompletedOrStartedAndGetTask(path);
        return task.sprite;
    }

    ImageFetchTask EnsureDownloadCompletedOrStartedAndGetTask(string path)
    {
        if (!taskMap.TryGetValue(path, out var task))
        {
            task = taskMap[path] = new()
            {
                path = path,
                state = ImageFetchTask.State.Downloading,
            };
            // GameManager.Instance.StartCoroutine(Request(task));
            IOManager.Instance.StartCoroutine(Request(task));
        }
        return task;
    }

    IEnumerator Request(ImageFetchTask task)
    {
        // using (var webRequest = UnityWebRequest.Get(task.path))
        using (var webRequest = UnityWebRequestTexture.GetTexture(task.path))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // textLoaded?.Invoke(null, webRequest.downloadHandler.text);
                Debug.Log($"UnityWebRequest succ to get: {task.path}");

                task.texture = DownloadHandlerTexture.GetContent(webRequest);
                task.state = ImageFetchTask.State.Downloaded;
            }
            else
            {
                Debug.LogWarning($"UnityWebRequest failed to get: {task.path}");
                task.state = ImageFetchTask.State.Fail;
            }
        }
    }

    static UnityWebRequestImageReader _instance = new();
    public static UnityWebRequestImageReader Instance => _instance;
}