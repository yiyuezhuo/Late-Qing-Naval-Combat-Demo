using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;


public class GameManager : MonoBehaviour
{
    public NavalGameState navalGameState = new();

    static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if(_instance == null)
                _instance = FindFirstObjectByType<GameManager>();
            return _instance;
        }
    }

    public void OnDestroy()
    {        
        if(_instance == this)
            _instance = null;
    }
}