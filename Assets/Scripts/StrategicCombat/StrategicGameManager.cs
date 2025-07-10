
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

using CoreUtils;
using StrategicCombatCore;

public enum StrategicMapEditMode
{
    Select,
    PaintTerrain,
    CreateOrEditLabel,
    DeleteLabel,
    PaintHexPairFeatureBegin,
    PaintHexPairFeatureEnd,
    DeleteHexPairFeatureBegin,
    DeleteHexPairFeatureEnd
}

public class StrategicGameManager : SingletonMonoBehaviour<StrategicGameManager>
{
    [CreateProperty]
    public StrategicGameState navalGameState => StrategicGameState.Instance;

    public StrategicMapEditMode mapEditMode;
    public TerrainType currentTerrainType;
    public int tempMapWidth = 60;
    public int tempMapHeight = 40;
    public EdgeFeatureType currentEdgeFeatureType;

    public static string initialScenPath = "Strategic/StrategicGameState.xml";

    [CreateProperty]
    public bool showReferenceMap
    {
        get => HexMapShower.Instance.showReferenceMap;
        set => HexMapShower.Instance.showReferenceMap = value;
    }

    [CreateProperty]
    public bool showBorder
    {
        get => HexMapShower.Instance.showBorder;
        set => HexMapShower.Instance.showBorder = value;
    }

    [CreateProperty]
    public bool showAccurateSeaLand
    {
        get => HexMapShower.Instance.showAccurateSeaLand;
        set => HexMapShower.Instance.showAccurateSeaLand = value;
    }

    public Cell lastSelectedCell;

    void Start()
    {
        var width = tempMapWidth;
        var height = tempMapHeight;

        // Default state
        StrategicGameState.Instance.GenerateTerrainMatrix(width, height);

        // Try to fetch default scenario file and update the state
        StartCoroutine(Utils.FetchFile(initialScenPath, (initialScenText) =>
        {
            var strategicGameState = XmlUtils.FromXML<StrategicGameState>(initialScenText);
            StrategicGameState.Instance.UpdateTo(strategicGameState);
        }));
    }

    public Vector2 ToCenter(Vector2 xy)
    {
        if (xy.x % 2 >= 1)
        {
            return new Vector2(Mathf.Floor(xy.x), Mathf.Floor(xy.y)) + new Vector2(0.5f, 1.0f);
        }
        return new Vector2(Mathf.Floor(xy.x), Mathf.Floor(xy.y)) + new Vector2(0.5f, 0.5f);
    }

    public Vector2Int FromCenter(Vector2 xy)
    {
        if (xy.x % 2 >= 1)
        {
            var _xy = xy - new Vector2(0.5f, 1.0f);
            return new Vector2Int(Mathf.RoundToInt(_xy.x), Mathf.RoundToInt(_xy.y));
        }
        else
        {
            var _xy = xy - new Vector2(0.5f, 0.5f);
            return new Vector2Int(Mathf.RoundToInt(_xy.x), Mathf.RoundToInt(_xy.y));
        }
    }

    public Vector2Int GetCellXY(Vector2 uv)
    {
        var xy = uv * new Vector2(StrategicGameState.Instance.GetMapWidth(), StrategicGameState.Instance.GetMapHeight());

        float minMag = float.MaxValue;
        Vector2 minCenter = Vector2.zero;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var testCenter = ToCenter(xy + new Vector2(dx, dy));
                var testDiff = testCenter - xy;
                if (testDiff.sqrMagnitude < minMag)
                {
                    minMag = testDiff.sqrMagnitude;
                    minCenter = testCenter;
                }
            }
        }

        return FromCenter(minCenter);
    }

    public void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
            {
                var cam = PlaneCameraController.Instance.cam;
                var worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);

                var hit = Physics2D.Raycast(worldPoint, Vector2.zero);
                if (hit.collider != null)
                {
                    Debug.Log($"Hit: {hit.collider} {hit.point}");

                    var localPoint = hit.collider.transform.InverseTransformPoint(hit.point);
                    var uv = new Vector2(localPoint.x + 0.5f, localPoint.y + 0.5f);
                    var cellXY = GetCellXY(uv);

                    Debug.Log($"localPoint={localPoint}, cellXY={cellXY}");

                    if (cellXY.x >= 0 && cellXY.x < StrategicGameState.Instance.GetMapWidth() && cellXY.y >= 0 && cellXY.y < StrategicGameState.Instance.GetMapHeight())
                    {
                        if (mapEditMode == StrategicMapEditMode.PaintTerrain)
                        {
                            StrategicGameState.Instance.SetMapCellTerrain(cellXY.x, cellXY.y, currentTerrainType);

                            Debug.Log($"SetMapCellTerrain({cellXY.x}, {cellXY.y}, {currentTerrainType})");
                        }

                        if (mapEditMode == StrategicMapEditMode.CreateOrEditLabel)
                        {
                            var label = StrategicGameState.Instance.labels.FirstOrDefault(l => l.x == cellXY.x && l.y == cellXY.y);
                            if (label == null)
                            {
                                label = new StrategicLocationLabel
                                {
                                    x = cellXY.x,
                                    y = cellXY.y,
                                    name = new()
                                };
                                StrategicGameState.Instance.labels.Add(label);
                            }

                            DialogRoot.Instance.PopupLocationLabelDialog(label);

                            // Launch temp dialog to edit global string
                            Debug.Log($"CreateOrEditLabel({cellXY.x}, {cellXY.y}, {currentTerrainType})");
                        }

                        if (mapEditMode == StrategicMapEditMode.DeleteLabel)
                        {
                            StrategicGameState.Instance.labels.RemoveAll(l => l.x == cellXY.x && l.y == cellXY.y);
                        }

                        if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureBegin)
                        {
                            lastSelectedCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                            mapEditMode = StrategicMapEditMode.PaintHexPairFeatureEnd;
                        }
                        else if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureEnd)
                        {
                            if (lastSelectedCell != null)
                            {
                                var cell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                                StrategicGameState.Instance.AddEdgeFeature(lastSelectedCell, cell, currentEdgeFeatureType);
                                mapEditMode = StrategicMapEditMode.PaintHexPairFeatureBegin;
                            }
                        }

                        if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureBegin)
                        {
                            lastSelectedCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                            mapEditMode = StrategicMapEditMode.DeleteHexPairFeatureEnd;
                        }
                        else if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureEnd)
                        {
                            if (lastSelectedCell != null)
                            {
                                var cell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                                StrategicGameState.Instance.DeleteEdgeFeature(lastSelectedCell, cell, currentEdgeFeatureType);
                                mapEditMode = StrategicMapEditMode.DeleteHexPairFeatureBegin;
                            }
                        }
                    }

                    // var xnp = localPoint.x + 0.5f;
                    // var ynp = localPoint.y + 0.5f;
                    // var xp = xnp * StrategicGameState.Instance.GetMapLength();
                    // var yp = ynp * StrategicGameState.Instance.GetMapHeight();
                    // var x = Mathf.Round(xp);
                    // var y = Mathf.Round(yp);

                        // Debug.Log($"localPoint={localPoint}, np=({xnp}, {ynp}), p=({xp}, {yp}), xy=({x}, {y})");
                }
            }
        }
    }
}