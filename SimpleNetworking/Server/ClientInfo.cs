using System.Net;
using System.Net.Sockets;

namespace SimpleNetworking.Server
{
    public sealed class ClientInfo
    {
        public uint AssignedId { get; private set; }
        public string IpAddress { get; private set; }
        public int Port { get; private set; }

        internal ClientInfo(uint assignedId, TcpClient tcpClient)
        {
            AssignedId = assignedId;

            if (!(tcpClient is null) && tcpClient.Client.RemoteEndPoint is IPEndPoint endPoint)
            {
                IpAddress = endPoint.Address.ToString();
                Port = endPoint.Port;
            }
        }
    }
}
