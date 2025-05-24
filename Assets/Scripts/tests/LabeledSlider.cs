using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class LabeledSlider : VisualElement
{
    string labelValue = "Label:";

    [UxmlAttribute]
    public string label
    {
        get
        {
            return labelValue;
        }
        set
        {
            if (value != labelValue)
            {
                labelValue = value;
                _label.text = value;
            }
        }
    }// = "Label:";

    [UxmlAttribute]
    public float min { get; set; } = 0;

    [UxmlAttribute]
    public float max { get; set; } = 100;

    [UxmlAttribute]
    public float value { get; set; } = 50;

    private Label _label;
    private Slider _slider;

    public LabeledSlider()
    {
        _label = new Label(label);
        _slider = new Slider(min, max) { value = this.value, style = { flexGrow = 1 } };

        _slider.RegisterValueChangedCallback(evt =>
        {
            this.value = evt.newValue;
        });

        var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        container.Add(_label);
        container.Add(_slider);
        Add(container);
    }

    public void SetAttributes()
    {
        _label.text = label;
        _slider.lowValue = min;
        _slider.highValue = max;
        _slider.value = value;
    }
}