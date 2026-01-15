using YYZ;
using System;
using System.Xml.Serialization;
using UnityEngine;
using NavalCombatCore;
using System.Collections.Generic;

public class ConnectionInfo
{
    public List<string> takeCommandIds = new();
    public NavalNetworkingCommands.MergeRequest mergeRequest;
}

public static class NavalNetworkingCommands
{
    static Type[] serializableCommands = new Type[]
    {
        typeof(ConnectCommand),
        typeof(RequestFullStateSync),
        typeof(FullStateSync),
        typeof(UpdateTakeCommand),
        typeof(MergeRequest),
        typeof(GameStateSync)
        // typeof(AdvanceSimulation)
    };

    public static XmlSerializer networkCommandPackageSerializer = new XmlSerializer(
        typeof(NetworkCommandPackage),
        serializableCommands
    );

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void RegisterNetworkCommandPackageSerializer()
    {
        Debug.Log("RegisterNetworkCommandPackageSerializer");

        NetworkingManager.networkCommandPackageSerializer = networkCommandPackageSerializer;
    }

    public class RequestFullStateSync : NetworkingCommand
    {

        public override void Execute() // Host receive this and extract itself's state to send it to the requested client with FullSync command
        {
            // var detachGameState = GameManager.Instance.detachStreamingAssets; // TODO: Well it's strange to set a flag in the File tab to utilize custom data, maybe set it always false would be better.
            var detachGameState = false;
            var fullState = TopTabs.CaptureFullState(detachGameState);
            var command = new FullStateSync()
            {
                fullState = fullState
            };
            GameManager.Instance.networkingManager?.SendCommand(sourceConnection, command);
        }
    }

    public class FullStateSync : NetworkingCommand
    {
        public FullState fullState;

        public override void Execute()
        {
            var gmr = GameManager.Instance;

            GameManager.startupConfig = new()
            {
                fullState = fullState,
                mode = GameManager.StartupConfig.Mode.FullState
            };

            gmr.StartCoroutine(gmr.CompleteFullStateAndUpdateCoroutine(fullState));
        }
    }

    public class UpdateTakeCommand : NetworkingCommand
    {
        public List<string> takeCommandIds = new();

        public override void Execute()
        {
            var gmr = GameManager.Instance;

            if(!gmr.connectionInfoMap.TryGetValue(sourceConnection, out var connectionInfo))
                connectionInfo = gmr.connectionInfoMap[sourceConnection] = new();
            connectionInfo.takeCommandIds = takeCommandIds;
        }
    }

    public class MergeRequest : NetworkingCommand
    {
        public List<ShipLog> syncShipLogs = new();
        public List<ShipGroup> syncShipGroups = new(); // sync doctrine

        public override void Execute()
        {
            var connInfo = GameManager.Instance.GetConnectionInfo(sourceConnection);
            connInfo.mergeRequest = this;
        }

        public void DoMerge()
        {
            // Use up
            var connInfo = GameManager.Instance.GetConnectionInfo(sourceConnection);
            connInfo.mergeRequest = null;

            // Do Merge
            var gameState = NavalGameState.Instance;
            foreach(var syncShipLog in syncShipLogs)
            {
                var idx = gameState.shipLogs.FindIndex(shipLog => shipLog.objectId == syncShipLog.objectId);
                if(idx != -1)
                {
                    gameState.shipLogs[idx] = syncShipLog;
                }
            }
            foreach(var syncShipGroup in syncShipGroups)
            {
                var idx = gameState.shipGroups.FindIndex(shipGroup => shipGroup.objectId == syncShipGroup.objectId);
                if(idx != -1)
                {
                    gameState.shipGroups[idx] = syncShipGroup;
                }
            }

            gameState.ResetAndRegisterAll();
        }
    }

    // public class AdvanceSimulation : NetworkingCommand
    // {
    //     public int advanceSimulationSeconds;
    //     public int seed;
    //     public 

    //     public override void Execute()
    //     {
    //         var gm = GameManager.Instance;
    //         RandomUtils.SetSeed(seed);

    //         // TODO: Advance Simulation
    //         gm.remainAdvanceSimulationSecondsRequestedByUserInput = advanceSimulationSeconds;
    //     }
    // }

    public class GameStateSync : NetworkingCommand
    {
        public NavalGameState gameState;

        public override void Execute()
        {
            NavalGameState.UpdateInstance(gameState);
        }
    }
}