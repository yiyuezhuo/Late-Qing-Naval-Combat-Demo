
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

    public enum PositionMode
    {
        None,
        Centering,
        Left
    }

    // public bool centering = true;
    public PositionMode positionMode = PositionMode.Centering;
    public bool fullScreen = false;
    // public bool draggable = false;
    public bool draggable = true;
    public bool cancelEnabled = true;

    VisualElement el;

    public bool closed;

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
        template.CloneTree(root);
        el = root[root.childCount - 1];
        el.dataSource = templateDataSource;

        onCreated?.Invoke(this, el);

        var confirmButton = el.Q<Button>("ConfirmButton");
        var cancelButton = el.Q<Button>("CancelButton");

        Utils.BindItemsSourceRecursive(el);

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
            cancelButton.SetEnabled(cancelEnabled);

            if (cancelEnabled)
            {
                cancelButton.clicked += () =>
                {
                    // root.Remove(el);
                    Close();

                    onCancelled?.Invoke(this, el);
                };
            }
        }

        if (positionMode == PositionMode.Centering)
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
        else if(positionMode == PositionMode.Left)
        {
            el.style.position = Position.Absolute;
            el.style.left = new Length(0, LengthUnit.Percent);
            el.style.top = new Length(50, LengthUnit.Percent);
            el.style.translate = new StyleTranslate(
                new Translate(
                    new Length(0, LengthUnit.Percent),
                    new Length(-50, LengthUnit.Percent)
                )
            );
        }


        // if (positionMode == PositionMode.Centering || positionMode == PositionMode.Left)
        // {
        //     el.style.position = Position.Absolute;
        //     el.style.translate = StyleKeyword.Null;
        //     PositionAfterLayout();
        // }

        if (fullScreen)
        {
            el.style.flexGrow = 1;
        }

        if (draggable)
        {
            // root.AddManipulator(new MyDragger());
            var titles = el.Query(className: "title").ToList();
            if(titles.Count > 0)
            {
                var title = titles[0];
                title.AddManipulator(new DragManipulator(el));
            }
        }
    }

    public void SoftHide()
    {
        // Hide();

        var f1 = root.focusController?.focusedElement;
        if(f1 != null)
            f1.Blur();

        el.style.display = DisplayStyle.None;

        // OnHidden();
    }

    public void Reshow()
    {
        // Show();
        el.style.display = DisplayStyle.Flex;

        // OnShown();
    }

    // void PositionAfterLayout()
    // {
    //     if (TryPosition())
    //         return;

    //     void OnGeometryChanged(GeometryChangedEvent _)
    //     {
    //         if (TryPosition())
    //             el.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    //     }

    //     el.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    // }

    // bool TryPosition()
    // {
    //     if (root == null || el == null)
    //         return false;

    //     var rootWidth = root.resolvedStyle.width;
    //     var rootHeight = root.resolvedStyle.height;
    //     var elementWidth = el.resolvedStyle.width;
    //     var elementHeight = el.resolvedStyle.height;

    //     if (!IsValidLayoutSize(rootWidth)
    //         || !IsValidLayoutSize(rootHeight)
    //         || !IsValidLayoutSize(elementWidth)
    //         || !IsValidLayoutSize(elementHeight))
    //         return false;

    //     var left = positionMode == PositionMode.Left ? 0 : (rootWidth - elementWidth) * 0.5f;
    //     var top = (rootHeight - elementHeight) * 0.5f;

    //     el.style.left = left < 0 ? 0 : left;
    //     el.style.top = top < 0 ? 0 : top;
    //     return true;
    // }

    static bool IsValidLayoutSize(float value)
    {
        return !float.IsNaN(value) && value > 0;
    }
}
