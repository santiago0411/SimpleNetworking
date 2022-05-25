using System;
using SimpleNetworking.Server;
using SimpleNetworking.Utils;
using FailedOperation = SimpleNetworking.Server.FailedOperation;

namespace ServerTests
{
    public class ServerTests
    {
        private static readonly ConsoleLogger logger = new ConsoleLogger();
        private static Server server;

        private static void Main(string[] args)
        {
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
                IPAddress = System.Net.IPAddress.Any,
                Port = 50005,
                Protocol = ServerProtocol.Both,
                MaxClients = 100,
                DisconnectClientOnError = true,
                ClientConnectedCallback = OnClientConnected,
                ClientConnectionEstablishedCallback = OnClientAccepted,
                ClientDisconnectedCallback = OnClientDisconnected,
                DataReceivedCallback = OnDataReceived,
                NetworkOperationFailedCallback = OnNetworkOperationFailed,
                ServerIsFullCallback = OnServerFull,
                //UDPDataReceivedCallback = OnReceiveUdpData,
                //RequireClientToSendIdInUdpData = false,
                Logger = logger,
                ReceiveDataBufferSize = 4096,
                SendDataBufferSize = 4096,
                ReceiveDataTimeout = 0,
                SendDataTimeout = 0,
                InternalThreadRefreshRate = 30
            };
        }

        private static bool OnClientConnected(ClientInfo clientInfo)
        {
            logger.Info($"Authenticating client: {clientInfo.TcpEndPoint.Address} with id {clientInfo.AssignedId}");
            return true;
        }


        private static void OnClientAccepted(ClientInfo clientInfo)
        {
            using var packet = new Packet();
            packet.Write(clientInfo.AssignedId);
            packet.Write("Welcome to the server!");
            server.SendPacketTcp(clientInfo.AssignedId, packet);
        }

        private static void OnClientDisconnected(ClientInfo clientInfo, ServerProtocol protocol)
        {
            logger.Info($"Client {clientInfo.AssignedId} with ip {clientInfo.IpAddress} has disconnected.");
        }

        private static void OnDataReceived(ClientInfo client, Packet packet)
        {
            logger.Info($"Received message from client: {client.AssignedId} - {packet.ReadString()}");
        }

        private static void OnReceiveUdpData(Packet packet)
        {
            logger.Info($"Manually parsing the whole UDP packet.");
            logger.Info($"Client id is: {packet.ReadInt()}");
            logger.Info($"Packet length is: {packet.ReadInt()}");
            logger.Info($"Received UDP data: {packet.ReadString()}");
        }

        private static void OnNetworkOperationFailed(ClientInfo clientInfo, FailedOperation failedOperation, Exception ex)
        {
            logger.Error($"There was an error on {failedOperation} for client {clientInfo.AssignedId}: {ex.Message}");
        }

        private static void OnServerFull()
        {
            logger.Warn("Server is full!!");
        }
    }
}
