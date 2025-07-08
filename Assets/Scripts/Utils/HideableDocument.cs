using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    public virtual void OnDestroy()
    {
        Debug.Log($"OnDestroy: {typeof(T)}");
        
        if (_instance == this)
            _instance = null;
    }
}

public class SingletonDocument<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
{
    protected UIDocument doc;
    protected VisualElement root => doc.rootVisualElement;

    protected virtual void Awake()
    {
        doc = GetComponent<UIDocument>();
        // root = doc.rootVisualElement;
    }

    public virtual void OnShow()
    {

    }

    public void Show()
    {
        // root.style.display = DisplayStyle.Flex;
        // doc.enabled = false;
        // if(!doc.enabled)
        //     doc.enabled = true;
        gameObject.SetActive(true);
            
        if (root.style.display != DisplayStyle.Flex)
            root.style.display = DisplayStyle.Flex;

        // enabled = false; // Hack to invoke OnEnable
        // enabled = true;
        OnShow();
    }
    // public void Hide() => root.style.display = DisplayStyle.None;
    public void Hide()
    {
        // root.style.display = DisplayStyle.None;
        // doc.enabled = false;
        gameObject.SetActive(false);
    }

    public void SoftHide()
    {
        // Hide();
        root.style.display = DisplayStyle.None;
    }

    // public void SoftShow()
    // {
    //     root.style.display = DisplayStyle.Flex;
    // }
}

public class HideableDocument<T> : SingletonDocument<T> where T : MonoBehaviour
{
    protected override void Awake()
    {
        base.Awake();
        // Hide();
    }

    // void OnDisable()
    // {
    //     Debug.LogWarning($"OnDisable {GetType()}");
    // }

    void Start()
    {
        Hide();
    }
}