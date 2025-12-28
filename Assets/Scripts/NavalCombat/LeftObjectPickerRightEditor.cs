using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;

// TODO: Enforce INamed constraint for ET
public abstract class LeftObjectPickerRightEditor<ST, ET> : HideableDocument<ST> where ET : class, IObjectIdLabeled, new() where ST : MonoBehaviour
{
    public string selectedId;
    public ListView objectListView;

    public List<ET> fullObjects; // Modification (add/remove) to this should be synced to its original datasource.
    string _filterString;
    public List<ET> filteredObjects;

    [CreateProperty]
    public string filterString
    {
        get => _filterString;
        set
        {
            _filterString = value;
            RefreshFilter();
        }
    }

    public void RefreshFilter()
    {
        if(fullObjects == null)
            return;

        IEnumerable<ET> _filteredObjects = fullObjects;

        var filterStringInvalid = _filterString == null || _filterString == "";

        if(!filterStringInvalid)
        {
            _filteredObjects = _filteredObjects.Where(s => (s as INamed)?.GetName()?.MatchAny(_filterString) ?? false);
        }

        _filteredObjects = ExtraFilter(_filteredObjects);

        filteredObjects = _filteredObjects.ToList();
    }


    [CreateProperty]
    public ET selectedObject { get => EntityManager.Instance.Get<ET>(selectedId); }

    // Parameter
    protected virtual void GetFullObjects()
    {
        fullObjects = new();
    }

    protected virtual IEnumerable<ET> ExtraFilter(IEnumerable<ET> objs)
    {
        return objs;
    }

    protected virtual void ProcessAddedOne(ET newObj)
    {
    }

    protected virtual void OnEnable()
    {
        GetFullObjects();
        filterString = "";
        // RefreshFilter();

        root.dataSource = this;

        Utils.BindItemsSourceRecursive(root);

        objectListView = root.Q<ListView>("ObjectListView");
        if(objectListView.showAddRemoveFooter)
        {
            Utils.BindItemsAddedRemoved<ET>(objectListView, () => null);
        }

        objectListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            Debug.Log("LeftObjectPickerRightEditorStrategic.selectionChanged");

            var obj = objects.FirstOrDefault() as ET;
            selectedId = obj?.objectId;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += OnConfirmButtonClicked;

        var copyLastButton = root.Q<Button>("CopyLastButton");
        if(copyLastButton != null)
        {
            copyLastButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding<List<ET>>(objectListView, out var objList))
                {
                    if (objList.Count >= 1)
                    {
                        var lastObj = objList[^1];
                        var newObj = XmlUtils.FromXML<ET>(XmlUtils.ToXML(lastObj));
                        newObj.objectId = null;
                        objList.Add(newObj);

                        ProcessCopiedLastOne(newObj);

                        SuperGameState.Instance.GetCurrentGameState().ResetAndRegisterAll();
                        // currentGameState.ResetAndRegisterAll(); // Assign a new guid
                    }
                }
            };
        }
        
        // Optional when Add Remove Footer are not enabled
        var addObjectButton = root.Q<Button>("AddObjectButton");
        if(addObjectButton != null)
        {
            addObjectButton.clicked += () =>
            {
                var newObj = new ET();
                EntityManager.Instance.Register(newObj, null);
                fullObjects.Add(newObj);

                ProcessAddedOne(newObj);

                RefreshFilter();
            };
        }

        var deleteObjectButton = root.Q<Button>("RemoveObjectButton");
        if(deleteObjectButton != null)
        {
            deleteObjectButton.clicked += () =>
            {
                if(selectedObject != null)
                {
                    fullObjects.Remove(selectedObject);

                    RefreshFilter();
                }
            };
        }
    }


    protected virtual void ProcessCopiedLastOne(ET obj)
    {
    }

    // public abstract string GetObjectListViewName();

    protected virtual void OnConfirmButtonClicked()
    {
        OnConfirmButtonClickedBefore();
        Hide();
    }

    protected virtual void OnConfirmButtonClickedBefore()
    {
    }

    [CreateProperty]
    public bool selectedValid => selectedObject != null;
}
