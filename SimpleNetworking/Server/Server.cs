using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    public sealed class Server
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Server));

        /// <summary>The protocols the server is using. </summary>
        public enum ServerProtocol { TcpOnly, TcpAndUdp }
        /// <summary>The port this server is listening on.</summary>
        public ushort Port => options.Port;

        private Dictionary<uint, ServerClient> clients = null;
        private TcpListener tcpListener = null;
        private UdpClient udpListener = null;
        private ThreadManager threadManager = null;
        private Thread mainThread = null;

        private readonly ServerOptions options = null;

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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Server(ServerOptions options)
        {
            if (options is null)
                throw new ArgumentNullException("ServerOptions was null.");

            if (options.IPAddress is null)
                throw new ArgumentNullException("IPAddress was null.");

            if (options.DataReceivedCallback is null)
                throw new ArgumentNullException("DataReceivedCallback was null.");

            if (options.MaxClients != 0 && options.ClientsPoolStartingSize > options.MaxClients)
                throw new ArgumentOutOfRangeException("ClientsPoolStartingSize is greater than MaxClients.");

            LoggerConfig.CheckLoggerConfig(options.DisableInternalLogging);

            threadManager = new ThreadManager();

            SetUpClients(options);

            this.options = options;
        }

        /// <summary>Launches the main thread and starts the listeners.</summary>
        public void Listen()
        {
            StartThread(options.MainThreadRefreshRate);

            try
            {
                log.Info($"Starting server using protocol: {options.Protocol} on port: {options.Port}.");

                log.Info("Starting TCP listener...");
                tcpListener = new TcpListener(options.IPAddress, Port);
                tcpListener.Start();
                tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

                if (options.Protocol == ServerProtocol.TcpAndUdp)
                {
                    log.Info("Starting UDP listener...");
                    udpListener = new UdpClient(Port);
                    udpListener.BeginReceive(UDPReceiveCallback, null);
                }
            }
            catch 
            {
                log.Error("Error starting TCP/UDP listeners.");
                throw; 
            }
        }

        /// <summary>Stops both the TCP and UDP listeners.</summary>
        public void Stop()
        {
            try
            {
                log.Info("Stoping TCP and UDP listeners...");
                tcpListener?.Stop();
                udpListener?.Close();
                log.Info("TCP and UDP listeners have been stopped.");

                log.Debug("Stopping main thread and joining...");
                threadManager.StopMainThread();
                mainThread.Join();
            }
            catch
            {
                log.Error("Error closing TCP and UDP listeners.");
                throw;
            }
        }

        /// <summary>Returns the client info for the specified client id. Returns null if there is no client with the id.</summary>
        /// <param name="clientId">The client id.</param>
        public ClientInfo GetClientInfo(uint clientId)
        {
            if (clients.TryGetValue(clientId, out ServerClient client))
                return new ClientInfo(clientId, client.Tcp.Socket);

            return null;
        }

        /// <summary>Sends a packet via TCP.</summary>
        public void SendPacketTCP(uint toClient, Packet packet)
        {
            if (clients.TryGetValue(toClient, out ServerClient client))
            {
                log.Debug($"Sending TCP data to client with id: {toClient}.");
                client.Tcp.SendData(packet);
                return;
            }

            log.Error($"No client with id: {toClient} was found to send data.");
        }

        /// <summary>Sends a packet via UDP.</summary>
        public void SendPacketUDP(uint toClient, Packet packet)
        {
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
                clients.Add(i, new ServerClient(i, options, udpListener, threadManager));
        }

        private void StartThread(double refreshRate)
        {
            log.Debug("Starting new thread.");
            mainThread = new Thread(new ThreadStart(() => threadManager.StartMainThread(refreshRate)));
            mainThread.Start();
        }

        private void TCPConnectCallback(IAsyncResult result)
        {
            try
            {
                log.Info("New TCP connection received.");

                TcpClient client = tcpListener.EndAcceptTcpClient(result);
                tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

                log.Debug("Looking for an available slot in clients pool.");

                uint availableId = 0;
                for (uint i = 1; i <= clients.Count; i++)
                {
                    if (clients[i].Tcp.Socket is null && clients[i].Udp.EndPoint is null)
                    {
                        availableId = i;
                        break;
                    }
                }

                if (availableId == 0)
                {
                    if (clients.Count >= options.MaxClients)
                    {
                        log.Warn($"All clients in the pool are connected and the server has reached the maximum number ({options.MaxClients}) of clients connected.");
                        log.Debug("Invoking ServerIsFullCallback.");
                        options.ServerIsFullCallback?.Invoke();
                        return;
                    }

                    log.Debug("Adding new client to the pool beacuse all the clients in the pool have active connections.");
                    availableId = (uint)clients.Count + 1;
                    clients.Add(availableId, new ServerClient(availableId, options, udpListener, threadManager));
                }

                log.Debug("Invoking AcceptClientCallback.");
                var clientInfo = new ClientInfo(availableId, client);
                bool? accepted = options.AcceptClientCallback?.Invoke(clientInfo);

                if (!accepted.HasValue || (accepted.HasValue && accepted.Value))
                {
                    clients[availableId].Tcp.Connect(client);
                    log.Debug("Invoking ClientConnectedCallback.");
                    options.ClientConnectedCallback?.Invoke(clientInfo);
                    return;
                }
                
                log.Info("The new client has been refused by the ClientConnectCallback");
            }
            catch { throw; }
        }

        private void UDPReceiveCallback(IAsyncResult result)
        {
            try
            {
                log.Debug("New UDP data received.");

                IPEndPoint clientEndpoint = new IPEndPoint(options.IPAddress, 0);
                byte[] data = udpListener.EndReceive(result, ref clientEndpoint);
                udpListener.BeginReceive(UDPReceiveCallback, null);

                if (data.Length == 0)
                {
                    log.Warn("The received UDP data contains no bytes. Disconnecting client...");
                    
                    foreach (var c in clients.Values)
                        if (c.Udp.EndPoint.Equals(clientEndpoint))
                            c.Disconnect();

                    return;
                }

                if (data.Length < 4)
                {
                    log.Warn("Could not read client id from the packet because the data received had less than 4 bytes.");
                    return;
                }

                log.Debug("Reading data into a packet to be processed.");

                using Packet packet = new Packet(data);
                uint clientId = packet.ReadUInt();
                    
                if (clients.TryGetValue(clientId, out ServerClient client))
                {
                    if (client.Udp.EndPoint == null)
                    {
                        client.Udp.Connect(clientEndpoint);
                        return;
                    }

                    if (client.Udp.EndPoint.Equals(clientEndpoint))
                    {
                        client.Udp.HandleData(packet);
                        return;
                    }
                }

                log.Warn("Packet contained a client id that is not registered.");
            }
            catch { throw; }
        }
    }
}
