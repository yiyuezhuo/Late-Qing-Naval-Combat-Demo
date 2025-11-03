using StrategicCombatCore;
using UnityEngine.UIElements;
using Unity.Properties;
using NavalCombatCore;
using CoreUtils;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;

// using System.Diagnostics;
using UnityEngine;


public class NavalCombatResolver // Dialog
{
    // parameters
    public VisualElement root;
    // public Cell cell;
    public PendingNavalCombat pendingNavalCombat;

    public Cell cell => pendingNavalCombat.cell;

    // derived states
    public LocalNavalCombatBuilder builder;
    public FullState fullState;
    public ScenarioDynamicSetupGenerator scenarioDynamicSetupGenerator;
    public OneSideState leftSideState;
    public OneSideState rightSideState;

    public EventHandler closed;

    public void Bind()
    {
        builder = new LocalNavalCombatBuilder()
        {
            pendingNavalCombat=pendingNavalCombat,
        };
        // fullState = builder.BuildFullState(cell);
        fullState = builder.BuildFullState();
        scenarioDynamicSetupGenerator = new()
        {
            anchor = new LatLon(cell.latitude, cell.longitude)
        };

        // Build Tree View
        leftSideState = new()
        {
            parent = this,
            sideBuilder = builder.GetSide0(),
            sideRoot = root.Q<VisualElement>("LeftSideContainer"),
        };
        leftSideState.Bind();

        rightSideState = new()
        {
            parent = this,
            sideBuilder = builder.GetSide1(),
            sideRoot = root.Q<VisualElement>("RightSideContainer"),
        };
        rightSideState.Bind();

        root.Q<Button>("ResolveButton").clicked += OnResolve;
    }

    [CreateProperty]
    public string battleName => $"The battle of Cell ({cell.x}, {cell.y})";

    [CreateProperty]
    public string datetimeStr => CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(
        fullState.navalGameState.scenarioState.dateTime
    );
    // public string datetimeStr => fullState.navalGameState.scenarioState.dateTime.ToString();

    public void OnResolve()
    {
        Debug.Log("OnResolve");

        // It should be removed from the pending list once returning from naval tactical game.
        // StrategicGameState.Instance.pendingNavalCombats.RemoveAll(c => c.xy.x == cell.x && c.xy.y == cell.y); // Assume a hex has only at most 1 pending combat.

        TryGotoTacticalNavalCombat();
    }

    public void TryGotoTacticalNavalCombat()
    {
        // var builder = new LocalNavalCombatBuilder();

        // var fullState = builder.BuildFullState(cell);
        if (fullState != null)
        {
            GameManager.startupConfig = new()
            {
                fullState = fullState,
                mode = GameManager.StartupConfig.Mode.FullState,
                scenarioSetupGenerator = new()
                {
                    anchor = new LatLon(cell.latitude, cell.longitude)
                }
            };
            StrategicGameState.Instance.scenarioState.pendingNavalCombatId = pendingNavalCombat.objectId;

            StrategicGameManager.Instance.PrepareReturnFromNavalGame();
            SceneManager.LoadScene("Naval Game");
        }
    }


    public class OneSideState
    {
        // parameters
        public NavalCombatResolver parent;
        public LocalNavalCombatBuilder.LocalNavalCombatBuilderOneSide sideBuilder;
        public VisualElement sideRoot;

        [CreateProperty]
        public StyleBackground countryFlag => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(sideBuilder.GetCountry()));

        [CreateProperty]
        public StyleBackground leaderPortrait => sideBuilder.GetLeader()?.portraitReference.pictureStyleBackground ?? null;

        [CreateProperty]
        public string description
        {
            get
            {
                var shipLogs = sideBuilder.WalkRootGroup<ShipLog>().ToList();
                var shipCounts = shipLogs.Count;
                var shipTons = shipLogs.Sum(s => s?.shipClass.displacementTons);
                return $"{sideBuilder.GetCountry()}\n{sideBuilder.GetLeader()?.name.GetMergedName()}\n{shipCounts} ships\n{shipTons} tons";
            }
        }

        public void Bind()
        {
            // Bind Tree View
            var treeViewBuilder = new UITKTreeViewBuilder<IShipGroupMember, string>()
            {
                tree = sideBuilder.builder
            };
            var nodes = new List<IShipGroupMember>() { sideBuilder.GetRootGroup() };
            var rootItems = treeViewBuilder.CreateTreeViewRootItems(nodes);

            var oobTreeView = sideRoot.Q<TreeView>("OOBTreeView");
            oobTreeView.SetRootItems(rootItems);
            oobTreeView.Rebuild();
            oobTreeView.ExpandAll();

            sideRoot.Q<Button>("WithdrawButton").clicked += () =>
            {
                Debug.Log("WithdrawButton clicked");

                var effectedGroups = sideBuilder.pendingNavalCombatSideState.GetGroups();
                foreach (var group in effectedGroups)
                {
                    group.StartReturnToBase(24);
                }

                StrategicGameState.Instance.RefreshPendingNavalCombats(); // Or just remove the current combat? Refresh would re-assign new id to combats, which may not ideal to me.
                // closed?.Invoke(this, null);
                // var country = sideBuilder.GetCountry();
                parent.closed?.Invoke(this, null);
            };
        }
    }
}