using System;

namespace SimpleNetworking.Server
{
    public sealed class ServerOptions
    {
        /// <summary>(OPTIONAL) The IPAddreses to listen to incoming connections on. The default is any (0.0.0.0).</summary>
        public System.Net.IPAddress IPAddress { get; set; } = System.Net.IPAddress.Any;

        /// <summary>The port to listen on.</summary>
        public ushort Port { get; set; } = 0;

        /// <summary>(OPTIONAL) Maximum number of clients able to connect to the server at the same time. The default value is 100000.</summary>
        public int MaxClients { get; set; } = 100000;

        /// <summary>(OPTIONAL) The size of the socket receive buffer. The default value is 8192 bytes.</summary>
        public int ReceiveDataBufferSize { get; set; } = 8192;

        /// <summary>(OPTIONAL) The size of the socket send buffer. The default value is 8192 bytes.</summary>
        public int SendDataBufferSize { get; set; } = 8192;

        /// <summary>(OPTIONAL) The time in MILISECONDS after the receive data operation will time out. The default value is 0 which means no timeout.</summary>
        public int ReceiveDataTimeout { get; set; } = 0;

        /// <summary>(OPTIONAL) The time in MILISECONDS after the send data operation will time out. The default value is 0 which means no timeout.</summary>
        public int SendDataTimeout { get; set; } = 0;

        /// <summary>The protocol(s) to use. The default value is both Tcp and Udp.</summary>
        public ServerProtocol Protocol { get; set; } = ServerProtocol.Both;

        /// <summary>(OPTIONAL) Whether a client is required to send their id that was assigned through TCP. The default value is TRUE. If set to TRUE the client will have to send 4 bytes with its ID at the beginning of the UDP packet otherwise the data will be ignored. If set to FALSE UDPDataReceivedCallback will be invoked instead containing a packet with the raw data.</summary>
        public bool RequireClientToSendIdInUdpData { get; set; } = true;

        /// <summary>(OPTIONAL) The interval in MILLISECONDS at which the server thread is refreshed. The default value is 30.</summary>
        public double InternalThreadRefreshRate { get; set; } = 30;

        /// <summary>(OPTIONAL) Indicates whether the clients are automatically disconnected when an error occurs trying to read or send data. The default value is true.</summary>
        public bool DisconnectClientOnError { get; set; } = true;

        /// <summary> The depth level of internal logging. The default value is All.</summary>
        public log4net.Core.Level InternalLoggingLevel { get; set; } = log4net.Core.Level.All;

        /// <summary>Callback to execute when a new packet is received from a client. Arg1 is the client's assigned Id. Arg2 is the received data.</summary>
        public Action<int, Utils.Packet> DataReceivedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when the RequireClientToSendIdInUdpData flag is set to FALSE and new UDP data that can't be identified is received. Arg1 is the received data.</summary>
        public Action<Utils.Packet> UDPDataReceivedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when a new client connects to the server. Arg1 is the info of the client. Returning false will refuse the connection and disconnect the client.</summary>
        public Func<ClientInfo, bool> ClientConnectedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute after a new client has been accepted and a connection has been establish. Arg1 is the info of the accepted client.</summary>
        public Action<ClientInfo> ClientConnectionEstablishedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when a client disconnects or is disconnected from the server. Arg1 is the info the client. Arg2 is the protocol that got disconnected.</summary>
        public Action<ClientInfo, ServerProtocol> ClientDisconnectedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when a network operation fails and an exception is thrown. Arg1 is the info of the client on which the operation failed. Arg2 is the operation that failed. Arg3 is the exception thrown.</summary>
        public Action<ClientInfo, FailedOperation, Exception> NetworkOperationFailedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute everytime a client tries to connect but the server has reached it maximum capacity.</summary>
        public Action ServerIsFullCallback { get; set; } = null;
    }
}
