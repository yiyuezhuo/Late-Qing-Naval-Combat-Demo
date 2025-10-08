using UnityEngine;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;

using StrategicCombatCore;
using CoreUtils;
using NavalCombatCore;

public class StrategicTopTabs : SingletonDocument<StrategicTopTabs>
{
    Button advance1DayButton;

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = StrategicGameManager.Instance;

        root.Q<Button>("SaveButton").clicked += () =>
        {
            Debug.Log("SaveButton clicked");

            var gameState = DetachGameState(StrategicGameState.Instance, StreamingAssetReference.Instance);

            IOManager.Instance.SaveTextFile(
                XmlUtils.ToXML(gameState),
                "StrategicGameState", "xml"
            );
        };

        root.Q<Button>("LoadButton").clicked += () =>
        {
            Debug.Log("LoadButton clicked");

            // IOManager.Instance.textLoaded += OnMapXMLLoaded;
            IOManager.Instance.LoadTextFile(OnMapXMLLoaded, "xml");
        };

        root.Q<Button>("GenerateMapButton").clicked += () =>
        {
            Debug.Log("GenerateMapButton clicked");

            var width = StrategicGameManager.Instance.tempMapWidth;
            var height = StrategicGameManager.Instance.tempMapHeight;

            StrategicGameState.Instance.GenerateTerrainMatrix(width, height);
        };

        root.Q<Button>("LeaderEditorButton").clicked += () =>
        {
            LeaderEditor.Instance.Show();
        };

        var shipClassEditorButton = root.Q<Button>("ClassEditorButton");
        shipClassEditorButton.clicked += () => ShipClassEditor.Instance.Show();

        var namedShipEditorButton = root.Q<Button>("NamedShipEditorButton");
        namedShipEditorButton.clicked += NamedShipEditor.Instance.Show;

        var shipLogEditorButton = root.Q<Button>("ShipLogEditorButton");
        shipLogEditorButton.clicked += () => ShipLogEditor.Instance.Show();

        root.Q<Button>("HighCommandEditorButton").clicked += HighCommandEditor.Instance.Show;
        root.Q<Button>("LandUnitTemplateEditorButton").clicked += LandUnitTemplateEditor.Instance.Show;
        root.Q<Button>("LandUnitEditorButton").clicked += LandUnitEditor.Instance.Show;
        root.Q<Button>("WeaponEditorButton").clicked += WeaponEditor.Instance.Show;

        root.Q<Button>("ReturnToMainMenuButton").clicked += () =>
        {
            SceneManager.LoadScene("Main Menu");
        };

        root.Q<Button>("TPSGeoreferencingButton").clicked += () =>
        {
            Debug.Log("TPSGeoreferencingButton is clicked");
            DoTPSGeoreferencing();
        };

        root.Q<Button>("CreateDefaultShipLogButton").clicked += () =>
        {
            var createdObjectIds = StrategicGameState.Instance.shipLogs.Select(shipLog => shipLog.namedShip.objectId).Where(id => id != null && id != "").ToHashSet();

            // StrategicGameState.Instance.shipLogs = StrategicGameState.Instance.namedShips
            StrategicGameState.Instance.shipLogs.AddRange(StrategicGameState.Instance.namedShips
                .Where(namedShip => !createdObjectIds.Contains(namedShip.objectId) && !namedShip.notAvailableForFirstSinoJapaneseWar)
                .Select(namedShip =>
                {
                    Debug.LogWarning($"Create new ship log for: {namedShip.name.GetMergedName()}");

                    var shipLog = new ShipLog();
                    shipLog.namedShipObjectId = namedShip.objectId;
                    return shipLog;
                })
            );

            StrategicGameState.Instance.ResetAndRegisterAll();

            foreach (var shipLog in StrategicGameState.Instance.shipLogs)
            {
                shipLog.ResetDamageExpenditureState();
                if (shipLog.mapState == MapState.NotDeployed) // NotDeployed in strategic game is not defined now
                    shipLog.mapState = MapState.Deployed;
            }

            StrategicGameState.Instance.ResetAndRegisterAll();
        };

        root.Q<Button>("StrategicGroupEditorButton").clicked += StrategicGroupEditor.Instance.Show;

        root.Q<Button>("SideStateButton").clicked += SideStateEditor.Instance.Show;

