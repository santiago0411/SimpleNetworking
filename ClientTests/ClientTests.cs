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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
            
        }

        private static ClientOptions CreateOptions()
        {
            return new ClientOptions
            {
                IPAddress = "127.0.0.1",
                Port = 30500,
                DataReceivedCallback = ReceivedData,
            };
        }

        private static void ReceivedData(Packet packet)
        {
            log.Info($"Received string: {packet.ReadString()}");

            using var p = new Packet();
            p.Write($"Hello from client {client.Id}");
            client.SendPacketTCP(p);
        }
    }
}
