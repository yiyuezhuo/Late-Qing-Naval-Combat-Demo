using UnityEngine;
using UnityEngine.UIElements;

using System;
using System.Diagnostics;
using Unity.Properties;
using UnityEngine.Bindings;
using UnityEngine.Scripting.APIUpdating;
using System.Linq;
using CoreUtils;
// using static UnityEngine.EnumDataUtility;

[UxmlElement]
// public partial class LocalizedEnumField : VisualElement// : BaseField<Enum>
public partial class LocalizedEnumField2 : BindableElement
// public partial class LocalizedEnumField : BaseField<int>
{
    // Custom controls need a default constructor. This default constructor calls the other constructor in this
    // class.
    // public LocalizedEnumField() : this(null) { }

    // // This constructor allows users to set the contents of the label.
    // public LocalizedEnumField(string label) : base(label, null)
    // {
    //     // Style the control overall.
    //     AddToClassList(ussClassName);
    // }

    DropdownField m_dropdownField;
    // Label resolvedTypeLabel;
    // Label labelElement;
        
    // private Type m_EnumType;

     // Label m_Input;

    // This default constructor is RadialProgress's only constructor.
    public LocalizedEnumField2() // : this(null)
    {
        // Create a Label, add a USS class name, and add it to this visual tree.
        m_dropdownField = new();

        Add(m_dropdownField);

        // resolvedTypeLabel = new();
        // resolvedTypeLabel.text = "Not Resolved";
        // Add(resolvedTypeLabel);
    }

    // public LocalizedEnumField(string label) : base(label, new Label() { })
    // {
    //     // This is the input element instantiated for the base constructor.
    //     m_Input = this.Q<Label>(className: inputUssClassName);
    // }

    // public Type type
    // {
    //     // [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
    //     get => m_EnumType;
    // }

    // [UxmlAttribute("type")]
    // public string typeStr;

    [UxmlAttribute]
    public string label
    {
        get => m_dropdownField.label;
        set
        {
            m_dropdownField.label = value;

            // NotifyPropertyChanged(labelProperty);
        }
    }

    [UxmlAttribute]
    public string resolvedTypeStr
    {
        get => m_EnumType == null ? "Not Resolved" : $"Resolved: {m_EnumType}";
        set
        {}
    }

    // internal string typeAsString
    // {
    //     get => UxmlUtility.TypeToString(m_EnumType);
    //     // [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
    //     set
    //     {
    //         m_EnumType = UxmlUtility.ParseType(value);
    //         // if (m_EnumType == null)
    //         // {
    //         //     this.value = null;
    //         //     m_TextElement.text = string.Empty;
    //         // }
    //     }
    // }

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
                    m_dropdownField.choices = Enum.GetNames(type).ToList(); // Localize here
                    index = -1;
                }
                else
                {
                    m_dropdownField.choices.Clear();
                    index = -1;
                }

                // resolvedTypeLabel.text = $"Resolved: {type}";
            }
        }
    }

    [UxmlAttribute]
    public int index
    {
        get => m_dropdownField.index;
        set => m_dropdownField.index = value;
    }
}