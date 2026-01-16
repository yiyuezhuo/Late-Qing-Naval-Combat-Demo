using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Serialization;

namespace YYZ
{
    [XmlInclude(typeof(ConnectCommand))]
    public class NetworkingCommand
    {
        [XmlIgnore]
        public NetworkingManager.Connection sourceConnection; // Optional

        public virtual void Execute()
        {
            
        }

        public virtual bool isUndoAble{get=>false;}

        public virtual void Undo(){}
    }

    // Client send ConnectCommand to the server and the server execute it.
    public class ConnectCommand: NetworkingCommand
    {
        public override void Execute()
        {
            // Executed by the first connected server
            YDebug.Log($"Server FirstConnectCommand");

            sourceConnection.manager.NotifyConnectionsChanged(); // Update Name
        }

        public override string ToString()
        {
            return $"FirstConnectCommand()";
        }
    }


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
        private readonly object sendLock = new();

        public class Connection
        {
            public NetworkingManager manager;
            public TcpClient client;
            public string name;
        }

        public void Update() // It's expected called from MonoBehaviour's Update
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

        public void NotifyConnectionsChanged()
        {
            executionQueue.Enqueue(() => connectionsChanged?.Invoke(this, EventArgs.Empty));
        }


        protected void ConnectionWorker(TcpClient client, bool sendConnectToCommand)
        {
            var connection = new Connection(){
                manager=this,
                client=client,
                name="Connecting..."
            }; // host, name should be resolved by the first/second connect command, which may be updated by other command as well
            connections.Add(connection);

            NotifyConnectionsChanged();

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
                        // idx += Math.Min(buffer.Length, messageLength);
                        idx += readLength;
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

            // connectionsChanged?.Invoke(this, EventArgs.Empty);
            NotifyConnectionsChanged();

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
                    command.sourceConnection = connection;

                    executionQueue.Enqueue(() => command.Execute());
                }
            }
        }

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

            lock(sendLock)
            {
                stream.Write(messageLengthBytes);
                stream.Write(messageBytes);
            }
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

        public void SendCommandToAll(NetworkingCommand command)
        {
            foreach(var conn in connections)
            {
                SendCommand(conn, command);
            }
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
        }
    }

    public class NetworkingClientManager : NetworkingManager
    {
        public TcpClient ConnectTo(string host)
        {
            var ipAndPort = host.Split(":");
            var ip = ipAndPort[0];
            var port = ushort.Parse(ipAndPort[1]);

            TcpClient client;
            try{
                client = new TcpClient(ip, port);
                // SendCommand(client.GetStream(), new ConnectCommand(){clientName=myName});
            }catch(Exception ex){
                YDebug.LogError($"Connection establishment failed: {ex}");
                return null;
            }

            var thread = new Thread(() => ConnectionWorker(client, true));
            thread.Start();
            
            YDebug.Log($"Connect to {client.Client.RemoteEndPoint}");

            return client;
        }
    }

}