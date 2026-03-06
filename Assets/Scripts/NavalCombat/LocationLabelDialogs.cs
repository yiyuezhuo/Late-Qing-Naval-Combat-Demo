using System;
using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

public class LocationLabelEditDialogModel
{
    readonly LocationLabel target;
    readonly Action<LocationLabel> confirmCallback;
    readonly Action afterConfirm;
    readonly LocationLabel workingCopy;

    LocationLabelEditDialogModel(LocationLabel target, Action<LocationLabel> confirmCallback, Action afterConfirm)
    {
        this.target = target;
        this.confirmCallback = confirmCallback;
        this.afterConfirm = afterConfirm;
        workingCopy = target?.Clone() ?? new LocationLabel();
        workingCopy.name ??= new GlobalString();
    }

    public static LocationLabelEditDialogModel ForCreate(LatLon latLon, Action<LocationLabel> confirmCallback, Action afterConfirm = null)
    {
        var model = new LocationLabelEditDialogModel(null, confirmCallback, afterConfirm);
        if (latLon != null)
        {
            model.latitude = latLon.LatDeg;
            model.longitude = latLon.LonDeg;
        }
        return model;
    }

    public static LocationLabelEditDialogModel ForEdit(LocationLabel target, Action afterConfirm = null)
    {
        return new LocationLabelEditDialogModel(target, null, afterConfirm);
    }

    [CreateProperty]
    public GlobalString name => workingCopy.name ??= new GlobalString();

    [CreateProperty]
    public float latitude
    {
        get => workingCopy.latitude;
        set => workingCopy.latitude = value;
    }

    [CreateProperty]
    public float longitude
    {
        get => workingCopy.longitude;
        set => workingCopy.longitude = value;
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        if (target != null)
        {
            target.CopyFrom(workingCopy);
            confirmCallback?.Invoke(target);
        }
        else
        {
            confirmCallback?.Invoke(workingCopy.Clone());
        }

        afterConfirm?.Invoke();
    }
}

public class LocationLabelsEditorDialog
{
    const float RowHeight = 52f;
    const float SelectionGutterWidth = 16f;

    ListView listView;

    [CreateProperty]
    public ScenarioState scenarioState => NavalGameState.Instance.scenarioState;

    public void OnCreated(object sender, VisualElement root)
    {
        scenarioState.locationLabels ??= new List<LocationLabel>();

        listView = root.Q<ListView>("LocationLabelsListView");
        Utils.BindItemsAddedRemoved<LocationLabel>(listView, () => null);

        listView.makeItem = () =>
        {
            var rowRoot = new VisualElement();
            rowRoot.AddToClassList("location-label-row");
            rowRoot.style.flexDirection = FlexDirection.Row;
            rowRoot.style.minHeight = RowHeight;
            rowRoot.style.height = RowHeight;
            rowRoot.style.alignItems = Align.Stretch;

            var selectGutter = new VisualElement();
            selectGutter.AddToClassList("location-label-row-gutter");
            selectGutter.style.width = SelectionGutterWidth;
            selectGutter.style.minWidth = SelectionGutterWidth;
            selectGutter.style.backgroundColor = Color.clear;

            var button = new Button();
            button.AddToClassList("location-label-row-button");
            button.style.flexGrow = 1;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.minHeight = RowHeight;
            button.style.height = RowHeight;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 8;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.clicked += () =>
            {
                if (button.userData is int idx)
                {
                    OpenEditorForIndex(idx);
                }
            };

            rowRoot.Add(selectGutter);
            rowRoot.Add(button);

            return rowRoot;
        };

        listView.bindItem = (element, index) =>
        {
            var button = element.Q<Button>();
            if (button == null || index < 0 || index >= scenarioState.locationLabels.Count)
                return;

            button.userData = index;
            button.text = scenarioState.locationLabels[index]?.GetShortSummary() ?? "[Empty]";
        };

        listView.itemsAdded += indices =>
        {
            var latLon = GameManager.Instance?.lastSelectedLatLon;
            int? firstAdded = null;
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= scenarioState.locationLabels.Count)
                    continue;

                var label = scenarioState.locationLabels[idx];
                if (label == null)
                    continue;

                label.name ??= new GlobalString();
                if (latLon != null)
                {
                    label.latitude = latLon.LatDeg;
                    label.longitude = latLon.LonDeg;
                }

                firstAdded ??= idx;
            }

            listView.Rebuild();

            if (firstAdded.HasValue)
            {
                OpenEditorForIndex(firstAdded.Value);
            }
        };

        listView.itemsRemoved += _ => listView.Rebuild();
    }

    void OpenEditorForIndex(int index)
    {
        if (index < 0 || index >= scenarioState.locationLabels.Count)
            return;

        listView.selectedIndex = index;
        var label = scenarioState.locationLabels[index];
        DialogRoot.Instance.PopupNavalLocationLabelEditorDialog(label, () => listView.Rebuild());
    }
}
