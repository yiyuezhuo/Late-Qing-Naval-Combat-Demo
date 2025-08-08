using CoreUtils;
using UnityEngine.UIElements;
using SFB;
using UnityEngine;
using System.IO;


public static class PictureReferenceBinder
{
    public static void Bind(VisualElement el)
    {
        var setButton = el.Q<Button>("SetButton");
        setButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(el, out PictureReference pictureReference))
            {
                var newPath = IOManager.Instance.LoadImagePath();
                if (newPath != null)
                {
                    var streamingAssetsPath = Application.streamingAssetsPath.Replace("\\", "/");

                    if (newPath.StartsWith(streamingAssetsPath))
                    {
                        var relPath = Path.GetRelativePath(streamingAssetsPath, newPath);
                        pictureReference.path = relPath;
                        pictureReference.isBuiltin = true;
                    }
                    else
                    {
                        pictureReference.path = newPath;
                        pictureReference.isBuiltin = false;
                    }
                }
            };
        };
    }
}