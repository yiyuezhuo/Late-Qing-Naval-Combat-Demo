using System.Linq;
using UnityEngine.UIElements;

public class UnityWebRequestImageReaderShower : SingletonDocument<UnityWebRequestImageReaderShower>
{
    Label statusLabel;

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
        if (paths.Count == 0)
        {
            root.style.display = DisplayStyle.None;
            return;
        }
        root.style.display = DisplayStyle.Flex;
        statusLabel.text = "Fetching\n" + string.Join("\n", paths);
    }
}