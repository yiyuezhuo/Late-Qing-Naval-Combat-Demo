using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;
using System.Collections;

using CoreUtils;

namespace NavalCombat
{
    public enum EventType
    {
        FirstLoaded,
        CameraMoved,
        CameraZoomed,
        PerSecond,
        PerMinute,
        // Advanced,
        UnitClicked,
        ShipLogEditorOpened,
        NamedShipEditorOpened,
        ShipClassEditorOpened,
        ShipClassEditorClosed,
        DistanceMeasureLineFixed,
        OrderOfBattleEditorShown,
        InsertShipToMapDialogOpened,
        ShipStateInsertedToMap,
        FirstRemainOneOperationalFleetPrompted,
        ReachEndDateTimePrompted
    }

    public class EventItem
    {
        public string name;
        public EventType eventType;
        // public string scriptPath;
        // public PictureReference pathReference = new();
        public TextReference pathReference = new();

        [CreateProperty]
        public string script => pathReference.text;

        [CreateProperty]
        public string labelName => $"{name}|{eventType}";

        public IEnumerator Refresh()
        {
            pathReference.TryToClearCache();
            yield return pathReference.RequestIfNotLoadedYet(); 
        }
    }

    public class EventState
    {
        static EventState instance = new();
        public static EventState Instance => instance;

        public static void UpdateTo(EventState newInstance)
        {
            instance = newInstance ?? new();
        }

        public List<EventItem> eventItems = new();

        public IEnumerator SyncAndRegister()
        {
            yield return BehaviourUtils.Instance.StartAndWaitAll(eventItems.Select(item => item.pathReference.RequestIfNotLoadedYet()));
            Register();
        }

        public IEnumerator RefreshAll()
        {
            // foreach (var item in eventItems)
            // {
            //     yield return item.Refresh();
            // }
            // eventItems.Select(item => IOManager.Instance.StartCoroutine(item.Refresh()));
            yield return BehaviourUtils.Instance.StartAndWaitAll(eventItems.Select(item => item.Refresh()));
        }

        void Register()
        {
            var manager = GameManager.Instance;
            var cameraController = CameraController2.Instance;
            var namedShipEditor = NamedShipEditor.Instance;
            var shipClassEditor = ShipClassEditor.Instance;
            var distanceMeasureLine = MeasureLine.Instance;
            var orderOfBattleEditor = OOBEditor.Instance;
            var switchCenter = SwitchCenter.Instance;

            ClearAllBindings(manager, cameraController, namedShipEditor, shipClassEditor, distanceMeasureLine, orderOfBattleEditor, switchCenter);

            foreach (var grouping in eventItems.GroupBy(x => x.eventType))
            {
                var eventType = grouping.Key;
                if (eventType == EventType.FirstLoaded)
                {
                    ResetAndBind(ref manager.firstLoaded, grouping);
                }
                else if (eventType == EventType.PerMinute)
                {
                    ResetAndBind(ref manager.minuteChanged, grouping);
                }
                else if (eventType == EventType.CameraMoved)
                {
                    ResetAndBind(ref cameraController.cameraMoved, grouping);
                }
                else if (eventType == EventType.CameraZoomed)
                {
                    ResetAndBind(ref cameraController.cameraZoomed, grouping);
                }
                else if (eventType == EventType.UnitClicked)
                {
                    ResetAndBind(ref manager.shipLogClicked, grouping);
                }
                else if (eventType == EventType.ShipLogEditorOpened) // Use name shipLogViewShown? But it's referred by Scripting Layer Though.
                {
                    // ResetAndBind(ref shipLogEditor.shown, grouping);
                    ResetAndBind(ref switchCenter.shipLogViewShown, grouping);
                }
                else if (eventType == EventType.NamedShipEditorOpened)
                {
                    ResetAndBind(ref namedShipEditor.shown, grouping);
                }
                else if (eventType == EventType.ShipClassEditorOpened)
                {
                    ResetAndBind(ref shipClassEditor.shown, grouping);
                }
                else if (eventType == EventType.ShipClassEditorClosed)
                {
                    ResetAndBind(ref shipClassEditor.hidden, grouping);
                }
                else if (eventType == EventType.DistanceMeasureLineFixed)
                {
                    ResetAndBind(ref distanceMeasureLine.distanceMeasureLineFixed, grouping);
                }
                else if (eventType == EventType.OrderOfBattleEditorShown)
                {
                    ResetAndBind(ref orderOfBattleEditor.shown, grouping);
                }
                else if (eventType == EventType.InsertShipToMapDialogOpened)
                {
                    ResetAndBind(ref manager.insertShipToMapDialogOpened, grouping);
                }
                else if (eventType == EventType.ShipStateInsertedToMap)
                {
                    ResetAndBind(ref manager.shipStateInsertedToMap, grouping);
                }
                else if (eventType == EventType.FirstRemainOneOperationalFleetPrompted)
                {
                    ResetAndBind(ref manager.firstRemainOneOperationalFleetPrompted, grouping);
                }
                else if (eventType == EventType.ReachEndDateTimePrompted)
                {
                    ResetAndBind(ref manager.reachEndDateTimePrompted, grouping);
                }
            }
        }

        static void ClearAllBindings(
            GameManager manager,
            CameraController2 cameraController,
            NamedShipEditor namedShipEditor,
            ShipClassEditor shipClassEditor,
            MeasureLine distanceMeasureLine,
            OOBEditor orderOfBattleEditor,
            SwitchCenter switchCenter)
        {
            manager.firstLoaded = null;
            manager.minuteChanged = null;
            cameraController.cameraMoved = null;
            cameraController.cameraZoomed = null;
            manager.shipLogClicked = null;
            manager.insertShipToMapDialogOpened = null;
            manager.shipStateInsertedToMap = null;
            manager.firstRemainOneOperationalFleetPrompted = null;
            manager.reachEndDateTimePrompted = null;
            switchCenter.shipLogViewShown = null;
            namedShipEditor.shown = null;
            shipClassEditor.shown = null;
            shipClassEditor.hidden = null;
            distanceMeasureLine.distanceMeasureLineFixed = null;
            orderOfBattleEditor.shown = null;
        }

        void ResetAndBind(ref System.EventHandler eventHandler, IGrouping<EventType, EventItem> grouping)
        {
            eventHandler += (sender, args) =>
            {
                foreach (var item in grouping)
                {
                    ScriptEngine.Instance.Execute(item.script);
                }
            };
        }
    }
}
