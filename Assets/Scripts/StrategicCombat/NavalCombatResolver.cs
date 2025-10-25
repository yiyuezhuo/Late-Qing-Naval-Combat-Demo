using StrategicCombatCore;
using UnityEngine.UIElements;
using Unity.Properties;
using NavalCombatCore;
using CoreUtils;
using System.Linq;

public class NavalCombatResolver
{
    // parameters
    public VisualElement root;
    public Cell cell;

    // derived states
    public LocalNavalCombatBuilder builder;
    public FullState fullState;
    public ScenarioDynamicSetupGenerator scenarioDynamicSetupGenerator;
    public OneSideState leftSideState;
    public OneSideState rightSideState;

    public void Bind()
    {
        builder = new LocalNavalCombatBuilder();
        fullState = builder.BuildFullState(cell);
        scenarioDynamicSetupGenerator = new()
        {
            anchor = new LatLon(cell.latitude, cell.longitude)
        };

        // Build Tree View
        leftSideState = new()
        {
            sideBuilder = builder.GetSide0(),
            sideRoot = root.Q<VisualElement>("LeftSideContainer"),
        };
        leftSideState.Bind();

        rightSideState = new()
        {
            sideBuilder = builder.GetSide1(),
            sideRoot = root.Q<VisualElement>("RightSideContainer"),
        };
        rightSideState.Bind();
    }

    [CreateProperty]
    public string battleName => $"The battle of Cell ({cell.x}, {cell.y})";

    [CreateProperty]
    public string datetimeStr => fullState.navalGameState.scenarioState.dateTime.ToString();

    public void OnConfirm()
    {

    }

    public class OneSideState
    {
        // parameters
        public LocalNavalCombatBuilder.LocalNavalCombatBuilderOneSide sideBuilder;
        public VisualElement sideRoot;

        // public StyleBackground leaderPortrait => EntityManager.Instance.Get<Leader>(
        //     groupRoot?.leader?.objectId
        // )?.portraitReference?.pictureStyleBackground ?? null;

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
                return $"{sideBuilder.GetCountry()}\n{shipCounts} ships\n{shipTons} tons";
            }
        }
        // Group Root Reference

        public void Bind()
        {
            // Bind Tree View
        }
    }

}