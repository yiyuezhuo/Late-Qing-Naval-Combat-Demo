using StrategicCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

public class WorldSpaceGroupIcon : MonoBehaviour
{
    UIDocument doc;
    VisualElement root; // => doc.rootVisualElement;
    public StrategicGroup currentDataSource;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement;
    }

    public void SetDataSource(StrategicGroup group)
    {
        currentDataSource = group;
        root.dataSource = group;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
