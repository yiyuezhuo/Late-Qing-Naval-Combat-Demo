using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

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
        Advanced,
        ShipLogEditorOpened,
        NamedShipEditorOpened,
        ShipClassEditorOpened,
    }

    public class EventItem
    {
        public string name;
        public EventType eventType;
        // public string scriptPath;
        public PictureReference pathReference = new();
        public string script;

        [CreateProperty]
        public string labelName => $"{name}|{eventType}";
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

        public void Register()
        {
            var manager = GameManager.Instance;

            foreach (var grouping in eventItems.GroupBy(x => x.eventType))
            {
                var eventType = grouping.Key;
                if (eventType == EventType.FirstLoaded)
                {
                    manager.firstLoaded = null;
                    manager.firstLoaded += (sender, args) =>
                    {
                        foreach (var item in grouping)
                        {
                            ScriptEngine.Instance.Execute(item.script);
                        }
                    };
                }
                else if (eventType == EventType.PerMinute)
                {
                    manager.minuteChanged = null;
                    manager.minuteChanged += (sender, args) =>
                    {
                        foreach (var item in grouping)
                        {
                            ScriptEngine.Instance.Execute(item.script);
                        }
                    };
                }
            }
        }
    }
}