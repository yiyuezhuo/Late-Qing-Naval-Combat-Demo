using CoreUtils;
using UnityEngine.UIElements;
using SFB;
using UnityEngine;
using System.IO;
using System;


public static class PathReferenceBinder
{
    static ExtensionFilter[] imageExtensions = new ExtensionFilter[]
    {
        new ExtensionFilter("Images", "png", "jpg", "jpeg", "gif"),
    };
    static ExtensionFilter[] jsExtensions = new ExtensionFilter[]
    {
        new ExtensionFilter("JavaScript", "js"),
    };
    static ExtensionFilter[] xmlExtensions = new ExtensionFilter[]
    {
        new ExtensionFilter("XML", "xml"),
    };

    public static void Bind(VisualElement el, ExtensionFilter[] extensions)
    {
        var setButton = el.Q<Button>("SetButton");
        setButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(el, out PathReference pathReference))
            {
                var newPath = IOManager.Instance.LoadPath(extensions);
                if (newPath != null)
                {
                    var streamingAssetsPath = Application.streamingAssetsPath.Replace("\\", "/");

                    if (newPath.StartsWith(streamingAssetsPath))
                    {
                        var relPath = Path.GetRelativePath(streamingAssetsPath, newPath);
                        pathReference.path = relPath;
                        pathReference.isBuiltin = true;
                    }
                    else
                    {
                        pathReference.path = newPath;
                        pathReference.isBuiltin = false;
                    }
                }
            }
            ;
        };
    }

    public static void BindPictureReference(VisualElement el) => Bind(el, imageExtensions);
    public static void BindJSReference(VisualElement el) => Bind(el, jsExtensions);
    public static void AddCallback(VisualElement el, Action callback)
    {
        var setButton = el.Q<Button>("SetButton");
        setButton.clicked += callback;
    }
}