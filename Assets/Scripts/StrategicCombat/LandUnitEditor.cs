using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;

// public abstract class LeftObjectPickerRightEditorStrategic<ST, ET> : HideableDocument<ST> where ET : class, IObjectIdLabeled, new() where ST : MonoBehaviour
// {
//     public string selectedId;
//     ListView objectListView;

//     [CreateProperty]
//     public ET selectedObject { get => EntityManager.Instance.Get<ET>(selectedId); }

//     void OnEnable()
//     {
//         root.dataSource = this;

//         Utils.BindItemsSourceRecursive(root);

//         objectListView = root.Q<ListView>("ObjectListView");
//         Utils.BindItemsAddedRemoved<ET>(objectListView, () => null);

//         objectListView.selectionChanged += (IEnumerable<object> objects) =>
//         {
//             Debug.Log("LeftObjectPickerRightEditorStrategic.selectionChanged");

//             var obj = objects.FirstOrDefault() as ET;
//             selectedId = obj?.objectId;
//         };

//         var confirmButton = root.Q<Button>("ConfirmButton");
//         confirmButton.clicked += Hide;

//         var copyLastButton = root.Q<Button>("CopyLastButton");
//         copyLastButton.clicked += () =>
//         {
//             if (Utils.TryResolveCurrentValueForBinding<List<ET>>(objectListView, out var objList))
//             {
//                 if (objList.Count >= 1)
//                 {
//                     var lastObj = objList[^1];
//                     var newObj = XmlUtils.FromXML<ET>(XmlUtils.ToXML(lastObj));
//                     newObj.objectId = null;
//                     objList.Add(newObj);

//                     currentGameState.ResetAndRegisterAll(); // Assign a new guid
//                 }
//             }
//         };
//     }

//     // public abstract string GetObjectListViewName();

//     [CreateProperty]
//     public StrategicGameState currentGameState => StrategicGameState.Instance;

//     [CreateProperty]
//     public bool selectedValid => selectedObject != null;
// }

public class LandUnitEditor : LeftObjectPickerRightEditorStrategic<LandUnitEditor, LandUnit>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
}

// public class LandUnitEditor : HideableDocument<LandUnitEditor>
// {
//     public string selectedLandUnitId;

//     [CreateProperty]
//     public LandUnit selectedLandUnit { get => EntityManager.Instance.Get<LandUnit>(selectedLandUnitId); }

//     void OnEnable()
//     {
//         root.dataSource = this;

//         Utils.BindItemsSourceRecursive(root);

//         var landUnitListView = root.Q<ListView>("LandUnitListView");
//         Utils.BindItemsAddedRemoved<LandUnit>(landUnitListView, () => null);

//         landUnitListView.selectionChanged += (IEnumerable<object> objects) =>
//         {
//             Debug.Log("landUnitListView.selectionChanged");

//             var landUnit = objects.FirstOrDefault() as LandUnit;
//             selectedLandUnitId = landUnit?.objectId;
//         };

//         var confirmButton = root.Q<Button>("ConfirmButton");
//         confirmButton.clicked += Hide;
//     }

//     [CreateProperty]
//     public StrategicGameState currentGameState => StrategicGameState.Instance;

//     [CreateProperty]
//     public bool selectedValid => selectedLandUnit != null;
// }