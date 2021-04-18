using System;
using System.Threading;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    public sealed class Client
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Client));

        /// <summary>The id assigned by the server to the client upon connection.</summary>
        public uint Id { get; internal set; }
        /// <summary>Contains whether a Tcp connection has been established and is active.</summary>
        public bool TcpIsConnected { get; internal set; }
        /// <summary>Contains whether a Udp connection has been established and is active.</summary>
        public bool UdpIsConnected { get; internal set; }

        internal ThreadManager ThreadManager { get; private set; } = null;
        internal ClientOptions Options { get; private set; } = null;

        internal ClientTCP tcp = null;
        internal ClientUDP udp = null;

        private Thread mainThread = null;

        public Client(string ipAddress, ushort port) :
            this(new ClientOptions 
            { 
                IPAddress = ipAddress, 
                Port = port 
            })
        {
        }

        /// <summary> Initializes the client with the options received.</summary>
        /// <exception cref="ArgumentNullException">Thrown when options or one of the options is null.</exception>
        public Client(ClientOptions options)
        {
            if (options is null)
                throw new ArgumentNullException("ClientOptions was null.");

            if (options.IPAddress is null)
                throw new ArgumentNullException("IPAddress was null.");

            if (options.DataReceivedCallback is null)
                throw new ArgumentNullException("DataReceivedCallback was null.");

            LoggerConfig.CheckLoggerConfig(options.DisableInternalLogging);

            Options = options;
            ThreadManager = new ThreadManager();
            tcp = new ClientTCP(this);
            udp = new ClientUDP(this);
        }

        /// <summary>Attempts to connect to the client via TCP.</summary>
        public void ConnectToServerTCP()
        {
            StartThread();
            tcp.Connect();
        }

        /// <summary>Attempts to connect to the client via UDP.</summary>
        public void ConnectToServerUDP()
        {
            if (TcpIsConnected)
            {
                udp.Connect(((System.Net.IPEndPoint)tcp.Socket.Client.LocalEndPoint).Port);
                return;
            }

            log.Warn("Cannot connect through UDP before a TCP connection is established.");
        }

        /// <summary>Disconnects the client from the server and raises the ClientDisconnectedCallback.</summary>
        public void Disconnect()
        {
            log.Info("Disconnecting client...");
            tcp.Disconnect();
            udp.Disconnect();
            Options.ClientDisconnectedCallback?.Invoke();

            log.Debug("Stopping main thread and joining...");
            ThreadManager.StopMainThread();
            mainThread.Join();
        }

        /// <summary>Sends a packet via TCP.</summary>
        public void SendPacketTCP(Packet packet)
        {
            log.Debug($"Sending TCP data to server.");
            tcp.SendData(packet);
        }

        /// <summary>Sends a packet via UDP.</summary>
        public void SendPacketUDP(Packet packet)
        {
            log.Debug($"Sending UDP data to server.");
            udp.SendData(packet);
        }

        private void StartThread()
        {
            log.Debug("Starting new thread.");
            mainThread = new Thread(new ThreadStart(() => ThreadManager.StartMainThread(Options.MainThreadRefreshRate)));
            mainThread.Start();
        }
    }
}
