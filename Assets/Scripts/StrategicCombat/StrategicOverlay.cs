using UnityEngine;
using UnityEngine.UIElements;

public class StrategicOverlay : SingletonDocument<StrategicOverlay>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;
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
