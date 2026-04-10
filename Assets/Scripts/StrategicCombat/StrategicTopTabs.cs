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
using YYZ;

public class StrategicTopTabs : SingletonDocument<StrategicTopTabs>
{
    Coroutine advanceCoroutine;
    bool isRealtimeAdvanceCoroutineRunning;

    void DoSave(bool editSave)
    {
        var gameState = DetachGameState(StrategicGameState.Instance, StreamingAssetReference.Instance);
        var fullState = new StrategicFullState()
        {
            gameState = gameState,
            viewState = StrategicGameManager.Instance.CaptureViewState()
        };

        if(editSave)
        {
            gameState.scenarioState.firstLoaded = false;
            fullState.viewState.viewerSideId = null; // Reset the current Viewer setting
        }

        // lastOpenedScenarioPath

        var name = StrategicGameManager.lastOpenedScenarioPath != null ? Path.GetFileNameWithoutExtension(StrategicGameManager.lastOpenedScenarioPath) : "StrategicGameState";

        IOManager.Instance.SaveTextFile(
            XmlUtils.ToXML(fullState),
            name, "xml"
        );
    }

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = StrategicGameManager.Instance;
        Utils.BindItemsSourceRecursive(root);

        root.Q<Button>("SaveButton").clicked += () =>
        {
            Debug.Log("SaveButton clicked");
            DoSave(false);

            // IOManager.Instance.SaveTextFile(
            //     XmlUtils.ToXML(gameState),
            //     "StrategicGameState", "xml"
            // );
        };

        root.Q<Button>("SaveEditButton").clicked += () =>
        {
            DoSave(true);
        };

        root.Q<Button>("LoadButton").clicked += () =>
        {
            Debug.Log("LoadButton clicked");

            // IOManager.Instance.textLoaded += OnMapXMLLoaded;
            IOManager.Instance.LoadTextFile(OnMapXMLLoaded, "xml");
        };

        root.Q<Button>("RestartButton").clicked += () =>
        {
            DialogRoot.Instance.PopupConfirmDialog(Localize(
                "Confirm to restart current scene?"
            ), () =>
            {
                StrategicGameManager.RestartCurrentScene();
            });
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

        // root.Q<Button>("StrategicGroupEditorButton").clicked += StrategicGroupEditor.Instance.Show;
        root.Q<Button>("StrategicGroupEditorButton").clicked += () => SwitchCenter.Instance.SwitchToStrategicGroupView(null);
        root.Q<Button>("EditMoveButton").clicked += StrategicGameManager.Instance.StartToEditMove;

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

        root.Q<Button>("SetFogOrWarViewerButton").clicked += () =>
        {
            DialogRoot.Instance.PopupSideStatePickerDialog(sideState =>
            {
                StrategicGameManager.Instance.viewerSideId = sideState?.objectId;
            });
        };

        root.Q<Button>("QuickViewerSideButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicViewerSideQuickPickerDialog();
        };

        root.Q<Button>("QuickViewerSideButtonCommand").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicViewerSideQuickPickerDialog();
        };

        root.Q<Button>("StrategicMissionEditorButton").clicked += () =>
        {
            StrategicMissionEditor.Instance.Show();
        };

