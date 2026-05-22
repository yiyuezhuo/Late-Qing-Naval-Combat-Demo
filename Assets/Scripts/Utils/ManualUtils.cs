using UnityEngine;

public static class ManualUtils
{
    const string ManualReadmeRelativePath = "Manuals/readme.pdf";

    public static string GetReadmePath()
    {
        return $"{Application.streamingAssetsPath}/{ManualReadmeRelativePath}".Replace('\\', '/');
    }

    public static void PopupReadme()
    {
        DialogRoot.Instance.PopupConfirmOpenURLDialog(GetReadmePath());
    }
}
