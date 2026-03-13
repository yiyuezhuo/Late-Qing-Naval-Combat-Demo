using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UnityWebRequestImageReaderShower : SingletonDocument<UnityWebRequestImageReaderShower>
{
    Label statusLabel;

    float busyAccSeconds = 0;

    public static float displaybusyAccSecondsThreshold = 0.5f;

    protected override void Awake()
    {
        base.Awake();

        root.style.display = DisplayStyle.None;

        statusLabel = root.Q<Label>("StatusLabel");
    }

    void Update()
    {
        var reader = UnityWebRequestImageReader.Instance;
        var paths = reader.activingTasks.Select(task => task.path).ToList();
        paths.AddRange(AssetReferenceManager.Instance.loadingAssetReferences.Select(assetRef => $"Addressable Asset: {assetRef.RuntimeKey}"));
        // paths.AddRange(AssetReferenceManager.Instance.loadingAssetReferences.Select(assetRef => $"Addressable Asset: {assetRef.SubObjectName}"));
        paths.AddRange(StreamingTextAssetManager.Instance.busyUnityWebRequests.Select(req => req.url));

        if (paths.Count == 0)
        {
            root.style.display = DisplayStyle.None;
            busyAccSeconds = 0;
            return;
        }
        busyAccSeconds += Time.deltaTime;
        if(busyAccSeconds > displaybusyAccSecondsThreshold)
        {
            root.style.display = DisplayStyle.Flex;
            statusLabel.text = $"Fetching {paths.Count} files\n" + string.Join("\n", paths);
        }
    }
}