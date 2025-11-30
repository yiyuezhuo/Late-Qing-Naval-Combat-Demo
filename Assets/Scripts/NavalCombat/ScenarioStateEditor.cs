using System;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

public interface IDateTimeHolder
{
    public DateTime GetDateTime();
    public void SetDateTime(DateTime dateTime);
}

public class DateTimeHolder : IDateTimeHolder
{
    // public DateTime dateTime;
    public Func<DateTime> getter;
    public Action<DateTime> setter;

    public DateTime GetDateTime() => getter();
    public void SetDateTime(DateTime dateTime) => setter(dateTime);

}

public class ScenarioStateDateTimeViewModel
{
    public IDateTimeHolder dateTimeHolder;

    bool IsDayValid(int _year, int _month, int _day)
    {
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        return _day <= daysInMonth && _day >= 1;
    }

    [CreateProperty]
    public int year
    {
        get => dateTimeHolder.GetDateTime().Year;
        set
        {
            if (value < 0 || value > 2025)
            {
                Debug.LogWarning($"Invalid year value: {value}");
                return;
            }
            var newYear = value;
            if (!IsDayValid(newYear, month, day))
            {
                Debug.LogWarning($"Invalid year value (blocked by day): {newYear}, {month}, {day}");
                return;
            }
            var t = dateTimeHolder.GetDateTime();
            dateTimeHolder.SetDateTime(new DateTime(newYear, t.Month, t.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Utc));
            // NavalGameState.Instance.scenarioState.dateTime.Year = 2;
        }
    }

    [CreateProperty]
    public int month
    {
        get => dateTimeHolder.GetDateTime().Month;
        set
        {
            if (value < 1 || value > 12)
            {
                Debug.LogWarning($"Invalid month value: {value}");
                return;
            }
            var newMonth = value;
            if (!IsDayValid(year, newMonth, day))
            {
                Debug.LogWarning($"Invalid month value (blocked by day): {year}, {newMonth}, {day}");
                return;
            }
            var t = dateTimeHolder.GetDateTime();
            
            dateTimeHolder.SetDateTime(new System.DateTime(t.Year, newMonth, t.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Utc));
        }
    }

    [CreateProperty]
    public int day
    {
        get => dateTimeHolder.GetDateTime().Day;
        set
        {
            var newDay = value;
            if (!IsDayValid(year, month, newDay))
            {
                Debug.LogWarning($"Invalid day value (blocked by day): {year}, {month}, {newDay}");
                return;
            }
            var t = dateTimeHolder.GetDateTime();
            
            dateTimeHolder.SetDateTime(new System.DateTime(t.Year, t.Month, newDay, t.Hour, t.Minute, t.Second, DateTimeKind.Utc));
        }
    }

    [CreateProperty]
    public int hour
    {
        get => dateTimeHolder.GetDateTime().Hour;
        set
        {
            if (value < 0 || value > 23)
            {
                Debug.LogWarning($"Invalid hour value: {value}");
                return;
            }
            var t = dateTimeHolder.GetDateTime();
            var newHour = value;
            dateTimeHolder.SetDateTime(new System.DateTime(t.Year, t.Month, t.Day, newHour, t.Minute, t.Second, DateTimeKind.Utc));
        }
    }

    [CreateProperty]
    public int minute
    {
        get => dateTimeHolder.GetDateTime().Minute;
        set
        {
            if (value < 0 || value > 59)
            {
                Debug.LogWarning($"Invalid minute value: {value}");
                return;
            }

            var t = dateTimeHolder.GetDateTime();
            var newMinute = value;
            dateTimeHolder.SetDateTime(new System.DateTime(t.Year, t.Month, t.Day, t.Hour, newMinute, t.Second, DateTimeKind.Utc));
        }
    }

    [CreateProperty]
    public int second
    {
        get => dateTimeHolder.GetDateTime().Second;
        set
        {
            if (value < 0 || value > 59)
            {
                Debug.LogWarning($"Invalid second value: {value}");
                return;
            }
            var t = dateTimeHolder.GetDateTime();
            var newSecond = value;
            dateTimeHolder.SetDateTime(new System.DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, newSecond, DateTimeKind.Utc));
        }
    }
}

namespace NavalCombatCore
{
    public partial class ScenarioState : IDateTimeHolder
    {
        public DateTime GetDateTime() => dateTime;
        public void SetDateTime(DateTime dt) => dateTime = dt;

        // public DateTimeOffset GetReferenceTimeZoneDateTimeOffset()
        // {
        //     var dateTimeOffset = new DateTimeOffset(dateTime);
        //     return dateTimeOffset.ToOffset(TimeSpan.FromHours(CoreParameter.Instance.referenceTimeZoneOffset));
        // }

        ScenarioStateDateTimeViewModel _dateTimeViewModel; // Note it's possible to initialize the view model attribute from empty constructor but this may break core's capabbility to leverage empty constructor

        [CreateProperty]
        public ScenarioStateDateTimeViewModel dateTimeViewModel
        {
            get
            {
                if (_dateTimeViewModel == null)
                {
                    _dateTimeViewModel = new ScenarioStateDateTimeViewModel() { dateTimeHolder = this };
                }
                return _dateTimeViewModel;
            }
        }

        ScenarioStateDateTimeViewModel _endDateTimeViewModel;
        
        [CreateProperty]
        public ScenarioStateDateTimeViewModel endDateTimeViewModel
        {
            get
            {
                if(_endDateTimeViewModel == null)
                {
                    _endDateTimeViewModel = new ScenarioStateDateTimeViewModel() {
                        dateTimeHolder = new DateTimeHolder()
                        {
                            getter = () => endDateTime,
                            setter = (dt) => endDateTime = dt
                        }
                    };
                }
                return _endDateTimeViewModel;
            }
        }

        // GetReferenceTimeZoneDateTimeOffsetString

        // [CreateProperty]
        // public DateTimeOffset referenceTimeZoneDateTimeOffset => GetReferenceTimeZoneDateTimeOffset();
        [CreateProperty]
        public DateTimeOffset referenceTimeZoneDateTimeOffset => CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffset(dateTime);

        [CreateProperty]
        public DateTimeOffset referenceTimeZoneEndDateTimeOffset => CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffset(endDateTime);

    }
}


public class ScenarioStateEditor : HideableDocument<ScenarioStateEditor>
{
    // protected override void Awake()
    void OnEnable()
    {
        // base.Awake();

        root.dataSource = GameManager.Instance;

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            // var content = GameManager.Instance.navalGameState.ScenarioStateToXML();
            var content = NavalGameState.Instance.ScenarioStateToXML();
            // IOManager.Instance.SaveTextFile(content, "ScenarioState" + GameManager.scenarioSuffix, "xml");
            IOManager.Instance.SaveTextFile(content, "ScenarioState.xml", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnImportXmlLoaded;
            IOManager.Instance.LoadTextFile(OnImportXmlLoaded, "xml");
        };
    }

    void OnImportXmlLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnImportXmlLoaded;
        NavalGameState.Instance.ScenarioStateFromXML(text);
    }
}