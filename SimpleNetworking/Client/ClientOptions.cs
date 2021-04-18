using System;

namespace SimpleNetworking.Client
{
    public sealed class ClientOptions
    {
        /// <summary>The server ip address.</summary>
        public string IPAddress { get; set; } = null;

        /// <summary> The port to connect to.</summary>
        public ushort Port { get; set; } = 0;

        /// <summary> The size of the socket receive buffer. The default value is 8192 bytes.</summary>
        public int ReceiveDataBufferSize { get; set; } = 8192;

        /// <summary> The size of the socket send buffer. The default value is 8192 bytes.</summary>
        public int SendDataBufferSize { get; set; } = 8192;

        /// <summary> (OPTIONAL) The interval in MILLISECONDS at which the main thread is refreshed. The default value is 30.</summary>
        public double MainThreadRefreshRate { get; set; } = 30;

        /// <summary>(OPTIONAL) Indicates whether the client is automatically disconnected when an error occurs trying to read or send data. The default value is true.</summary>
        public bool DisconnectClientOnError { get; set; } = true;

        /// <summary> Disable all internal logging.</summary>
        public bool DisableInternalLogging { get; set; } = false;

        /// <summary> Action to execute when a new packet is received from the server. First argument is the data received in a packet.</summary>
        public Action<Utils.Packet> DataReceivedCallback { get; set; } = null;

        /// <summary> (OPTIONAL) Action to execute when the client is disconnected from the server.</summary>
        public Action ClientDisconnectedCallback { get; set; } = null;
    }
}
