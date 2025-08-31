using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;
using System.Collections;

using CoreUtils;
using System.Windows.Forms;

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

            foreach (var grouping in eventItems.GroupBy(x => x.eventType))
            {
                var eventType = grouping.Key;
                if (eventType == EventType.FirstLoaded)
                {
                    // manager.firstLoaded = null; // TODO: use -= ?
                    // manager.firstLoaded += (sender, args) =>
                    // {
                    //     foreach (var item in grouping)
                    //     {
                    //         ScriptEngine.Instance.Execute(item.script);
                    //     }
                    // };

                    ResetAndBind(ref manager.firstLoaded, grouping);
                }
                else if (eventType == EventType.PerMinute)
                {
                    // manager.minuteChanged = null;
                    // manager.minuteChanged += (sender, args) =>
                    // {
                    //     foreach (var item in grouping)
                    //     {
                    //         ScriptEngine.Instance.Execute(item.script);
                    //     }
                    // };

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
            }
        }

        void ResetAndBind(ref System.EventHandler eventHandler, IGrouping<EventType, EventItem> grouping)
        {
            eventHandler = null; // TODO: use -= ?
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