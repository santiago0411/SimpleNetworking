namespace SimpleNetworking.Server
{
    internal class ServerClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerClient));

        public uint Id { get; private set; }
        public ServerTCP Tcp { get; private set; }
        public ServerUDP Udp { get; private set; }
        public ClientInfo ClientInfo { get; set; }

        private readonly Server server = null;

        internal ServerClient(uint id, Server server)
        {
            Id = id;
            this.server = server;

            if (server.Options.Protocol != ServerProtocol.Udp)
                Tcp = new ServerTCP(this, server.Options, server.ThreadManager);

            if (server.Options.Protocol != ServerProtocol.Tcp)
                Udp = new ServerUDP(this, server.Options, server.ThreadManager, server.UdpListener);
        }

        public void Disconnect()
        {
            Tcp?.Disconnect(false);
            Udp?.Disconnect(false);

            ClientInfo = null;

            log.Debug("Invoking ClientDisconnectedCallback.");
            server.Options.ClientDisconnectedCallback?.Invoke(ClientInfo, ServerProtocol.Both);
        }
    }
}