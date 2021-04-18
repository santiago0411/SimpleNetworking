using System;
using SimpleNetworking.Server;
using SimpleNetworking.Utils;

namespace ServerTests
{
    public class ServerTests
    {
        private static Server server = null;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerTests));

        private static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            server = new Server(CreateOptions());

            try
            {
                server.Listen();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }

        private static ServerOptions CreateOptions()
        {
            return new ServerOptions
            {
                Port = 30500,
                ClientsPoolStartingSize = 50,
                MaxClients = 100,
                DataReceivedCallback = ReceivedData,
                ClientConnectedCallback = ClientConnected
            };
        }

        private static void ReceivedData(uint clientId, Packet packet)
        {
            log.Info($"Received message from client: {clientId} - {packet.ReadString()}");
        }

        private static void ClientConnected(ClientInfo clientInfo)
        {
            using var packet = new Packet();
            packet.Write("Welcome to the server!!");
            server.SendPacketTCP(clientInfo.AssignedId, packet);
        }
    }
}
