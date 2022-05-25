using System;
using SimpleNetworking.Client;
using SimpleNetworking.Utils;

namespace ClientTests
{
    public class ClientTests
    {
        private static readonly ConsoleLogger logger = new ConsoleLogger();
        private static Client client;

        private static void Main(string[] args)
        {
            client = new Client(CreateOptions());

            try
            {
                client.ConnectToServerTcp();
                client.ConnectToServerUdp();

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
            logger.Info($"Id is: {id}");
            client.Id = 1;
            logger.Info($"Received string: {packet.ReadString()}");

            using var p = new Packet();
            p.Write($"Hi server, this is client: {id}.");
            client.SendPacketUdp(p);
            //client.SendPacketUDP(p, false);
        }
    }
}
