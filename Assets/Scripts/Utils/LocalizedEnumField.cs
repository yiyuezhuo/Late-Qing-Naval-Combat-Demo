using UnityEngine.UIElements;
using System;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

using CoreUtils;

[UxmlElement]
public partial class LocalizedEnumField : BaseField<int>
{
    DropdownField m_Input;

    public new static readonly string ussClassName = "localized-enum-field";


    // Default constructor is required for compatibility with UXML factory
    public LocalizedEnumField() : this(null)
    {

    }

    // Main constructor accepts label parameter to mimic BaseField constructor.
    // Second argument to base constructor is the input element, the one that displays the value this field is
    // bound to.
    public LocalizedEnumField(string label) : base(label, new DropdownField() { })
    {
        AddToClassList(ussClassName);

        // This is the input element instantiated for the base constructor.
        m_Input = this.Q<DropdownField>(className: inputUssClassName);

        // m_Input.RegisterValueChangedCallback(evt => this.value = m_Input.index);
        m_Input.RegisterValueChangedCallback(OnDropdownFieldValueChanged);

        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
    }

    private void OnAttachToPanel(AttachToPanelEvent e)
    {
        // UnityEngine.Debug.Log("LocalizedEnumField.OnAttachToPanel");

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        RefreshDropdownFieldChoices();
    }

    private void OnDetachFromPanel(DetachFromPanelEvent e)
    {
        // UnityEngine.Debug.Log("LocalizedEnumField.OnDetachFromPanel");

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale locale)
    {
        RefreshDropdownFieldChoices();
    }

    // protected static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);
    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    
    void RefreshDropdownFieldChoices()
    {
        if (m_EnumType != null)
        {
            // m_Input.choices = Enum.GetNames(m_EnumType).Select(LocalizeEnum).ToList();
            var idx = m_Input.index;

            m_Input.choices = Enum.GetNames(m_EnumType).Select(x => Localize($"{m_EnumType.Name}.{x}")).ToList();
            // this.value = this.value; // try to refresh displayed field

            m_Input.index = idx;
            // if(idx >= 0 && idx < m_Input.choices.Count)
            // {
            //     m_Input.value = m_Input.choices[idx];
            // }
        }
        else
        {
            m_Input.choices.Clear();
            // this.value = this.value;
        }
    }

    void OnDropdownFieldValueChanged(ChangeEvent<string> evt)
    {
        // This is the value of the dropdown field.
        value = m_Input.index;
    }

    // SetValueWithoutNotify needs to be overridden by calling the base version and then making a change to the
    // underlying value be reflected in the input element.
    public override void SetValueWithoutNotify(int newValue)
    {
        base.SetValueWithoutNotify(newValue);

        // m_Input.text = value.ToString("N");
        m_Input.index = newValue;
    }

    [UxmlAttribute]
    public string resolvedTypeStr
    {
        get => m_EnumType == null ? "Not Resolved" : $"Resolved: {m_EnumType}";
        set
        { }
    }
    
    Type m_EnumType;
    string _typeStr;

    [UxmlAttribute]
    public string typeStr
    {
        get => _typeStr;
        set
        {
            _typeStr = value;

            Type type = null;
            if (value != null)
            {
                type = Type.GetType(value, false);
            }
            if (type != m_EnumType)
            {
                m_EnumType = type;
                if (type != null)
                {
                    m_Input.choices = Enum.GetNames(type).ToList(); // Localize here
                    this.value = -1;
                }
                else
                {
                    m_Input.choices.Clear();
                    this.value = -1;
                }

                // resolvedTypeLabel.text = $"Resolved: {type}";
            }
        }
    }



}