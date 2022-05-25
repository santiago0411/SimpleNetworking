using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerClient
    {
        public int Id { get; }
        public ServerTcp Tcp { get; }
        public ServerUdp Udp { get; }
        public ClientInfo ClientInfo { get; set; }
        public InternalLogger Logger => server.Logger;

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
            
            if (invokeCallback)
            {
                server.Logger.Debug("Invoking ClientDisconnectedCallback.");
                server.Options.ClientDisconnectedCallback?.Invoke(ClientInfo, ServerProtocol.Both);
            }
            
            ClientInfo = null;
            server.RemoveClient(Id);
        }
    }
}