using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Serialization;
using YYZ;

[XmlInclude(typeof(ConnectCommand))]
public class NetworkingCommand
{
    [XmlIgnore]
    public TcpClient currentClient; // Optional

    // public virtual void ExecuteWithConnection(NetworkingManager.Connection connection)
    // {
    //     Execute();
    // }

    public virtual void Execute()
    {
        
    }

    public virtual bool isUndoAble{get=>false;}

    public virtual void Undo(){}
}

// Client send ConnectCommand to the server and the server execute it.
public class ConnectCommand: NetworkingCommand
{
    // public string clientName;
    // public string clientHost;
    // public bool first;

    public override void Execute()
    {
        // Executed by the first connected server
        YDebug.Log($"Server FirstConnectCommand");
    }

    public override string ToString()
    {
        return $"FirstConnectCommand()";
    }
}


// public static class Command
// {
//     public abstract class StateCommand: NetworkingCommand
//     {
//         public override bool isUndoAble{get=>true;}
//     }
// }


/// <summary>
/// Package containing multiple networking commands with sender information.
/// </summary>
public class NetworkCommandPackage
{
    // public string senderHost = "Unknown sender host";
    public string senderName = "Unknown sender name";
    public List<NetworkingCommand> commands = new();

    public override string ToString()
    {
        var desc = string.Join(",", commands.Select(c => c.ToString()));
        return $"NetworkCommandPackage({desc})";
    }
}

public class NetworkingManager
{
    public List<Connection> connections = new();
    protected Queue<Action> executionQueue = new();
    public string myName;

    public event EventHandler connectionsChanged;

    // public event 

    public class Connection
    {
        public TcpClient client;
        // public Thread thread;
        // public string host;
        public string name;
    }

    
    // public List<Connection> connectionsActive = new();

    public void Update()
    {
        lock(executionQueue)
        {
            while(executionQueue.Count > 0)
            {
                var action = executionQueue.Dequeue();
                action();
            }
        }
    }


    protected void ConnectionWorker(TcpClient client, bool sendConnectToCommand)
    {
        var connection = new Connection(){client=client,
            // host="Unknown host", // host is the "server" address of a client (peer), not the "direct" host address.
            name="Connecting..."
        }; // host, name should be resolved by the first/second connect command, which may be updated by other command as well
        connections.Add(connection);

        if(sendConnectToCommand)
        {
            SendCommand(connection, new ConnectCommand());
        }
        
        try
        {
            var buffer = new byte[10240];
            var stream = client.GetStream();
            while(true) // per message
            {
                // Read message length from the message header
                var ok = ReadBytes(stream, buffer, 4); 
                if(!ok)
                    break;
                var messageLength = BitConverter.ToInt32(buffer, 0);
                var idx = 0;

                // Read the message body according to the given length
                var messageBytes = new byte[messageLength];
                while(idx < messageLength)
                {
                    var readLength = Math.Min(buffer.Length, messageLength - idx);
                    ok = ReadBytes(stream, buffer, readLength);
                    if(!ok)
                        break;
                    Buffer.BlockCopy(buffer, 0, messageBytes, idx, readLength);
                    idx += Math.Min(buffer.Length, messageLength);
                }
                
                // Process the message
                ProcessReceivedBytes(connection, messageBytes);
            }
        }
        catch(Exception e)
        {
            YDebug.LogWarning($"ConnectionWorker: {e}"); // may be caused by network error, peer closed the connection, or other exception
        }

        // Handle Close
        
        connections.RemoveAll(conn => conn == connection);
        // connectionsPassive.RemoveAll(conn => conn.client == client);
        // executionQueue.Enqueue(() => SyncListView());
        // HandleClientEnd(client);

        connectionsChanged?.Invoke(this, EventArgs.Empty);

        client.Close();
    }

    protected virtual void ProcessReceivedBytes(Connection connection, byte[] messageBytes)
    {
        var text = Encoding.UTF8.GetString(messageBytes);
        var package = DeserializeNetworkCommandPackage(text);
        
        connection.name = package.senderName;
        // var connection = connections.FirstOrDefault(c => c.client == client);

        lock(executionQueue)
        {
            foreach(var command in package.commands)
            {
                command.currentClient = connection.client;
                // executionQueue.Enqueue(() => command.Execute()); // Command would be executed in the main thread
            
                // var connection = connections.FirstOrDefault(c => c.client == client);
                // executionQueue.Enqueue(() => command.ExecuteWithConnection(connection));
                executionQueue.Enqueue(() => command.Execute());
            }

            // TODO: Notify command received?
            // if(packages.commands.Count > 0)
            //     executionQueue.Enqueue(() => GameManager.Instance.majorChanged.Invoke(GameManager.Instance));
        }
    }

    // protected virtual void HandleClientEnd(TcpClient client)
    // {
        
    // }

    static bool ReadBytes(NetworkStream stream, byte[] buffer, int n)
    {
        var idx = 0;
        while(n > 0)
        {
            var bytesRead = stream.Read(buffer, idx, n);
            if(bytesRead == 0)
            {
                YDebug.Log("Stream Read failed"); // Connection closed by self, peer or other reasons
                return false;
            }
            idx += bytesRead;
            n -= bytesRead;
        }
        return true;
    }

