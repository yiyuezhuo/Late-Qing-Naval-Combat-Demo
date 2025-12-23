using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace CoreUtils
{
    public partial class PathReference
    {
        public string ResolvePath()
        {
            if (isBuiltin)
            {
                return Application.streamingAssetsPath + "/" + path;
            }
            return path;
        }

        [CreateProperty]
        public bool setButtonAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }
    }

    public partial class PictureReference
    {
        [CreateProperty]
        public StyleBackground pictureStyleBackground
        {
            get
            {
                var path = ResolvePath();
                return UnityWebRequestImageReader.Instance.FetchStyleBackground(path);
            }
        }

        [CreateProperty]
        public Texture2D texture2d
        {
            get
            {
                var path = ResolvePath();
                return UnityWebRequestImageReader.Instance.FetchTexture2D(path);
            }
        }

        public void RequestIfNotRequestedYetOtherwiseExecuteDirectly(Action<StyleBackground> callback)
        {
            var path = ResolvePath();
            UnityWebRequestImageReader.Instance.RequestIfNotRequestedYetOtherwiseExecuteDirectly(new()
            {
                path=path,
                styleBackgroundCallbacks = new()
                {
                    callback
                }
            });
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditorMode;
    }

    // works like a UnityWebRequest wrapper. Fetch value after request sent
    public partial class TextReference
    {
        IEnumerator Request()
        {
            var path = ResolvePath();
            using (var webRequest = UnityWebRequest.Get(path))
            {
                yield return webRequest.SendWebRequest();
                pathToText[path] = webRequest.downloadHandler.text;
            }
        }

        public IEnumerator RequestIfNotLoadedYet()
        {
            var path = ResolvePath();
            if (path == null)
                yield break;
            if (!pathToText.ContainsKey(path))
                yield return Request();
            // if it is loaded, a coroutine which isDone=true is returned, so no one frame delay would be introduced.
        }

        public void TryToClearCache()
        {
            var path = ResolvePath();

            if (path == null)
                return;

            if (pathToText.ContainsKey(path))
                pathToText.Remove(path);
        }

        public string text
        {
            get
            {
                var path = ResolvePath();
                if (path == null)
                    return null;
                return pathToText.GetValueOrDefault(path);
            }
        }
    }
}