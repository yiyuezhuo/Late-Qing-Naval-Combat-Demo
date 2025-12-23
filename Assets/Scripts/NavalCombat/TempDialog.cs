
using System;
using UnityEngine.UIElements;

public class TempDialog: ISwitchable
{
    public VisualElement root;
    public VisualTreeAsset template;
    public object templateDataSource;
    public event EventHandler<VisualElement> onCreated;
    public event EventHandler<VisualElement> onConfirmed;
    public event EventHandler<VisualElement> onCancelled;
    public event EventHandler<VisualElement> onClosed;

    public Func<VisualElement, bool> confirmCheck;

    public bool centering = true;
    public bool fullScreen = false;
    public bool draggable = false;

    VisualElement el;

    bool closed;

    public void Close()
    {
        if(!closed)
        {
            closed = true;
            
            onClosed?.Invoke(this, el);
            root.Remove(el);
        }
    }

    void ISwitchable.SwitchClose()
    {
        Close();
    }


    public void Popup()
    {
        // var el = template.CloneTree();
        el = template.CloneTree();
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
                if(confirmCheck == null || confirmCheck(el))
                {
                    // root.Remove(el);
                    Close();

                    onConfirmed?.Invoke(this, el);
                }
            };
        }

        if (cancelButton != null)
        {
            cancelButton.clicked += () =>
            {
                // root.Remove(el);
                Close();

                onCancelled?.Invoke(this, el);
            };
        }

        if (centering)
        {
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

        if (fullScreen)
        {
            el.style.flexGrow = 1;
        }

        // if (draggable)
        // {
        //     root.AddManipulator(new MyDragger());
        // }
    }
}