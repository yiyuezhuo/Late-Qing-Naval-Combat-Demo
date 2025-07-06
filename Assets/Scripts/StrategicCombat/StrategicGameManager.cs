
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Properties;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;
using System.Collections;

using StrategicCombatCore;

public enum StrategicMapEditMode
{
    Select,
    Paint,
}

public class StrategicGameManager : SingletonMonoBehaviour<StrategicGameManager>
{
    [CreateProperty]
    public StrategicGameState navalGameState => StrategicGameState.Instance;

    public StrategicMapEditMode mapEditMode;
    public TerrainType currentTerrainType;
    public int tempMapWidth;
    public int tempMapHeight;
}