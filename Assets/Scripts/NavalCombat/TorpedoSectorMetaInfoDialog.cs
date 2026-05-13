using System;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

public class TorpedoSectorMetaInfoDialog
{
    public TorpedoSector torpedoSector;
    public Action callback;

    [CreateProperty]
    public bool hasMetaInfo
    {
        get => torpedoSector?.metaInfo != null;
        set
        {
            if (torpedoSector == null)
                return;

            if (value)
                torpedoSector.metaInfo ??= new TorpedoSectorMetaInfo();
            else
                torpedoSector.metaInfo = null;
        }
    }

    public void OnCreated(object sender, VisualElement root)
    {
        root.dataSource = this;

        var inferOtherButton = root.Q<Button>("InferOtherButton");
        if (inferOtherButton != null)
        {
            inferOtherButton.clicked += () =>
            {
                if (torpedoSector == null)
                    return;

                if (!torpedoSector.InferDamageClassFromMetaInfo(out var message))
                {
                    DialogRoot.Instance.PopupMessageDialog(message);
                }
            };
        }
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        callback?.Invoke();
    }
}
