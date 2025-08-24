using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

public class Overlay : SingletonDocument<Overlay>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;
        Utils.BindItemsSourceRecursive(root);

        root.Q<Button>("ClearLogButton").clicked += () => NavalGameState.Instance.tempSubjectLogs.Clear();
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
