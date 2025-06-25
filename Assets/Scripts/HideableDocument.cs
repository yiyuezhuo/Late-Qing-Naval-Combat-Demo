using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
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
                _instance = FindFirstObjectByType<T>();
            return _instance;
        }
    }

    public void OnDestroy()
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
        doc.enabled = true;
        enabled = false; // Hack to invoke OnEnable
        enabled = true;
        OnShow();
    }
    // public void Hide() => root.style.display = DisplayStyle.None;
    public void Hide()
    {
        // root.style.display = DisplayStyle.None;
        doc.enabled = false;
    }
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