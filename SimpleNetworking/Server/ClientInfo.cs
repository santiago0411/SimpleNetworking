using System.Net;
using System.Net.Sockets;

namespace SimpleNetworking.Server
{
    public sealed class ClientInfo
    {
        /// <summary>The id this client was assigned by the server.</summary>
        public int AssignedId { get; private set; }
        /// <summary>The ip address of this client. This value is set upon connection and it will exists until this instance is destroyed.</summary>
        public string IpAddress { get; private set; }
        /// <summary>The port on which the client is connected. This value is set upon connection and it will exists until this instance is destroyed.</summary>
        public int TcpPort { get; private set; }
        /// <summary>The TcpEndPoint of the client. This property will only have a value while the connection is alive.</summary>
        public IPEndPoint TcpEndPoint { get; private set; }
        /// <summary>The UdpEndPoint of the client. This property will only have a value while the connection is alive.</summary>
        public IPEndPoint UdpEndPoint { get; internal set; }
        /// <summary>Whether a Tcp connection has been established.</summary>
        public bool HasActiveTcpConnection { get; internal set; }
        /// <summary>Whether a Udp connection has been established.</summary>
        public bool HasActiveUdpConnection { get; internal set; }
        /// <summary>Public variable to hold any aditional data related to this client.</summary>
        public object ClientData { get; set; }

        internal ClientInfo(int assignedId)
        {
            AssignedId = assignedId;
        }

        internal ClientInfo(int assignedId, TcpClient client)
            : this(assignedId)
        {
            if (client.Client.RemoteEndPoint is IPEndPoint endPoint)
            {
                TcpEndPoint = endPoint;
                IpAddress = endPoint.Address.ToString();
                TcpPort = endPoint.Port;
            }
        }
    }
}