        root.Q<Button>("ResetStrengthSupplyButton").clicked += () =>
        {
            var gameState = StrategicGameState.Instance;

            // Create default and reset shiplog states
            // gameState.CreateDefaultShipLog(); // Since Russo-Japanese War's data is added, it's hard to auto-generate without extra tag.
            gameState.ResetShipLogStates();

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

        root.Q<Button>("ReplanAutoSupplyButton").clicked += () =>
        {
            StrategicGameState.Instance.ReplanAutoSupply(StrategicGameManager.Instance.GetViewerSide());
        };

        root.Q<Button>("PendingNavalCombatsButton").clicked += () =>
        {
            DialogRoot.Instance.PopupPendingNavalCombatDialog();
        };

        root.Q<Button>("LossStatusButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicVictoryStatusDialog();
        };

        root.Q<Button>("OOBTreeButton").clicked += () =>
        {
            var viewableGroups = StrategicGameState.Instance.strategicGroups;

            if(!GamePreference.Instance.isInEditMode)
            {
                var viewerSider = StrategicGameManager.Instance.GetViewerSide();
                if(viewerSider != null)
                {
                    var viewerCountries = viewerSider.countries.ToHashSet();
                    viewableGroups = viewableGroups.Where(g => viewerCountries.Contains(g.country)).ToList();
                }
                else
                {
                    viewableGroups = new();
                }
            }

            DialogRoot.Instance.PopupOOBTreeDialog(viewableGroups);
        };

        root.Q<Button>("StrategicInfluenceMapButton").clicked += DialogRoot.Instance.PopupStrategicInfluenceMapDialog;

        root.Q<Button>("LandBattleEditorButton").clicked += () =>
        {
            LandBattleEditor.Instance.Show();
        };

        root.Q<Button>("StrategicScenarioStateButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicScenarioStateEditorDialog();
        };

        root.Q<Button>("TheaterButton").clicked += () =>
        {
            DialogRoot.Instance.PopupTheaterSelectorDialog();
        };

        root.Q<Button>("RunDebugScriptButton").clicked += () =>
        {
            var navalContactReports = StrategicGameState.Instance.navalContactReports;
            Debug.Log($"navalContactReports.Count = {navalContactReports.Count}"); 
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

    public bool TryStartRealtimeAdvance()
    {
        if (advanceCoroutine != null)
            return isRealtimeAdvanceCoroutineRunning;

        if (CheckHasPendingNavalCombatAndPopupIfAny())
            return false;

        advanceCoroutine = StartCoroutine(AdvanceHoursCoroutine(null, true));
        return true;
    }

    public void StopRealtimeAdvance()
    {
        if (!isRealtimeAdvanceCoroutineRunning)
            return;

        StopAdvanceCoroutine();
    }

    IEnumerator AdvanceHoursCoroutine(int? hourLimit, bool realtimeMode)
    {
        isRealtimeAdvanceCoroutineRunning = realtimeMode;

        var advancedHours = 0;
        while (!hourLimit.HasValue || advancedHours < hourLimit.Value)
        {
            if (CheckHasPendingNavalCombatAndPopupIfAny())
                break;

            if (StrategicGameManager.Instance.currentLogOnly && advancedHours == 0)
                StrategicGameState.Instance.ClearLogs();

            StrategicGameState.Instance.Advance1Hour(StrategicGameManager.Instance.GetViewerSide());
            advancedHours++;
            yield return new WaitForSeconds(StrategicGameManager.Instance.GetStrategicAdvanceIntervalSeconds());
        }

        if (realtimeMode && StrategicGameManager.Instance.isRealtimeAdvancing)
        {
            StrategicGameManager.Instance.isRealtimeAdvancing = false;
            yield break;
        }

        StopAdvanceCoroutine();
    }

    void StopAdvanceCoroutine()
    {
        if (advanceCoroutine != null)
        {
            StopCoroutine(advanceCoroutine);
            advanceCoroutine = null;
        }

        isRealtimeAdvanceCoroutineRunning = false;
    }

    public override void OnDestroy()
    {
        if (StrategicGameManager.Instance != null && StrategicGameManager.Instance.isRealtimeAdvancing)
        {
            StrategicGameManager.Instance.isRealtimeAdvancing = false;
        }

        StopAdvanceCoroutine();
        base.OnDestroy();
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
        var fullState = XmlUtils.FromXML<StrategicFullState>(text);
        StrategicGameManager.SetRestartConfig(new StrategicGameManager.StartupConfig()
        {
            mode = StrategicGameManager.StartupConfig.Mode.FullState,
            fullState = fullState
        });
        StartCoroutine(StrategicGameManager.Instance.ProcessFullState(fullState));
    }
}
