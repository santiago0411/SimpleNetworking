using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerUDP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerUDP));

        public IPEndPoint EndPoint { get; private set; }

        private readonly ServerClient serverClient;
        private readonly ServerOptions options;
        private readonly UdpClient udpListener;

        public ServerUDP(ServerClient serverClient, ServerOptions options, UdpClient udpListener)
        {
            this.serverClient = serverClient;
            this.options = options;
            this.udpListener = udpListener;
        }

        public void Connect(IPEndPoint clientEndpoint)
        {
            EndPoint = clientEndpoint;
        }

        public void Disconnect(bool invokeCallback = true)
        {
            EndPoint = null;
            serverClient.ClientInfo.HasActiveUdpConnection = false;
            log.Info("UDP EndPoint disconnected.");

            if (invokeCallback)
                options.ClientDisconnectedCallback?.Invoke(serverClient.ClientInfo, ServerProtocol.Udp);
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();

                if (!(EndPoint is null))
                    udpListener.BeginSend(packet.ToArray(), packet.Length(), EndPoint, null, null);
            }
            catch (Exception ex)
            {
                log.Error($"There was an error trying to send UDP data to the client with id: {serverClient.Id}.", ex);
                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.SendDataUDP, ex);
            }
        }

        public void HandleData(Packet receivedData)
        {
            log.Debug($"Received new UDP data from client: {serverClient.Id}.");

            int packetLength = receivedData.ReadInt();
            log.Debug($"Packet length is: {packetLength}");

            if (packetLength <= 0) return;

            byte[] packetBytes = receivedData.ReadBytes(packetLength);

            log.Debug($"Raw data is [{BitConverter.ToString(packetBytes).Replace("-", "")}].");

            log.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            using var packet = new Packet(packetBytes);
            options.DataReceivedCallback(serverClient.Id, packet);
        }
    }
}