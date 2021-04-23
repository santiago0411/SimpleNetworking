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
                Console.WriteLine("Asdasdasdasd");
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
                InternalLoggingLevel = log4net.Core.Level.Info,
                ReceiveDataBufferSize = 4096,
                SendDataBufferSize = 4096,
                ReceiveDataTimeout = 0,
                SendDataTimeout = 0,
                InternalThreadRefreshRate = 30
            };
        }

        private static bool OnClientConnected(ClientInfo clientInfo)
        {
            log.Info($"Authenticating client: {clientInfo.TcpEndPoint.Address} with id {clientInfo.AssignedId}");
            return true;
        }


        private static void OnClientAccepted(ClientInfo clientInfo)
        {
            using var packet = new Packet();
            packet.Write(clientInfo.AssignedId);
            packet.Write("Welcome to the server!");
            server.SendPacketTCP(clientInfo.AssignedId, packet);
        }

        private static void OnClientDisconnected(ClientInfo clientInfo, ServerProtocol protocol)
        {
            log.Info($"Client {clientInfo.AssignedId} with ip {clientInfo.IpAddress} has disconnected.");
        }

        private static void OnDataReceived(int clientId, Packet packet)
        {
            log.Info($"Received message from client: {clientId} - {packet.ReadString()}");
        }

        private static void OnReceiveUdpData(Packet packet)
        {
            log.Info($"Manually parsing the whole UDP packet.");
            log.Info($"Client id is: {packet.ReadInt()}");
            log.Info($"Packet length is: {packet.ReadInt()}");
            log.Info($"Received UDP data: {packet.ReadString()}");
        }

        private static void OnNetworkOperationFailed(ClientInfo clientInfo, FailedOperation failedOperation, Exception ex)
        {
            log.Error($"There was an error on {failedOperation} for client {clientInfo.AssignedId}: {ex.Message}");
        }

        private static void OnServerFull()
        {
            log.Warn("Server is full!!");
        }
    }
}
