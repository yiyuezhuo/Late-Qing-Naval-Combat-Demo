using UnityEngine;
using UnityEngine.UIElements;
using StrategicCombatCore;

public class StrategicOverlay : SingletonDocument<StrategicOverlay>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = StrategicGameManager.Instance;
        Utils.BindItemsSourceRecursive(root);

        root.Q<Button>("ClearLogButton").clicked += () => StrategicGameState.Instance.logs.Clear();

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
