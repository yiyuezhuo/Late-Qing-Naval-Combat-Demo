using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class TopTabs : SingletonDocument<TopTabs>
{
    DropdownField playerDropdownField;

    protected override void Awake()
    {
        base.Awake();

        var leaderEditorButton = root.Q<Button>("LeaderEditorButton");
        leaderEditorButton.clicked += () => LeaderEditor.Instance.Show();

        var shipClassEditorButton = root.Q<Button>("ClassEditorButton");
        shipClassEditorButton.clicked += () => ShipClassEditor.Instance.Show();

        var shipLogEditorButton = root.Q<Button>("ShipLogEditorButton");
        shipLogEditorButton.clicked += () => ShipLogEditor.Instance.Show();

        var oobEditorButton = root.Q<Button>("OOBEditorButton");
        oobEditorButton.clicked += () => OOBEditor.Instance.Show();

        playerDropdownField = root.Q<DropdownField>("PlayerDropdownField");

        NavalGameState.Instance.shipGroupsChanged -= OnRootShipGroupsChanged;
        NavalGameState.Instance.shipGroupsChanged += OnRootShipGroupsChanged;

        playerDropdownField.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
        {
            SyncPlayerViewpoint();
        });

        var saveButton = root.Q<Button>("SaveButton");
        var loadButton = root.Q<Button>("LoadButton");

        saveButton.clicked += () =>
        {
            var fullState = new FullState()
            {
                navalGameState = NavalGameState.Instance,
                viewState = CaptureViewState(),
            };

            IOManager.Instance.SaveTextFile(fullState.ToXML(), "FullState", "xml");
        };

        loadButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnFullStateXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };
    }

    void OnFullStateXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnFullStateXMLLoaded;

        var fullState = FullState.FromXML(text);
        LoadViewState(fullState.viewState);
        NavalGameState.Instance.UpdateTo(fullState.navalGameState);
    }

    public ViewState CaptureViewState()
    {
        var t = CameraController2.Instance.transform;
        return new()
        {
            xRotation = t.eulerAngles.x,
            yRotation = t.eulerAngles.y,
            orthographicSize = CameraController2.Instance.cam.orthographicSize
        };
    }

    public void LoadViewState(ViewState viewState)
    {
        var c = CameraController2.Instance;
        foreach (var cam in c.cameras)
            cam.orthographicSize = viewState.orthographicSize;
        c.transform.rotation = Quaternion.Euler(viewState.xRotation, viewState.yRotation, 0);
    }

    public void OnRootShipGroupsChanged(object sender, List<ShipGroup> groups)
    {
        SyncPlayerDropdownField();
    }

    public void SyncPlayerDropdownField()
    {
        // foreach (var shipMember in NavalGameState.Instance.GetShipGroupMembersRecursive())
        // {

        // }
        var members = NavalGameState.Instance.GetShipGroupMembersRecursive().ToList();
        var names = members.Select(m => m.GetMemberName()).ToList();
        playerDropdownField.choices = names;
        playerDropdownField.userData = members;
    }

    public void SyncPlayerViewpoint()
    {
        var refGroup = playerDropdownField.index == -1 ? null : (playerDropdownField.userData as List<IShipGroupMember>)[playerDropdownField.index];
        var postureTypeMap = NavalGameState.Instance.CalcualtePostureMap(refGroup);
        foreach ((var objectId, var viewer) in GameManager.Instance.objectId2Viewer)
        {
            var model = EntityManager.Instance.Get<ShipLog>(objectId);
            var postureType = postureTypeMap.GetValueOrDefault(model);
            // Sync shader parameter for PortraitViewer?
            // var natoViewer = viewer.GetComponent<NATOIconViewer>();
            // natoViewer.SyncPostureType(postureType);
        }
    }
}