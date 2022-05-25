using System;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    public sealed class ClientOptions
    {
        /// <summary>The server ip address.</summary>
        public string IPAddress { get; set; } = null;

        /// <summary>The port to connect to.</summary>
        public ushort Port { get; set; } = 0;

        /// <summary>(OPTIONAL) The size of the socket receive buffer. The default value is 8192 bytes.</summary>
        public int ReceiveDataBufferSize { get; set; } = 8192;

        /// <summary>(OPTIONAL) The size of the socket send buffer. The default value is 8192 bytes.</summary>
        public int SendDataBufferSize { get; set; } = 8192;

        /// <summary>(OPTIONAL) The time in MILISECONDS after the receive data operation will time out. The default value is 0 which means no timeout.</summary>
        public int ReceiveDataTimeout { get; set; } = 0;

        /// <summary>(OPTIONAL) The time in MILISECONDS after the send data operation will time out. The default value is 0 which means no timeout.</summary>
        public int SendDataTimeout { get; set; } = 0;

        /// <summary>(OPTIONAL) The interval in MILLISECONDS at which the client thread is refreshed. The default value is 30.</summary>
        public double InternalThreadRefreshRate { get; set; } = 30;

        /// <summary>(OPTIONAL) Indicates whether the client is automatically disconnected when an error occurs trying to read or send data. The default value is true.</summary>
        public bool DisconnectClientOnError { get; set; } = true;

        /// <summary> (OPTIONAL) An ILogger implementation for internal logging.</summary>
        public ILogger Logger { get; set; }

        /// <summary>Callback to execute when a new packet is received from the server. Arg1 is the data received in a packet.</summary>
        public Action<Utils.Packet> DataReceivedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when the client is disconnected from the server. Arg1 indicates which protocol was disconnected.</summary>
        public Action<Protocol> ClientDisconnectedCallback { get; set; } = null;

        /// <summary>(OPTIONAL) Callback to execute when a network operation fails and an exception is thrown. Arg1 is the operation that failed. Arg2 is the exception thrown.</summary>
        public Action<FailedOperation, Exception> NetworkOperationFailedCallback { get; set; } = null;
    }
}