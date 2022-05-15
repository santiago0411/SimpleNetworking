namespace SimpleNetworking.Server
{
    internal class ServerClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerClient));

        public int Id { get; }
        public ServerTcp Tcp { get; }
        public ServerUdp Udp { get; }
        public ClientInfo ClientInfo { get; set; }

        private readonly Server server;

        internal ServerClient(int id, Server server)
        {
            Id = id;
            this.server = server;

            if (server.Options.Protocol != ServerProtocol.Udp)
                Tcp = new ServerTcp(this, server.Options);

            if (server.Options.Protocol != ServerProtocol.Tcp)
                Udp = new ServerUdp(this, server.Options, server.UdpListener);
        }

        public bool IsConnectedTcp()
        {
            return Tcp?.Socket is { } && Tcp.Socket.Connected;
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