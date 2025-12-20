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
using System.IO;

public class StrategicTopTabs : SingletonDocument<StrategicTopTabs>
{
    Button advance1DayButton;

    protected override void Awake()
    {
        base.Awake();

        // root.dataSource = StrategicGameManager.Instance;

        root.Q<Button>("SaveButton").clicked += () =>
        {
            Debug.Log("SaveButton clicked");

            var gameState = DetachGameState(StrategicGameState.Instance, StreamingAssetReference.Instance);
            var fullState = new StrategicFullState()
            {
                gameState = gameState,
                viewState = StrategicGameManager.Instance.CaptureViewState()
            };

            // lastOpenedScenarioPath

            var name = StrategicGameManager.lastOpenedScenarioPath != null ? Path.GetFileNameWithoutExtension(StrategicGameManager.lastOpenedScenarioPath) : "StrategicGameState";

            IOManager.Instance.SaveTextFile(
                XmlUtils.ToXML(fullState),
                name, "xml"
            );

            // IOManager.Instance.SaveTextFile(
            //     XmlUtils.ToXML(gameState),
            //     "StrategicGameState", "xml"
            // );
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
            // if(DialogRoot.D)
            DialogRoot.Instance.PopupConfirmDialog(Localize(
                "Confirm to return to main menu?"
            ), () =>
                {
                    SceneManager.LoadScene("Main Menu");
                }
            );
        };

        root.Q<Button>("TPSGeoreferencingButton").clicked += () =>
        {
            Debug.Log("TPSGeoreferencingButton is clicked");
            DoTPSGeoreferencing();
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
            if (CheckHasPendingNavalCombatAndPopupIfAny())
                return;

            if (StrategicGameManager.Instance.currentLogOnly)
                StrategicGameState.Instance.ClearLogs();

            StrategicGameState.Instance.Advance1Hour();
        };

        advance1DayButton = root.Q<Button>("Advance1DayButton");
        advance1DayButton.clicked += () =>
        {            
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

        root.Q<Button>("ResetStrengthSupplyButton").clicked += () =>
        {
            var gameState = StrategicGameState.Instance;

            // Create default and reset shiplog states

            gameState.CreateDefaultAndResetShipLogStates();

            // Reset Strength

            foreach (var landUnit in gameState.landUnits)
            {
                // tempfix
                // landUnit.strength = landUnit.stregnth;
                // var template = landUnit.GetLandUnitTemplate();
                // if (template != null && landUnit.strength != 0 && landUnit.strength != template.strength
                //     && !(landUnit.strength == 500 && template.strength == 505))
                // {
                //     landUnit.strengthManualOverride = true;
                // }

                var template = landUnit.GetLandUnitTemplate();

                if (template != null && !landUnit.strengthManualOverride)
                {
                    landUnit.strength = template.strength;
                }
            }

            // Reset Supply

            foreach (var landUnit in gameState.landUnits)
            {
                landUnit.supplyTons = landUnit.GetSupplyCapTons();
            }
            foreach (var shipLog in gameState.shipLogs)
            {
                shipLog.supplyTons = shipLog.GetSupplyCapTons();
            }
        };

        root.Q<Button>("PendingNavalCombatsButton").clicked += () =>
        {
            DialogRoot.Instance.PopupPendingNavalCombatDialog();
        };

        root.Q<Button>("OOBTreeButton").clicked += () =>
        {
            DialogRoot.Instance.PopupOOBTreeDialog();
        };

        root.Q<Button>("LandBattleEditorButton").clicked += () =>
        {
            LandBattleEditor.Instance.Show();
        };

        root.Q<Button>("StrategicScenarioStateButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicScenarioStateEditorDialog();
        };
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    bool CheckHasPendingNavalCombatAndPopupIfAny()
    {
        if (GamePreference.Instance.forcedNavalCombatResolution &&
            StrategicGameState.Instance.pendingNavalCombats.Count > 0)
        {
            DialogRoot.Instance.PopupPendingNavalCombatDialog();
            return true;
        }
        return false;
    }

    IEnumerator Advance1Day()
    {
        advance1DayButton.SetEnabled(false);

        for (int i = 0; i < 24; i++)
        {
            if (CheckHasPendingNavalCombatAndPopupIfAny())
                break;
            
            if(StrategicGameManager.Instance.currentLogOnly && i == 0)
                StrategicGameState.Instance.ClearLogs();

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

        // var strategicGameState = XmlUtils.FromXML<StrategicGameState>(text);
        // StrategicGameState.Instance.UpdateTo(strategicGameState);

        StartCoroutine(StrategicGameManager.Instance.OnScenTextLoaded(text));
    }
}