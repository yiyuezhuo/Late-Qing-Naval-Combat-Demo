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
        if (_instance == this)
            _instance = null;
    }
}

public class SingletonDocument<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
{
    protected VisualElement root;

    protected virtual void Awake()
    {
        var doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement;
    }

    public void Show() => root.style.display = DisplayStyle.Flex;
    public void Hide() => root.style.display = DisplayStyle.None;
}

public class HideableDocument<T> : SingletonDocument<T> where T : MonoBehaviour
{
    protected override void Awake()
    {
        base.Awake();
        Hide();
    }
}