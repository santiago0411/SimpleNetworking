using System;
using SimpleNetworking.Client;
using SimpleNetworking.Utils;

namespace ClientTests
{
    public class ClientTests
    {
        private static Client client = null;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ClientTests));

        private static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            client = new Client(CreateOptions());

            try
            {
                client.ConnectToServerTCP();
                client.ConnectToServerUDP();

                /*using var p = new Packet();
                p.Write("This is the client sending data to the UDP only server.");
                client.SendPacketUDP(p, false);*/
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                client.Disconnect();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            
        }

        private static ClientOptions CreateOptions()
        {
            return new ClientOptions
            {
                IPAddress = "127.0.0.1",
                Port = 50005,
                DataReceivedCallback = ReceivedData,
            };
        }

        private static void ReceivedData(Packet packet)
        {
            int id = packet.ReadInt();
            log.Info($"Id is: {id}");
            client.Id = 1;
            log.Info($"Received string: {packet.ReadString()}");

            using var p = new Packet();
            p.Write($"Hi server, this is client: {id}.");
            client.SendPacketUDP(p);
            //client.SendPacketUDP(p, false);
        }
    }
}
