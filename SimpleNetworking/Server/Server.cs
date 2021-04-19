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
    public enum FailedOperation { ConnectTCP, SendDataTCP, ReceiveDataTCP, SendDataUDP }

    public sealed class Server
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Server));

        /// <summary>The port this server is listening on.</summary>
        public ushort Port => Options.Port;

        private Dictionary<uint, ServerClient> clients = null;
        private Thread mainThread = null;
        private TcpListener tcpListener = null;

        internal UdpClient UdpListener { get; private set; } = null;
        internal ThreadManager ThreadManager { get; private set; } = new ThreadManager();
        internal ServerOptions Options { get; private set; } = null;

        public Server(ushort port, Action<uint, Packet> dataReceivedCallback)
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
                throw new ArgumentNullException("ServerOptions was null.");

            if (options.IPAddress is null)
                throw new ArgumentNullException("IPAddress was null.");

            if (options.DataReceivedCallback is null)
                throw new ArgumentNullException("DataReceivedCallback was null.");

            if (options.MaxClients != 0 && options.ClientsPoolStartingSize > options.MaxClients)
                throw new InvalidOptionsException("ClientsPoolStartingSize is greater than MaxClients.");

            if (options.ReceiveDataBufferSize <= 0)
                throw new InvalidOptionsException("The ReceiveDataBufferSize value cannot be smaller than 1.");

            if (options.SendDataBufferSize <= 0)
                throw new InvalidOptionsException("The SendDataBufferSize value cannot be smaller than 1.");

            if (options.ReceiveDataTimeout < 0)
                throw new InvalidOptionsException("The ReceiveDataTimeout value cannot be smaller than 0.");

            if (options.SendDataTimeout < 0)
                throw new InvalidOptionsException("The SendDataTimeout value cannot be smaller than 0.");

            if ((int)options.Protocol > 2)
                throw new InvalidOptionsException("Procotol does not contain a valid option.");

            if (options.MainThreadRefreshRate < 0)
                throw new InvalidOptionsException("The MainThreadRefreshRate value cannot be smaller than 0.");

            if (options.DataReceivedCallback is null)
                throw new InvalidOptionsException("The DataReceivedCallback cannot be null because data will not be able to be sent back.");

            if (options.Protocol == ServerProtocol.Udp && options.UDPDataReceivedCallback is null)
                throw new InvalidOptionsException("The server protocol is set to UDP only and UDPDataReceivedCallback is null. Server will not be able to send back UDP data.");

            if (!options.RequireClientToSendIdInUdpData && options.UDPDataReceivedCallback is null)
                throw new InvalidOptionsException("The RequireClientToSendIdInUdpData flag is set to FALSE and UDPDataReceivedCallback is null. Server will not be able to send back UDP data.");

            Options = options;
            
            LoggerConfig.CheckLoggerConfig(options.DisableInternalLogging); 

            SetUpClients(options);
        }

        /// <summary>Launches the main thread and prompts the server to start listening for connections using the selected protocols.</summary>
        public void Listen()
        {
            StartThread(Options.MainThreadRefreshRate);

            log.Info($"Starting server using protocol: {Options.Protocol} on port: {Options.Port}.");

            if (Options.Protocol != ServerProtocol.Udp)
            {
                log.Info("Starting TCP listener...");
                tcpListener = new TcpListener(Options.IPAddress, Port);
                tcpListener.Start();
                tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);
            }

            if (Options.Protocol != ServerProtocol.Tcp)
            {
                log.Info("Starting UDP listener...");
                UdpListener = new UdpClient(Port);
                UdpListener.BeginReceive(UDPReceiveCallback, null);
            }
        }

        /// <summary>Stops both the TCP and UDP listeners.</summary>
        public void Stop()
        {
            log.Info("Stoping TCP and UDP listeners...");
            tcpListener?.Stop();
            UdpListener?.Close();
            log.Info("TCP and UDP listeners have been stopped.");

            log.Debug("Stopping main thread and joining...");
            ThreadManager.StopMainThread();
            mainThread.Join();
        }

        /// <summary>Returns the client info for the specified client id. Returns null if no client with that id is found.</summary>
        public ClientInfo GetClientInfo(uint clientId)
        {
            if (clients.TryGetValue(clientId, out ServerClient client))
                return client.ClientInfo;

            return null;
        }

        /// <summary>Disconnects a client with the specified client from the server.</summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="protocol">The protocol to disconnect.</param>
        /// <returns> Returns false if no client with that id is found.</returns>
        public bool DisconnectClient(uint clientId, ServerProtocol protocol)
        {
            if (clients.TryGetValue(clientId, out ServerClient client))
            {
                log.Info($"Disconnecting client with id: {clientId} on protocol: {protocol}");

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
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTCP(uint toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                log.Debug($"Sending TCP data to client with id: {toClient}.");
                client.Tcp.SendData(packet);
                return;
            }

            log.Error($"No client with id: {toClient} was found to send data.");
        }

        /// <summary>Sends a packet via UDP.</summary>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use UDP.</exception>
        public void SendPacketUDP(uint toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                log.Debug($"Sending UDP data to client with id: {toClient}.");
                client.Udp.SendData(packet);
                return;
            }

            log.Error($"No client with id: {toClient} was found to send data.");
        }

        private void SetUpClients(ServerOptions options)
        {
            clients = new Dictionary<uint, ServerClient>();

            for (uint i = 1; i <= options.ClientsPoolStartingSize; i++)
                clients.Add(i, new ServerClient(i, this));
        }

        private void StartThread(double refreshRate)
        {
            log.Debug("Starting new thread.");
            mainThread = new Thread(new ThreadStart(() => ThreadManager.StartMainThread(refreshRate)));
            mainThread.Start();
        }

        private void TCPConnectCallback(IAsyncResult result)
        {
            log.Info("New TCP connection received.");

            TcpClient client = tcpListener.EndAcceptTcpClient(result);
            tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

            if (!FindAvailableClient(out uint availableId))
            {
                client.Close();
                client.Dispose();
                return;
            }

            var clientInfo = new ClientInfo(availableId, client);

            if (ApproveNewConnection(clientInfo))
            {
                FinishProcessingAcceptedTcpClient(clientInfo.AssignedId, client, clientInfo);
                return;
            }

            client.Close();
            client.Dispose();
            log.Info("The new client has been refused by the ClientConnectCallback and the connection has been closed.");
        }

        private bool FindAvailableClient(out uint availableId)
        {
            log.Debug("Looking for an available slot in clients pool.");

            availableId = 0;

            for (uint i = 1; i <= clients.Count; i++)
            {
                if (clients[i].Tcp?.Socket is null && clients[i].Udp?.EndPoint is null)
                {
                    availableId = i;
                    return true;
                }
            }

            if (clients.Count >= Options.MaxClients)
            {
                log.Warn($"All clients in the pool are connected and the server has reached the maximum number ({Options.MaxClients}) of clients connected.");
                log.Debug("Invoking ServerIsFullCallback.");
                Options.ServerIsFullCallback?.Invoke();
                return false;
            }

            log.Debug("Adding new client to the pool beacuse all the clients in the pool have active connections.");
            availableId = (uint)clients.Count + 1;
            clients.Add(availableId, new ServerClient(availableId, this));
            return true;
        }

        private bool ApproveNewConnection(ClientInfo clientInfo)
        {
            log.Debug("Invoking ClientConnectedCallback.");
            bool? accepted = Options.ClientConnectedCallback?.Invoke(clientInfo);
            return accepted ?? true;
        }

        private void FinishProcessingAcceptedTcpClient(uint clientId, TcpClient client, ClientInfo clientInfo)
        {
            ServerClient serverClient = clients[clientId];
            serverClient.Tcp.Connect(client);
            clientInfo.HasActiveTcpConnection = true;
            serverClient.ClientInfo = clientInfo;

            log.Debug("Invoking ClientAcceptedCallback.");
            Options.ClientAcceptedCallback?.Invoke(clientInfo);
        }

        private void UDPReceiveCallback(IAsyncResult result)
        {
            log.Debug("New UDP data received.");

            IPEndPoint clientEndPoint; 
            byte[] data;

            try
            {
                clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                data = UdpListener.EndReceive(result, ref clientEndPoint);
                UdpListener.BeginReceive(UDPReceiveCallback, null);
            }
            catch (Exception ex)
            {
                log.Error("Error receiving UDP data.", ex);
                return;
            }

            using var packet = new Packet(data);

            if (!Options.RequireClientToSendIdInUdpData)
            {
                log.Debug("RequireClientToSendIdInUdpData is set to false, data will not be processed because it can't be identified.");
                log.Debug("Invoking UDPDataReceivedCallback");
                Options.UDPDataReceivedCallback?.Invoke(packet);
                return;
            }

            log.Debug("Reading client id from data received.");

            if (data.Length < 4)
            {
                log.Warn("Failed to read client id, data does not have enough bytes. Packet will not be processed.");
                return;
            }

            uint clientId = packet.ReadUInt();

            if (clients.TryGetValue(clientId, out ServerClient serverClient))
            {
                if (serverClient.ClientInfo is null)
                {
                    log.Debug($"There is no client {clientId} with an active TCP connection.");
                    return;
                }

                if (serverClient.Udp.EndPoint is null)
                {
                    if (!serverClient.ClientInfo.TcpEndPoint.Address.Equals(clientEndPoint.Address))
                    {
                        log.Warn($"Endpoint: {clientEndPoint.Address} is trying to impersonate client: {serverClient.Id} with endpoint: {serverClient.ClientInfo.TcpEndPoint.Address}");
                        return;
                    }

                    log.Debug($"First packet ever received from client {clientId}, setting EndPoint.");
                    serverClient.Udp.Connect(clientEndPoint);
                    serverClient.ClientInfo.HasActiveUdpConnection = true;
                    serverClient.ClientInfo.UdpEndPoint = clientEndPoint;
                }

                if (serverClient.Udp.EndPoint.Equals(clientEndPoint))
                {
                    serverClient.Udp.HandleData(packet);
                    return;
                }

                log.Warn($"Client {clientEndPoint.Address} tried to impersonate another client by sending a false id: {clientId}.");
                return;
            }

            log.Debug($"No client with id: {clientId}");
        }
    }
}