        root.Q<Button>("GamePreferenceButton").clicked += DialogRoot.Instance.PopupGamePreferenceDialog;

        root.Q<Button>("SubStrategicCombatResolverButton").clicked += () =>
        {
            DialogRoot.Instance.PopupSubStrategicCombatDialog(new());
        };

        root.Q<Button>("SelectSideButton").clicked += () =>
        {
            DialogRoot.Instance.PopupSideStatePickerDialog(sideState =>
            {
                StrategicGameManager.Instance.currentSideStateObjectId = sideState?.objectId;
            });
        };

        root.Q<Button>("Advance1HourButton").clicked += () =>
        {
            StrategicGameState.Instance.Advance1Hour();
        };

        advance1DayButton = root.Q<Button>("Advance1DayButton");
        advance1DayButton.clicked += () =>
        {
            // for (int i = 0; i < 24; i++)
            // {
            //     StrategicGameState.Instance.Advance1Hour();
            // }
            StartCoroutine(Advance1Day());
        };

        root.Q<Button>("SetFogOrWarViewerButton").clicked += () =>
        {
            DialogRoot.Instance.PopupSideStatePickerDialog(sideState =>
            {
                StrategicGameState.Instance.scenarioState.fogOfWarViewerSideObjectId = sideState?.objectId;
            });
        };

        root.Q<Button>("StrategicMissionEditorButton").clicked += () =>
        {
            StrategicMissionEditor.Instance.Show();
        };

        root.Q<Button>("ResetSupplyButton").clicked += () =>
        {
            var gameState = StrategicGameState.Instance;
            foreach (var landUnit in gameState.landUnits)
            {
                landUnit.supplyTons = landUnit.GetSupplyCapTons();
            }
            foreach (var shipLog in gameState.shipLogs)
            {
                shipLog.supplyTons = shipLog.GetSupplyCapTons();
            }
        };
    }

    IEnumerator Advance1Day()
    {
        advance1DayButton.SetEnabled(false);

        for (int i = 0; i < 24; i++)
        {
            StrategicGameState.Instance.Advance1Hour();
            yield return new WaitForSeconds(GamePreference.Instance.dayAdvanceHourIntervalSeconds);
        }

        advance1DayButton.SetEnabled(true);
    }

    void DoTPSGeoreferencing()
    {
        var cellMat = StrategicGameState.Instance.cellMatrix;

        var groundControlPoints = new List<Cell>();
        for (int i = 0; i < cellMat.GetLength(0); i++)
        {
            for (int j = 0; j < cellMat.GetLength(1); j++)
            {
                var cell = cellMat[i, j];
                if (cell.GroundControlPoint)
                {
                    groundControlPoints.Add(cell);
                }
            }
        }

        var src = groundControlPoints.Select(cell =>
        {
            (var x, var y) = HexMapShower.CellXYToLocalXY(cell.x, cell.y);
            return ((double)x, (double)y);
        }).ToList();

        var dst = groundControlPoints.Select(cell =>
        {
            return ((double)cell.longitude, (double)cell.latitude);
        }).ToList();

        var tps = new ThinPlateSpline(src, dst);

        for (int i = 0; i < cellMat.GetLength(0); i++)
        {
            for (int j = 0; j < cellMat.GetLength(1); j++)
            {
                var cell = cellMat[i, j];
                if (!cell.GroundControlPoint)
                {
                    (var x, var y) = HexMapShower.CellXYToLocalXY(cell.x, cell.y);
                    (var longtitude, var latitude) = tps.Transform(x, y);
                    cell.longitude = (float)longtitude;
                    cell.latitude = (float)latitude;
                }
            }
        }
    }

    StrategicGameState DetachGameState(StrategicGameState _s, StreamingAssetReference sar)
    {
        // deep copy
        var s = XmlUtils.FromXML<StrategicGameState>(XmlUtils.ToXML(_s));

        if (sar.leadersPath != null && sar.leadersPath != "")
            s.leaders = null;

        if (sar.shipClassesPath != null && sar.shipClassesPath != "")
            s.shipClasses = null;

        if (sar.namedShipsPath != null && sar.namedShipsPath != "")
            s.namedShips = null;

        return s;
    }


    void OnMapXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnMapXMLLoaded;

        var strategicGameState = XmlUtils.FromXML<StrategicGameState>(text);
        StrategicGameState.Instance.UpdateTo(strategicGameState);
    }
}