    static Type[] builtinCommands = new Type[]
    {
        typeof(ConnectCommand),
    };

    // This should be replaced if caller extend more commands
    public static XmlSerializer networkCommandPackageSerializer = new XmlSerializer(
        typeof(NetworkCommandPackage), builtinCommands
    );

    static NetworkCommandPackage DeserializeNetworkCommandPackage(string serializedXml)
    {
        using(var reader = new System.IO.StringReader(serializedXml))
        {
            var package = (NetworkCommandPackage)networkCommandPackageSerializer.Deserialize(reader);
            return package;
        }
        // return XmlUtils.FromXML<NetworkCommandPackage>(serializedXml);
    }

    public static string SerializeNetworkCommandPackage(NetworkCommandPackage package)
    {
        using(var textWriter = new System.IO.StringWriter())
        {
            networkCommandPackageSerializer.Serialize(textWriter, package);
            return textWriter.ToString();
        }
        // return XmlUtils.ToXML(package);
    }

    public void SendPackage(NetworkStream stream, NetworkCommandPackage package)
    {
        var serialized = SerializeNetworkCommandPackage(package);
        var messageBytes = Encoding.UTF8.GetBytes(serialized);
        var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);

        stream.Write(messageLengthBytes);
        stream.Write(messageBytes);
    }

    public void SendCommand(NetworkStream stream, NetworkingCommand command)
    {
        var package = new NetworkCommandPackage(){
            // senderHost=GetMyHost(),
            senderName=myName,
            commands=new List<NetworkingCommand>(){command}
        };
        SendPackage(stream, package);
    }

    public void SendCommand(TcpClient client, NetworkingCommand command) => SendCommand(client.GetStream(), command);
    public void SendCommand(Connection connection, NetworkingCommand command) => SendCommand(connection.client, command);

    public void CloseAllConnections()
    {
        foreach(var connection in connections)
        {
            connection.client.Close();
        }
        connections.Clear();
    }
}

public class NetworkingHostManager : NetworkingManager
{
    TcpListener tcpListener;
    Thread listeningThread;

    public void StartHostServer(IPAddress localaddr, int port)
    {
        tcpListener = new TcpListener(localaddr, port);

        try{
            tcpListener.Start();
        }
        catch(SocketException ex)
        {
            YDebug.LogError($"Failed to bind to {localaddr} {port}: {ex}");
            return;
        }

        listeningThread = new Thread(ListenThreadWorker);
        listeningThread.Start();

        // TODO: Notify List changed
        // connectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StartHostServer(string localaddr, int port) => StartHostServer(IPAddress.Parse(localaddr), port);

    void ListenThreadWorker()
    {
        while(true)
        {
            try{
                var client = tcpListener.AcceptTcpClient();
                var thread = new Thread(() => ConnectionWorker(client, true));
                thread.Start();

                // var connection = new Connection(){client=client, thread=thread,
                //     // host="Unknown host", // host is the "server" address of a client (peer), not the "direct" host address.
                //     name="Connecting..."
                // }; // host, name should be resolved by the first/second connect command, which may be updated by other command as well
                // connections.Add(connection);
                
                YDebug.Log($"Accepted a connection from {client.Client.RemoteEndPoint}");
            }
            catch(SocketException ex)
            {
                YDebug.LogWarning($"Failed to accept a connection: {ex}");
                break;
            }
        }

        // Handle Close
        tcpListener.Stop();
        
        tcpListener = null;
        listeningThread = null;

        // executionQueue.Enqueue(() => SyncListView());

        // tcpListener.Stop();
    }
}

public class NetworkingClientManager : NetworkingManager
{
    public void ConnectTo(string host)
    {
        // if(host2connection.ContainsKey(host))
        // {
        //     return;
        // }
        // if(connectionsActive.Any(c => c.host == host))
        //     return;

        var ipAndPort = host.Split(":");
        var ip = ipAndPort[0];
        var port = ushort.Parse(ipAndPort[1]);

        // var endpoint = NetworkEndpoint.Parse(ip, port);

        // TODO: Block repeated connection here.

        TcpClient client;
        try{
            client = new TcpClient(ip, port);
            // SendCommand(client.GetStream(), new ConnectCommand(){clientName=myName});
            // FIXME: Sometimes it should send SecondConnectCommand instead
        }catch(Exception ex){
            YDebug.LogError($"Connection establishment failed: {ex}");
            return;
        }

        var thread = new Thread(() => ConnectionWorker(client, true));
        thread.Start();
        // var connection = new Connection(){client=client, name=myName};
        // connections.Add(connection);
        // executionQueue.Enqueue(() =>
        // {
        //     var connection = new Connection(){client=client, thread=thread, host=host, name=myName};
        //     connectionsActive.Add(connection);
        //     // SyncListView();
        // });
        
        YDebug.Log($"Connect to {client.Client.RemoteEndPoint}");

        // host2connection[host] = connection;
        // connectToRecords.Add(new PlayerRecord(){host=host, name="Connecting..."});
        // connection.host = host;
        
        // TODO: Notify Connection changed
        // SyncListView();
    }
}