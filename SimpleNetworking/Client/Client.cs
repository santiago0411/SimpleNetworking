using System;
using System.Threading;
using SimpleNetworking.Exceptions;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    /// <summary>The protocols the client can use. Used to notified which protocol was disconnected in ClientDisconnectedCallback.</summary>
    public enum Protocol { Tcp, Udp, Both }

    /// <summary>The operations that can fail and raise a NetworkOperationFailedCallback.</summary>
    public enum FailedOperation { SendDataTcp, ReceiveDataTcp, SendDataUdp, ReceiveDataUdp }

    public sealed class Client
    {
        /// <summary>The id assigned by the server. Must be set by the user, it is NOT set automatically.</summary>
        public int Id { get; set; }
        /// <summary>Contains whether a Tcp connection has been established and is active.</summary>
        public bool IsConnected { get; internal set; }

        internal ClientOptions Options { get; }
        internal InternalLogger Logger { get; private set; }

        private Thread mainThread;
        private bool running;
        
        private readonly ClientTcp tcp;
        private readonly ClientUdp udp;

        public Client(string ipAddress, ushort port, Action<Packet> dataReceivedCallback) :
            this(new ClientOptions 
            { 
                IPAddress = ipAddress, 
                Port = port,
                DataReceivedCallback = dataReceivedCallback
            })
        {
        }

        /// <summary> Initializes the client with the options received.</summary>
        /// <exception cref="ArgumentNullException">Thrown when options or one of the options is null.</exception>
        /// <exception cref="InvalidOptionsException">Thrown when one of the required options has an invalid value.</exception>
        public Client(ClientOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(Options));

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

            if (options.InternalThreadRefreshRate < 0)
                throw new InvalidOptionsException("The MainThreadRefreshRate value cannot be smaller than 0.");

            if (options.DataReceivedCallback is null)
                throw new InvalidOptionsException("The DataReceivedCallback cannot be null because data will not be able to be sent back.");

            Logger = new InternalLogger(options.Logger);

            Options = options;
            tcp = new ClientTcp(this);
            udp = new ClientUdp(this);

            StartThread(() =>
            {
                DateTime nextLoop = DateTime.Now;
                running = true;
                while (running)
                {
                    while (nextLoop < DateTime.Now)
                    {
                        nextLoop = nextLoop.AddMilliseconds(options.InternalThreadRefreshRate);

                        if (nextLoop > DateTime.Now)
                            Thread.Sleep(nextLoop - DateTime.Now);
                    }
                }
            });
        }

        public void SetLogger(ILogger logger)
        {
            Logger = new InternalLogger(logger);
        }
        
        /// <summary>Attempts to connect to the server via TCP.</summary>
        public void ConnectToServerTcp()
        {
            tcp.Connect();
        }

        /// <summary>Attempts to connect to the server via UDP.</summary>
        public void ConnectToServerUdp()
        {
            udp.Connect();
        }

        /// <summary>Disconnects the client from the server and raises the ClientDisconnectedCallback.</summary>
        public void Disconnect()
        {
            Logger.Info("Disconnecting client...");
            tcp?.Disconnect(false);
            udp?.Disconnect(false);
            Options.ClientDisconnectedCallback?.Invoke(Protocol.Both);

            running = false;
            Logger.Info("Stopping main thread and joining...");
            mainThread.Join();
        }

        /// <summary>Sends a packet via TCP.</summary>
        public void SendPacketTcp(Packet packet)
        {
            Logger.Debug($"Sending TCP data to server.");
            tcp.SendData(packet);
        }

        /// <summary>Sends a packet via UDP.</summary>
        public void SendPacketUdp(Packet packet, bool writeId = true)
        {
            Logger.Debug($"Sending UDP data to server.");
            udp.SendData(packet, writeId);
        }

        private void StartThread(Action startClient)
        {
            Logger.Debug("Starting new thread.");
            mainThread = new Thread(() => startClient());
            mainThread.Start();
        }
    }
}
