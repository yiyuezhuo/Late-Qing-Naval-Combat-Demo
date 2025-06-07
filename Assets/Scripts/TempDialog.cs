
using System;
using UnityEngine.UIElements;

public class TempDialog
{
    public VisualElement root;
    public VisualTreeAsset template;
    public object templateDataSource;
    public event EventHandler<VisualElement> onCreated;
    public event EventHandler<VisualElement> onConfirmed;
    public event EventHandler<VisualElement> onCancelled;

    public void Popup()
    {
        var el = template.CloneTree();
        el.dataSource = templateDataSource;

        onCreated?.Invoke(this, el);

        var confirmButton = el.Q<Button>("ConfirmButton");
        var cancelButton = el.Q<Button>("CancelButton");

        Utils.BindItemsSourceRecursive(el);

        root.Add(el);

        if (confirmButton != null)
        {
            confirmButton.clicked += () =>
            {
                root.Remove(el);

                onConfirmed?.Invoke(this, el);
            };
        }

        if (cancelButton != null)
        {
            cancelButton.clicked += () =>
            {
                root.Remove(el);

                onCancelled?.Invoke(this, el);
            };
        }

        el.style.position = Position.Absolute;
        el.style.left = new Length(50, LengthUnit.Percent);
        el.style.top = new Length(50, LengthUnit.Percent);
        el.style.translate = new StyleTranslate(
            new Translate(
                new Length(-50, LengthUnit.Percent),
                new Length(-50, LengthUnit.Percent)
            )
        );
    }
}