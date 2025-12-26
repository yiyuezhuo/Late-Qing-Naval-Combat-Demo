using StrategicCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

public interface IWorldSpaceGroupIconDataSource
{
    string sizeStr{get;}
    StyleBackground typeIcon{get;}
    Color countryColor{get;}
    string bottomLabelText{get;}
}

public interface ILayableWorldSpaceGroupIconDataSource : IWorldSpaceGroupIconDataSource
{
    // bool IsOnGridCell();
    // bool IsOnAreaCell();

    // int x{get;}
    // int y{get;}
    // string areaCellObjectId{get;}

    SideState side{get;}
    Cell cell{get;}

}

// For prompting in the UITK builder 
public class WorldSpaceGroupIconDatasourcePlaceholder : IWorldSpaceGroupIconDataSource
{
    [CreateProperty]
    public string sizeStr{get;}

    [CreateProperty]
    public StyleBackground typeIcon{get;}

    [CreateProperty]
    public Color countryColor{get;}

    [CreateProperty]
    public string bottomLabelText{get;}

    // [CreateProperty]
    // public DisplayStyle opacity{get;}
}

public class WorldSpaceGroupIcon : MonoBehaviour
{
    UIDocument doc;
    VisualElement root; // => doc.rootVisualElement;
    public IWorldSpaceGroupIconDataSource currentDataSource;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement;
    }

    public void SetDataSource(IWorldSpaceGroupIconDataSource group)
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
