namespace SimpleNetworking.Server
{
    internal class ServerClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerClient));

        public int Id { get; private set; }
        public ServerTCP Tcp { get; private set; }
        public ServerUDP Udp { get; private set; }
        public ClientInfo ClientInfo { get; set; }

        private readonly Server server = null;

        internal ServerClient(int id, Server server)
        {
            Id = id;
            this.server = server;

            if (server.Options.Protocol != ServerProtocol.Udp)
                Tcp = new ServerTCP(this, server.Options);

            if (server.Options.Protocol != ServerProtocol.Tcp)
                Udp = new ServerUDP(this, server.Options, server.UdpListener);
        }

        public bool IsConnectedTCP()
        {
            if (Tcp is null) return false;
            if (Tcp.Socket is null) return false;
            return Tcp.Socket.Connected;
        }

        public void Disconnect(bool invokeCallback = true)
        {
            Tcp?.Disconnect(false);
            Udp?.Disconnect(false);

            ClientInfo = null;

            if (invokeCallback)
            {
                log.Debug("Invoking ClientDisconnectedCallback.");
                server.Options.ClientDisconnectedCallback?.Invoke(ClientInfo, ServerProtocol.Both);
            }

            server.RemoveClient(Id);
        }
    }
}