using System;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerClient));

        public uint Id { get; private set; }
        public ServerTCP Tcp { get; private set; }
        public ServerUDP Udp { get; private set; }

        private readonly Action<ClientInfo> clientDisconnectedCallback = null;

        internal ServerClient(uint id, ServerOptions options, System.Net.Sockets.UdpClient udpListener, ThreadManager threadManager)
        {
            Id = id;
            clientDisconnectedCallback = options.ClientDisconnectedCallback;

            Tcp = new ServerTCP(id, options.ReceiveDataBufferSize, options.SendDataBufferSize, options.DisconnectClientOnError, threadManager, Disconnect, options.DataReceivedCallback);

            if (options.Protocol == Server.ServerProtocol.TcpAndUdp)
                Udp = new ServerUDP(id, options.DisconnectClientOnError, udpListener, threadManager, Disconnect, options.DataReceivedCallback);

        }

        public void Disconnect()
        {
            Tcp.Disconnect();
            Udp.Disconnect();

            log.Debug("Invoking ClientDisconnectedCallback.");
            clientDisconnectedCallback?.Invoke(new ClientInfo(Id, null));
        }
    }
}