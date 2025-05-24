using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class TopTabs : SingletonDocument<TopTabs>
{
    protected override void Awake()
    {
        base.Awake();

        var shipClassEditorButton = root.Q<Button>("ClassEditorButton");
        shipClassEditorButton.clicked += () => ShipClassEditor.Instance.Show();

        var shipLogEditorButton = root.Q<Button>("ShipLogEditorButton");
        shipLogEditorButton.clicked += () => ShipLogEditor.Instance.Show();

        var oobEditorButton = root.Q<Button>("OOBEditorButton");
        oobEditorButton.clicked += () => OOBEditor.Instance.Show();
    }
}