using UnityEngine.UIElements;

[UxmlElement]
public partial class ExampleField2 : BaseField<int>
{
    Label m_Input;

    // Default constructor is required for compatibility with UXML factory
    public ExampleField2() : this(null)
    {

    }

    // Main constructor accepts label parameter to mimic BaseField constructor.
    // Second argument to base constructor is the input element, the one that displays the value this field is
    // bound to.
    public ExampleField2(string label) : base(label, new Label() { })
    {
        // This is the input element instantiated for the base constructor.
        m_Input = this.Q<Label>(className: inputUssClassName);
    }

    // SetValueWithoutNotify needs to be overridden by calling the base version and then making a change to the
    // underlying value be reflected in the input element.
    public override void SetValueWithoutNotify(int newValue)
    {
        base.SetValueWithoutNotify(newValue);

        m_Input.text = value.ToString("N");
    }
}