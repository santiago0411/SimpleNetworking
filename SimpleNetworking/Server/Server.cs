using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimpleNetworking.Exceptions;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    /// <summary>The protocols the server can use.</summary>
    public enum ServerProtocol { Tcp, Udp, Both }

    /// <summary>The operations that can fail and raise a NetworkOperationFailedCallback.</summary>
    public enum FailedOperation { ConnectTcp, SendDataTcp, ReceiveDataTcp, SendDataUdp }

    public sealed class Server
    {
        /// <summary>The port this server is listening on.</summary>
        public ushort Port => Options.Port;

        private Thread internalThread;
        private TcpListener tcpListener;

        internal UdpClient UdpListener { get; private set; }
        internal ServerOptions Options { get; }
        internal InternalLogger Logger { get; private set; }

        private bool running;
        
        private readonly Dictionary<int, ServerClient> clients = new Dictionary<int, ServerClient>();
        private readonly ServerClientsIdAssigner idAssigner;

        public Server(ushort port, Action<int, Packet> dataReceivedCallback)
            : this(new ServerOptions
            {
                Port = port,
                DataReceivedCallback = dataReceivedCallback
            })
        {
        }

        /// <summary> Initializes the server with the options received.</summary>
        /// <exception cref="ArgumentNullException">Thrown when options or one of the options is null.</exception>
        /// <exception cref="InvalidOptionsException">Thrown when one of the required options has an invalid value.</exception>
        public Server(ServerOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            if (options.IPAddress is null)
                throw new ArgumentNullException(nameof(options.IPAddress));

            if (options.DataReceivedCallback is null)
                throw new ArgumentNullException(nameof(options.DataReceivedCallback));

            if (options.ReceiveDataBufferSize <= 0)
                throw new InvalidOptionsException("The ReceiveDataBufferSize value cannot be smaller than 1.");

            if (options.SendDataBufferSize <= 0)
                throw new InvalidOptionsException("The SendDataBufferSize value cannot be smaller than 1.");

            if (options.ReceiveDataTimeout < 0)
                throw new InvalidOptionsException("The ReceiveDataTimeout value cannot be smaller than 0.");

            if (options.SendDataTimeout < 0)
                throw new InvalidOptionsException("The SendDataTimeout value cannot be smaller than 0.");

            if ((int)options.Protocol > 2)
                throw new InvalidOptionsException("Protocol does not contain a valid option.");

            if (options.InternalThreadRefreshRate < 0)
                throw new InvalidOptionsException("The MainThreadRefreshRate value cannot be smaller than 0.");

            if (options.DataReceivedCallback is null)
                throw new InvalidOptionsException("The DataReceivedCallback cannot be null because data will not be able to be sent back.");

            if (options.Protocol == ServerProtocol.Udp && options.UDPDataReceivedCallback is null)
                throw new InvalidOptionsException("The server protocol is set to UDP only and UDPDataReceivedCallback is null. Server will not be able to send back UDP data.");

            if (!options.RequireClientToSendIdInUdpData && options.UDPDataReceivedCallback is null)
                throw new InvalidOptionsException("The RequireClientToSendIdInUdpData flag is set to FALSE and UDPDataReceivedCallback is null. Server will not be able to send back UDP data.");

            Options = options;
            idAssigner = new ServerClientsIdAssigner(Options.MaxClients);

            Logger = new InternalLogger(options.Logger);
        }

        public void SetLogger(ILogger logger)
        {
            Logger = new InternalLogger(logger);
        }

        /// <summary>Launches the main thread and prompts the server to start listening for connections using the selected protocols.</summary>
        public void Listen()
        {
            StartThread(() =>
            {
                Logger.Info($"Starting server using protocol: '{Options.Protocol}' on port: {Options.Port}.");

                if (Options.Protocol != ServerProtocol.Udp)
                {
                    Logger.Info("Starting TCP listener...");
                    tcpListener = new TcpListener(Options.IPAddress, Port);
                    tcpListener.Start();
                    tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
                }

                if (Options.Protocol != ServerProtocol.Tcp)
                {
                    Logger.Info("Starting UDP listener...");
                    UdpListener = new UdpClient(Port);
                    UdpListener.BeginReceive(UdpReceiveCallback, null);
                }
            });
        }

        /// <summary>Stops both the TCP and UDP listeners.</summary>
        public void Stop()
        {
            Logger.Info("Disconnecting all clients.");
            
            var clientsList = new List<ServerClient>(clients.Values);
            
            foreach (var client in clientsList)
            {
                client.Disconnect(false);
                idAssigner.FreeId(client.Id);
            }

            Logger.Info("Stopping TCP and UDP listeners...");
            tcpListener?.Stop();
            UdpListener?.Close();
            Logger.Info("TCP and UDP listeners have been stopped.");

            running = false;
            Logger.Info("Stopping main thread and joining...");
            internalThread.Join();
        }

        /// <summary>Returns the client info for the specified client id. Returns null if no client with that id is found.</summary>
        public ClientInfo GetClientInfo(int clientId)
        {
            if (clients.TryGetValue(clientId, out ServerClient client))
                return client?.ClientInfo;

            return null;
        }


        internal void RemoveClient(int id)
        {
            clients.Remove(id);
            idAssigner.FreeId(id);
        }

        /// <summary>Disconnects a client with the specified client from the server.</summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="protocol">The protocol to disconnect.</param>
        /// <returns> Returns false if no client with that id is found.</returns>
        public bool DisconnectClient(int clientId, ServerProtocol protocol)
        {
            if (clients.TryGetValue(clientId, out ServerClient client))
            {
                Logger.Debug($"Disconnecting client with id: {clientId} on protocol: {protocol}");

                switch (protocol)
                {
                    case ServerProtocol.Tcp:
                        client.Tcp?.Disconnect();
                        break;
                    case ServerProtocol.Udp:
                        client.Udp?.Disconnect();
                        break;
                    case ServerProtocol.Both:
                        client.Disconnect();
                        break;
                }
                
                return true;
            }

            return false;
        }

        /// <summary>Sends a packet via TCP.</summary>
        /// <param name="toClient">The client to which the packet should be sent.</param>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTcp(int toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                if (!client.IsConnectedTcp())
                {
                    Logger.Error($"Cannot send TCP data to client: {toClient} because it doesn't have an active TCP connection.");
                    return;
                }

                Logger.Debug($"Sending TCP data to client with id: {toClient}.");
                client.Tcp.SendData(packet);
                return;
            }

            Logger.Error($"Cannot send TCP data to client: {toClient} because it doesn't exist.");
        }

        /// <summary>Sends a packet via TCP to all the connected clients.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTcpToAll(Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            Logger.Debug("Sending TCP data to all connected clients.");

            foreach (var client in clients.Values)
            {
                if (client.IsConnectedTcp())
                    client.Tcp.SendData(packet);
            }
        }

        /// <summary>Sends a packet via TCP to all the connected clients except a specific one.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="exceptClient">The client to skip sending the data.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTcpToAll(Packet packet, int exceptClient)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            Logger.Debug($"Sending TCP data to all connected clients except client: {exceptClient}.");

            foreach (var client in clients.Values)
            {
                if (client.IsConnectedTcp() && client.Id != exceptClient)
                    client.Tcp.SendData(packet);
            }
        }

        /// <summary>Sends a packet via UDP.</summary>
        /// <param name="toClient">The client to which the packet should be sent.</param>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use UDP.</exception>
        public void SendPacketUdp(int toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                Logger.Debug($"Sending UDP data to client with id: {toClient}.");
                client.Udp.SendData(packet);
                return;
            }

            Logger.Error($"Cannot send UDP data to client: {toClient} because it doesn't exist.");
        }

        /// <summary>Sends a packet via UDP to all the connected clients.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketUdpToAll(Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            Logger.Debug("Sending UDP data to all connected clients.");

            foreach (var client in clients.Values)
                client.Udp.SendData(packet);
        }

        /// <summary>Sends a packet via TCP to all the connected clients except a specific one.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="exceptClient">The client to skip sending the data.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketUdpToAll(Packet packet, int exceptClient)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            Logger.Debug($"Sending UDP data to all connected clients except client: {exceptClient}.");

            foreach (var client in clients.Values)
            {
                if (client.Id != exceptClient)
                    client.Udp.SendData(packet);
            }
        }

        private void StartThread(Action startServer)
        {
            Logger.Debug("Starting new thread.");
            internalThread = new Thread(() =>
            {
                startServer();
                DateTime nextLoop = DateTime.Now;
                running = true;
                while (running)
                {
                    while (nextLoop < DateTime.Now)
                    {
                        nextLoop = nextLoop.AddMilliseconds(Options.InternalThreadRefreshRate);

                        if (nextLoop > DateTime.Now)
                            Thread.Sleep(nextLoop - DateTime.Now);
                    }
                }
            });

            internalThread.Start();
        }

        private void TcpConnectCallback(IAsyncResult result)
        {
            Logger.Debug("New TCP connection received.");

            TcpClient client = tcpListener.EndAcceptTcpClient(result);
            tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

            if (!idAssigner.FindAvailableId(out int availableId))
            {
                Logger.Debug("Could not find an available id to create a client. Server is full!");
                Logger.Debug("Invoking ServerIsFullCallback.");
                Options.ServerIsFullCallback?.Invoke();
                client.Close();
                client.Dispose();
                return;
            }

            var serverClient = new ServerClient(availableId, this)
            {
                ClientInfo = new ClientInfo(availableId, client)
            };

            if (ApproveNewConnection(serverClient.ClientInfo))
            {
                FinishProcessingAcceptedTcpClient(serverClient, client);
                return;
            }

            client.Close();
            client.Dispose();
            Logger.Info("The new client has been refused by the ClientConnectCallback and the connection has been closed.");
        }

        private bool ApproveNewConnection(ClientInfo clientInfo)
        {
            Logger.Debug("Invoking ClientConnectedCallback.");
            bool? accepted = Options.ClientConnectedCallback?.Invoke(clientInfo);
            return accepted ?? true;
        }

        private void FinishProcessingAcceptedTcpClient(ServerClient serverClient, TcpClient tcpClient)
        {
            serverClient.Tcp.Connect(tcpClient);
            serverClient.ClientInfo.HasActiveTcpConnection = true;
            clients.Add(serverClient.Id, serverClient);

            Logger.Debug("Invoking ClientAcceptedCallback.");
            Options.ClientConnectionEstablishedCallback?.Invoke(serverClient.ClientInfo);
        }

        private void UdpReceiveCallback(IAsyncResult result)
        {
            Logger.Debug("New UDP data received.");

            IPEndPoint clientEndPoint; 
            byte[] data;

            try
            {
                clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                data = UdpListener.EndReceive(result, ref clientEndPoint);
                UdpListener.BeginReceive(UdpReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error receiving UDP data.\n{ex}");
                return;
            }

            using var packet = new Packet(data);

            if (!Options.RequireClientToSendIdInUdpData)
            {
                Logger.Debug("RequireClientToSendIdInUdpData is set to false, data will not be processed because it can't be identified.");
                Logger.Debug("Invoking UDPDataReceivedCallback");
                Options.UDPDataReceivedCallback?.Invoke(packet);
                return;
            }

            Logger.Debug("Reading client id from data received.");

            if (data.Length < 4)
            {
                Logger.Warn("Failed to read client id, data does not have enough bytes. Packet will not be processed.");
                return;
            }

            int clientId = packet.ReadInt();

            if (clients.TryGetValue(clientId, out ServerClient serverClient))
            {
                if (serverClient.ClientInfo is null)
                {
                    Logger.Debug($"There is no client {clientId} with an active TCP connection.");
                    return;
                }

                if (serverClient.Udp.EndPoint is null)
                {
                    if (!serverClient.ClientInfo.TcpEndPoint.Address.Equals(clientEndPoint.Address))
                    {
                        Logger.Warn($"{clientEndPoint} is trying to establish a UDP connection impersonating client: {serverClient.Id} with IP: {serverClient.ClientInfo.TcpEndPoint.Address}.");
                        return;
                    }

                    Logger.Debug($"First packet ever received from client {clientId}, setting EndPoint.");
                    serverClient.Udp.Connect(clientEndPoint);
                    serverClient.ClientInfo.HasActiveUdpConnection = true;
                    serverClient.ClientInfo.UdpEndPoint = clientEndPoint;
                }

                if (serverClient.Udp.EndPoint.Equals(clientEndPoint) && packet.UnreadLength() > 0)
                {
                    serverClient.Udp.HandleData(packet);
                    return;
                }

                Logger.Warn($"{clientEndPoint.Address} tried to impersonate another client by sending a false id: {clientId}.");
                return;
            }

            Logger.Debug($"No client with id: {clientId}");
        }

        private class ServerClientsIdAssigner
        {
            private readonly int[] locks;

            public ServerClientsIdAssigner(int maxClients)
            {
                locks = new int[maxClients];
            }

            public bool FindAvailableId(out int id)
            {
                for (int i = 1; i <= locks.Length; i++)
                {
                    ref int lck = ref locks[i - 1];
                    
                    if (lck == 0)
                    {
                        lck = 1;
                        id = i;
                        return true;
                    }
                }
                
                id = default;
                return false;
            }

            public void FreeId(int id)
            {
                locks[id - 1] = 0;
            }
        }
    }
}