﻿using System;
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

        private Thread internalThread = null;
        private TcpListener tcpListener = null;

        internal UdpClient UdpListener { get; private set; } = null;
        internal ServerOptions Options { get; private set; } = null;

        private readonly Dictionary<int, ServerClient> clients = new Dictionary<int, ServerClient>();
        private readonly ServerClientsIdAssigner idAssigner = null;

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
                throw new ArgumentNullException("ServerOptions was null.");

            if (options.IPAddress is null)
                throw new ArgumentNullException("IPAddress was null.");

            if (options.DataReceivedCallback is null)
                throw new ArgumentNullException("DataReceivedCallback was null.");

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

            LoggerConfig.CheckLoggerConfig(options.InternalLoggingLevel);
        }

        /// <summary>Launches the main thread and prompts the server to start listening for connections using the selected protocols.</summary>
        public void Listen()
        {
            StartThread(() =>
            {
                log.Info($"Starting server using protocol: '{Options.Protocol}' on port: {Options.Port}.");

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
            });
        }

        /// <summary>Stops both the TCP and UDP listeners.</summary>
        public void Stop()
        {
            log.Info("Disconnecting all clients.");

            foreach (var entry in clients)
            {
                entry.Value.Disconnect(false);
                idAssigner.FreeId(entry.Key);
            }

            log.Info("Stoping TCP and UDP listeners...");
            tcpListener?.Stop();
            UdpListener?.Close();
            log.Info("TCP and UDP listeners have been stopped.");

            log.Info("Stopping main thread and joining...");
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
                log.Debug($"Disconnecting client with id: {clientId} on protocol: {protocol}");

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
        public void SendPacketTCP(int toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                if (!client.IsConnectedTCP())
                {
                    log.Error($"Cannot send TCP data to client: {toClient} because it doesn't have an active TCP connection.");
                    return;
                }

                log.Debug($"Sending TCP data to client with id: {toClient}.");
                client.Tcp.SendData(packet);
                return;
            }

            log.Error($"Cannot send TCP data to client: {toClient} because it doesn't exist.");
        }

        /// <summary>Sends a packet via TCP to all the connected clients.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTCPToAll(Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            log.Debug("Sending TCP data to all connected clients.");

            foreach (var client in clients.Values)
            {
                if (client.IsConnectedTCP())
                    client.Tcp.SendData(packet);
            }
        }

        /// <summary>Sends a packet via TCP to all the connected clients except a specific one.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="exceptClient">The client to skip sending the data.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketTCPToAll(Packet packet, int exceptClient)
        {
            if (Options.Protocol == ServerProtocol.Udp)
                throw new InvalidProtocolException("Cannot send TCP data the server protocol is set to UDP only.");

            log.Debug($"Sending TCP data to all connected clients except client: {exceptClient}.");

            foreach (var client in clients.Values)
            {
                if (client.IsConnectedTCP() && client.Id != exceptClient)
                    client.Tcp.SendData(packet);
            }
        }

        /// <summary>Sends a packet via UDP.</summary>
        /// <param name="toClient">The client to which the packet should be sent.</param>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use UDP.</exception>
        public void SendPacketUDP(int toClient, Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                log.Debug($"Sending UDP data to client with id: {toClient}.");
                client.Udp.SendData(packet);
                return;
            }

            log.Error($"Cannot send UDP data to client: {toClient} because it doesn't exist.");
        }

        /// <summary>Sends a packet via UDP to all the connected clients.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketUDPToAll(Packet packet)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            log.Debug("Sending UDP data to all connected clients.");

            foreach (var client in clients.Values)
                client.Udp.SendData(packet);
        }

        /// <summary>Sends a packet via TCP to all the connected clients except a specific one.</summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="exceptClient">The client to skip sending the data.</param>
        /// <exception cref="InvalidProtocolException">Thrown when the server is not configured to use TCP.</exception>
        public void SendPacketUDPToAll(Packet packet, int exceptClient)
        {
            if (Options.Protocol == ServerProtocol.Tcp)
                throw new InvalidProtocolException("Cannot send UDP data the server protocol is set to TCP only.");

            log.Debug($"Sending UDP data to all connected clients except client: {exceptClient}.");

            foreach (var client in clients.Values)
            {
                if (client.Id != exceptClient)
                    client.Udp.SendData(packet);
            }
        }

        private void StartThread(Action startServer)
        {
            log.Debug("Starting new thread.");
            internalThread = new Thread(new ThreadStart(() =>
            {
                startServer();
                DateTime nextLoop = DateTime.Now;
                while (true)
                {
                    while (nextLoop < DateTime.Now)
                    {
                        nextLoop = nextLoop.AddMilliseconds(Options.InternalThreadRefreshRate);

                        if (nextLoop > DateTime.Now)
                            Thread.Sleep(nextLoop - DateTime.Now);
                    }
                }
            }));

            internalThread.Start();
        }

        private void TCPConnectCallback(IAsyncResult result)
        {
            log.Debug("New TCP connection received.");

            TcpClient client = tcpListener.EndAcceptTcpClient(result);
            tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

            if (!idAssigner.FindAvailableId(out int availableId))
            {
                log.Debug("Could not find an available id to create a client. Server is full!");
                log.Debug("Invoking ServerIsFullCallback.");
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
            log.Info("The new client has been refused by the ClientConnectCallback and the connection has been closed.");
        }

        private bool ApproveNewConnection(ClientInfo clientInfo)
        {
            log.Debug("Invoking ClientConnectedCallback.");
            bool? accepted = Options.ClientConnectedCallback?.Invoke(clientInfo);
            return accepted ?? true;
        }

        private void FinishProcessingAcceptedTcpClient(ServerClient serverClient, TcpClient tcpClient)
        {
            serverClient.Tcp.Connect(tcpClient);
            serverClient.ClientInfo.HasActiveTcpConnection = true;
            clients.Add(serverClient.Id, serverClient);

            log.Debug("Invoking ClientAcceptedCallback.");
            Options.ClientConnectionEstablishedCallback?.Invoke(serverClient.ClientInfo);
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

            int clientId = packet.ReadInt();

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
                        log.Warn($"{clientEndPoint} is trying to establish a UDP connection impersonating client: {serverClient.Id} with IP: {serverClient.ClientInfo.TcpEndPoint.Address}.");
                        return;
                    }

                    log.Debug($"First packet ever received from client {clientId}, setting EndPoint.");
                    serverClient.Udp.Connect(clientEndPoint);
                    serverClient.ClientInfo.HasActiveUdpConnection = true;
                    serverClient.ClientInfo.UdpEndPoint = clientEndPoint;
                }

                if (serverClient.Udp.EndPoint.Equals(clientEndPoint) && packet.UnreadLength() > 0)
                {
                    serverClient.Udp.HandleData(packet);
                    return;
                }

                log.Warn($"{clientEndPoint.Address} tried to impersonate another client by sending a false id: {clientId}.");
                return;
            }

            log.Debug($"No client with id: {clientId}");
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
                id = 0;

                for (int i = 1; i <= locks.Length; i++)
                {
                    if (Interlocked.CompareExchange(ref locks[i - 1], 1, 0) == 0)
                    {
                        id = i;
                        return true;
                    }
                }

                return false;
            }

            public void FreeId(int id)
            {
                Interlocked.Exchange(ref locks[id - 1], 0);
            }
        }
    }
}