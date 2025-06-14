using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;

public class LaunchedTorpedoEditor : HideableDocument<LaunchedTorpedoEditor>
{

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        Utils.BindItemsSourceRecursive(root);

        var launchedTorpedoListView = root.Q<ListView>("LaunchedTorpedoListView");

        Utils.BindItemsAddedRemoved<LaunchedTorpedo>(launchedTorpedoListView, () => null);

        launchedTorpedoListView.selectionChanged += (IEnumerable<object> objs) =>
        {
            var launchedTorpedo = objs.FirstOrDefault() as LaunchedTorpedo;
            GameManager.Instance.selectedLaunchedTorpedoObjectId = launchedTorpedo.objectId;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;
    }
}
