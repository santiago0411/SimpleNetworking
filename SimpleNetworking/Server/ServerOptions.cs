using System;

namespace SimpleNetworking.Server
{
    public sealed class ServerOptions
    {
        /// <summary>(OPTIONAL) The IPAddreses to listen to incoming connections on. The default is any (0.0.0.0).</summary>
        public System.Net.IPAddress IPAddress { get; set; } = System.Net.IPAddress.Any;

        /// <summary> The port to listen on.</summary>
        public ushort Port { get; set; } = 0;

        /// <summary>(OPTIONAL) Maximum number of clients able to connect to the server at the same time. The default value is 0 which means no maximum.</summary>
        public uint MaxClients { get; set; } = 0;

        /// <summary>(OPTIONAL) The number of clients to set up in the pool as soon as the server starts. The default value is 100.</summary>
        public uint ClientsPoolStartingSize { get; set; } = 100;

        /// <summary> The size of the socket receive buffer. The default value is 8192 bytes.</summary>
        public int ReceiveDataBufferSize { get; set; } = 8192;

        /// <summary> The size of the socket send buffer. The default value is 8192 bytes.</summary>
        public int SendDataBufferSize { get; set; } = 8192;

        /// <summary> The protocol(s) to use. The default value is both Tcp and Udp.</summary>
        public Server.ServerProtocol Protocol { get; set; } = Server.ServerProtocol.TcpAndUdp;

        /// <summary> (OPTIONAL) The interval in MILLISECONDS at which the main thread is refreshed. The default value is 30.</summary>
        public double MainThreadRefreshRate { get; set; } = 30;

        /// <summary>(OPTIONAL) Indicates whether the clients are automatically disconnected when an error occurs trying to read or send data. The default value is true.</summary>
        public bool DisconnectClientOnError { get; set; } = true;

        /// <summary> Disable all internal logging.</summary>
        public bool DisableInternalLogging { get; set; } = false;

        /// <summary> Action to execute when a new packet is received from a client. First argument is the client's assigned Id and the second is the received data.</summary>
        public Action<uint, Utils.Packet> DataReceivedCallback { get; set; } = null;

        /// <summary> (OPTIONAL) Action to execute when a new client connects to the server. Returning false will refuse the connection and disconnect the client.</summary>
        public Func<ClientInfo, bool> AcceptClientCallback { get; set; } = null;

        /// <summary> (OPTIONAL) Action to execute after a new client has been accepted and a connection has been establish.</summary>
        public Action<ClientInfo> ClientConnectedCallback { get; set; } = null;

        /// <summary> (OPTIONAL) Action to execute when a client disconnects or is disconnected from the server.</summary>
        public Action<ClientInfo> ClientDisconnectedCallback { get; set; } = null;

        /// <summary> (OPTIONAL) Action to execute when the server reaches it maximum capacity.</summary>
        public Action ServerIsFullCallback { get; set; } = null;
    }
}
